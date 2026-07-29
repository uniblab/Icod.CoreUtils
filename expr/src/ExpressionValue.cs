namespace Icod.CoreUtils.Expr;

using System.Globalization;
using System.Numerics;

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

	public bool IsInteger { get; }

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

	public static ExpressionValue Zero => new( BigInteger.Zero );

	public static ExpressionValue FromInteger( BigInteger value ) => new( value );

	public static ExpressionValue FromString( string value ) {
		ArgumentNullException.ThrowIfNull( value );
		return new ExpressionValue( value );
	}

	public string AsString() {
		return this.IsInteger
			? this.integer.ToString( CultureInfo.InvariantCulture )
			: this.text ?? string.Empty;
	}

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
