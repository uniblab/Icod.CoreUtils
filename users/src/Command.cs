// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Users;

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

/// <summary>
/// Best-effort `users`: prints the login names of users currently logged in,
/// separated by spaces. Tries to invoke system `users` or `who -q` on Unix-like
/// systems; falls back to the current user name when not available.
/// </summary>
public static class Command {
	private const System.String QSwitch = "-q";
	private static readonly System.String[] QSwitchArray;

	static Command() {
			QSwitchArray = new[] { QSwitch };
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
			// Try `who -q` which prints names followed by a line like: # users=2
			if ( TryRunTool( "who", QSwitchArray, out var outText ) ) {
				// who -q prints a names line and a trailing summary; take the first line
				using var sr = new StringReader( outText );
				var first = sr.ReadLine() ?? string.Empty;
				stdout.Write( first.Trim() );
				stdout.WriteLine();
				return 0;
			}

			// Fallback: current user
			stdout.WriteLine( Environment.UserName ?? string.Empty );
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"users: {ex.Message}" );
			return 1;
		}
	}

	private static bool TryRunTool( string tool, string[] args, out string output ) {
		output = string.Empty;
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
			output = p.StandardOutput.ReadToEnd();
			p.WaitForExit();
			return p.ExitCode == 0 && !string.IsNullOrEmpty( output );
		} catch {
			return false;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: users" );
	}
}
