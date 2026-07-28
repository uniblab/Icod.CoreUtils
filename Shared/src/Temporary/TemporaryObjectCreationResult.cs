namespace Icod.CoreUtils.Shared.Temporary;

/// <summary>Describes a complete secure temporary-name generation operation.</summary>
/// <param name="IsSuccess">Whether the operation succeeded.</param>
/// <param name="Path">The generated pathname when successful.</param>
/// <param name="ErrorMessage">A controlled error message when unsuccessful.</param>
/// <param name="Attempts">The number of candidate names attempted.</param>
/// <param name="Kind">The requested operation kind.</param>
public sealed record TemporaryObjectCreationResult(
	bool IsSuccess,
	string? Path,
	string? ErrorMessage,
	int Attempts,
	TemporaryObjectKind Kind
) {
	/// <summary>Creates a successful result.</summary>
	public static TemporaryObjectCreationResult Succeeded(
		string path,
		int attempts,
		TemporaryObjectKind kind
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		return new( true, path, null, attempts, kind );
	}

	/// <summary>Creates an unsuccessful result.</summary>
	public static TemporaryObjectCreationResult Failed(
		string errorMessage,
		int attempts,
		TemporaryObjectKind kind
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( errorMessage );
		return new( false, null, errorMessage, attempts, kind );
	}
}
