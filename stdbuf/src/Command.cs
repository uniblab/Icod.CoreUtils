// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.StdBuf;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Best-effort `stdbuf`:
///  - Parses buffering options but prefers to invoke system `stdbuf` when available.
///  - If `stdbuf` is not present, runs the command directly and prints a warning that
///    fine-grained buffering control is unavailable on this platform/build.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 || args.Contains( "-h" ) || args.Contains( "--help" ) ) {
			PrintUsage( stdout );
			return 0;
		}

		// Collect options until we hit the command to run
		var options = new System.Collections.Generic.List<string>();
		var remainder = new System.Collections.Generic.List<string>();
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a.StartsWith( '-' ) && a.Length > 1 && ( a == "-i" || a == "-o" || a == "-e" || a.StartsWith( "-i" ) || a.StartsWith( "-o" ) || a.StartsWith( "-e" ) ) ) {
				options.Add( a );
				// if option requires separate arg (e.g. -iL), this simplistic parse just takes token as-is
				continue;
			}
			// first non-option is the command
			break;
		}

		if ( i >= args.Length ) {
			stderr.WriteLine( "stdbuf: missing command" );
			return 2;
		}

		// Build command + args to run
		for ( ; i < args.Length; i++ )
			remainder.Add( args[ i ] );

		// Try to run system 'stdbuf' if available (only meaningful on Unix-like systems)
		if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			try {
				var psi = new ProcessStartInfo {
					FileName = "stdbuf",
					Arguments = string.Join( " ", options.Concat( remainder ).Select( QuoteArg ) ),
					UseShellExecute = false,
					RedirectStandardOutput = false,
					RedirectStandardError = false,
					RedirectStandardInput = false,
					CreateNoWindow = true
				};
				using var p = Process.Start( psi );
				if ( p is null ) {
					stderr.WriteLine( "stdbuf: failed to start 'stdbuf' command" );
					return 1;
				}
				p.WaitForExit();
				return p.ExitCode;
			} catch {
				// fall through to fallback
			}
		}

		// Fallback: run the requested command directly without buffering control.
		stderr.WriteLine( "stdbuf: warning: platform does not support stdbuf control; running command without buffering changes" );
		try {
			var psi2 = new ProcessStartInfo {
				FileName = remainder[ 0 ],
				Arguments = string.Join( " ", remainder.Skip( 1 ).Select( QuoteArg ) ),
				UseShellExecute = false,
				RedirectStandardOutput = false,
				RedirectStandardError = false,
				RedirectStandardInput = false,
				CreateNoWindow = true
			};
			using var p2 = Process.Start( psi2 );
			if ( p2 is null ) {
				stderr.WriteLine( "stdbuf: failed to start command" );
				return 1;
			}
			p2.WaitForExit();
			return p2.ExitCode;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"stdbuf: {ex.Message}" );
			return 1;
		}
	}

	private static string QuoteArg( string s ) {
		if ( string.IsNullOrEmpty( s ) )
			return "\"\"";
		if ( s.Contains( ' ' ) || s.Contains( '"' ) )
			return $"\"{s.Replace( "\"", "\\\"" )}\"";
		return s;
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: stdbuf OPTION... COMMAND [ARG]..." );
		stdout.WriteLine( "  -i MODE    adjust stdin buffering (L,0,F)" );
		stdout.WriteLine( "  -o MODE    adjust stdout buffering (L,0,F)" );
		stdout.WriteLine( "  -e MODE    adjust stderr buffering (L,0,F)" );
		stdout.WriteLine( "With no available 'stdbuf' this program runs COMMAND without buffering control." );
	}
}
