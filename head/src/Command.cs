// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Head;

using System;
using System.IO;
using System.Text;

/// <summary>
/// head: output the first part of files.
/// Supports: -n N	print first N lines (default 10)
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var lines = 10;
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			if ( args[ i ] == "-n" && i + 1 < args.Length ) {
				i++;
				if ( !int.TryParse( args[ i ], out lines ) ) {
					stderr.WriteLine( $"head: invalid number '{args[ i ]}'" );
					return 1;
				}
			} else {
				break;
			}
		}

		var rem = new System.Collections.Generic.List<string>();
		for ( ; i < args.Length; i++ ) {
			rem.Add( args[ i ] );
		}

		if ( rem.Count == 0 ) {
			return OutputHead( "<stdin>", Console.In, stdout, stderr, lines );
		}

		var exit = 0;
		foreach ( var path in rem ) {
			try {
				using var sr = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
				var rc = OutputHead( path, sr, stdout, stderr, lines );
				if ( rc != 0 ) {
					exit = rc;
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"head: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static int OutputHead( string sourceName, TextReader reader, TextWriter stdout, TextWriter stderr, int lines ) {
		try {
			var count = 0;
			string? line;
			while ( count < lines && ( line = reader.ReadLine() ) is not null ) {
				stdout.WriteLine( line );
				count++;
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"head: {sourceName}: {ex.Message}" );
			return 1;
		}
	}
}
