// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Yes;

using System;
using System.IO;
using System.Text;
using System.Threading;

/// <summary>
/// yes: output a string repeatedly until killed.
/// If no arguments, prints 'y'.
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var text = "y";
		if ( args.Length > 0 ) {
			text = string.Join( " ", args );
		}

		try {
			while ( true ) {
				stdout.WriteLine( text );
				// small sleep to be cooperative
				Thread.Sleep( 0 );
			}
		} catch ( Exception ex ) {
			stderr.WriteLine( $"yes: {ex.Message}" );
			return 1;
		}
	}
}
