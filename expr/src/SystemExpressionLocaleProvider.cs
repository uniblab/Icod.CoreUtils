namespace Icod.CoreUtils.Expr;

using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;

/// <summary>
/// Implements <c>expr</c> collation with a .NET culture and treats Unicode scalar values as logical characters.
/// </summary>
/// <remarks>
/// Invalid UTF-16 sequences are consumed as replacement characters so operations remain deterministic and make forward progress.
/// </remarks>
public sealed class SystemExpressionLocaleProvider : IExpressionLocaleProvider {
	private readonly CompareInfo compareInfo;

	/// <summary>
	/// Initializes a provider using the current process culture.
	/// </summary>
	public SystemExpressionLocaleProvider() : this( CultureInfo.CurrentCulture ) {
	}

	/// <summary>
	/// Initializes a provider using the specified culture.
	/// </summary>
	/// <param name="culture">The culture whose collation rules are used.</param>
	/// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
	public SystemExpressionLocaleProvider( CultureInfo culture ) {
		ArgumentNullException.ThrowIfNull( culture );
		this.compareInfo = culture.CompareInfo;
	}

	/// <summary>
	/// Gets a new provider bound to the culture current when the property is read.
	/// </summary>
	/// <value>A newly constructed provider.</value>
	public static SystemExpressionLocaleProvider CurrentCulture => new();

	/// <summary>
	/// Compares two strings with the configured culture collation.
	/// </summary>
	/// <param name="left">The left string operand.</param>
	/// <param name="right">The right string operand.</param>
	/// <param name="cancellationToken">The token observed before and after culture collation.</param>
	/// <returns>A negative value, zero, or a positive value according to the configured culture.</returns>
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

	/// <summary>
	/// Counts Unicode scalar values in a string.
	/// </summary>
	/// <param name="value">The string whose Unicode scalar values are counted.</param>
	/// <param name="cancellationToken">The token checked while Unicode scalar values are counted.</param>
	/// <returns>The number of Unicode scalar values.</returns>
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

	/// <summary>
	/// Finds the one-based scalar position of the first character contained in a scalar-value set.
	/// </summary>
	/// <param name="value">The string to search by Unicode scalar value.</param>
	/// <param name="characterSet">The string whose scalar values form the search set.</param>
	/// <param name="cancellationToken">The token checked while the search set and source are decoded.</param>
	/// <returns>The one-based scalar position of the first match, or zero when none is found.</returns>
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

	/// <summary>
	/// Extracts a scalar-indexed substring using one-based position and length operands.
	/// </summary>
	/// <param name="value">The source string.</param>
	/// <param name="position">The one-based Unicode-scalar starting position.</param>
	/// <param name="length">The maximum number of Unicode scalar values to return.</param>
	/// <param name="cancellationToken">The token checked while the source string is decoded.</param>
	/// <returns>The selected scalar-aligned substring, or an empty string for an invalid or out-of-range request.</returns>
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
