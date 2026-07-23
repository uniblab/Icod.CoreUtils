// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Sync;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// `sync` flushes filesystem buffers. Best-effort: invoke system `sync` on Unix.
/// On platforms without a `sync` utility this is a nop but returns success.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length > 0 && ( args[ 0 ] == "-h" || args[ 0 ] == "--help" ) ) {
			PrintUsage( stdout );
			return 0;
		}

		if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			try {
				var psi = new ProcessStartInfo {
					FileName = "sync",
					UseShellExecute = false,
					RedirectStandardOutput = false,
					RedirectStandardError = false,
					CreateNoWindow = true
				};
				using var p = Process.Start( psi );
				if ( p is null ) {
					stderr.WriteLine( "sync: failed to start 'sync' command" );
					return 1;
				}
				p.WaitForExit();
				return p.ExitCode;
			} catch ( Exception ex ) {
				stderr.WriteLine( $"sync: invocation failed: {ex.Message}" );

				return 1;
			}
		}

		// Windows: best-effort no-op ( flushing volumes programmatically is complex ).
		// Return success but warn.
		stderr.WriteLine( "sync: warning: platform does not provide global sync; operation is a no-op on Windows" );
		return 0;
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: sync" );
		stdout.WriteLine( "Flush filesystem buffers. Best-effort implementation." );
	}
}
