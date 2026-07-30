namespace Icod.CoreUtils.Shared.Escapes;

using Icod.CoreUtils.Shared.Delimiters;

/// <summary>Contains either a GNU <c>paste</c> separator cycle or structured diagnostics.</summary>
public sealed class PasteDelimiterParseResult {

	private readonly IReadOnlyList<EscapeDiagnostic> myDiagnostics;

	/// <summary>Initializes a paste delimiter parsing result for the shared parser.</summary>
	/// <param name="value">The parsed cycle, or null after an error.</param>
	/// <param name="diagnostics">The diagnostics in source order.</param>
	internal PasteDelimiterParseResult(
		SeparatorCycle? value,
		IEnumerable<EscapeDiagnostic> diagnostics
	) {
		ArgumentNullException.ThrowIfNull( diagnostics );
		this.Value = value;
		this.myDiagnostics = Array.AsReadOnly( diagnostics.ToArray() );
	}

	/// <summary>Gets whether parsing succeeded.</summary>
	public bool IsSuccess => null != this.Value;

	/// <summary>Gets the parsed separator cycle when successful.</summary>
	public SeparatorCycle? Value { get; }

	/// <summary>Gets stable warnings and errors in source order.</summary>
	public IReadOnlyList<EscapeDiagnostic> Diagnostics => this.myDiagnostics;

}
