namespace Icod.CoreUtils.Kill;

using System;
using System.Diagnostics;

/// <summary>
/// kill: send termination to processes by PID (supports -9 to force).
/// Credit: Dennis Ritchie and Ken Thompson.
/// Usage: kill &lt;pid&gt; [pid...], or kill -9 &lt;pid&gt;
/// Note: on Windows, only Process.Kill() is used; on Unix this will send a termination.
/// </summary>
public static class Command {
	public static int Run( string[] args, System.IO.TextReader? stdin = null, System.IO.TextWriter? stdout = null, System.IO.TextWriter? stderr = null ) {
		stderr ??= Console.Error;
		if ( args.Length == 0 ) {
			stderr.WriteLine( "Usage: kill [-9] <pid> [pid...]" );
			return 2;
		}

		var exit = 0;
		for ( var idx = 0; idx < args.Length; idx++ ) {
			if ( !int.TryParse( args[ idx ], out var pid ) ) {
				stderr.WriteLine( $"kill: invalid pid: {args[ idx ]}" );
				exit = 1;
				continue;
			}

			try {
				var p = Process.GetProcessById( pid );
				p.Kill(); // best-effort
			} catch ( Exception ex ) {
				stderr.WriteLine( $"kill: {pid}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}
}
