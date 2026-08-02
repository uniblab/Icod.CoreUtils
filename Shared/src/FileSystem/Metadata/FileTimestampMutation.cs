namespace Icod.CoreUtils.Shared.FileSystem.Metadata;

/// <summary>
/// Identifies timestamp mutations supported by a provider for one observed entry.
/// </summary>
[Flags]
public enum FileTimestampMutationCapabilities {
	/// <summary>No timestamp mutation is supported.</summary>
	None = 0,
	/// <summary>The access timestamp can be changed.</summary>
	AccessTime = 1,
	/// <summary>The modification timestamp can be changed.</summary>
	ModificationTime = 2,
	/// <summary>The birth or creation timestamp can be changed.</summary>
	BirthTime = 4,
	/// <summary>A symbolic-link object's timestamps can be changed without dereferencing it.</summary>
	NoFollowSymbolicLink = 8
}

/// <summary>
/// Identifies the requested treatment of one timestamp.
/// </summary>
public enum FileTimestampChangeKind {
	/// <summary>Preserve the existing timestamp.</summary>
	Unchanged = 0,
	/// <summary>Set the timestamp to the provider's current time.</summary>
	CurrentTime = 1,
	/// <summary>Set the timestamp to an explicit instant.</summary>
	Explicit = 2
}

/// <summary>
/// Describes one requested timestamp change.
/// </summary>
public readonly record struct FileTimestampChange {
	private FileTimestampChange( FileTimestampChangeKind kind, DateTimeOffset? value ) {
		Kind = kind;
		Value = value;
	}

	/// <summary>Gets the change kind.</summary>
	public FileTimestampChangeKind Kind { get; }

	/// <summary>Gets the explicit instant when <see cref="Kind"/> is <see cref="FileTimestampChangeKind.Explicit"/>.</summary>
	public DateTimeOffset? Value { get; }

	/// <summary>Gets a request that preserves the timestamp.</summary>
	public static FileTimestampChange Unchanged { get; } = new( FileTimestampChangeKind.Unchanged, null );

	/// <summary>Gets a request that uses the provider's current time.</summary>
	public static FileTimestampChange CurrentTime { get; } = new( FileTimestampChangeKind.CurrentTime, null );

	/// <summary>Creates a request for one explicit instant.</summary>
	/// <param name="value">The requested timestamp.</param>
	/// <returns>The timestamp change.</returns>
	public static FileTimestampChange At( DateTimeOffset value ) => new( FileTimestampChangeKind.Explicit, value );
}

/// <summary>
/// Describes an atomic timestamp-mutation request for one filesystem entry.
/// </summary>
public sealed class FileTimestampMutationRequest {
	/// <summary>Gets or initializes the access-time change.</summary>
	public FileTimestampChange AccessTime { get; init; } = FileTimestampChange.Unchanged;

	/// <summary>Gets or initializes the modification-time change.</summary>
	public FileTimestampChange ModificationTime { get; init; } = FileTimestampChange.Unchanged;

	/// <summary>Gets or initializes the birth- or creation-time change.</summary>
	public FileTimestampChange BirthTime { get; init; } = FileTimestampChange.Unchanged;

	/// <summary>Gets whether the request changes at least one timestamp.</summary>
	public bool HasChanges => AccessTime.Kind != FileTimestampChangeKind.Unchanged
		|| ModificationTime.Kind != FileTimestampChangeKind.Unchanged
		|| BirthTime.Kind != FileTimestampChangeKind.Unchanged;
}
