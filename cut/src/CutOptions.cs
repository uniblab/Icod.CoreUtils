namespace Icod.CoreUtils.Cut;

using Icod.CoreUtils.Shared.Ranges;
using Icod.CoreUtils.Shared.Text;

/// <summary>Contains validated options for one <c>cut</c> execution.</summary>
internal sealed class CutOptions {
	/// <summary>Initializes validated command options.</summary>
	internal CutOptions(
		CutMode mode,
		RangeSet ranges,
		IReadOnlyList<string> operands,
		ITextLocaleProvider localeProvider,
		byte recordSeparator,
		byte[] generatedRecordSeparator,
		byte[]? fieldDelimiter,
		byte[]? outputDelimiter,
		bool suppressUndelimited,
		bool noPartialCharacters,
		bool whitespaceDelimited,
		bool trimWhitespace
	) {
		this.Mode = mode;
		this.Ranges = ranges;
		this.Operands = operands;
		this.LocaleProvider = localeProvider;
		this.RecordSeparator = recordSeparator;
		this.GeneratedRecordSeparator = generatedRecordSeparator;
		this.FieldDelimiter = fieldDelimiter;
		this.OutputDelimiter = outputDelimiter;
		this.SuppressUndelimited = suppressUndelimited;
		this.NoPartialCharacters = noPartialCharacters;
		this.WhitespaceDelimited = whitespaceDelimited;
		this.TrimWhitespace = trimWhitespace;
	}
	/// <summary>Gets the selected positional mode.</summary>
	internal CutMode Mode { get; }
	/// <summary>Gets the normalized one-based selection ranges.</summary>
	internal RangeSet Ranges { get; }
	/// <summary>Gets ordered file operands.</summary>
	internal IReadOnlyList<string> Operands { get; }
	/// <summary>Gets the active deterministic locale profile.</summary>
	internal ITextLocaleProvider LocaleProvider { get; }
	/// <summary>Gets the record-separator byte.</summary>
	internal byte RecordSeparator { get; }
	/// <summary>Gets the generated terminator for an unterminated final record.</summary>
	internal byte[] GeneratedRecordSeparator { get; }
	/// <summary>Gets the explicit field delimiter, or <see langword="null"/> for whitespace fields.</summary>
	internal byte[]? FieldDelimiter { get; }
	/// <summary>Gets the explicit or mode-default output delimiter, or <see langword="null"/> when byte and character ranges are concatenated directly.</summary>
	internal byte[]? OutputDelimiter { get; }
	/// <summary>Gets whether records without a field delimiter are suppressed.</summary>
	internal bool SuppressUndelimited { get; }
	/// <summary>Gets whether byte selection must avoid partial multibyte characters.</summary>
	internal bool NoPartialCharacters { get; }
	/// <summary>Gets whether runs of locale blanks delimit fields.</summary>
	internal bool WhitespaceDelimited { get; }
	/// <summary>Gets whether leading and trailing whitespace delimiters are ignored.</summary>
	internal bool TrimWhitespace { get; }
}
