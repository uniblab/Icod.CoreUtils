// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Ptx;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ptx: produce permuted index (simplified).
/// This is a simplified implementation: for each token in each input line, output a line:
///   KEYWORD&lt;TAB&gt;left-context&lt;TAB&gt;right-context
/// then sort by keyword.
/// </summary>
public static partial class Command {

	private const char SPACE = ' ';
	private const char TAB = '\t';
	private static readonly System.Char[] SPACE_TAB;

	static Command() {
		SPACE_TAB = [ SPACE, TAB ];
	}

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var rem = new List<string>();
		foreach ( var a in args ) {
			rem.Add( a );
		}

		var entries = new List<(string key, string left, string right)>();
		try {
			if ( rem.Count == 0 ) {
				ReadStream( "<stdin>", stdin, entries );
			} else {
				foreach ( var path in rem ) {
					if ( path == "-" ) {
						ReadStream( path, stdin, entries );
					} else {
						using var sr = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
						ReadStream( path, sr, entries );
					}
				}
			}

			foreach ( var e in entries.OrderBy( e => e.key, StringComparer.OrdinalIgnoreCase ) ) {
				stdout.WriteLine( $"{e.key}\t{e.left}\t{e.right}" );
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"ptx: {ex.Message}" );
			return 1;
		}
	}

	private static void ReadStream( string sourceName, TextReader reader, List<(string key, string left, string right)> entries ) {
		string? line;
		while ( ( line = reader.ReadLine() ) is not null ) {
			var words = line.Split( SPACE_TAB, StringSplitOptions.RemoveEmptyEntries );
			for ( var i = 0; i < words.Length; i++ ) {
				var key = words[ i ];
				var left = ( i > 0 ) ? words[ i - 1 ] : string.Empty;
				var right = ( i + 1 < words.Length ) ? words[ i + 1 ] : string.Empty;
				entries.Add( (key, left, right) );
			}
		}
	}
}
