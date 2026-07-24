// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Tsort;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Minimal topological sort.
/// Reads whitespace-separated tokens in pairs (A B meaning edge A -> B) from files
/// or standard input when no files are specified. Prints one node per line in a
/// topologically sorted order. Detects cycles and returns non-zero on error.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 1 && ( args[ 0 ] == "-h" || args[ 0 ] == "--help" ) ) {
			PrintUsage( stdout );
			return 0;
		}

		var sources = args.Length == 0 ? new[] { "-" } : args;
		var nodes = new HashSet<string>( StringComparer.Ordinal );
		var adj = new Dictionary<string, List<string>>( StringComparer.Ordinal );
		var indegree = new Dictionary<string, int>( StringComparer.Ordinal );

		try {
			foreach ( var name in sources ) {
				string content;
				if ( name == "-" )
					content = stdin.ReadToEnd();
				else
					content = File.ReadAllText( name );

				var tokens = content.Split( (char[]?)null, StringSplitOptions.RemoveEmptyEntries );
				if ( tokens.Length % 2 != 0 ) {
					stderr.WriteLine( "tsort: input contains an odd number of fields" );
					return 1;
				}

				for ( var i = 0; i < tokens.Length; i += 2 ) {
					var a = tokens[ i ];
					var b = tokens[ i + 1 ];
					nodes.Add( a );
					nodes.Add( b );
					if ( !adj.TryGetValue( a, out var list ) ) {
						list = new List<string>();
						adj[ a ] = list;
					}
					list.Add( b );
					indegree.TryGetValue( b, out var d );
					indegree[ b ] = d + 1;
					indegree.TryAdd( a, indegree.TryGetValue( a, out var value ) ? value : 0 );
				}
			}

			// nodes that never appeared in any pair (isolated) should be emitted too
			foreach ( var n in nodes ) {
				indegree.TryAdd( n, 0 );
				adj.TryAdd( n, new List<string>() );
			}

			// Kahn's algorithm with deterministic ordering
			var zero = new SortedSet<string>( indegree.Where( kv => kv.Value == 0 ).Select( kv => kv.Key ), StringComparer.Ordinal );
			var output = new List<string>();

			while ( zero.Count > 0 ) {
				var n = zero.Min!;
				zero.Remove( n );
				output.Add( n );
				foreach ( var m in adj[ n ] ) {
					indegree[ m ]--;
					if ( indegree[ m ] == 0 )
						zero.Add( m );
				}
			}

			if ( output.Count != indegree.Count ) {
				stderr.WriteLine( "tsort: input contains a cycle" );
				return 1;
			}

			foreach ( var o in output )
				stdout.WriteLine( o );
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"tsort: {ex.Message}" );
			return 1;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: tsort [FILE]..." );
		stdout.WriteLine( "Read pairs of items (A B) and print a topological ordering." );
	}
}
