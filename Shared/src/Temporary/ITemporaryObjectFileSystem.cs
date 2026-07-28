namespace Icod.CoreUtils.Shared.Temporary;

/// <summary>Provides exclusive temporary-object filesystem operations.</summary>
public interface ITemporaryObjectFileSystem {
	/// <summary>Attempts to create or reserve one candidate pathname.</summary>
	/// <param name="path">The candidate pathname.</param>
	/// <param name="kind">The requested temporary-object operation.</param>
	/// <returns>The attempt result.</returns>
	TemporaryObjectAttemptResult TryCreate(
		string path,
		TemporaryObjectKind kind
	);

	/// <summary>Attempts to delete an object created by this provider.</summary>
	/// <param name="path">The pathname to delete.</param>
	/// <param name="kind">The created object kind.</param>
	/// <param name="errorMessage">Receives a controlled error message when deletion fails.</param>
	/// <returns><see langword="true"/> when deletion succeeded or the object was already absent.</returns>
	bool TryDelete(
		string path,
		TemporaryObjectKind kind,
		out string? errorMessage
	);
}
