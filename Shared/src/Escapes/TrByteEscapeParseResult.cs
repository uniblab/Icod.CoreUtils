namespace Icod.CoreUtils.Shared.Escapes;

/// <summary>Contains the low-level escaped bytes and diagnostics used by GNU <c>tr</c> set parsing.</summary>
public sealed class TrByteEscapeParseResult {

	private readonly IReadOnlyList<EscapedByte> myBytes;
	private readonly IReadOnlyList<EscapeDiagnostic> myDiagnostics;

	/// <summary>Initializes a tr byte-escape parsing result for the shared parser.</summary>
	/// <param name="bytes">The parsed bytes in source order.</param>
	/// <param name="diagnostics">The diagnostics in source order.</param>
	internal TrByteEscapeParseResult(
		IEnumerable<EscapedByte> bytes,
		IEnumerable<EscapeDiagnostic> diagnostics
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		ArgumentNullException.ThrowIfNull( diagnostics );
		this.myBytes = Array.AsReadOnly( bytes.ToArray() );
		this.myDiagnostics = Array.AsReadOnly( diagnostics.ToArray() );
	}

	/// <summary>Gets whether no error-severity diagnostic occurred.</summary>
	public bool IsSuccess => !this.myDiagnostics.Any( value => EscapeDiagnosticSeverity.Error == value.Severity );

	/// <summary>Gets parsed bytes in source order.</summary>
	public IReadOnlyList<EscapedByte> Bytes => this.myBytes;

	/// <summary>Gets stable warnings and errors in source order.</summary>
	public IReadOnlyList<EscapeDiagnostic> Diagnostics => this.myDiagnostics;

}
