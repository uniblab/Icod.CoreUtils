// Original behavior/reference: sed (Lee E. McMahon)
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Sed;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// sed: stream editor (simplified).
/// Supported features:
///   -n        suppress automatic printing
///   -e script add script to the commands to run
///   -i[SUFFIX] edit files in-place, optional backup SUFFIX (e.g. -i.bak or -i)
///   -?        display this help/usage text
/// Script forms supported (subset):
///   s/old/new/g   substitute (supports regex) with optional g flag
///   N i TEXT      insert TEXT before line N (example: "1i this is foo")
/// Multiple -e accepted; first script applied if none specified.
/// This is a small, portable subset for common use cases.
/// </summary>
public static class Command
{
	private sealed class Script
	{
		public enum KindT { Substitute, Insert }

		public KindT Kind { get; }
		public string? Text { get; }
		public string? Pattern { get; }
		public string? Replacement { get; }
		public string? Flags { get; }
		public int LineNumber { get; }

		private Script( KindT k, string? text = null, string? pattern = null, string? replacement = null, string? flags = null, int lineNumber = 0 )
		{
			Kind = k;
			Text = text;
			Pattern = pattern;
			Replacement = replacement;
			Flags = flags;
			LineNumber = lineNumber;
		}

		public static Script Subst( string pattern, string replacement, string? flags ) =>
			new Script( KindT.Substitute, pattern: pattern, replacement: replacement, flags: flags );

