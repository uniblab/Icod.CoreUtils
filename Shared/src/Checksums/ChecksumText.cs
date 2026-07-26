namespace Icod.CoreUtils.Shared.Checksums;

using System.Globalization;
using System.Text;

internal sealed record ChecksumManifestRecord(
	ChecksumAlgorithmKind? Algorithm,
	int LengthBits,
	byte[] ExpectedDigest,
	string FileName,
	bool Binary
);

internal static class ChecksumText {

	public static string ToHex(
		ReadOnlySpan<byte> value
	) {
		return Convert.ToHexString(
			value
		).ToLowerInvariant();
	}

	public static string EscapeFileName(
		string value
	) {
		return value.Replace(
			"\\",
			"\\\\",
			StringComparison.Ordinal
		).Replace(
			"\n",
			"\\n",
			StringComparison.Ordinal
		);
	}

	public static string UnescapeFileName(
		string value
	) {
		var output = new StringBuilder(
			value.Length
		);
		for (
			var index = 0;
			index < value.Length;
			index++
		) {
			if (
				'\\' != value[ index ]
				|| index + 1 >= value.Length
			) {
				output.Append(
					value[ index ]
				);
				continue;
			}
			index++;
			output.Append(
				'n' == value[ index ]
					? '\n'
					: value[ index ]
			);
		}
		return output.ToString();
	}

	public static bool NeedsEscaping(
		string value
	) {
		return value.Contains(
			'\\'
		) || value.Contains(
			'\n'
		);
	}

	public static bool TryParseStandaloneRecord(
		string line,
		ChecksumAlgorithmKind fixedAlgorithm,
		int? fixedLengthBits,
		out ChecksumManifestRecord? record
	) {
		record = null;
		var escaped = line.StartsWith(
			'\\'
		);
		if ( escaped ) {
			line = line.Substring(
				1
			);
		}

		var tag = ChecksumProcessor.GetDisplayName(
			fixedAlgorithm
		);
		if (
			line.StartsWith(
				string.Concat(
					tag,
					" ("
				),
				StringComparison.OrdinalIgnoreCase
			)
		) {
			var separator = line.LastIndexOf(
				") = ",
				StringComparison.Ordinal
			);
			if ( separator <= tag.Length + 2 ) {
				return false;
			}
			var fileName = line.Substring(
				tag.Length + 2,
				separator - tag.Length - 2
			);
			var digestText = line.Substring(
				separator + 4
			);
			if (
				!TryParseDigest(
					digestText,
					out var digest
				)
			) {
				return false;
			}
			var lengthBits = checked(
				digest.Length * 8
			);
			if (
				fixedLengthBits.HasValue
				&& fixedLengthBits.Value != lengthBits
			) {
				return false;
			}
			record = new ChecksumManifestRecord(
				fixedAlgorithm,
				lengthBits,
				digest,
				escaped
					? UnescapeFileName(
						fileName
					)
					: fileName,
				Binary: false
			);
			return true;
		}

		var firstSpace = line.IndexOf(
			' '
		);
		if (
			firstSpace <= 0
			|| firstSpace + 1 >= line.Length
		) {
			return false;
		}
		var digestString = line.Substring(
			0,
			firstSpace
		);
		if (
			!TryParseDigest(
				digestString,
				out var expected
			)
		) {
			return false;
		}
		var length = checked(
			expected.Length * 8
		);
		if (
			fixedLengthBits.HasValue
			&& fixedLengthBits.Value != length
		) {
			return false;
		}

		var modeIndex = firstSpace + 1;
		if (
			modeIndex >= line.Length
			|| (
				' ' != line[ modeIndex ]
				&& '*' != line[ modeIndex ]
			)
		) {
			return false;
		}
		var name = line.Substring(
			modeIndex + 1
		);
		if ( 0 == name.Length ) {
			return false;
		}
		record = new ChecksumManifestRecord(
			fixedAlgorithm,
			length,
			expected,
			escaped
				? UnescapeFileName(
					name
				)
				: name,
			'*' == line[ modeIndex ]
		);
		return true;
	}

	public static bool TryParseTaggedRecord(
		string line,
		out ChecksumManifestRecord? record
	) {
		record = null;
		var escaped = line.StartsWith(
			'\\'
		);
		if ( escaped ) {
			line = line.Substring(
				1
			);
		}
		var open = line.IndexOf(
			" (",
			StringComparison.Ordinal
		);
		var separator = line.LastIndexOf(
			") = ",
			StringComparison.Ordinal
		);
		if (
			open <= 0
			|| separator <= open + 2
		) {
			return false;
		}
		if (
			!TryParseAlgorithmLabel(
				line.Substring(
					0,
					open
				),
				out var algorithm
			)
		) {
			return false;
		}
		var digestText = line.Substring(
			separator + 4
		);
		byte[] digest;
		if (
			!TryParseDigest(
				digestText,
				out digest
			)
		) {
			try {
				digest = Convert.FromBase64String(
					digestText
				);
			} catch ( FormatException ) {
				return false;
			}
		}
		var name = line.Substring(
			open + 2,
			separator - open - 2
		);
		record = new ChecksumManifestRecord(
			algorithm,
			checked(
				digest.Length * 8
			),
			digest,
			escaped
				? UnescapeFileName(
					name
				)
				: name,
			Binary: false
		);
		return true;
	}

	public static bool TryParseAlgorithmLabel(
		string value,
		out ChecksumAlgorithmKind algorithm
	) {
		switch ( value.ToUpperInvariant() ) {
			case "BLAKE2B":
				algorithm = ChecksumAlgorithmKind.Blake2b;
				return true;
			case "CRC32B":
				algorithm = ChecksumAlgorithmKind.Crc32b;
				return true;
			case "MD5":
				algorithm = ChecksumAlgorithmKind.Md5;
				return true;
			case "SHA1":
				algorithm = ChecksumAlgorithmKind.Sha1;
				return true;
			case "SHA224":
				algorithm = ChecksumAlgorithmKind.Sha224;
				return true;
			case "SHA256":
				algorithm = ChecksumAlgorithmKind.Sha256;
				return true;
			case "SHA384":
				algorithm = ChecksumAlgorithmKind.Sha384;
				return true;
			case "SHA512":
				algorithm = ChecksumAlgorithmKind.Sha512;
				return true;
			case "SHA3-224":
				algorithm = ChecksumAlgorithmKind.Sha3_224;
				return true;
			case "SHA3-256":
				algorithm = ChecksumAlgorithmKind.Sha3_256;
				return true;
			case "SHA3-384":
				algorithm = ChecksumAlgorithmKind.Sha3_384;
				return true;
			case "SHA3-512":
				algorithm = ChecksumAlgorithmKind.Sha3_512;
				return true;
			case "SM3":
				algorithm = ChecksumAlgorithmKind.Sm3;
				return true;
			default:
				algorithm = default;
				return false;
		}
	}

	public static bool TryParseDigest(
		string value,
		out byte[] digest
	) {
		digest = Array.Empty<byte>();
		if (
			0 == value.Length
			|| 0 != value.Length % 2
		) {
			return false;
		}
		try {
			digest = Convert.FromHexString(
				value
			);
			return true;
		} catch ( FormatException ) {
			return false;
		}
	}

	public static string FormatDecimal(
		ulong value
	) {
		return value.ToString(
			CultureInfo.InvariantCulture
		);
	}

}
