// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Env;

using System;
using System.IO;
using System.Collections;

/// <summary>
/// env: print or modify the environment.
/// Behavior: with no arguments prints current environment variables.
/// If one or more NAME=VALUE assignments are provided without a command, sets them in the current process and prints environment.
/// Running arbitrary commands is not implemented in this BCL-only port.
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var assignments = new System.Collections.Generic.List<string>();
		var rem = new System.Collections.Generic.List<string>();
		foreach ( var a in args ) {
			if ( a.Contains( '=' ) ) {
				assignments.Add( a );
			} else {
				rem.Add( a );
			}
		}

		foreach ( var assign in assignments ) {
			var idx = assign.IndexOf( '=' );
			if ( idx <= 0 ) {
				stderr.WriteLine( $"env: invalid assignment '{assign}'" );
				return 1;
			}

			var name = assign.Substring( 0, idx );
			var value = assign.Substring( idx + 1 );
			try {
				Environment.SetEnvironmentVariable( name, value );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"env: cannot set '{name}': {ex.Message}" );
				return 1;
			}
		}

		if ( rem.Count > 0 ) {
			// Running commands is not implemented in the BCL-only portable port.
			stderr.WriteLine( "env: executing commands is not implemented in this port" );
			throw new NotImplementedException( "env: running external commands is not implemented in BCL-only port." );
		}

		// Print environment
		foreach ( DictionaryEntry de in Environment.GetEnvironmentVariables() ) {
			stdout.WriteLine( $"{de.Key}={de.Value}" );
		}

		return 0;
	}
}
