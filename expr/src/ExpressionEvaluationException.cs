namespace Icod.CoreUtils.Expr;

internal sealed class ExpressionEvaluationException : Exception {
	public ExpressionEvaluationException(
		string message,
		int exitStatus
	) : this( [ message ], exitStatus ) {
	}

	public ExpressionEvaluationException(
		IReadOnlyList<string> diagnosticMessages,
		int exitStatus
	) : base( GetFirstMessage( diagnosticMessages ) ) {
		this.DiagnosticMessages = Array.AsReadOnly( diagnosticMessages.ToArray() );
		this.ExitStatus = exitStatus;
	}

	public IReadOnlyList<string> DiagnosticMessages { get; }

	public int ExitStatus { get; }

	private static string GetFirstMessage( IReadOnlyList<string> messages ) {
		ArgumentNullException.ThrowIfNull( messages );
		if ( 0 == messages.Count ) {
			throw new ArgumentException( "At least one diagnostic message is required.", nameof( messages ) );
		}
		return messages[ 0 ] ?? throw new ArgumentException(
			"Diagnostic messages cannot contain null values.",
			nameof( messages )
		);
	}
}
