using Icod.CoreUtils.Shared.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.Platform;

namespace Icod.CoreUtils.Shared.FileSystem.Mutation;

/// <summary>
/// Identifies one basic single-path mutation.
/// </summary>
public enum FileSystemMutationOperation {
	/// <summary>Create one directory.</summary>
	CreateDirectory = 0,
	/// <summary>Create one ordinary file.</summary>
	CreateFile = 1,
	/// <summary>Create one hard link.</summary>
	CreateHardLink = 2,
	/// <summary>Create one symbolic link.</summary>
	CreateSymbolicLink = 3,
	/// <summary>Create one FIFO.</summary>
	CreateFifo = 4,
	/// <summary>Create one block or character device node.</summary>
	CreateDeviceNode = 5,
	/// <summary>Remove one non-directory pathname object.</summary>
	RemoveFile = 6,
	/// <summary>Remove one empty physical directory.</summary>
	RemoveDirectory = 7,
	/// <summary>Set the mode of one existing entry.</summary>
	SetMode = 8,
	/// <summary>Create one Windows directory junction.</summary>
	CreateJunction = 9
}

/// <summary>
/// Identifies an expected destination existence state.
/// </summary>
public enum FileSystemMutationExistence {
	/// <summary>The caller has no existence requirement.</summary>
	Any = 0,
	/// <summary>The pathname must exist.</summary>
	MustExist = 1,
	/// <summary>The pathname must not exist.</summary>
	MustNotExist = 2
}

/// <summary>
/// Identifies a controlled mutation failure.
/// </summary>
public enum FileSystemMutationErrorCode {
	/// <summary>No error occurred.</summary>
	None = 0,
	/// <summary>The host does not support the requested operation or policy.</summary>
	Unsupported = 1,
	/// <summary>The pathname is invalid.</summary>
	InvalidPath = 2,
	/// <summary>The pathname already exists.</summary>
	AlreadyExists = 3,
	/// <summary>The pathname does not exist.</summary>
	NotFound = 4,
	/// <summary>A required parent directory does not exist.</summary>
	ParentNotFound = 5,
	/// <summary>The observed object kind is not valid for the requested operation.</summary>
	WrongObjectKind = 6,
	/// <summary>The caller lacks ordinary access to the object.</summary>
	AccessDenied = 7,
	/// <summary>The operation requires a privilege not held by the current process.</summary>
	PrivilegeRequired = 8,
	/// <summary>The observed stable identity changed before mutation.</summary>
	IdentityChanged = 9,
	/// <summary>The requested pathname-indirection policy cannot be honored safely.</summary>
	UnsafePathIndirection = 10,
	/// <summary>The directory is not empty.</summary>
	DirectoryNotEmpty = 11,
	/// <summary>The source and destination are on different filesystems.</summary>
	CrossDevice = 12,
	/// <summary>The request was cancelled.</summary>
	Cancelled = 13,
	/// <summary>The host reported another input/output failure.</summary>
	IoFailure = 14,
	/// <summary>The supplied major or minor device number is not representable or valid.</summary>
	InvalidDeviceNumber = 15
}

/// <summary>
/// Describes the basic mutation capabilities of one provider.
/// </summary>
public sealed class FileSystemMutationCapabilities {
	/// <summary>
	/// Initializes a capability description.
	/// </summary>
	public FileSystemMutationCapabilities(
		bool canCreateDirectories,
		bool canCreateFiles,
		bool canCreateHardLinks,
		bool canCreateSymbolicLinks,
		bool canCreateFifos,
		bool canCreateDeviceNodes,
		bool canRemoveFiles,
		bool canRemoveDirectories,
		bool canSetModes,
		bool canSetModeWithoutFollowingPathIndirection,
		bool canCreateJunctions = false
	) {
		CanCreateDirectories = canCreateDirectories;
		CanCreateFiles = canCreateFiles;
		CanCreateHardLinks = canCreateHardLinks;
		CanCreateSymbolicLinks = canCreateSymbolicLinks;
		CanCreateFifos = canCreateFifos;
		CanCreateDeviceNodes = canCreateDeviceNodes;
		CanRemoveFiles = canRemoveFiles;
		CanRemoveDirectories = canRemoveDirectories;
		CanSetModes = canSetModes;
		CanSetModeWithoutFollowingPathIndirection = canSetModeWithoutFollowingPathIndirection;
		CanCreateJunctions = canCreateJunctions;
	}

