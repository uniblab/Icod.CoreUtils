namespace Icod.CoreUtils.Expr;

using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;

/// <summary>Implements <c>expr</c> collation with a .NET culture and logical characters with Unicode scalar values.</summary>
public sealed class SystemExpressionLocaleProvider : IExpressionLocaleProvider {
	private readonly CompareInfo compareInfo;

	/// <summary>Initializes a provider using <see cref="CultureInfo.CurrentCulture"/>.</summary>
	public SystemExpressionLocaleProvider() : this( CultureInfo.CurrentCulture ) {
	}

	/// <summary>Initializes a provider using a specified culture.</summary>
	/// <param name="culture">The culture whose collation rules are used.</param>
	public SystemExpressionLocaleProvider( CultureInfo culture ) {
		ArgumentNullException.ThrowIfNull( culture );
		this.compareInfo = culture.CompareInfo;
	}

	/// <summary>Gets a provider using the culture current when this property is read.</summary>
	public static SystemExpressionLocaleProvider CurrentCulture => new();

	/// <inheritdoc/>
	public int Compare(
		string left,
		string right,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( left );
		ArgumentNullException.ThrowIfNull( right );
		cancellationToken.ThrowIfCancellationRequested();
		var result = this.compareInfo.Compare( left, right, CompareOptions.None );
		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}

	/// <inheritdoc/>
	public BigInteger GetLength(
		string value,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( value );
		cancellationToken.ThrowIfCancellationRequested();
		var count = BigInteger.Zero;
		for ( var offset = 0; value.Length > offset; ) {
			cancellationToken.ThrowIfCancellationRequested();
			_ = DecodeRune( value.AsSpan( offset ), out var consumed );
			offset += consumed;
			count++;
		}
		return count;
	}

	/// <inheritdoc/>
	public BigInteger IndexOfAny(
		string value,
		string characterSet,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentNullException.ThrowIfNull( characterSet );
		cancellationToken.ThrowIfCancellationRequested();
		var sought = new HashSet<Rune>();
		for ( var offset = 0; characterSet.Length > offset; ) {
			cancellationToken.ThrowIfCancellationRequested();
			var rune = DecodeRune( characterSet.AsSpan( offset ), out var consumed );
			offset += consumed;
			sought.Add( rune );
		}
		var position = BigInteger.One;
		for ( var offset = 0; value.Length > offset; ) {
			cancellationToken.ThrowIfCancellationRequested();
			var rune = DecodeRune( value.AsSpan( offset ), out var consumed );
			if ( sought.Contains( rune ) ) {
				return position;
			}
			offset += consumed;
			position++;
		}
		return BigInteger.Zero;
	}

	/// <inheritdoc/>
	public string Substring(
		string value,
		BigInteger position,
		BigInteger length,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( value );
		cancellationToken.ThrowIfCancellationRequested();
		if ( BigInteger.One > position || BigInteger.One > length ) {
			return string.Empty;
		}
		var first = position - BigInteger.One;
		var end = first + length;
		var current = BigInteger.Zero;
		var builder = new StringBuilder();
		for ( var offset = 0; value.Length > offset; ) {
			cancellationToken.ThrowIfCancellationRequested();
			_ = DecodeRune( value.AsSpan( offset ), out var consumed );
			if ( current >= end ) {
				break;
			}
			if ( current >= first ) {
				builder.Append( value.AsSpan( offset, consumed ) );
			}
			offset += consumed;
			current++;
		}
		return builder.ToString();
	}

	private static Rune DecodeRune(
		ReadOnlySpan<char> value,
		out int consumed
	) {
		if (
			OperationStatus.Done == Rune.DecodeFromUtf16(
				value,
				out var rune,
				out consumed
			)
		) {
			return rune;
		}
		consumed = 1;
		return new Rune( 0xFFFD );
	}
}
