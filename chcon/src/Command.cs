// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Chcon;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// chcon: change file security context (SELinux) by delegating to the platform `chcon`.
/// Supported options (delegated to system `chcon`):
///   -v, --verbose        explain what is being done
///   -R, --recursive      operate on files and directories recursively
///   --reference=RFILE    use RFILE's context instead of CONTEXT
///   -t TYPE              set SELinux file type to TYPE
///   -u USER              set SELinux user to USER
///   -r ROLE              set SELinux role to ROLE
///   -h                   affect symbolic link itself (where supported)
///   -?, --help           display this help and exit
/// Usage:
///   chcon [OPTION]... CONTEXT FILE...
/// Notes:
///   This implementation is a safe wrapper that invokes the system `chcon` on Unix-like systems.
///   On Windows an error is returned because SELinux contexts are not supported.
/// </summary>
public static class Command {

	private const System.Char SPACE = ' ';
	private const System.Char TAB = '\t';
	private const System.Char DQUOTE = '\"';
	private static readonly System.Char[] SPACE_TAB_DQUOTE;

	static Command() {
		SPACE_TAB_DQUOTE = new[] { SPACE, TAB, DQUOTE };
	}

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			PrintUsage( stdout );
			return 1;
		}

		// handle help quickly
		foreach ( var a in args ) {
			if ( a == "-?" || a == "--help" ) {
				PrintUsage( stdout );
				return 0;
			}
		}

		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			stderr.WriteLine( "chcon: operation not supported on Windows" );
			return 1;
		}

		// Verify `chcon` exists by attempting to run it with --version (best-effort)
		try {
			var check = new ProcessStartInfo {
				FileName = "chcon",
				Arguments = "--version",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using ( var p = Process.Start( check ) ) {
				if ( p is null )
					throw new InvalidOperationException( "unable to start 'chcon'" );
				// allow non-zero exit; presence is the more important check
				p.WaitForExit();
			}
		} catch {
			stderr.WriteLine( "chcon: system 'chcon' not found or not executable" );
			return 1;
		}

		// Build argument string preserving quoting for arguments that contain spaces
		var quoted = new List<string>();
		foreach ( var a in args ) {
			if ( string.IsNullOrEmpty( a ) )
				continue;
			if ( a.IndexOfAny( SPACE_TAB_DQUOTE ) >= 0 ) {
				quoted.Add( $"\"{a.Replace( "\"", "\\\"" )}\"" );
			} else {
				quoted.Add( a );
			}
		}
		var argString = string.Join( " ", quoted );

		var psi = new ProcessStartInfo {
			FileName = "chcon",
			Arguments = argString,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		try {
			using var proc = Process.Start( psi ) ?? throw new InvalidOperationException( "failed to start 'chcon'" );
			// forward stdout and stderr to the provided writers
			var outTask = proc.StandardOutput.BaseStream.CopyToAsync( Console.OpenStandardOutput() );
			var errTask = proc.StandardError.BaseStream.CopyToAsync( Console.OpenStandardError() );
			proc.WaitForExit();
			outTask.Wait();
			errTask.Wait();
			return proc.ExitCode;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"chcon: execution failed: {ex.Message}" );
			return 1;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: chcon [OPTION]... CONTEXT FILE..." );
		stdout.WriteLine( "  -v, --verbose        explain what is being done" );
		stdout.WriteLine( "  -R, --recursive      operate on files and directories recursively" );
		stdout.WriteLine( "  --reference=RFILE    use RFILE's context instead of CONTEXT" );
		stdout.WriteLine( "  -t TYPE              set SELinux file type to TYPE" );
		stdout.WriteLine( "  -u USER              set SELinux user to USER" );
		stdout.WriteLine( "  -r ROLE              set SELinux role to ROLE" );
		stdout.WriteLine( "  -h                   affect symbolic link itself (where supported)" );
		stdout.WriteLine( "  -?, --help           display this help and exit" );
	}
}
