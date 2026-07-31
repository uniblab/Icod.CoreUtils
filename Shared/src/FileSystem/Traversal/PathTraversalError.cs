namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Identifies the stage at which pathname expansion or traversal failed.
/// </summary>
public enum PathTraversalOperationStage {
	/// <summary>Parsing or expanding a pathname pattern.</summary>
	ExpandPattern = 0,
	/// <summary>Observing a root pathname.</summary>
	ObserveRoot = 1,
	/// <summary>Enumerating one directory level.</summary>
	EnumerateDirectory = 2,
	/// <summary>Observing a child entry.</summary>
	ObserveEntry = 3,
	/// <summary>Following a symbolic link or reparse point.</summary>
	FollowLink = 4,
	/// <summary>Obtaining a stable entry or filesystem identity.</summary>
	ReadIdentity = 5,
	/// <summary>Preparing to descend into a directory.</summary>
	Descend = 6
}

/// <summary>
/// Identifies the continuation scope affected by an error.
/// </summary>
public enum PathTraversalErrorScope {
	/// <summary>Only one entry is skipped.</summary>
	Entry = 0,
	/// <summary>One directory subtree is skipped.</summary>
	Subtree = 1,
	/// <summary>One traversal root is skipped.</summary>
	Root = 2,
	/// <summary>The complete traversal cannot continue.</summary>
	Traversal = 3
}

/// <summary>
/// Identifies a stable shared traversal error category.
/// </summary>
public enum PathTraversalErrorCode {
	/// <summary>The provider failed to observe an entry.</summary>
	ObservationFailed = 0,
	/// <summary>The provider failed while enumerating a directory.</summary>
	EnumerationFailed = 1,
	/// <summary>A required stable identity was unavailable.</summary>
	IdentityUnavailable = 2,
	/// <summary>A pathname pattern was invalid.</summary>
	InvalidPattern = 3,
	/// <summary>A pattern produced no matches and the selected policy treats that as an error.</summary>
	NoPatternMatch = 4,
	/// <summary>A configured directory-entry resource limit was exceeded.</summary>
	DirectoryEntryLimitExceeded = 5,
	/// <summary>A pathname could not be combined or represented on the current platform.</summary>
	InvalidPath = 6
}

/// <summary>
/// Describes a structured pathname-expansion or traversal failure.
/// </summary>
public sealed class PathTraversalError {
	/// <summary>
	/// Initializes a structured error.
	/// </summary>
	/// <param name="code">The stable error category.</param>
	/// <param name="root">The associated root, when one has been established.</param>
	/// <param name="path">The associated pathname.</param>
	/// <param name="stage">The failed operation stage.</param>
	/// <param name="scope">The affected continuation scope.</param>
	/// <param name="message">The consumer-independent message.</param>
	/// <param name="exception">The underlying exception, when available.</param>
	public PathTraversalError(
		PathTraversalErrorCode code,
		PathTraversalRoot? root,
		string path,
		PathTraversalOperationStage stage,
		PathTraversalErrorScope scope,
		string message,
		Exception? exception = null
	) {
		ArgumentNullException.ThrowIfNull( path );
		ArgumentException.ThrowIfNullOrEmpty( message );
		if ( !Enum.IsDefined( typeof( PathTraversalErrorCode ), code ) ) {
			throw new ArgumentOutOfRangeException( nameof( code ) );
		}
		if ( !Enum.IsDefined( typeof( PathTraversalOperationStage ), stage ) ) {
			throw new ArgumentOutOfRangeException( nameof( stage ) );
		}
		if ( !Enum.IsDefined( typeof( PathTraversalErrorScope ), scope ) ) {
			throw new ArgumentOutOfRangeException( nameof( scope ) );
		}
		Code = code;
		Root = root;
		Path = path;
		Stage = stage;
		Scope = scope;
		Message = message;
		Exception = exception;
	}

	/// <summary>Gets the stable error category.</summary>
	public PathTraversalErrorCode Code { get; }

	/// <summary>Gets the associated root, when established.</summary>
	public PathTraversalRoot? Root { get; }

	/// <summary>Gets the associated pathname.</summary>
	public string Path { get; }

	/// <summary>Gets the failed operation stage.</summary>
	public PathTraversalOperationStage Stage { get; }

	/// <summary>Gets the affected continuation scope.</summary>
	public PathTraversalErrorScope Scope { get; }

	/// <summary>Gets the consumer-independent message.</summary>
	public string Message { get; }

	/// <summary>Gets the underlying exception, when available.</summary>
	public Exception? Exception { get; }
}
