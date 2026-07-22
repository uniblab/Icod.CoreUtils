// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Date;

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// date: display or set the system date and time (display only in this port).
/// Supported options:
///   -u		   use UTC
///   +FORMAT	  format using .NET format string (best-effort; not full strftime)
/// If FORMAT is omitted prints the current date/time in RFC1123 format.
/// </summary>
public static partial class Command {
	[GeneratedRegex( "%[aAbBcdHIjmMpSUwWxXyYzZ]" )]
	private static partial Regex StrftimeTokenRegex();

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var useUtc = false;
		string? format = null;
		foreach ( var a in args ) {
			if ( a == "-u" ) {
				useUtc = true;
			} else if ( a.StartsWith( '+' ) ) {
				format = a.Substring( 1 );
			} else {
				// ignore unknown args for display-only port
			}
		}

		try {
			var now = useUtc ? DateTime.UtcNow : DateTime.Now;
			if ( string.IsNullOrEmpty( format ) ) {
				stdout.WriteLine( now.ToString( "r", CultureInfo.InvariantCulture ) );
				return 0;
			}

			// Sanitize simple format tokens to .NET where possible.
			// This port accepts .NET format strings directly; warn if tokens look like strftime.
			if ( StrftimeTokenRegex().IsMatch( format ) ) {
				// user likely provided strftime; attempt basic replacements for common tokens
				format = format.Replace( "%Y", "yyyy" ).Replace( "%m", "MM" ).Replace( "%d", "dd" ).Replace( "%H", "HH" ).Replace( "%M", "mm" ).Replace( "%S", "ss" );
			}

			stdout.WriteLine( now.ToString( format, CultureInfo.InvariantCulture ) );
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"date: {ex.Message}" );
			return 1;
		}
	}
}
