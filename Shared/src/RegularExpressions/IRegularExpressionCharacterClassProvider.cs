using System.Text;

namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Supplies locale-sensitive classification, collation, and case comparison for regular expressions.</summary>
public interface IRegularExpressionCharacterClassProvider {
	/// <summary>Gets whether a POSIX character-class name is supported.</summary>
	/// <param name="className">The invariant lower-case class name.</param>
	/// <returns><see langword="true"/> when the class is supported.</returns>
	bool IsSupportedClass( string className );

	/// <summary>Gets whether a scalar belongs to a named POSIX character class.</summary>
	/// <param name="value">The Unicode scalar.</param>
	/// <param name="className">The invariant lower-case class name.</param>
	/// <param name="ignoreCase">Whether case distinctions are ignored.</param>
	/// <returns><see langword="true"/> when the scalar belongs to the class.</returns>
	bool IsCharacterClass( Rune value, string className, bool ignoreCase );

	/// <summary>Gets whether a scalar is a GNU word character.</summary>
	/// <param name="value">The Unicode scalar.</param>
	/// <returns><see langword="true"/> for alphanumeric scalars and underscore.</returns>
	bool IsWordCharacter( Rune value );

	/// <summary>Compares two scalars according to the provider's collation policy.</summary>
	/// <param name="left">The left scalar.</param>
	/// <param name="right">The right scalar.</param>
	/// <param name="ignoreCase">Whether comparison ignores case.</param>
	/// <returns>A negative value, zero, or a positive value.</returns>
	int Compare( Rune left, Rune right, bool ignoreCase );

	/// <summary>Tests literal-character equality according to the provider's case policy.</summary>
	/// <param name="left">The left scalar.</param>
	/// <param name="right">The right scalar.</param>
	/// <param name="ignoreCase">Whether comparison ignores case.</param>
	/// <returns><see langword="true"/> when the characters are equal.</returns>
	bool AreCharactersEqual( Rune left, Rune right, bool ignoreCase );

	/// <summary>Tests whether two scalars belong to the same locale collation-equivalence class.</summary>
	/// <param name="left">The left scalar.</param>
	/// <param name="right">The right scalar.</param>
	/// <param name="ignoreCase">Whether comparison ignores case.</param>
	/// <returns><see langword="true"/> when the collating elements are equivalent.</returns>
	bool AreCollatingElementsEquivalent( Rune left, Rune right, bool ignoreCase );
}
