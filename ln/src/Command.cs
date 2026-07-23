// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Ln;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

/// <summary>
/// Minimal `ln` implementation:
///  - supports -s (symbolic), -f (force overwrite), -v (verbose)
///  - best-effort cross-platform behavior for hard and symbolic links
/// </summary>
public static class Command {
	[DllImport( "Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode )]
	private static extern bool CreateHardLink( string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes );

	[DllImport( "libc", SetLastError = true )]
	private static extern int link( string oldpath, string newpath );

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var symbolic = false;
		var force = false;
		var verbose = false;
		var files = new System.Collections.Generic.List<string>();

		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a == "-s" ) {
				symbolic = true;
				continue;
			}
			if ( a == "-f" ) {
				force = true;
				continue;
			}
			if ( a == "-v" ) {
				verbose = true;
				continue;
			}
			if ( a == "-h" || a == "--help" ) {
				PrintUsage( stdout );
				return 0;
			}
			files.Add( a );
		}

		if ( files.Count < 2 ) {
			stderr.WriteLine( "ln: missing file operand" );
			return 2;
		}

		var dest = files[ ^1 ];
		var sources = files.GetRange( 0, files.Count - 1 );

		// If multiple sources, dest must be directory
		if ( sources.Count > 1 ) {
			if ( !Directory.Exists( dest ) ) {
				stderr.WriteLine( $"ln: target '{dest}' is not a directory" );
				return 1;
			}
			foreach ( var src in sources ) {
				var name = Path.GetFileName( src ) ?? src;
				var d = Path.Combine( dest, name );
				if ( !TryLink( src, d, symbolic, force, stdout, stderr, verbose ) )
					return 1;
			}
			return 0;
		}

		// single source
		var source = sources[ 0 ];
		var finalDest = dest;
		if ( Directory.Exists( dest ) ) {
			var name = Path.GetFileName( source ) ?? source;
			finalDest = Path.Combine( dest, name );
		}

		return TryLink( source, finalDest, symbolic, force, stdout, stderr, verbose ) ? 0 : 1;
	}

	private static bool TryLink( string src, string dest, bool symbolic, bool force, TextWriter stdout, TextWriter stderr, bool verbose ) {
		try {
			if ( !File.Exists( src ) && !Directory.Exists( src ) ) {
				stderr.WriteLine( $"ln: '{src}': No such file or directory" );
				return false;
			}

			if ( File.Exists( dest ) || Directory.Exists( dest ) || IsSymlink( dest ) ) {
				if ( !force ) {
					stderr.WriteLine( $"ln: failed to create link '{dest}': File exists" );
					return false;
				}
				// remove existing
				try {
					var attr = File.GetAttributes( dest );
					if ( attr.HasFlag( FileAttributes.Directory ) && !IsSymlink( dest ) )
						Directory.Delete( dest );
					else
						File.Delete( dest );
				} catch {
					// ignore removal error - attempt link anyway
				}
			}

			if ( symbolic ) {
				// create symbolic link
				if ( Directory.Exists( src ) ) {
					Directory.CreateSymbolicLink( dest, src );
				} else {
					File.CreateSymbolicLink( dest, src );
				}
			} else {
				// hard link (files only)
				if ( Directory.Exists( src ) ) {
					stderr.WriteLine( $"ln: hard link not supported for directories: '{src}'" );
					return false;
				}

				if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
					if ( !CreateHardLink( dest, src, IntPtr.Zero ) ) {
						var err = Marshal.GetLastWin32Error();
						stderr.WriteLine( $"ln: failed to create hard link '{dest}': {err}" );
						return false;
					}
				} else {
					if ( link( src, dest ) != 0 ) {
						var errno = Marshal.GetLastWin32Error();
						stderr.WriteLine( $"ln: failed to create hard link '{dest}': {errno}" );
						return false;
					}
				}
			}

			if ( verbose )
				stdout.WriteLine( $"'{dest}' -> '{src}'" );

			return true;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"ln: {ex.Message}" );
			return false;
		}
	}

	private static bool IsSymlink( string path ) {
		try {
			if ( !File.Exists( path ) && !Directory.Exists( path ) )
				return false;
			var fi = new FileInfo( path );
			return fi.LinkTarget is not null;
		} catch {
			return false;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: ln [OPTION]... SOURCE... DEST" );
		stdout.WriteLine( "  -s        make symbolic links instead of hard links" );
		stdout.WriteLine( "  -f        remove existing destination files" );
		stdout.WriteLine( "  -v        print name of each linked file" );
	}
}