		public static Script Insert( int lineNumber, string text ) =>
			new Script( KindT.Insert, text: text, lineNumber: lineNumber );
	}

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null )
	{
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var scripts = new List<Script>();
		var suppress = false;
		var files = new List<string>();
		var inPlace = false;
		string? backupSuffix = null;

		int i = 0;
		for ( ; i < args.Length; i++ )
		{
			var a = args[ i ];
			if ( !a.StartsWith( "-" ) )
			{
				break;
			}

			if ( a == "-n" )
			{
				suppress = true;
			}
			else if ( a == "-e" )
			{
				if ( i + 1 < args.Length )
				{
					i++;
					ParseAndAddScript( args[ i ], scripts );
				}
			}
			else if ( a == "-i" )
			{
				inPlace = true;
				backupSuffix = string.Empty; // no backup
			}
			else if ( a.StartsWith( "-i" ) && a.Length > 2 )
			{
				inPlace = true;
				backupSuffix = a.Substring( 2 ); // -iSUFFIX
			}
			else if ( a == "-?" )
			{
				PrintUsage( stdout );
				return 0;
			}
			else
			{
				// ignore other options
			}
		}

		for ( ; i < args.Length; i++ )
		{
			files.Add( args[ i ] );
		}

		// If no -e scripts provided, GNU sed treats first non-option arg as the script.
		// Handle both single-argument scripts ("1i this") and split forms where the insert token
		// ("1i") and the text are separate args (common on some shells / careless usage).
		if ( scripts.Count == 0 && files.Count > 0 )
		{
			var candidate = files[ 0 ];

			// Case: script provided as single arg (e.g. "1i this is foo" or "s/old/new/")
			if ( candidate.StartsWith( "s/" ) || Regex.IsMatch( candidate, @"^[0-9]+i\b" ) )
			{
				// If the candidate is of form "Ni" with no text (e.g. "1i") and there are additional
				// tokens, attempt to assemble the insert text from middle tokens and treat the last
				// token as filename (common when users forget to quote). Only do this when there
				// are at least three tokens (script-start, text..., filename).
				var simpleInsertOnly = Regex.IsMatch( candidate, @"^[0-9]+i$" );
				if ( simpleInsertOnly && files.Count >= 3 )
				{
					// join all tokens between the "Ni" token and the final token as the insert text
					var middle = string.Join( " ", files.GetRange( 1, files.Count - 2 ) );
					var scriptText = candidate + " " + middle;
					ParseAndAddScript( scriptText, scripts );
					// remaining files are just the last token (filename). If user intended multiple files,
					// they should quote the script; this heuristic is pragmatic.
					var last = files[ files.Count - 1 ];
					files.Clear();
					files.Add( last );
				}
				else
				{
					// Normal single-argument script
					ParseAndAddScript( candidate, scripts );
					files.RemoveAt( 0 );
				}
			}
		}

		if ( scripts.Count == 0 )
		{
			stderr.WriteLine( "sed: no scripts provided" );
			return 2;
		}

		if ( files.Count == 0 )
		{
			files.Add( "-" );
		}

		try
		{
			foreach ( var path in files )
			{
				if ( inPlace && path == "-" )
				{
					stderr.WriteLine( "sed: cannot edit standard input in-place" );
					return 2;
				}

				TextReader reader;
				if ( path == "-" )
				{
					reader = stdin;
				}
				else
				{
					reader = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
				}

				if ( inPlace && path != "-" )
				{
					var dir = Path.GetDirectoryName( path ) ?? ".";
					var tmp = Path.Combine( dir, $".sed.{Path.GetRandomFileName()}.tmp" );
					using ( reader )
					using ( var outStream = new FileStream( tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None ) )
					using ( var writer = new StreamWriter( outStream, ( (StreamReader)reader ).CurrentEncoding ?? Encoding.UTF8 ) )
					{
						int lineNo = 1;
						string? line;
						while ( ( line = reader.ReadLine() ) is not null )
						{
							// handle insert scripts: print inserted text(s) before the current line
							foreach ( var sc in scripts )
							{
								if ( sc.Kind == Script.KindT.Insert && sc.LineNumber == lineNo )
								{
									writer.WriteLine( sc.Text );
								}
							}

							var outLine = line;
							foreach ( var sc in scripts )
							{
								if ( sc.Kind == Script.KindT.Substitute )
								{
									var oldPat = sc.Pattern ?? string.Empty;
									var newText = sc.Replacement ?? string.Empty;
									var flags = sc.Flags ?? string.Empty;
									var regexOptions = RegexOptions.None;
									var replaceCount = flags.Contains( 'g' ) ? -1 : 1;
									outLine = RegexReplace( outLine, oldPat, newText, regexOptions, replaceCount );
								}
							}

							if ( !suppress )
							{
								writer.WriteLine( outLine );
							}
							lineNo++;
						}

						// If any insert command targets a line after EOF (e.g. N > last line),
						// we do not attempt to emulate sed 'i' after EOF. (Simple behavior.)
						writer.Flush();
					}

					if ( backupSuffix is not null && backupSuffix.Length > 0 )
					{
						var bak = path + backupSuffix;
						if ( File.Exists( bak ) )
						{
							File.Delete( bak );
						}
						File.Move( path, bak );
					}
					if ( File.Exists( path ) )
					{
						File.Delete( path );
					}
					File.Move( tmp, path );
				}
				else
				{
					using ( reader )
					{
						int lineNo = 1;
						string? line;
						while ( ( line = reader.ReadLine() ) is not null )
						{
							// insert scripts write even when -n is specified
							foreach ( var sc in scripts )
							{
								if ( sc.Kind == Script.KindT.Insert && sc.LineNumber == lineNo )
								{
									stdout.WriteLine( sc.Text );
								}
							}

							var outLine = line;
							foreach ( var sc in scripts )
							{
								if ( sc.Kind == Script.KindT.Substitute )
								{
									var oldPat = sc.Pattern ?? string.Empty;
									var newText = sc.Replacement ?? string.Empty;
									var flags = sc.Flags ?? string.Empty;
									var regexOptions = RegexOptions.None;
									var replaceCount = flags.Contains( 'g' ) ? -1 : 1;
									outLine = RegexReplace( outLine, oldPat, newText, regexOptions, replaceCount );
								}
							}

							if ( !suppress )
							{
								stdout.WriteLine( outLine );
							}
							lineNo++;
						}
					}
				}
			}

			return 0;
		}
		catch ( Exception ex )
		{
			stderr.WriteLine( $"sed: {ex.Message}" );
			return 1;
		}
	}

	private static void ParseAndAddScript( string scriptText, List<Script> scripts )
	{
		// attempt substitution parse first
		var m = Regex.Match( scriptText, @"^s/(?<old>.*?)/(?<new>.*?)/(?<flags>.*)$" );
		if ( !m.Success )
		{
			m = Regex.Match( scriptText, @"^s/(?<old>.*?)/(?<new>.*)$" );
		}
		if ( m.Success )
		{
			var oldPat = m.Groups[ "old" ].Value;
			var newText = m.Groups[ "new" ].Value;
			var flags = m.Groups[ "flags" ].Success ? m.Groups[ "flags" ].Value : string.Empty;
			scripts.Add( Script.Subst( oldPat, newText, flags ) );
			return;
		}

		// attempt insert: form "<number>i[ ]TEXT"
		var im = Regex.Match( scriptText, @"^(?<ln>[0-9]+)i(?:\s+)(?<text>.*)$" );
		if ( im.Success )
		{
			if ( int.TryParse( im.Groups[ "ln" ].Value, out var ln ) )
			{
				var text = im.Groups[ "text" ].Value;
				scripts.Add( Script.Insert( ln, text ) );
				return;
			}
		}

		// unsupported script: ignore (preserve original behavior)
	}

	private static string RegexReplace( string input, string pattern, string replacement, RegexOptions options, int maxReplacements )
	{
		try
		{
			if ( maxReplacements == -1 )
			{
				return Regex.Replace( input, pattern, replacement, options );
			}

			var regex = new Regex( pattern, options );
			var count = 0;
			return regex.Replace( input, m =>
			{
				count++;
				if ( count <= maxReplacements )
				{
					return replacement;
				}

				return m.Value;
			} );
		}
		catch
		{
			return input;
		}
	}

	private static void PrintUsage( TextWriter stdout )
	{
		stdout.WriteLine( "Usage: sed [-n] [-e script]... [-i[SUFFIX]] [file...]" );
		stdout.WriteLine( "  -n           suppress automatic printing of pattern space" );
		stdout.WriteLine( "  -e script    add the script to the commands to be executed" );
		stdout.WriteLine( "  -i[SUFFIX]   edit files in-place, optional backup SUFFIX" );
		stdout.WriteLine( "  -?           display this help and exit" );
	}
}
