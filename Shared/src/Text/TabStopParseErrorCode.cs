namespace Icod.CoreUtils.Shared.Text;

/// <summary>Identifies a deterministic GNU tab-stop grammar failure.</summary>
public enum TabStopParseErrorCode {
	/// <summary>The specification contains a character outside digits, comma, blank, <c>/</c>, and <c>+</c>.</summary>
	InvalidCharacter,
	/// <summary>A decimal integer exceeds <see cref="ulong.MaxValue"/>.</summary>
	NumberOverflow,
	/// <summary>An unprefixed explicit tab stop is zero.</summary>
	Zero,
	/// <summary>Explicit tab stops are not strictly increasing.</summary>
	NotIncreasing,
	/// <summary>A recurring interval was effectively followed by another value of the same kind.</summary>
	ContinuationNotLast,
	/// <summary>Both absolute <c>/N</c> and relative <c>+N</c> continuations were supplied.</summary>
	MutuallyExclusiveContinuations,
	/// <summary>A <c>/</c> or <c>+</c> specifier occurs after digits in the same value.</summary>
	SpecifierNotAtStart
}
