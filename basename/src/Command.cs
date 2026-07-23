// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Basename;

using System;
using System.IO;
using System.Text;

/// <summary>
/// basename: strip directory and suffix from filenames.
/// Usage: basename NAME [SUFFIX]
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			stderr.WriteLine( "basename: missing operand" );
			return 1;
		}

		if ( args.Length == 1 ) {
			var nameOnly = SharedPathBasename( args[ 0 ] );
			stdout.WriteLine( nameOnly );
			return 0;
		}

		var suffix = args[ 1 ];
		for ( var i = 0; i < 1; i++ ) {
			// primary name(s) are in args[0..(n-1)], but POSIX allows multiple names when using -a; simple behavior: process first argument only
		}

		// If multiple NAME arguments, behave by printing each on its own line
		for ( var i = 0; i < args.Length; i++ ) {
			if ( i == 1 ) {
				// skip suffix position
				continue;
			}

			var name = args[ i ];
			var baseName = SharedPathBasename( name );
			if ( !string.IsNullOrEmpty( suffix ) && baseName.EndsWith( suffix, StringComparison.Ordinal ) ) {
				baseName = baseName.Substring( 0, baseName.Length - suffix.Length );
			}

			stdout.WriteLine( baseName );
		}

		return 0;
	}

	private static string SharedPathBasename( string path ) {
		if ( string.IsNullOrEmpty( path ) ) {
			return ".";
		}

		var trimmed = path.TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
		if ( trimmed.Length == 0 ) {
			return Path.DirectorySeparatorChar.ToString();
		}

		return Path.GetFileName( trimmed );
	}
}
