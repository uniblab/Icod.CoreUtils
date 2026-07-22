namespace Icod.CoreUtils.Ps;

using System;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// ps: list processes (basic). Credit: Bell Labs.
/// Usage: ps
/// </summary>
public static partial class Command {
	public static int Run( string[] args, System.IO.TextReader? stdin = null, System.IO.TextWriter? stdout = null, System.IO.TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		try {
			var procs = Process.GetProcesses().OrderBy( p => p.Id );
			stdout.WriteLine( $"{"PID",6} {"Name",-30} {"StartTime",-24}" );
			foreach ( var p in procs ) {
				string start;
				try {
					start = p.StartTime.ToString( "s" );
				} catch {
					start = "";
				}

				stdout.WriteLine( $"{p.Id,6} {p.ProcessName,-30} {start,-24}" );
			}

			return 0;
		} catch ( Exception ex ) {
			stderr?.WriteLine( $"ps: {ex.Message}" );
			return 1;
		}
	}
}
