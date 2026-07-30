namespace Icod.CoreUtils.Shared.Escapes;

/// <summary>Describes one structured escape-parsing warning or error.</summary>
public sealed class EscapeDiagnostic {

	/// <summary>Initializes an escape diagnostic.</summary>
	/// <param name="code">The stable diagnostic category.</param>
	/// <param name="severity">Whether the condition is a warning or an error.</param>
	/// <param name="sourceOffset">The zero-based UTF-16 source offset.</param>
	/// <param name="sourceLength">The number of source code units covered by the diagnostic.</param>
	/// <param name="message">A command-neutral explanatory message.</param>
	public EscapeDiagnostic(
		EscapeDiagnosticCode code,
		EscapeDiagnosticSeverity severity,
		int sourceOffset,
		int sourceLength,
		string message
	) {
		if ( !Enum.IsDefined( code ) ) {
			throw new ArgumentOutOfRangeException( nameof( code ) );
		}
		if ( !Enum.IsDefined( severity ) ) {
			throw new ArgumentOutOfRangeException( nameof( severity ) );
		}
		if ( sourceOffset < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( sourceOffset ) );
		}
		if ( sourceLength < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( sourceLength ) );
		}
		ArgumentNullException.ThrowIfNull( message );
		this.Code = code;
		this.Severity = severity;
		this.SourceOffset = sourceOffset;
		this.SourceLength = sourceLength;
		this.Message = message;
	}

	/// <summary>Gets the stable diagnostic category.</summary>
	public EscapeDiagnosticCode Code { get; }

	/// <summary>Gets whether the condition is a warning or an error.</summary>
	public EscapeDiagnosticSeverity Severity { get; }

	/// <summary>Gets the zero-based UTF-16 source offset.</summary>
	public int SourceOffset { get; }

	/// <summary>Gets the number of source code units covered by the diagnostic.</summary>
	public int SourceLength { get; }

	/// <summary>Gets the command-neutral explanatory message.</summary>
	public string Message { get; }

}
