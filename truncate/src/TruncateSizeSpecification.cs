namespace Icod.CoreUtils.Truncate;

using System.Globalization;
using System.Numerics;

/// <summary>
/// Identifies how a parsed <c>truncate --size</c> magnitude is applied to the current file length.
/// </summary>
internal enum TruncateSizeMode {
	/// <summary>
	/// Sets the file length to the parsed magnitude.
	/// </summary>
	Absolute,
	/// <summary>
	/// Adds the parsed signed magnitude to the current file length.
	/// </summary>
	Relative,
	/// <summary>
	/// Reduces the file only when its current length exceeds the magnitude.
	/// </summary>
	AtMost,
	/// <summary>
	/// Extends the file only when its current length is less than the magnitude.
	/// </summary>
	AtLeast,
	/// <summary>
	/// Rounds the current length downward to a multiple of the magnitude.
	/// </summary>
	RoundDown,
	/// <summary>
	/// Rounds the current length upward to a multiple of the magnitude.
	/// </summary>
	RoundUp,
}

/// <summary>
/// Represents a normalized GNU <c>truncate</c> size operation.
/// </summary>
/// <remarks>
/// Command orchestration applies the specification to each operand after resolving reference size and optional I/O-block scaling.
/// </remarks>
/// <param name="Mode">The rule used to combine the magnitude with the current file length.</param>
/// <param name="Magnitude">The non-negative byte or I/O-block magnitude after suffix parsing.</param>
internal readonly record struct TruncateSizeSpecification(
	TruncateSizeMode Mode,
	long Magnitude
);

/// <summary>
/// Parses GNU <c>truncate</c> size expressions, modifiers, suffixes, and inherited mode.
/// </summary>
/// <remarks>
/// Invalid input is reported through an error string without throwing.
/// </remarks>
internal static class TruncateSizeParser {

	/// <summary>
	/// Attempts to parse a GNU size expression into a normalized mode and non-negative magnitude.
	/// </summary>
	/// <param name="text">The user-supplied size expression.</param>
	/// <param name="inheritedMode">The mode selected by command-line options before a size-prefix override is applied.</param>
	/// <param name="specification">When parsing succeeds, receives the normalized mode and magnitude.</param>
	/// <param name="error">When parsing fails, receives a user-facing diagnostic; otherwise receives an empty string.</param>
	/// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
	public static bool TryParse(
		string? text,
		TruncateSizeMode inheritedMode,
		out TruncateSizeSpecification specification,
		out string error
	) {
		text ??= string.Empty;
		var span = text.AsSpan();
		var index = 0;
		SkipWhiteSpace(
			span,
			ref index
		);

		var mode = inheritedMode;
		if ( index < span.Length ) {
			var candidate = span[ index ];
			var modifierMode = candidate switch {
				'<' => TruncateSizeMode.AtMost,
				'>' => TruncateSizeMode.AtLeast,
				'/' => TruncateSizeMode.RoundDown,
				'%' => TruncateSizeMode.RoundUp,
				_ => ( TruncateSizeMode? )null,
			};
			if ( modifierMode.HasValue ) {
				// GNU keeps the previous mode across repeated --size options, but an
				// explicit non-sign modifier on the latest option replaces that mode.
				mode = modifierMode.Value;
				index++;
				SkipWhiteSpace(
					span,
					ref index
				);
			}
		}

		var hadSign = false;
		var negative = false;
		if (
			index < span.Length
			&& ( '+' == span[ index ] || '-' == span[ index ] )
		) {
			if ( TruncateSizeMode.Absolute != mode ) {
				return Fail(
					"multiple relative modifiers specified",
					out specification,
					out error
				);
			}
			mode = TruncateSizeMode.Relative;
			hadSign = true;
			negative = '-' == span[ index ];
			index++;
		}

		var digitStart = index;
		while (
			index < span.Length
			&& char.IsAsciiDigit( span[ index ] )
		) {
			index++;
		}

		var suffix = span[ index.. ].ToString();
		if ( !TryGetMultiplier( suffix, out var multiplier ) ) {
			return Fail(
				String.Concat( "invalid number: '", text, "'" ),
				out specification,
				out error
			);
		}

		BigInteger magnitude;
		if ( digitStart == index ) {
			// GNU accepts a bare recognized suffix as one unit, but a sign still
			// requires an explicit integer (for example, K is valid and +K is not).
			if ( hadSign || string.IsNullOrEmpty( suffix ) ) {
				return Fail(
					String.Concat( "invalid number: '", text, "'" ),
					out specification,
					out error
				);
			}
			magnitude = BigInteger.One;
		} else if ( !BigInteger.TryParse(
			span[ digitStart..index ],
			NumberStyles.None,
			CultureInfo.InvariantCulture,
			out magnitude
		) ) {
			return Fail(
				String.Concat( "invalid number: '", text, "'" ),
				out specification,
				out error
			);
		}

		var value = magnitude * multiplier;
		if ( negative ) {
			value = -value;
		}
		if (
			value < long.MinValue
			|| value > long.MaxValue
		) {
			return Fail(
				String.Concat( "size is too large: '", text, "'" ),
				out specification,
				out error
			);
		}
		if (
			mode is TruncateSizeMode.RoundDown or TruncateSizeMode.RoundUp
			&& BigInteger.Zero == value
		) {
			return Fail(
				"division by zero",
				out specification,
				out error
			);
		}

		specification = new TruncateSizeSpecification(
			mode,
			( long )value
		);
		error = string.Empty;
		return true;
	}

	private static bool TryGetMultiplier(
		string suffix,
		out BigInteger multiplier
	) {
		if ( string.Empty == suffix ) {
			multiplier = BigInteger.One;
			return true;
		}

		var exponent = suffix switch {
			"K" or "k" or "KiB" or "kiB" => 1,
			"M" or "m" or "MiB" or "miB" => 2,
			"G" or "g" or "GiB" or "giB" => 3,
			"T" or "t" or "TiB" or "tiB" => 4,
			"P" or "PiB" => 5,
			"E" or "EiB" => 6,
			"Z" or "ZiB" => 7,
			"Y" or "YiB" => 8,
			"R" or "RiB" => 9,
			"Q" or "QiB" => 10,
			_ => -1,
		};
		if ( 0 <= exponent ) {
			multiplier = BigInteger.Pow(
				new BigInteger( 1024 ),
				exponent
			);
			return true;
		}

		exponent = suffix switch {
			"KB" or "kB" => 1,
			"MB" or "mB" => 2,
			"GB" or "gB" => 3,
			"TB" or "tB" => 4,
			"PB" => 5,
			"EB" => 6,
			"ZB" => 7,
			"YB" => 8,
			"RB" => 9,
			"QB" => 10,
			_ => -1,
		};
		if ( 0 <= exponent ) {
			multiplier = BigInteger.Pow(
				new BigInteger( 1000 ),
				exponent
			);
			return true;
		}

		multiplier = BigInteger.Zero;
		return false;
	}

	private static void SkipWhiteSpace(
		ReadOnlySpan<char> span,
		ref int index
	) {
		while (
			index < span.Length
			&& char.IsWhiteSpace( span[ index ] )
		) {
			index++;
		}
	}

	private static bool Fail(
		string message,
		out TruncateSizeSpecification specification,
		out string error
	) {
		specification = default;
		error = message;
		return false;
	}
}
