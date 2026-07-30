namespace Icod.CoreUtils.Fmt;

using System.Text;

/// <summary>Represents the normalized GNU <c>fmt</c> prefix option.</summary>
internal sealed class FmtPrefix {
	/// <summary>Gets the absent-prefix value.</summary>
	internal static FmtPrefix None { get; } = new( false, 0, 0, [ ], string.Empty );

	private FmtPrefix(
		bool isSpecified,
		int leadingSpaces,
		int fullLength,
		byte[] coreBytes,
		string originalValue
	) {
		this.IsSpecified = isSpecified;
		this.LeadingSpaces = leadingSpaces;
		this.FullLength = fullLength;
		this.CoreBytes = coreBytes;
		this.OriginalValue = originalValue;
	}

	/// <summary>Gets the UTF-8 bytes of the prefix after removing leading and trailing ASCII spaces.</summary>
	internal byte[] CoreBytes { get; }

	/// <summary>Gets the byte length after leading ASCII spaces are removed but before trailing spaces are removed.</summary>
	internal int FullLength { get; }

	/// <summary>Gets whether the user supplied a prefix option.</summary>
	internal bool IsSpecified { get; }

	/// <summary>Gets the number of leading ASCII spaces required before the prefix.</summary>
	internal int LeadingSpaces { get; }

	/// <summary>Gets the original option value.</summary>
	internal string OriginalValue { get; }

	/// <summary>Parses one prefix option value using GNU's byte-counting semantics.</summary>
	/// <param name="value">The option value.</param>
	/// <returns>The normalized prefix.</returns>
	internal static FmtPrefix Parse( string value ) {
		ArgumentNullException.ThrowIfNull( value );
		var start = 0;
		while ( start < value.Length && ' ' == value[start] ) {
			start++;
		}
		var end = value.Length;
		while ( start < end && ' ' == value[end - 1] ) {
			end--;
		}
		var fullBytes = Encoding.UTF8.GetBytes( value[start..] );
		return new FmtPrefix(
			true,
			start,
			fullBytes.Length,
			Encoding.UTF8.GetBytes( value[start..end] ),
			value
		);
	}
}
