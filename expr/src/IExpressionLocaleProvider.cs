namespace Icod.CoreUtils.Expr;

using System.Numerics;

/// <summary>Supplies locale-sensitive collation and logical-character operations for <c>expr</c>.</summary>
public interface IExpressionLocaleProvider {
	/// <summary>Compares two strings using the active collation policy.</summary>
	/// <param name="left">The left string.</param>
	/// <param name="right">The right string.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A value less than, equal to, or greater than zero.</returns>
	int Compare(
		string left,
		string right,
		CancellationToken cancellationToken = default
	);

	/// <summary>Counts logical characters in a string.</summary>
	/// <param name="value">The string to measure.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The logical-character count.</returns>
	BigInteger GetLength(
		string value,
		CancellationToken cancellationToken = default
	);

	/// <summary>Finds the first logical character in <paramref name="value"/> that occurs in <paramref name="characterSet"/>.</summary>
	/// <param name="value">The string to search.</param>
	/// <param name="characterSet">The set of logical characters.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A one-based logical-character position, or zero when no character matches.</returns>
	BigInteger IndexOfAny(
		string value,
		string characterSet,
		CancellationToken cancellationToken = default
	);

	/// <summary>Extracts a one-based logical-character substring.</summary>
	/// <param name="value">The source string.</param>
	/// <param name="position">The one-based starting position.</param>
	/// <param name="length">The maximum logical-character length.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The selected substring, or the empty string for an invalid or out-of-range request.</returns>
	string Substring(
		string value,
		BigInteger position,
		BigInteger length,
		CancellationToken cancellationToken = default
	);
}
