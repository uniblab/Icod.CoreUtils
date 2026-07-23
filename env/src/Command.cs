// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Env;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Minimal `env`:
///  - print environment when run with no command or only assignments
///  - support KEY=VALUE assignments followed by COMMAND to run with modified environment
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			PrintEnvironment( stdout );
			return 0;
		}

		var assignments = new Dictionary<string, string>( StringComparer.Ordinal );
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a.Contains( '=' ) ) {
				var idx = a.IndexOf( '=' );
				var k = a.Substring( 0, idx );
				var v = a.Substring( idx + 1 );
				assignments[ k ] = v;
			} else
				break;
		}

		// no command -> print resulting environment
		if ( i >= args.Length ) {
			var env = new Dictionary<string, string>( StringComparer.Ordinal );
			foreach ( DictionaryEntry de in Environment.GetEnvironmentVariables() )
				env[ (string)de.Key ] = (string)de.Value!;
			foreach ( var kv in assignments )
				env[ kv.Key ] = kv.Value;
			foreach ( var kv in env.OrderBy( k => k.Key ) )
				stdout.WriteLine( $"{kv.Key}={kv.Value}" );
			return 0;
		}

		// run command with modified environment
		var cmd = args[ i ];
		var cmdArgs = args.Length > i + 1 ? string.Join( " ", args[ ( i + 1 ).. ] ) : string.Empty;
		try {
			var psi = new ProcessStartInfo {
				FileName = cmd,
				Arguments = cmdArgs,
				UseShellExecute = false,
				RedirectStandardOutput = false,
				RedirectStandardError = false,
				RedirectStandardInput = false,
				CreateNoWindow = true
			};
			// copy current environment and apply assignments
			foreach ( DictionaryEntry de in Environment.GetEnvironmentVariables() )
				psi.Environment[ de.Key!.ToString()! ] = de.Value?.ToString() ?? string.Empty;
			foreach ( var kv in assignments )
				psi.Environment[ kv.Key ] = kv.Value;
			using var p = Process.Start( psi );
			if ( p is null ) {
				stderr.WriteLine( "env: failed to start command" );
				return 1;
			}
			p.WaitForExit();
			return p.ExitCode;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"env: {ex.Message}" );
			return 1;
		}
	}

	private static void PrintEnvironment( TextWriter stdout ) {
		foreach ( DictionaryEntry de in Environment.GetEnvironmentVariables() )
			stdout.WriteLine( $"{de.Key}={de.Value}" );
	}
}
