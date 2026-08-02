namespace Icod.CoreUtils.Touch;

using System.Globalization;

/// <summary>Parses the POSIX/GNU <c>-t [[CC]YY]MMDDhhmm[.ss]</c> timestamp form.</summary>
internal static class TouchTimestampParser {
	/// <summary>Attempts to parse one touch timestamp.</summary>
	/// <param name="text">The timestamp text.</param>
	/// <param name="now">The current time used when the year is omitted.</param>
	/// <param name="timeZone">The default time zone.</param>
	/// <param name="value">The parsed instant.</param>
	/// <param name="diagnostic">A diagnostic when parsing fails.</param>
	/// <returns><see langword="true"/> when parsing succeeds.</returns>
	public static bool TryParse(
		string text,
		DateTimeOffset now,
		TimeZoneInfo timeZone,
		out DateTimeOffset value,
		out string diagnostic
	) {
		ArgumentNullException.ThrowIfNull( text );
		ArgumentNullException.ThrowIfNull( timeZone );
		value = default;
		diagnostic = string.Empty;
		var dot = text.IndexOf( '.', StringComparison.Ordinal );
		var main = dot < 0 ? text : text[..dot];
		var secondsText = dot < 0 ? null : text[(dot + 1)..];
		if ( dot >= 0 && (secondsText!.Length != 2 || text.IndexOf( '.', dot + 1 ) >= 0) ) {
			diagnostic = "seconds must contain exactly two digits";
			return false;
		}
		if ( main.Length is not 8 and not 10 and not 12 || !AllDigits( main )
			|| (null != secondsText && !AllDigits( secondsText )) ) {
			diagnostic = "invalid date format";
			return false;
		}

		var cursor = 0;
		int year;
		if ( 12 == main.Length ) {
			year = Parse2( main, ref cursor ) * 100 + Parse2( main, ref cursor );
		} else if ( 10 == main.Length ) {
			var shortYear = Parse2( main, ref cursor );
			year = shortYear <= 68 ? 2000 + shortYear : 1900 + shortYear;
		} else {
			year = TimeZoneInfo.ConvertTime( now, timeZone ).Year;
		}
		var month = Parse2( main, ref cursor );
		var day = Parse2( main, ref cursor );
		var hour = Parse2( main, ref cursor );
		var minute = Parse2( main, ref cursor );
		var second = null == secondsText
			? 0
			: int.Parse( secondsText, NumberStyles.None, CultureInfo.InvariantCulture );
		var leapSecond = 60 == second;
		if ( leapSecond ) {
			second = 59;
		}
		try {
			var local = new DateTime( year, month, day, hour, minute, second, DateTimeKind.Unspecified );
			if ( leapSecond ) {
				local = local.AddSeconds( 1 );
			}
			if ( timeZone.IsInvalidTime( local ) ) {
				diagnostic = "the specified local time does not exist in the selected time zone";
				return false;
			}
			var offset = timeZone.GetUtcOffset( local );
			value = new DateTimeOffset( local, offset );
			return true;
		} catch ( ArgumentOutOfRangeException ) {
			diagnostic = "invalid date or time component";
			return false;
		}
	}

	private static bool AllDigits( string value ) {
		foreach ( var character in value ) {
			if ( !char.IsAsciiDigit( character ) ) {
				return false;
			}
		}
		return true;
	}

	private static int Parse2( string value, ref int cursor ) {
		var result = (value[cursor] - '0') * 10 + value[cursor + 1] - '0';
		cursor += 2;
		return result;
	}
}
