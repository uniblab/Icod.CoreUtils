// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Nice;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Best-effort `nice`:
///  - `nice -n ADJ COMMAND [ARGS...]` runs COMMAND with adjusted niceness.
///  - without COMMAND prints current niceness (approximate on Windows).
/// </summary>
public static class Command {
	private const int PRIO_PROCESS = 0;

	[DllImport( "libc", SetLastError = true )]
	private static extern int getpriority( int which, int who );

	[DllImport( "libc", SetLastError = true )]
	private static extern int setpriority( int which, int who, int prio );

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		int adj = 10; // default adjustment used by some `nice` implementations when running a command
		var i = 0;
		if ( args.Length >= 2 && args[ 0 ] == "-n" ) {
			if ( !int.TryParse( args[ 1 ], out adj ) ) {
				stderr.WriteLine( "nice: invalid increment" );
				return 2;
			}
			i = 2;
		} else if ( args.Length >= 1 && args[ 0 ] == "-h" || ( args.Length >= 1 && args[ 0 ] == "--help" ) ) {
			PrintUsage( stdout );
			return 0;
		}

		// no command -> print current niceness
		if ( i >= args.Length ) {
			try {
				if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
					var cur = getpriority( PRIO_PROCESS, 0 );
					stdout.WriteLine( cur );
				} else {
					// approximate mapping from PriorityClass
					var pc = Process.GetCurrentProcess().PriorityClass;
					var approx = pc switch {
						ProcessPriorityClass.RealTime => -20,
						ProcessPriorityClass.High => -10,
						ProcessPriorityClass.AboveNormal => -5,
						ProcessPriorityClass.Normal => 0,
						ProcessPriorityClass.BelowNormal => 5,
						ProcessPriorityClass.Idle => 19,
						_ => 0
					};
					stdout.WriteLine( approx );
				}
				return 0;
			} catch ( Exception ex ) {
				stderr.WriteLine( $"nice: {ex.Message}" );
				return 1;
			}
		}

		// run command with adjusted niceness
		var cmd = args[ i ];
		var cmdArgs = string.Empty;
		if ( i + 1 < args.Length )
			cmdArgs = string.Join( " ", args[ ( i + 1 ).. ] );

		try {
			var psi = new ProcessStartInfo {
				FileName = cmd,
				Arguments = cmdArgs,
				UseShellExecute = false,
			};
			using var p = Process.Start( psi );
			if ( p is null ) {
				stderr.WriteLine( "nice: failed to start command" );
				return 1;
			}

			// Adjust child priority best-effort
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				// attempt to increase the niceness (lower priority) by adj
				try {
					var pid = p.Id;
					var cur = getpriority( PRIO_PROCESS, pid );
					var target = cur + adj;
					_ = setpriority( PRIO_PROCESS, pid, target );
				} catch {
					// ignore failures
				}
			} else {
				// map adjustment to ProcessPriorityClass (best-effort)
				try {
					if ( adj >= 15 )
						p.PriorityClass = ProcessPriorityClass.Idle;
					else if ( adj >= 5 )
						p.PriorityClass = ProcessPriorityClass.BelowNormal;
					else if ( adj <= -10 )
						p.PriorityClass = ProcessPriorityClass.High;
					else if ( adj < 0 )
						p.PriorityClass = ProcessPriorityClass.AboveNormal;
					else
						p.PriorityClass = ProcessPriorityClass.Normal;
				} catch {
					// ignore failures
				}
			}

			p.WaitForExit();
			return p.ExitCode;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"nice: {ex.Message}" );
			return 1;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: nice [-n ADJ] COMMAND [ARG]..." );
		stdout.WriteLine( "Run COMMAND with an adjusted scheduling priority (best-effort)." );
	}
}
