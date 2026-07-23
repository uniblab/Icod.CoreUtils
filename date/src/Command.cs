// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Date;

using System;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>
/// Minimal `date`:
///   date            print current date/time (local)
///   date -u         print UTC
///   date +FORMAT    print using format (supports a subset of strftime: %Y %m %d %H %M %S %F %T)
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var utc = false;
		string? format = null;
		foreach ( var a in args ) {
			if ( a == "-u" ) {
				utc = true;
				continue;
			}
			if ( a == "-h" || a == "--help" ) {
				PrintUsage( stdout );
				return 0;
			}
			if ( a.StartsWith( '+' ) )
				format = a.Substring( 1 );
		}

		var now = utc ? DateTime.UtcNow : DateTime.Now;
		try {
			if ( string.IsNullOrEmpty( format ) ) {
				// default: RFC1123-ish but similar to GNU date default
				stdout.WriteLine( now.ToString( "ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture ) );
				return 0;
			}

			var netFormat = ConvertStrftimeToDotNet( format );
			var outText = now.ToString( netFormat, CultureInfo.InvariantCulture );
			stdout.WriteLine( outText );
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"date: {ex.Message}" );
			return 1;
		}
	}

	private static string ConvertStrftimeToDotNet( string fmt ) {
		// Handle a small subset of strftime specifiers
		var sb = new StringBuilder();
		for ( var i = 0; i < fmt.Length; i++ ) {
			if ( fmt[ i ] == '%' && i + 1 < fmt.Length ) {
				i++;
				var c = fmt[ i ];
				switch ( c ) {
					case 'Y':
						sb.Append( "yyyy" );
						break;
					case 'm':
						sb.Append( "MM" );
						break;
					case 'd':
						sb.Append( "dd" );
						break;
					case 'H':
						sb.Append( "HH" );
						break;
					case 'M':
						sb.Append( "mm" );
						break;
					case 'S':
						sb.Append( "ss" );
						break;
					case 'F':
						sb.Append( "yyyy-MM-dd" );
						break;
					case 'T':
						sb.Append( "HH:mm:ss" );
						break;
					default:
						sb.Append( '%' ).Append( c );
						break;
				}
			} else {
				sb.Append( fmt[ i ] );
			}
		}
		return sb.ToString();
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: date [-u] [+FORMAT]" );
		stdout.WriteLine( "  -u    display UTC time" );
		stdout.WriteLine( "FORMAT supports a subset of strftime (e.g. %Y %m %d %H %M %S %F %T)." );
	}
}
