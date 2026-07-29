namespace Icod.CoreUtils.Expr;

using System.Globalization;
using System.Numerics;

/// <summary>
/// Represents an <c>expr</c> value while preserving whether it originated as an integer or a string.
/// </summary>
/// <remarks>
/// GNU null-value rules and numeric coercion are implemented without losing the original string representation.
/// </remarks>
internal readonly record struct ExpressionValue {
	private readonly BigInteger integer;
	private readonly string? text;

	private ExpressionValue( BigInteger integer ) {
		this.integer = integer;
		this.text = null;
		this.IsInteger = true;
	}

	private ExpressionValue( string text ) {
		this.integer = BigInteger.Zero;
		this.text = text;
		this.IsInteger = false;
	}

	/// <summary>
	/// Gets whether the value is stored as an arbitrary-precision integer rather than text.
	/// </summary>
	/// <value><see langword="true"/> for an integer-backed value.</value>
	public bool IsInteger { get; }

	/// <summary>
	/// Gets whether the value is null according to GNU <c>expr</c> rules.
	/// </summary>
	/// <value><see langword="true"/> for integer zero, an empty string, or a decimal string consisting only of zeroes with an optional leading minus sign.</value>
	public bool IsNull {
		get {
			if ( this.IsInteger ) {
				return BigInteger.Zero == this.integer;
			}
			var value = this.text ?? string.Empty;
			if ( 0 == value.Length ) {
				return true;
			}
			var index = '-' == value[ 0 ] ? 1 : 0;
			if ( index == value.Length ) {
				return false;
			}
			for ( ; value.Length > index; index++ ) {
				if ( '0' != value[ index ] ) {
					return false;
				}
			}
			return true;
		}
	}

	/// <summary>
	/// Gets the canonical integer zero value.
	/// </summary>
	/// <value>An integer-backed value equal to zero.</value>
	public static ExpressionValue Zero => new( BigInteger.Zero );

	/// <summary>
	/// Creates an expression value from an arbitrary-precision integer.
	/// </summary>
	/// <param name="value">The arbitrary-precision integer to store.</param>
	/// <returns>An integer expression value.</returns>
	public static ExpressionValue FromInteger( BigInteger value ) => new( value );

	/// <summary>
	/// Creates an expression value that preserves the supplied text.
	/// </summary>
	/// <param name="value">The text to preserve exactly.</param>
	/// <returns>A string expression value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
	public static ExpressionValue FromString( string value ) {
		ArgumentNullException.ThrowIfNull( value );
		return new ExpressionValue( value );
	}

	/// <summary>
	/// Returns the invariant decimal representation of an integer or the original text value.
	/// </summary>
	/// <returns>The value rendered as text.</returns>
	public string AsString() {
		return this.IsInteger
			? this.integer.ToString( CultureInfo.InvariantCulture )
			: this.text ?? string.Empty;
	}

	/// <summary>
	/// Attempts GNU-compatible integer coercion without changing the stored value.
	/// </summary>
	/// <param name="value">When conversion succeeds, receives the arbitrary-precision integer; otherwise receives zero.</param>
	/// <returns><see langword="true"/> when the value is an integer or a syntactically valid decimal integer string.</returns>
	public bool TryGetInteger( out BigInteger value ) {
		if ( this.IsInteger ) {
			value = this.integer;
			return true;
		}
		var textValue = this.text ?? string.Empty;
		if ( !LooksLikeInteger( textValue ) ) {
			value = BigInteger.Zero;
			return false;
		}
		return BigInteger.TryParse(
			textValue,
			NumberStyles.AllowLeadingSign,
			CultureInfo.InvariantCulture,
			out value
		);
	}

	private static bool LooksLikeInteger( string value ) {
		if ( 0 == value.Length ) {
			return false;
		}
		var index = '-' == value[ 0 ] ? 1 : 0;
		if ( index == value.Length ) {
			return false;
		}
		for ( ; value.Length > index; index++ ) {
			if ( value[ index ] is < '0' or > '9' ) {
				return false;
			}
		}
		return true;
	}
}
