namespace Icod.CoreUtils.Shared.Ranges;

/// <summary>Identifies a deterministic positional range-list parsing failure.</summary>
public enum RangeParseErrorCode {
	/// <summary>The range list was empty.</summary>
	EmptyList,
	/// <summary>A numeric endpoint was expected.</summary>
	ExpectedNumber,
	/// <summary>A parsed endpoint was below the configured minimum.</summary>
	ValueBelowMinimum,
	/// <summary>A parsed endpoint exceeded the configured maximum.</summary>
	ValueAboveMaximum,
	/// <summary>A numeric endpoint overflowed <see cref="ulong"/>.</summary>
	NumberOverflow,
	/// <summary>A range contained more than one hyphen.</summary>
	MultipleDashes,
	/// <summary>An endpoint was omitted where the grammar requires one.</summary>
	MissingEndpoint,
	/// <summary>The upper endpoint preceded the lower endpoint.</summary>
	DecreasingRange,
	/// <summary>An unexpected character occurred in the list.</summary>
	UnexpectedCharacter,
	/// <summary>An open-ended range was disabled by the parser profile.</summary>
	OpenEndedNotAllowed,
	/// <summary>A leading-open range was disabled by the parser profile.</summary>
	LeadingOpenRangeNotAllowed
}
