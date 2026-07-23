// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Chcon;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// chcon: change file SELinux security context wrapper.
/// This implementation delegates to the system `chcon` when available on Unix-like systems.
/// On Windows the operation is not supported and an error is returned.
/// Supported options:
///   -v, --verbose    print changed files (delegated behavior)
///   -? --help        display this help and exit
/// Usage:
///   chcon CONTEXT FILE...
/// Note: this is a minimal wrapper that calls out to the platform `chcon` binary.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			PrintUsage( stdout );
			return 1;
		}

		// handle help
		foreach ( var a in args ) {
			if ( a is "-?" or "--help" ) {
				PrintUsage( stdout );
				return 0;
			}
		}

		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			stderr.WriteLine( "chcon: operation not supported on Windows" );
			return 1;
		}

		// Ensure `chcon` is available on PATH
		var chconExe = "chcon";
		try {
			var psiCheck = new ProcessStartInfo {
				FileName = chconExe,
				Arguments = "--version",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using ( var p = Process.Start( psiCheck ) ) {
				if ( p is null ) {
					stderr.WriteLine( "chcon: unable to start chcon" );
					return 1;
				}
				p.WaitForExit();
				if ( p.ExitCode != 0 ) {
					// still allow: some platforms may not support --version; fallback to existence check below
				}
			}
		} catch {
			// chcon not found or cannot be executed
			stderr.WriteLine( "chcon: system 'chcon' not found or not executable" );
			return 1;
		}

		// Build argument string preserving quoting for arguments that contain spaces
		var quotedArgs = new List<string>();
		foreach ( var a in args ) {
			if ( string.IsNullOrEmpty( a ) )
				continue;
			if ( a.IndexOfAny( new[] { ' ', '\t', '"' } ) >= 0 ) {
				quotedArgs.Add( $"\"{a.Replace( "\"", "\\\"" )}\"" );
			} else {
				quotedArgs.Add( a );
			}
		}

		var argString = string.Join( " ", quotedArgs );

		var psi = new ProcessStartInfo {
			FileName = chconExe,
			Arguments = argString,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		try {
			using var proc = Process.Start( psi ) ?? throw new InvalidOperationException( "failed to start chcon" );
			// forward output
			var stdoutTask = proc.StandardOutput.BaseStream.CopyToAsync( Console.OpenStandardOutput() );
			var stderrTask = proc.StandardError.BaseStream.CopyToAsync( Console.OpenStandardError() );
			proc.WaitForExit();
			stdoutTask.Wait();
			stderrTask.Wait();
			return proc.ExitCode;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"chcon: execution failed: {ex.Message}" );
			return 1;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: chcon [OPTION]... CONTEXT FILE..." );
		stdout.WriteLine( "  -v, --verbose    print verbose information" );
		stdout.WriteLine( "  -?, --help       display this help and exit" );
	}
}
