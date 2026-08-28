/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace Icod.CoreUtils.Shared.Numerics;

using System.Globalization;
using System.Numerics;

/// <summary>Specifies an exact rounding direction for rational values.</summary>
public enum RationalRoundingMode {
	/// <summary>Round toward positive infinity.</summary>
	Up,
	/// <summary>Round toward negative infinity.</summary>
	Down,
	/// <summary>Round away from zero.</summary>
	FromZero,
	/// <summary>Round toward zero.</summary>
	TowardsZero,
	/// <summary>Round to nearest, with ties away from zero.</summary>
	Nearest
}

/// <summary>Represents an arbitrary-precision rational number.</summary>
public readonly struct BigRational : IComparable<BigRational>, IEquatable<BigRational> {
	/// <summary>Gets the signed numerator.</summary>
	public BigInteger Numerator { get; }
	/// <summary>Gets the positive denominator.</summary>
	public BigInteger Denominator { get; }
	/// <summary>Gets zero.</summary>
	public static BigRational Zero => new( BigInteger.Zero, BigInteger.One );

	/// <summary>Initializes and normalizes a rational number.</summary>
	public BigRational( BigInteger numerator, BigInteger denominator ) {
		if ( BigInteger.Zero == denominator ) {
			throw new DivideByZeroException();
		}
		if ( BigInteger.Zero == numerator ) {
			this.Numerator = BigInteger.Zero;
			this.Denominator = BigInteger.One;
			return;
		}
		if ( BigInteger.Zero > denominator ) {
			numerator = -numerator;
			denominator = -denominator;
		}
		var divisor = BigInteger.GreatestCommonDivisor( BigInteger.Abs( numerator ), denominator );
		this.Numerator = numerator / divisor;
		this.Denominator = denominator / divisor;
	}

	/// <summary>Parses a culture-invariant decimal or scientific-notation value exactly.</summary>
	public static bool TryParseDecimal(
		string text,
		out BigRational value,
		out int sourceFractionDigits
	) {
		value = Zero;
		sourceFractionDigits = 0;
		if ( string.IsNullOrWhiteSpace( text ) ) {
			return false;
		}
		text = text.Trim();
		var index = 0;
		var negative = false;
		if ( '+' == text[ index ] || '-' == text[ index ] ) {
			negative = '-' == text[ index ];
			index++;
			if ( index == text.Length ) {
				return false;
			}
		}
		var digits = new List<char>( text.Length );
		var beforeDecimal = 0;
		while ( index < text.Length && char.IsAsciiDigit( text[ index ] ) ) {
			digits.Add( text[ index++ ] );
			beforeDecimal++;
		}
		if ( index < text.Length && '.' == text[ index ] ) {
			index++;
			while ( index < text.Length && char.IsAsciiDigit( text[ index ] ) ) {
				digits.Add( text[ index++ ] );
				sourceFractionDigits++;
			}
		}
		if ( 0 == beforeDecimal && 0 == sourceFractionDigits ) {
			return false;
		}
		var exponent = 0;
		if ( index < text.Length && ( 'e' == text[ index ] || 'E' == text[ index ] ) ) {
			index++;
			var exponentNegative = false;
			if ( index < text.Length && ( '+' == text[ index ] || '-' == text[ index ] ) ) {
				exponentNegative = '-' == text[ index++ ];
			}
			var exponentStart = index;
			while ( index < text.Length && char.IsAsciiDigit( text[ index ] ) ) {
				if ( 10000 < exponent ) {
					return false;
				}
				exponent = checked( exponent * 10 + ( text[ index++ ] - '0' ) );
			}
			if ( 10000 < exponent ) {
				return false;
			}
			if ( exponentStart == index ) {
				return false;
			}
			if ( exponentNegative ) {
				exponent = -exponent;
			}
		}
		if ( index != text.Length ) {
			return false;
		}
		var digitText = new string( digits.ToArray() );
		if ( !BigInteger.TryParse( digitText, NumberStyles.None, CultureInfo.InvariantCulture, out var numerator ) ) {
			return false;
		}
		if ( negative ) {
			numerator = -numerator;
		}
		var scale = sourceFractionDigits - exponent;
		if ( 0 <= scale ) {
			value = new BigRational( numerator, BigInteger.Pow( 10, scale ) );
		} else {
			value = new BigRational( numerator * BigInteger.Pow( 10, -scale ), BigInteger.One );
		}
		return true;
	}

	/// <summary>Rounds the value to an integer.</summary>
	public BigInteger Round( RationalRoundingMode mode ) {
		var quotient = BigInteger.DivRem( this.Numerator, this.Denominator, out var remainder );
		if ( BigInteger.Zero == remainder ) {
			return quotient;
		}
		var sign = this.Numerator.Sign;
		var increment = mode switch {
			RationalRoundingMode.Up => 0 < sign,
			RationalRoundingMode.Down => 0 > sign,
			RationalRoundingMode.FromZero => true,
			RationalRoundingMode.TowardsZero => false,
			RationalRoundingMode.Nearest => BigInteger.Abs( remainder ) * 2 >= this.Denominator,
			_ => throw new ArgumentOutOfRangeException( nameof( mode ) )
		};
		return increment ? quotient + sign : quotient;
	}

	/// <summary>Formats the value with exactly <paramref name="fractionDigits"/> decimal places.</summary>
	public string ToFixedString(
		int fractionDigits,
		RationalRoundingMode mode = RationalRoundingMode.Nearest
	) {
		ArgumentOutOfRangeException.ThrowIfNegative( fractionDigits );
		var scale = BigInteger.Pow( 10, fractionDigits );
		var scaled = ( this * scale ).Round( mode );
		var negative = BigInteger.Zero > scaled;
		var digits = BigInteger.Abs( scaled ).ToString( CultureInfo.InvariantCulture );
		if ( 0 == fractionDigits ) {
			return negative ? string.Concat( "-", digits ) : digits;
		}
		digits = digits.PadLeft( fractionDigits + 1, '0' );
		var split = digits.Length - fractionDigits;
		var result = string.Concat( digits.Substring( 0, split ), ".", digits.Substring( split ) );
		return negative ? string.Concat( "-", result ) : result;
	}

	/// <summary>Formats an exact terminating decimal, or a rounded decimal when necessary.</summary>
	public string ToDecimalString(
		int minimumFractionDigits = 0,
		int maximumFractionDigits = 18,
		RationalRoundingMode mode = RationalRoundingMode.Nearest,
		bool trimTrailingZeroes = true
	) {
		ArgumentOutOfRangeException.ThrowIfNegative( minimumFractionDigits );
		if ( maximumFractionDigits < minimumFractionDigits ) {
			throw new ArgumentOutOfRangeException( nameof( maximumFractionDigits ) );
		}
		var denominator = this.Denominator;
		var twos = 0;
		var fives = 0;
		while ( BigInteger.Zero == denominator % 2 ) { denominator /= 2; twos++; }
		while ( BigInteger.Zero == denominator % 5 ) { denominator /= 5; fives++; }
		var exactDigits = BigInteger.One == denominator ? Math.Max( twos, fives ) : maximumFractionDigits;
		var digits = Math.Clamp( exactDigits, minimumFractionDigits, maximumFractionDigits );
		var text = this.ToFixedString( digits, mode );
		if ( trimTrailingZeroes && 0 < digits ) {
			var decimalIndex = text.IndexOf( '.', StringComparison.Ordinal );
			var end = text.Length;
			while ( end > decimalIndex + 1 + minimumFractionDigits && '0' == text[ end - 1 ] ) {
				end--;
			}
			if ( end == decimalIndex + 1 ) {
				end--;
			}
			text = text.Substring( 0, end );
		}
		return text;
	}

	/// <inheritdoc/>
	public int CompareTo( BigRational other ) {
		return ( this.Numerator * other.Denominator ).CompareTo( other.Numerator * this.Denominator );
	}
	/// <inheritdoc/>
	public bool Equals( BigRational other ) {
		return this.Numerator == other.Numerator && this.Denominator == other.Denominator;
	}
	/// <inheritdoc/>
	public override bool Equals( object? obj ) { return obj is BigRational other && this.Equals( other ); }
	/// <inheritdoc/>
	public override int GetHashCode() { return HashCode.Combine( this.Numerator, this.Denominator ); }
	/// <inheritdoc/>
	public override string ToString() { return this.ToDecimalString(); }

	/// <summary>Adds two values.</summary>
	public static BigRational operator +( BigRational left, BigRational right ) {
		return new BigRational( left.Numerator * right.Denominator + right.Numerator * left.Denominator, left.Denominator * right.Denominator );
	}
	/// <summary>Subtracts two values.</summary>
	public static BigRational operator -( BigRational left, BigRational right ) {
		return new BigRational( left.Numerator * right.Denominator - right.Numerator * left.Denominator, left.Denominator * right.Denominator );
	}
	/// <summary>Multiplies two values.</summary>
	public static BigRational operator *( BigRational left, BigRational right ) {
		return new BigRational( left.Numerator * right.Numerator, left.Denominator * right.Denominator );
	}
	/// <summary>Multiplies by an integer.</summary>
	public static BigRational operator *( BigRational left, BigInteger right ) {
		return new BigRational( left.Numerator * right, left.Denominator );
	}
	/// <summary>Divides by an integer.</summary>
	public static BigRational operator /( BigRational left, BigInteger right ) {
		return new BigRational( left.Numerator, left.Denominator * right );
	}
	/// <summary>Negates a value.</summary>
	public static BigRational operator -( BigRational value ) { return new BigRational( -value.Numerator, value.Denominator ); }
	/// <summary>Tests equality.</summary>
	public static bool operator ==( BigRational left, BigRational right ) { return left.Equals( right ); }
	/// <summary>Tests inequality.</summary>
	public static bool operator !=( BigRational left, BigRational right ) { return !left.Equals( right ); }
	/// <summary>Tests ordering.</summary>
	public static bool operator <( BigRational left, BigRational right ) { return 0 > left.CompareTo( right ); }
	/// <summary>Tests ordering.</summary>
	public static bool operator >( BigRational left, BigRational right ) { return 0 < left.CompareTo( right ); }
	/// <summary>Tests ordering.</summary>
	public static bool operator <=( BigRational left, BigRational right ) { return 0 >= left.CompareTo( right ); }
	/// <summary>Tests ordering.</summary>
	public static bool operator >=( BigRational left, BigRational right ) { return 0 <= left.CompareTo( right ); }
}
