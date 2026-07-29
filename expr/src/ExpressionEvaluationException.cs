namespace Icod.CoreUtils.Expr;

/// <summary>
/// Represents a controlled GNU <c>expr</c> evaluation failure together with its process status and diagnostic lines.
/// </summary>
internal sealed class ExpressionEvaluationException : Exception {
	/// <summary>
	/// Initializes a controlled expression failure from one diagnostic message and an exit status.
	/// </summary>
	/// <param name="message">The primary diagnostic message.</param>
	/// <param name="exitStatus">The GNU-compatible process status for the failure.</param>
	/// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
	public ExpressionEvaluationException(
		string message,
		int exitStatus
	) : this( [ message ], exitStatus ) {
	}

	/// <summary>
	/// Initializes a controlled expression failure from an ordered diagnostic collection and an exit status.
	/// </summary>
	/// <param name="diagnosticMessages">The ordered diagnostic lines to expose to command orchestration.</param>
	/// <param name="exitStatus">The GNU-compatible process status for the failure.</param>
	/// <exception cref="ArgumentNullException"><paramref name="diagnosticMessages"/> is <see langword="null"/>.</exception>
	public ExpressionEvaluationException(
		IReadOnlyList<string> diagnosticMessages,
		int exitStatus
	) : base( GetFirstMessage( diagnosticMessages ) ) {
		this.DiagnosticMessages = Array.AsReadOnly( diagnosticMessages.ToArray() );
		this.ExitStatus = exitStatus;
	}

	/// <summary>
	/// Gets the ordered diagnostic lines associated with the evaluation failure.
	/// </summary>
	/// <value>An ordered, non-empty diagnostic collection.</value>
	public IReadOnlyList<string> DiagnosticMessages { get; }

	/// <summary>
	/// Gets the GNU <c>expr</c> process status associated with the failure.
	/// </summary>
	/// <value>A GNU-compatible nonzero process status.</value>
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
