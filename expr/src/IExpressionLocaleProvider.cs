namespace Icod.CoreUtils.Expr;

using System.Numerics;

/// <summary>
/// Defines locale-sensitive collation and logical-character operations required by <c>expr</c>.
/// </summary>
/// <remarks>
/// Implementations make culture and Unicode behavior injectable for deterministic tests and platform adaptation.
/// </remarks>
public interface IExpressionLocaleProvider {
	/// <summary>
	/// Compares two strings using the active collation policy.
	/// </summary>
	/// <param name="left">The left string operand.</param>
	/// <param name="right">The right string operand.</param>
	/// <param name="cancellationToken">The token used to cancel the collation operation.</param>
	/// <returns>A negative value, zero, or a positive value when <paramref name="left"/> sorts before, equal to, or after <paramref name="right"/>.</returns>
	int Compare(
		string left,
		string right,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Counts logical characters in a string.
	/// </summary>
	/// <param name="value">The string whose logical characters are counted.</param>
	/// <param name="cancellationToken">The token used to cancel logical-character enumeration.</param>
	/// <returns>The logical-character count.</returns>
	BigInteger GetLength(
		string value,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Finds the first logical character in a string that belongs to a supplied character set.
	/// </summary>
	/// <param name="value">The string to search.</param>
	/// <param name="characterSet">The string whose logical characters form the search set.</param>
	/// <param name="cancellationToken">The token used to cancel logical-character enumeration.</param>
	/// <returns>The one-based position of the first matching character, or zero when no character matches.</returns>
	BigInteger IndexOfAny(
		string value,
		string characterSet,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Extracts a substring using one-based logical-character positions.
	/// </summary>
	/// <param name="value">The source string.</param>
	/// <param name="position">The one-based logical-character starting position.</param>
	/// <param name="length">The maximum number of logical characters to return.</param>
	/// <param name="cancellationToken">The token used to cancel logical-character enumeration.</param>
	/// <returns>The selected substring, or an empty string when the requested range is invalid or outside the value.</returns>
	string Substring(
		string value,
		BigInteger position,
		BigInteger length,
		CancellationToken cancellationToken = default
	);
}
