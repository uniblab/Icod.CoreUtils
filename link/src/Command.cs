// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Link;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// link: create links between files.
/// Supported options:
///   -s, --symbolic    create symbolic links instead of hard links
///   -f, --force       remove existing destination
///   -t DIR            create links in DIR for each SOURCE
///   -? --help         display this help and exit
/// Notes:
///   On Unix this wrapper prefers the native `ln` utility when available for full feature parity.
///   On Windows creates hard links via CreateHardLink and symbolic links via File/Directory APIs.
/// </summary>
public static class Command {
	[DllImport( "Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true )]
	private static extern bool CreateHardLink( string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes );

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var symbolic = false;
		var force = false;
		var targetDir = (string?)null;
		var remaining = new List<string>();

		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a is "-s" or "--symbolic" ) {
				symbolic = true;
				continue;
			}
			if ( a is "-f" or "--force" ) {
				force = true;
				continue;
			}
			if ( a is "-v" or "--verbose" ) {
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
			if ( targetDir is not null ) {
				if ( !Directory.Exists( targetDir ) )
					Directory.CreateDirectory( targetDir );
				foreach ( var src in remaining ) {
					var name = Path.GetFileName( src )!;
					var linkPath = Path.Combine( targetDir, name );
					if ( force && File.Exists( linkPath ) )
						File.Delete( linkPath );
					CreateLink( src, linkPath, symbolic, stderr, stdout );
				}
				return 0;
			}

			if ( remaining.Count == 0 ) {
				stderr.WriteLine( "link: missing operand" );
				return 2;
			}

			if ( remaining.Count == 1 ) {
				stderr.WriteLine( "link: missing destination" );
				return 2;
			}

			if ( remaining.Count > 2 ) {
				// last arg must be directory
				var destDir = remaining[ ^1 ];
				if ( !Directory.Exists( destDir ) ) {
					stderr.WriteLine( $"link: target '{destDir}' is not a directory" );
					return 1;
				}
				for ( var i = 0; i < remaining.Count - 1; i++ ) {
					var src = remaining[ i ];
					var linkPath = Path.Combine( destDir, Path.GetFileName( src )! );
					if ( force && File.Exists( linkPath ) )
						File.Delete( linkPath );
					CreateLink( src, linkPath, symbolic, stderr, stdout );
				}
				return 0;
			}

			// exactly two args: source and linkname
			var sourcePath = remaining[ 0 ];
			var linkName = remaining[ 1 ];
			if ( Directory.Exists( linkName ) ) {
				// place inside directory
				var linkPath = Path.Combine( linkName, Path.GetFileName( sourcePath )! );
				if ( force && File.Exists( linkPath ) )
					File.Delete( linkPath );
				CreateLink( sourcePath, linkPath, symbolic, stderr, stdout );
			} else {
				if ( force && File.Exists( linkName ) )
					File.Delete( linkName );
				CreateLink( sourcePath, linkName, symbolic, stderr, stdout );
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"link: {ex.Message}" );
			return 1;
		}
	}

	private static void CreateLink( string src, string linkPath, bool symbolic, TextWriter? stderr, TextWriter? stdout ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( !File.Exists( src ) && !Directory.Exists( src ) ) {
			stderr.WriteLine( $"link: source '{src}' does not exist" );
			return;
		}

		if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			// Prefer system ln for fidelity
			try {
				var args = new StringBuilder();
				if ( symbolic )
					args.Append( "-s " );
				args.Append( $"\"{src}\" \"{linkPath}\"" );
				var psi = new ProcessStartInfo {
					FileName = "ln",
					Arguments = args.ToString(),
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				using var p = Process.Start( psi );
				if ( p is null )
					throw new InvalidOperationException( "failed to start ln" );
				p.WaitForExit();
				if ( p.ExitCode != 0 ) {
					stderr.WriteLine( $"link: ln failed for '{linkPath}'" );
				} else {
					stdout.WriteLine( linkPath );
				}
				return;
			} catch {
				// fallthrough to managed implementation
			}
		}

		// Managed implementation (Windows or fallback)
		try {
			if ( symbolic ) {
				// prefer File/Directory symlink helpers
				if ( Directory.Exists( src ) ) {
					Directory.CreateSymbolicLink( linkPath, src );
				} else {
					File.CreateSymbolicLink( linkPath, src );
				}
				stdout.WriteLine( linkPath );
				return;
			}

			// hard link
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				var ok = CreateHardLink( linkPath, src, IntPtr.Zero );
				if ( !ok ) {
					var err = Marshal.GetLastWin32Error();
					stderr.WriteLine( $"link: CreateHardLink failed: {err}" );
				} else {
					stdout.WriteLine( linkPath );
				}
			} else {
				// attempt native link syscall via /bin/ln fallback if earlier attempt failed
				var psi = new ProcessStartInfo {
					FileName = "ln",
					Arguments = $"\"{src}\" \"{linkPath}\"",
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				using var p = Process.Start( psi );
				if ( p is null )
					throw new InvalidOperationException( "failed to start ln" );
				p.WaitForExit();
				if ( p.ExitCode != 0 )
					stderr.WriteLine( $"link: ln failed for '{linkPath}'" );
				else
					stdout.WriteLine( linkPath );
			}
		} catch ( Exception ex ) {
			stderr.WriteLine( $"link: {ex.Message}" );
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: link [OPTION]... SOURCE... TARGET" );
		stdout.WriteLine( "  -s, --symbolic    create symbolic links instead of hard links" );
		stdout.WriteLine( "  -f, --force       remove existing destination" );
		stdout.WriteLine( "  -t DIR            create links in DIR for each SOURCE" );
		stdout.WriteLine( "  -v, --verbose     explain what is being done" );
		stdout.WriteLine( "  -?, --help        display this help and exit" );
	}
}
