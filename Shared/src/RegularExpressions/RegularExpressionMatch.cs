namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Represents the leftmost-longest match selected by the GNU basic regular-expression engine.</summary>
public sealed class RegularExpressionMatch {
	/// <summary>Initializes a selected regular-expression match.</summary>
	/// <param name="index">The zero-based UTF-16 input index.</param>
	/// <param name="length">The UTF-16 match length.</param>
	/// <param name="value">The matched text.</param>
	/// <param name="captures">The numbered subexpression captures.</param>
	public RegularExpressionMatch(
		int index,
		int length,
		string value,
		IReadOnlyList<RegularExpressionCapture> captures
	) {
		ArgumentOutOfRangeException.ThrowIfNegative( index );
		ArgumentOutOfRangeException.ThrowIfNegative( length );
		ArgumentNullException.ThrowIfNull( value );
		ArgumentNullException.ThrowIfNull( captures );
		Index = index;
		Length = length;
		Value = value;
		Captures = Array.AsReadOnly( captures.ToArray() );
	}

	/// <summary>Gets the zero-based UTF-16 input index.</summary>
	public int Index { get; }

	/// <summary>Gets the UTF-16 match length.</summary>
	public int Length { get; }

	/// <summary>Gets the matched text.</summary>
	public string Value { get; }

	/// <summary>Gets numbered captures in opening-parenthesis order; element zero represents subexpression 1.</summary>
	public IReadOnlyList<RegularExpressionCapture> Captures { get; }
}
