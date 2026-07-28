namespace Icod.CoreUtils.Shared.Temporary;

/// <summary>Identifies the outcome of one candidate-name attempt.</summary>
public enum TemporaryObjectAttemptStatus {
	/// <summary>The requested operation succeeded.</summary>
	Success,

	/// <summary>The candidate pathname already exists and another candidate may be tried.</summary>
	Collision,

	/// <summary>The operation failed for a reason other than a name collision.</summary>
	Failure
}

/// <summary>Describes the outcome of one filesystem operation against a candidate pathname.</summary>
/// <param name="Status">The attempt status.</param>
/// <param name="ErrorMessage">A controlled error message when the operation failed.</param>
public sealed record TemporaryObjectAttemptResult(
	TemporaryObjectAttemptStatus Status,
	string? ErrorMessage = null
) {
	/// <summary>Creates a successful result.</summary>
	public static TemporaryObjectAttemptResult Succeeded() {
		return new( TemporaryObjectAttemptStatus.Success );
	}

	/// <summary>Creates a collision result.</summary>
	public static TemporaryObjectAttemptResult Collided() {
		return new( TemporaryObjectAttemptStatus.Collision );
	}

	/// <summary>Creates a failure result.</summary>
	public static TemporaryObjectAttemptResult Failed( string errorMessage ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( errorMessage );
		return new( TemporaryObjectAttemptStatus.Failure, errorMessage );
	}
}
