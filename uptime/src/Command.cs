// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Uptime;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;

/// <summary>
/// uptime: show how long the system has been running (best-effort).
/// On Unix tries /proc/uptime; otherwise falls back to Environment.TickCount64.
/// </summary>
public static class Command {

	private const char SPACE = ' ';
	private const char TAB = '\t';
	private static readonly System.Char[] SPACE_TAB;

	static Command() {
		SPACE_TAB = [ SPACE, TAB ];
	}

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		try {
			double seconds = -1;
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) && File.Exists( "/proc/uptime" ) ) {
				var text = File.ReadAllText( "/proc/uptime" );
				var parts = text.Split( SPACE_TAB, StringSplitOptions.RemoveEmptyEntries );
				if ( parts.Length >= 1 && double.TryParse( parts[ 0 ], NumberStyles.Float, CultureInfo.InvariantCulture, out var s ) ) {
					seconds = s;
				}
			}

			if ( seconds < 0 ) {
				// Fallback: Environment.TickCount64 (ms since system start)
				seconds = Environment.TickCount64 / 1000.0;
			}

			var ts = TimeSpan.FromSeconds( seconds );
			var days = ts.Days;
			var hours = ts.Hours;
			var minutes = ts.Minutes;
			stdout.WriteLine( $"up {days} days, {hours:D2}:{minutes:D2}" );
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"uptime: {ex.Message}" );
			return 1;
		}
	}
}
