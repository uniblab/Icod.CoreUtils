namespace Icod.CoreUtils.Shared.BinaryFormatting;

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

/// <summary>
/// Identifies the semantic representation requested for a binary value.
/// </summary>
public enum BinaryFormatKind {
	NamedCharacter,
	Character,
	SignedDecimal,
	UnsignedDecimal,
	Octal,
	Hexadecimal,
	FloatingPoint
}

/// <summary>
/// Identifies the byte order used to interpret multi-byte values.
/// </summary>
public enum BinaryByteOrder {
	Native,
	LittleEndian,
	BigEndian
}

/// <summary>
/// Describes one parsed binary value format.
/// </summary>
public sealed record BinaryFormatSpecification(
	BinaryFormatKind Kind,
	int Size,
	bool AppendPrintableTrailer,
	string SourceText,
	char? FloatingAlias = null
);

/// <summary>
/// Parses GNU <c>od</c>-style type strings into reusable binary format specifications.
/// </summary>
public static class BinaryFormatParser {
	/// <summary>
	/// Parses a type string such as <c>x1z</c>, <c>dI</c>, or <c>fD</c>.
	/// </summary>
	public static bool TryParse(
		string value,
		out IReadOnlyList<BinaryFormatSpecification> specifications,
		out string? error
	) {
		specifications = Array.Empty<BinaryFormatSpecification>();
		error = null;
		if ( string.IsNullOrEmpty( value ) ) {
			error = "the type string is empty";
			return false;
		}

		var output = new List<BinaryFormatSpecification>();
		var index = 0;
		while ( index < value.Length ) {
			var start = index;
			var code = value[ index++ ];
			var kind = code switch {
				'a' => BinaryFormatKind.NamedCharacter,
				'c' => BinaryFormatKind.Character,
				'd' => BinaryFormatKind.SignedDecimal,
				'u' => BinaryFormatKind.UnsignedDecimal,
				'o' => BinaryFormatKind.Octal,
				'x' => BinaryFormatKind.Hexadecimal,
				'f' => BinaryFormatKind.FloatingPoint,
				_ => ( BinaryFormatKind? )null
			};
			if ( !kind.HasValue ) {
				error = string.Concat(
					"invalid character '",
					code,
					"' in type string '",
					value,
					"'"
				);
				return false;
			}

			var size = 1;
			char? floatingAlias = null;
			if (
				BinaryFormatKind.NamedCharacter != kind.Value
				&& BinaryFormatKind.Character != kind.Value
			) {
				if ( index < value.Length && char.IsDigit( value[ index ] ) ) {
					var numberStart = index;
					while ( index < value.Length && char.IsDigit( value[ index ] ) ) {
						index++;
					}
					if (
						!int.TryParse(
							value.AsSpan( numberStart, index - numberStart ),
							System.Globalization.NumberStyles.None,
							System.Globalization.CultureInfo.InvariantCulture,
							out size
						)
					) {
						error = string.Concat( "invalid size in type string '", value, "'" );
						return false;
					}
				} else if ( index < value.Length && IsAlias( kind.Value, value[ index ] ) ) {
					var alias = value[ index++ ];
					if ( BinaryFormatKind.FloatingPoint == kind.Value ) {
						floatingAlias = alias;
						size = GetFloatingAliasSize( alias );
					} else {
						size = GetIntegralAliasSize( alias );
					}
				} else {
					size = BinaryFormatKind.FloatingPoint == kind.Value
						? 8
						: 4
					;
				}
			}

			if ( !IsSupportedSize( kind.Value, size, floatingAlias ) ) {
				error = string.Concat(
					"unsupported size '",
					size.ToString( System.Globalization.CultureInfo.InvariantCulture ),
					"' in type string '",
					value,
					"'"
				);
				return false;
			}

			var appendTrailer = index < value.Length && 'z' == value[ index ];
			if ( appendTrailer ) {
				index++;
			}
			output.Add(
				new BinaryFormatSpecification(
					kind.Value,
					size,
					appendTrailer,
					value.Substring( start, index - start ),
					floatingAlias
				)
			);
		}

		specifications = new ReadOnlyCollection<BinaryFormatSpecification>( output );
		return true;
	}

	private static int GetIntegralAliasSize(
		char alias
	) {
		return alias switch {
			'C' => 1,
			'S' => 2,
			'I' => 4,
			'L' => OperatingSystem.IsWindows() ? 4 : IntPtr.Size,
			_ => 0
		};
	}

	private static int GetFloatingAliasSize(
		char alias
	) {
		return alias switch {
			'B' => 2,
			'H' => 2,
			'F' => 4,
			'D' => 8,
			'L' => GetLongDoubleSize(),
			_ => 0
		};
	}

	private static int GetLongDoubleSize() {
		if ( OperatingSystem.IsWindows() ) {
			return 8;
		}
		if (
			OperatingSystem.IsMacOS()
			&& Architecture.Arm64 == RuntimeInformation.ProcessArchitecture
		) {
			return 8;
		}
		return 16;
	}

	private static bool IsAlias(
		BinaryFormatKind kind,
		char value
	) {
		return BinaryFormatKind.FloatingPoint == kind
			? value is 'B' or 'H' or 'F' or 'D' or 'L'
			: value is 'C' or 'S' or 'I' or 'L'
		;
	}

	private static bool IsSupportedSize(
		BinaryFormatKind kind,
		int size,
		char? floatingAlias
	) {
		if (
			BinaryFormatKind.NamedCharacter == kind
			|| BinaryFormatKind.Character == kind
		) {
			return 1 == size;
		}
		if ( BinaryFormatKind.FloatingPoint == kind ) {
			if ( 'B' == floatingAlias || 'H' == floatingAlias ) {
				return 2 == size;
			}
			return size is 4 or 8;
		}
		return size is 1 or 2 or 4 or 8;
	}
}
