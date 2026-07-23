// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Pinky;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Best-effort `pinky`: on Unix platforms try to invoke the system `who`/`pinky` output.
/// On platforms without these tools, fall back to printing the current username and hostname.
/// </summary>
public static class Command {
	private const System.String USwitch = "-u";
	private static readonly System.String[] USwitchArray;

	static Command() {
		USwitchArray = new[] { USwitch };
	}

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length > 0 && ( args[ 0 ] == "-h" || args[ 0 ] == "--help" ) ) {
			PrintUsage( stdout );
			return 0;
		}

		try {
			// Preferred: try `pinky` or `who` if available on the system (Unix).
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				var tried = TryRunTool( "pinky", args, stdout, stderr ) || TryRunTool( "who", USwitchArray, stdout, stderr );
				if ( tried ) {
					return 0;
				}
			}

			// Fallback: print current username and hostname.
			var user = Environment.UserName ?? string.Empty;
			string host;
			try {
				host = Environment.MachineName ?? string.Empty;
			} catch {
				host = string.Empty;
			}

			var sb = new StringBuilder();
			sb.Append( user );
			if ( !string.IsNullOrEmpty( host ) ) {
				sb.Append( ' ' );
				sb.Append( host );
			}

			stdout.WriteLine( sb.ToString() );
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"pinky: {ex.Message}" );
			return 1;
		}
	}

	private static bool TryRunTool( string tool, string[] args, TextWriter stdout, TextWriter stderr ) {
		try {
			var psi = new ProcessStartInfo {
				FileName = tool,
				Arguments = string.Join( " ", args ),
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using var p = Process.Start( psi );
			if ( p is null )
				return false;
			var outText = p.StandardOutput.ReadToEnd();
			var errText = p.StandardError.ReadToEnd();
			p.WaitForExit();
			if ( !string.IsNullOrWhiteSpace( outText ) ) {
				stdout.Write( outText );
				return true;
			}
			if ( !string.IsNullOrWhiteSpace( errText ) ) {
				stderr.Write( errText );
				return true;
			}
		} catch {
			// ignore and indicate failure
		}
		return false;
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: pinky [OPTION]" );
		stdout.WriteLine( "Best-effort: prints information about the current user/session." );
	}
}