	/// <summary>Gets whether one directory can be created.</summary>
	public bool CanCreateDirectories { get; }
	/// <summary>Gets whether one ordinary file can be created.</summary>
	public bool CanCreateFiles { get; }
	/// <summary>Gets whether hard links can be created.</summary>
	public bool CanCreateHardLinks { get; }
	/// <summary>Gets whether symbolic links can be created.</summary>
	public bool CanCreateSymbolicLinks { get; }
	/// <summary>Gets whether Windows directory junctions can be created.</summary>
	public bool CanCreateJunctions { get; }
	/// <summary>Gets whether FIFOs can be created.</summary>
	public bool CanCreateFifos { get; }
	/// <summary>Gets whether block and character device nodes can be created.</summary>
	public bool CanCreateDeviceNodes { get; }
	/// <summary>Gets whether non-directory pathname objects can be removed.</summary>
	public bool CanRemoveFiles { get; }
	/// <summary>Gets whether empty physical directories can be removed.</summary>
	public bool CanRemoveDirectories { get; }
	/// <summary>Gets whether POSIX mode bits can be changed.</summary>
	public bool CanSetModes { get; }
	/// <summary>Gets whether a terminal path-indirection object's own mode can be changed.</summary>
	public bool CanSetModeWithoutFollowingPathIndirection { get; }
}

/// <summary>
/// Carries an E3/E3R observation that must still hold immediately before mutation.
/// </summary>
public sealed class FileSystemMutationPrecondition {
	/// <summary>
	/// Initializes a precondition.
	/// </summary>
	/// <param name="existence">The required existence state.</param>
	/// <param name="dereferenceMode">The terminal-object observation policy.</param>
	/// <param name="expectedKind">An optional required effective object kind.</param>
	/// <param name="expectedIdentity">An optional required stable identity.</param>
	/// <param name="rejectUncharacterizedIndirection">Whether unknown reparse-point indirection must be rejected.</param>
	public FileSystemMutationPrecondition(
		FileSystemMutationExistence existence,
		PathDereferenceMode dereferenceMode = PathDereferenceMode.NoFollow,
		FileSystemEntryKind? expectedKind = null,
		FileSystemEntryIdentity? expectedIdentity = null,
		bool rejectUncharacterizedIndirection = true
	) {
		if ( !Enum.IsDefined( typeof( FileSystemMutationExistence ), existence ) ) {
			throw new ArgumentOutOfRangeException( nameof( existence ) );
		}
		if ( !Enum.IsDefined( typeof( PathDereferenceMode ), dereferenceMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( dereferenceMode ) );
		}
		if ( expectedKind.HasValue && !Enum.IsDefined( typeof( FileSystemEntryKind ), expectedKind.Value ) ) {
			throw new ArgumentOutOfRangeException( nameof( expectedKind ) );
		}
		Existence = existence;
		DereferenceMode = dereferenceMode;
		ExpectedKind = expectedKind;
		ExpectedIdentity = expectedIdentity;
		RejectUncharacterizedIndirection = rejectUncharacterizedIndirection;
	}

	/// <summary>Gets the required existence state.</summary>
	public FileSystemMutationExistence Existence { get; }

	/// <summary>Gets the terminal-object observation policy.</summary>
	public PathDereferenceMode DereferenceMode { get; }

	/// <summary>Gets the optional required effective object kind.</summary>
	public FileSystemEntryKind? ExpectedKind { get; }

	/// <summary>Gets the optional required stable identity.</summary>
	public FileSystemEntryIdentity? ExpectedIdentity { get; }

	/// <summary>Gets whether uncharacterized pathname indirection must be rejected.</summary>
	public bool RejectUncharacterizedIndirection { get; }

	/// <summary>Creates a no-follow precondition requiring a missing destination.</summary>
	public static FileSystemMutationPrecondition DestinationMustNotExist() {
		return new FileSystemMutationPrecondition( FileSystemMutationExistence.MustNotExist );
	}

	/// <summary>Creates a precondition from one prior authoritative observation.</summary>
	/// <param name="kind">The previously observed effective kind.</param>
	/// <param name="identity">The previously observed stable identity.</param>
	/// <param name="dereferenceMode">The policy used to obtain the observation.</param>
	/// <returns>The identity-bearing precondition.</returns>
	public static FileSystemMutationPrecondition FromObservation(
		FileSystemEntryKind kind,
		FileSystemEntryIdentity identity,
		PathDereferenceMode dereferenceMode
	) {
		return new FileSystemMutationPrecondition(
			FileSystemMutationExistence.MustExist,
			dereferenceMode,
			kind,
			identity.IsAvailable ? identity : null
		);
	}
}

