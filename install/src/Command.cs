// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Install;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// install: copy files and set attributes (best-effort).
/// Supported options (best-effort implementation):
///   -d             create directories instead of copying files
///   -m MODE        set permission mode (octal, e.g. 0755)
///   -o OWNER       set file owner (Unix only)
///   -g GROUP       set file group (Unix only)
///   -t DIR         copy all SOURCE arguments into DIR
///   -T             treat DEST as a file (do not treat as directory)
///   -D             create all leading components of DEST before copying
///   -v, --verbose  explain what is being done
///   -s, --strip    run `strip` on installed file when available (Unix)
///   -? --help      display this help and exit
/// Notes:
///   This is a pragmatic wrapper: where platform support is limited it will call
///   system tools (`chmod`, `chown`, `strip`) on Unix. On Windows owner/group/mode
///   changes are no-ops (with a warning).
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var createDirs = false;
		var verbose = false;
		var stripFlag = false;
		var mode = (string?)null;
		var owner = (string?)null;
		var group = (string?)null;
		var targetDir = (string?)null;
		var treatDestAsFile = false;
		var makeParents = false;

		var remaining = new List<string>();
		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a == "-d" ) {
				createDirs = true;
				continue;
			}
			if ( a == "-v" || a == "--verbose" ) {
				verbose = true;
				continue;
			}
			if ( a == "-s" || a == "--strip" ) {
				stripFlag = true;
				continue;
			}
			if ( a == "-T" ) {
				treatDestAsFile = true;
				continue;
			}
			if ( a == "-D" ) {
				makeParents = true;
				continue;
			}
			if ( a == "-m" && i + 1 < args.Length ) {
				mode = args[ ++i ];
				continue;
			}
			if ( a == "-o" && i + 1 < args.Length ) {
				owner = args[ ++i ];
				continue;
			}
			if ( a == "-g" && i + 1 < args.Length ) {
				group = args[ ++i ];
				continue;
			}
			if ( a == "-t" && i + 1 < args.Length ) {
				targetDir = args[ ++i ];
				continue;
			}
			if ( a is "-?" or "--help" ) {
				PrintUsage( stdout );
				return 0;
			}
			remaining.Add( a );
		}

		try {
			if ( createDirs ) {
				if ( remaining.Count == 0 ) {
					stderr.WriteLine( "install: missing operand" );
					return 2;
				}
				foreach ( var d in remaining ) {
					if ( makeParents ) {
						Directory.CreateDirectory( d );
					} else {
						if ( !Directory.Exists( d ) )
							Directory.CreateDirectory( d );
					}
					if ( verbose )
						stdout.WriteLine( $"created directory '{d}'" );
					if ( !string.IsNullOrEmpty( mode ) ) {
						SetMode( d, mode, stderr, verbose );
					}
				}
				return 0;
			}

			// Copying/installing files
			if ( targetDir is not null ) {
				// copy all remaining into targetDir
				if ( !Directory.Exists( targetDir ) )
					Directory.CreateDirectory( targetDir );
				foreach ( var src in remaining ) {
					var fileName = Path.GetFileName( src );
					if ( fileName is null )
						fileName = src;
					var dest = Path.Combine( targetDir, fileName );
					CopyAndFinalize( src, dest, mode, owner, group, stripFlag, verbose, stderr, stdout );
				}
				return 0;
			}

			if ( remaining.Count == 0 ) {
				stderr.WriteLine( "install: missing operand" );
				return 2;
			}

			if ( remaining.Count == 1 ) {
				stderr.WriteLine( "install: missing destination" );
				return 2;
			}

			// multiple sources -> last arg must be directory unless -T specified
			var destArg = remaining[ ^1 ];
			var sources = remaining.GetRange( 0, remaining.Count - 1 );

			if ( sources.Count > 1 && !Directory.Exists( destArg ) && !treatDestAsFile ) {
				// attempt to create dest if -D specified
				if ( makeParents )
					Directory.CreateDirectory( destArg );
				if ( !Directory.Exists( destArg ) ) {
					stderr.WriteLine( $"install: target '{destArg}' is not a directory" );
					return 1;
				}
			}

			if ( Directory.Exists( destArg ) && !treatDestAsFile ) {
				foreach ( var src in sources ) {
					var dest = Path.Combine( destArg, Path.GetFileName( src )! );
					CopyAndFinalize( src, dest, mode, owner, group, stripFlag, verbose, stderr, stdout );
				}
			} else {
				// single source -> destArg is file path
				if ( sources.Count != 1 ) {
					stderr.WriteLine( "install: multiple sources but destination is not directory" );
					return 1;
				}
				CopyAndFinalize( sources[ 0 ], destArg, mode, owner, group, stripFlag, verbose, stderr, stdout );
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"install: {ex.Message}" );
			return 1;
		}
	}

	private static void CopyAndFinalize(
		string src, string dest, string? mode, string? owner, string? group, bool stripFlag,
		bool verbose, TextWriter? stderr, TextWriter? stdout
	) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		try {
			if ( !File.Exists( src ) )
				throw new FileNotFoundException( "source not found", src );

			// Ensure destination directory exists
			var dir = Path.GetDirectoryName( dest );
			if ( !string.IsNullOrEmpty( dir ) && !Directory.Exists( dir ) )
				Directory.CreateDirectory( dir );

			File.Copy( src, dest, overwrite: true );

			// mode/owner/group/strip best-effort
			if ( !string.IsNullOrEmpty( mode ) )
				SetMode( dest, mode, stderr, verbose );
			if ( !string.IsNullOrEmpty( owner ) || !string.IsNullOrEmpty( group ) )
				SetOwnerGroup( dest, owner, group, stderr );
			if ( stripFlag )
				RunStrip( dest, stderr, Console.Out );
			if ( stdout is not null )
				stdout.WriteLine( dest );
		} catch ( Exception ex ) {
			stderr!.WriteLine( $"install: {src}: {ex.Message}" );
			throw;
		}
	}

	private static void SetMode( string path, string modeText, TextWriter? stderr, bool verbose ) {
		stderr ??= Console.Error;
		if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			// use chmod if available
			try {
				var psi = new ProcessStartInfo {
					FileName = "chmod",
					Arguments = $"{modeText} \"{path}\"",
					RedirectStandardOutput = false,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				using var p = Process.Start( psi );
				p?.WaitForExit();
				if ( p is not null && p.ExitCode != 0 ) {
					stderr.WriteLine( $"install: chmod failed for '{path}'" );
				} else if ( verbose ) {
					Console.Out.WriteLine( $"mode of '{path}' set to {modeText}" );
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"install: chmod invocation failed: {ex.Message}" );
			}
		} else {
			// Windows: best-effort no-op
			if ( verbose )
				Console.Out.WriteLine( $"warning: mode change requested but not supported on Windows for '{path}'" );
		}
	}

	private static void SetOwnerGroup( string path, string? owner, string? group, TextWriter? stderr ) {
		stderr ??= Console.Error;
		if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			try {
				var target = string.Empty;
				if ( !string.IsNullOrEmpty( owner ) && !string.IsNullOrEmpty( group ) )
					target = $"{owner}:{group}";
				else if ( !string.IsNullOrEmpty( owner ) )
					target = owner;
				else if ( !string.IsNullOrEmpty( group ) )
					target = $":{group}";
				else
					return;

				var psi = new ProcessStartInfo {
					FileName = "chown",
					Arguments = $"{target} \"{path}\"",
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				using var p = Process.Start( psi );
				p?.WaitForExit();
				if ( p is not null && p.ExitCode != 0 ) {
					stderr.WriteLine( $"install: chown failed for '{path}'" );
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"install: chown invocation failed: {ex.Message}" );
			}
		} else {
			stderr.WriteLine( $"install: owner/group change requested but not supported on Windows for '{path}'" );
		}
	}

	private static void RunStrip( string path, TextWriter? stderr, TextWriter? stdout ) {
		stderr ??= Console.Error;
		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			stderr.WriteLine( "install: strip requested but not supported on Windows" );
			return;
		}
		try {
			var psi = new ProcessStartInfo {
				FileName = "strip",
				Arguments = $"\"{path}\"",
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using var p = Process.Start( psi );
			if ( p is null ) {
				stderr.WriteLine( "install: failed to start strip" );
				return;
			}
			p.WaitForExit();
			if ( p.ExitCode != 0 ) {
				stderr.WriteLine( $"install: strip failed for '{path}'" );
			} else {
				stdout?.WriteLine( $"stripped '{path}'" );
			}
		} catch ( Exception ex ) {
			stderr.WriteLine( $"install: strip invocation failed: {ex.Message}" );
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: install [OPTION]... SOURCE... DEST" );
		stdout.WriteLine( "  -d             create directories instead of copying files" );
		stdout.WriteLine( "  -m MODE        set permission mode (octal)" );
		stdout.WriteLine( "  -o OWNER       set owner (Unix only)" );
		stdout.WriteLine( "  -g GROUP       set group (Unix only)" );
		stdout.WriteLine( "  -t DIR         copy all SOURCE arguments into DIR" );
		stdout.WriteLine( "  -T             treat DEST as a file" );
		stdout.WriteLine( "  -D             create leading components of DEST" );
		stdout.WriteLine( "  -s, --strip    run strip on installed file (Unix)" );
		stdout.WriteLine( "  -v, --verbose  explain what is being done" );
		stdout.WriteLine( "  -?, --help     display this help and exit" );
	}
}
