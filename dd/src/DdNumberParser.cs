namespace Icod.CoreUtils.DD;

using System.Globalization;
using System.Numerics;

internal static class DdNumberParser {
	private static readonly IReadOnlyDictionary<string, BigInteger> SuffixMultipliers =
		CreateSuffixMultipliers();

	public static bool TryParseBlockSize(
		string value,
		out int blockSize,
		out string error
	) {
		blockSize = 0;
		if (
			!TryParseCore(
				value,
				out var parsed,
				out _,
				out error
			)
			|| parsed <= 0L
			|| int.MaxValue < parsed
		) {
			if ( string.IsNullOrEmpty( error ) ) {
				error = string.Concat(
					"invalid number: '",
					value,
					"'"
				);
			}
			return false;
		}
		blockSize = (int)parsed;
		return true;
	}

	public static bool TryParseQuantity(
		string value,
		out DdQuantity quantity,
		out string error
	) {
		quantity = default;
		if (
			!TryParseCore(
				value,
				out var parsed,
				out var endsInBytes,
				out error
			)
		) {
			return false;
		}
		quantity = new DdQuantity(
			parsed,
			endsInBytes
		);
		return true;
	}

	private static bool TryParseCore(
		string value,
		out long parsed,
		out bool endsInBytes,
		out string error
	) {
		parsed = 0L;
		endsInBytes = false;
		error = string.Empty;
		if ( string.IsNullOrEmpty( value ) ) {
			error = "invalid number: ''";
			return false;
		}
		var normalized = value.EndsWith(
			"xM",
			StringComparison.Ordinal
		)
			? string.Concat(
				value.Substring(
					0,
					value.Length - 2
				),
				"M"
			)
			: value
		;
		var factors = normalized.Split(
			'x',
			StringSplitOptions.None
		);
		if ( factors.Any( string.IsNullOrEmpty ) ) {
			error = InvalidNumber(
				value
			);
			return false;
		}

		var total = BigInteger.One;
		var finalSuffix = string.Empty;
		foreach ( var factorText in factors ) {
			var index = 0;
			while (
				index < factorText.Length
				&& char.IsAsciiDigit( factorText[ index ] )
			) {
				index++;
			}
			if (
				0 == index
				|| !BigInteger.TryParse(
					factorText.AsSpan(
						0,
						index
					),
					NumberStyles.None,
					CultureInfo.InvariantCulture,
					out var factor
				)
			) {
				error = InvalidNumber(
					value
				);
				return false;
			}
			var suffix = factorText.Substring(
				index
			);
			if (
				!SuffixMultipliers.TryGetValue(
					suffix,
					out var multiplier
				)
			) {
				error = InvalidNumber(
					value
				);
				return false;
			}
			total *= factor * multiplier;
			finalSuffix = suffix;
		}
		if ( total > long.MaxValue ) {
			error = string.Concat(
				InvalidNumber( value ),
				": Value too large for defined data type"
			);
			return false;
		}
		parsed = (long)total;
		endsInBytes = finalSuffix.EndsWith(
			"B",
			StringComparison.Ordinal
		);
		return true;
	}

	private static IReadOnlyDictionary<string, BigInteger> CreateSuffixMultipliers() {
		var output = new Dictionary<string, BigInteger>(
			StringComparer.Ordinal
		) {
			[ string.Empty ] = BigInteger.One,
			[ "c" ] = BigInteger.One,
			[ "B" ] = BigInteger.One,
			[ "w" ] = new BigInteger( 2 ),
			[ "b" ] = new BigInteger( 512 ),
			[ "k" ] = BigInteger.Pow( 1024, 1 ),
			[ "K" ] = BigInteger.Pow( 1024, 1 ),
			[ "kB" ] = BigInteger.Pow( 1000, 1 ),
			[ "KB" ] = BigInteger.Pow( 1000, 1 ),
			[ "KiB" ] = BigInteger.Pow( 1024, 1 ),
		};
		var prefixes = new[] {
			"M", "G", "T", "P", "E", "Z", "Y", "R", "Q",
		};
		for ( var index = 0; index < prefixes.Length; index++ ) {
			var power = index + 2;
			output[ prefixes[ index ] ] = BigInteger.Pow(
				1024,
				power
			);
			output[ string.Concat( prefixes[ index ], "B" ) ] = BigInteger.Pow(
				1000,
				power
			);
			output[ string.Concat( prefixes[ index ], "iB" ) ] = BigInteger.Pow(
				1024,
				power
			);
		}
		return output;
	}

	private static string InvalidNumber(
		string value
	) => string.Concat(
		"invalid number: '",
		value,
		"'"
	);
}