/// <summary>
/// Describes a successful mutation and the resulting physical pathname object.
/// </summary>
public sealed class FileSystemMutationOutcome {
	/// <summary>
	/// Initializes a successful mutation outcome.
	/// </summary>
	public FileSystemMutationOutcome(
		string path,
		FileSystemMutationOperation operation,
		FileSystemEntryKind kind,
		FileSystemEntryIdentity entryIdentity,
		bool? modeApplied,
		bool wasDereferenced
	) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		if ( !Enum.IsDefined( typeof( FileSystemMutationOperation ), operation ) ) {
			throw new ArgumentOutOfRangeException( nameof( operation ) );
		}
		if ( !Enum.IsDefined( typeof( FileSystemEntryKind ), kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		Path = path;
		Operation = operation;
		Kind = kind;
		EntryIdentity = entryIdentity;
		ModeApplied = modeApplied;
		WasDereferenced = wasDereferenced;
	}

	/// <summary>Gets the normalized pathname.</summary>
	public string Path { get; }
	/// <summary>Gets the completed operation.</summary>
	public FileSystemMutationOperation Operation { get; }
	/// <summary>Gets the resulting or removed object kind.</summary>
	public FileSystemEntryKind Kind { get; }
	/// <summary>Gets the resulting or removed stable identity.</summary>
	public FileSystemEntryIdentity EntryIdentity { get; }
	/// <summary>Gets whether a requested mode was applied, or null when no mode was requested.</summary>
	public bool? ModeApplied { get; }
	/// <summary>Gets whether the operation addressed a dereferenced terminal target.</summary>
	public bool WasDereferenced { get; }
}

/// <summary>
/// Describes a successful, unsupported, or failed basic filesystem mutation.
/// </summary>
public sealed class FileSystemMutationResult : PlatformOperationResult {
	private FileSystemMutationResult(
		bool supported,
		bool succeeded,
		string path,
		FileSystemMutationErrorCode errorCode,
		FileSystemMutationOutcome? outcome,
		string? message,
		Exception? exception
	) : base( supported, succeeded, message, exception ) {
		Path = path;
		ErrorCode = errorCode;
		Outcome = outcome;
	}

	/// <summary>Gets the pathname associated with the operation.</summary>
	public string Path { get; }

	/// <summary>Gets the controlled error code.</summary>
	public FileSystemMutationErrorCode ErrorCode { get; }

	/// <summary>Gets the successful mutation outcome.</summary>
	public FileSystemMutationOutcome? Outcome { get; }

	/// <summary>Creates a successful mutation result.</summary>
	/// <param name="outcome">The mutation outcome.</param>
	/// <param name="message">An optional controlled capability note.</param>
	/// <returns>The successful result.</returns>
	public static FileSystemMutationResult Success(
		FileSystemMutationOutcome outcome,
		string? message = null
	) {
		ArgumentNullException.ThrowIfNull( outcome );
		return new FileSystemMutationResult(
			true,
			true,
			outcome.Path,
			FileSystemMutationErrorCode.None,
			outcome,
			message,
			null
		);
	}

	/// <summary>Creates a supported but failed mutation result.</summary>
	/// <param name="path">The affected pathname.</param>
	/// <param name="errorCode">The controlled failure category.</param>
	/// <param name="message">The user-facing diagnostic.</param>
	/// <param name="exception">The optional underlying exception.</param>
	/// <returns>The failed result.</returns>
	public static FileSystemMutationResult Failure(
		string path,
		FileSystemMutationErrorCode errorCode,
		string message,
		Exception? exception = null
	) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		ArgumentException.ThrowIfNullOrEmpty( message );
		if ( errorCode is FileSystemMutationErrorCode.None or FileSystemMutationErrorCode.Unsupported ) {
			throw new ArgumentOutOfRangeException( nameof( errorCode ) );
		}
		return new FileSystemMutationResult(
			true,
			false,
			path,
			errorCode,
			null,
			message,
			exception
		);
	}

	/// <summary>Creates an unsupported mutation result.</summary>
	/// <param name="path">The affected pathname.</param>
	/// <param name="message">The user-facing diagnostic.</param>
	/// <returns>The unsupported result.</returns>
	public static FileSystemMutationResult Unsupported( string path, string message ) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		ArgumentException.ThrowIfNullOrEmpty( message );
		return new FileSystemMutationResult(
			false,
			false,
			path,
			FileSystemMutationErrorCode.Unsupported,
			null,
			message,
			null
		);
	}
}

/// <summary>
/// Describes a block or character device number.
/// </summary>
public readonly record struct DeviceNumber {
	/// <summary>
	/// Initializes a device number.
	/// </summary>
	/// <param name="major">The device-driver major number.</param>
	/// <param name="minor">The device minor number.</param>
	public DeviceNumber( uint major, uint minor ) {
		Major = major;
		Minor = minor;
	}

	/// <summary>Gets the major number.</summary>
	public uint Major { get; }

	/// <summary>Gets the minor number.</summary>
	public uint Minor { get; }
}
