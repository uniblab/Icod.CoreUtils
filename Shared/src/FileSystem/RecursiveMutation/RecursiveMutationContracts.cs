using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;

/// <summary>Identifies one phase in a mutation-aware recursive event stream.</summary>
public enum RecursiveMutationEventKind {
	/// <summary>A traversal root is beginning.</summary>
	Root = 0,
	/// <summary>A physical directory is exposed in preorder.</summary>
	EnterDirectory = 1,
	/// <summary>A physical non-directory entry is exposed.</summary>
	Entry = 2,
	/// <summary>A physical directory is exposed in postorder.</summary>
	LeaveDirectory = 3,
	/// <summary>A structured traversal or mutation-policy failure occurred.</summary>
	Error = 4,
	/// <summary>Descent would revisit an active directory identity.</summary>
	Cycle = 5,
	/// <summary>Descent would cross a configured filesystem boundary.</summary>
	FileSystemBoundary = 6
}

/// <summary>Identifies a stable recursive-operation failure category.</summary>
public enum RecursiveMutationErrorCode {
	/// <summary>The E1 traversal layer reported an error.</summary>
	TraversalFailed = 0,
	/// <summary>The requested root is protected by preserve-root policy.</summary>
	PreservedRoot = 1,
	/// <summary>The destination is the source or is contained by the source.</summary>
	DestinationInsideSource = 2,
	/// <summary>A stable identity required for race-aware mutation was unavailable.</summary>
	IdentityUnavailable = 3,
	/// <summary>A later mutation failed its identity-bearing precondition.</summary>
	MutationFailed = 4,
	/// <summary>Required metadata could not be observed or preserved.</summary>
	MetadataUnavailable = 5,
	/// <summary>Required sparse-file behavior is unavailable.</summary>
	SparsePreservationUnavailable = 6,
	/// <summary>Rollback or cleanup failed after a partial operation.</summary>
	CleanupFailed = 7
}

/// <summary>Identifies the stage at which a recursive operation failed.</summary>
public enum RecursiveMutationStage {
	/// <summary>Validating source, destination, and preserve-root policy.</summary>
	Preflight = 0,
	/// <summary>Enumerating through the E1 traversal engine.</summary>
	Traversal = 1,
	/// <summary>Revalidating and mutating one physical entry.</summary>
	Mutation = 2,
	/// <summary>Copying ordinary-file contents.</summary>
	ContentCopy = 3,
	/// <summary>Applying requested metadata.</summary>
	Metadata = 4,
	/// <summary>Rolling back or cleaning up a partial operation.</summary>
	Cleanup = 5
}

/// <summary>Identifies metadata classes that a recursive copy may preserve.</summary>
[Flags]
public enum RecursiveMetadataFields {
	/// <summary>Do not preserve metadata beyond copied content.</summary>
	None = 0,
	/// <summary>Preserve permission and special mode bits where representable.</summary>
	Mode = 1,
	/// <summary>Preserve numeric ownership where supported.</summary>
	Ownership = 2,
	/// <summary>Preserve the access timestamp where supported.</summary>
	AccessTime = 4,
	/// <summary>Preserve the modification timestamp where supported.</summary>
	ModificationTime = 8,
	/// <summary>Preserve the birth timestamp where supported.</summary>
	BirthTime = 16,
	/// <summary>Preserve every timestamp represented by the E3 contract.</summary>
	Timestamps = AccessTime | ModificationTime | BirthTime,
	/// <summary>Preserve host file attributes where representable.</summary>
	Attributes = 32,
	/// <summary>Preserve repeated hard-link identity.</summary>
	HardLinks = 64,
	/// <summary>Preserve sparse allocation where the host supports it.</summary>
	SparseLayout = 128,
	/// <summary>Preserve all metadata classes represented by the E5 contract.</summary>
	All = Mode | Ownership | Timestamps | Attributes | HardLinks | SparseLayout
}

/// <summary>Identifies the requested sparse-file treatment.</summary>
public enum RecursiveSparseFilePolicy {
	/// <summary>Copy every logical byte without requesting sparse preservation.</summary>
	Never = 0,
	/// <summary>Preserve holes when allocation information and destination support are available.</summary>
	WhenSupported = 1,
	/// <summary>Fail rather than silently materializing holes.</summary>
	Require = 2
}

/// <summary>Controls mutation-aware recursive traversal and copy planning.</summary>
public sealed class RecursiveMutationOptions {
	/// <summary>Gets the default options.</summary>
	public static RecursiveMutationOptions Default { get; } = new();

	/// <summary>Gets or initializes whether filesystem roots are protected.</summary>
	public bool PreserveRoot { get; init; } = true;

	/// <summary>Gets or initializes an optional destination root used for containment checks and relative mapping.</summary>
	public string? DestinationPath { get; init; }

	/// <summary>Gets or initializes whether every mutable entry must carry a stable E1 identity.</summary>
	public bool RequireStableEntryIdentity { get; init; } = true;

	/// <summary>Gets or initializes the terminal pathname-indirection traversal policy.</summary>
	public SymbolicLinkTraversalMode SymbolicLinkMode { get; init; } = SymbolicLinkTraversalMode.Never;

	/// <summary>Gets or initializes the filesystem-boundary policy.</summary>
	public FileSystemBoundaryMode FileSystemBoundaryMode { get; init; } = FileSystemBoundaryMode.CrossFileSystems;

	/// <summary>Gets or initializes child ordering.</summary>
	public PathTraversalChildOrder ChildOrder { get; init; } = PathTraversalChildOrder.Provider;

	/// <summary>Gets or initializes the maximum zero-based depth, or <see langword="null"/> for no configured limit.</summary>
	public int? MaximumDepth { get; init; }

	/// <summary>Gets or initializes the E1 selector that independently controls yielding and descent.</summary>
	public IPathTraversalSelector Selector { get; init; } = PathTraversalRuleSelector.AllowAll;

	/// <summary>Gets or initializes traversal error continuation policy.</summary>
	public PathTraversalErrorMode ErrorMode { get; init; } = PathTraversalErrorMode.Continue;

	/// <summary>Gets or initializes the maximum retained entries per directory.</summary>
	public int MaximumEntriesPerDirectory { get; init; } = 1_000_000;

	/// <summary>Gets or initializes the requested metadata classes.</summary>
	public RecursiveMetadataFields MetadataFields { get; init; } = RecursiveMetadataFields.None;

	/// <summary>Gets or initializes which requested metadata classes are mandatory.</summary>
	public RecursiveMetadataFields RequiredMetadataFields { get; init; } = RecursiveMetadataFields.None;

	/// <summary>Gets or initializes sparse-file policy.</summary>
	public RecursiveSparseFilePolicy SparseFilePolicy { get; init; } = RecursiveSparseFilePolicy.WhenSupported;

	/// <summary>Validates the option values before traversal or copy planning begins.</summary>
	internal void Validate() {
		if ( !Enum.IsDefined( typeof( SymbolicLinkTraversalMode ), SymbolicLinkMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( SymbolicLinkMode ) );
		}
		if ( !Enum.IsDefined( typeof( FileSystemBoundaryMode ), FileSystemBoundaryMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( FileSystemBoundaryMode ) );
		}
		if ( !Enum.IsDefined( typeof( PathTraversalChildOrder ), ChildOrder ) ) {
			throw new ArgumentOutOfRangeException( nameof( ChildOrder ) );
		}
		if ( !Enum.IsDefined( typeof( PathTraversalErrorMode ), ErrorMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( ErrorMode ) );
		}
		if ( !Enum.IsDefined( typeof( RecursiveSparseFilePolicy ), SparseFilePolicy ) ) {
			throw new ArgumentOutOfRangeException( nameof( SparseFilePolicy ) );
		}
		if ( (MetadataFields & ~RecursiveMetadataFields.All) != 0 ) {
			throw new ArgumentOutOfRangeException( nameof( MetadataFields ) );
		}
		if ( (RequiredMetadataFields & ~RecursiveMetadataFields.All) != 0 ) {
			throw new ArgumentOutOfRangeException( nameof( RequiredMetadataFields ) );
		}
		if ( MaximumDepth is < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( MaximumDepth ) );
		}
		if ( 1 > MaximumEntriesPerDirectory ) {
			throw new ArgumentOutOfRangeException( nameof( MaximumEntriesPerDirectory ) );
		}
		ArgumentNullException.ThrowIfNull( Selector );
		if ( (RequiredMetadataFields & ~MetadataFields) != 0 ) {
			throw new ArgumentException( "Required metadata fields must also be requested." );
		}
	}

	/// <summary>Creates the E1 option projection used by the mutation-aware traversal engine.</summary>
	/// <returns>The E1 traversal options.</returns>
	internal PathTraversalOptions CreateTraversalOptions() => new() {
		SymbolicLinkMode = SymbolicLinkMode,
		FileSystemBoundaryMode = FileSystemBoundaryMode,
		ChildOrder = ChildOrder,
		MaximumDepth = MaximumDepth,
		Selector = Selector,
		ErrorMode = ErrorMode,
		MaximumEntriesPerDirectory = MaximumEntriesPerDirectory
	};
}

/// <summary>Describes one physical entry prepared for race-aware mutation.</summary>
public sealed class RecursiveMutationEntry {
	/// <summary>Initializes one mutation-aware entry.</summary>
	/// <param name="traversalEntry">The underlying E1 entry and provenance.</param>
	/// <param name="precondition">The E4 identity-bearing mutation precondition.</param>
	/// <param name="destinationPath">The optional mapped destination pathname.</param>
	/// <param name="firstHardLink">The first source/destination anchor with the same stable identity, when repeated.</param>
	public RecursiveMutationEntry(
		PathTraversalEntry traversalEntry,
		FileSystemMutationPrecondition precondition,
		string? destinationPath,
		HardLinkIdentityAnchor? firstHardLink
	) {
		ArgumentNullException.ThrowIfNull( traversalEntry );
		ArgumentNullException.ThrowIfNull( precondition );
		TraversalEntry = traversalEntry;
		Precondition = precondition;
		DestinationPath = destinationPath;
		FirstHardLink = firstHardLink;
	}

	/// <summary>Gets the underlying E1 entry and its provenance.</summary>
	public PathTraversalEntry TraversalEntry { get; }

	/// <summary>Gets the E4 identity-bearing mutation precondition.</summary>
	public FileSystemMutationPrecondition Precondition { get; }

	/// <summary>Gets the destination mapped from the root-relative E1 path.</summary>
	public string? DestinationPath { get; }

	/// <summary>Gets the first source/destination anchor with this identity when this is a repeated hard link.</summary>
	public HardLinkIdentityAnchor? FirstHardLink { get; }

	/// <summary>Gets the first source pathname with this identity when this is a repeated hard link.</summary>
	public string? FirstHardLinkSourcePath => FirstHardLink?.SourcePath;

	/// <summary>Gets the first mapped destination pathname with this identity when available.</summary>
	public string? FirstHardLinkDestinationPath => FirstHardLink?.DestinationPath;

	/// <summary>Gets whether this entry repeats an earlier non-directory identity.</summary>
	public bool IsRepeatedHardLink => FirstHardLink is not null;
}

/// <summary>Describes a structured recursive-operation failure.</summary>
public sealed class RecursiveMutationError {
	/// <summary>Initializes a structured failure.</summary>
	/// <param name="code">The stable failure category.</param>
	/// <param name="stage">The operation stage at which the failure occurred.</param>
	/// <param name="scope">The affected E1-compatible continuation scope.</param>
	/// <param name="root">The E1 root and operand provenance.</param>
	/// <param name="path">The affected pathname.</param>
	/// <param name="message">The consumer-independent diagnostic.</param>
	/// <param name="exception">The optional underlying exception.</param>
	/// <param name="traversalError">The optional underlying E1 error.</param>
	/// <param name="mutationResult">The optional underlying E4 result.</param>
	public RecursiveMutationError(
		RecursiveMutationErrorCode code,
		RecursiveMutationStage stage,
		PathTraversalErrorScope scope,
		PathTraversalRoot root,
		string path,
		string message,
		Exception? exception = null,
		PathTraversalError? traversalError = null,
		FileSystemMutationResult? mutationResult = null
	) {
		if ( !Enum.IsDefined( typeof( RecursiveMutationErrorCode ), code ) ) {
			throw new ArgumentOutOfRangeException( nameof( code ) );
		}
		if ( !Enum.IsDefined( typeof( RecursiveMutationStage ), stage ) ) {
			throw new ArgumentOutOfRangeException( nameof( stage ) );
		}
		if ( !Enum.IsDefined( typeof( PathTraversalErrorScope ), scope ) ) {
			throw new ArgumentOutOfRangeException( nameof( scope ) );
		}
		ArgumentNullException.ThrowIfNull( root );
		ArgumentException.ThrowIfNullOrEmpty( path );
		ArgumentException.ThrowIfNullOrEmpty( message );
		Code = code;
		Stage = stage;
		Scope = scope;
		Root = root;
		Path = path;
		Message = message;
		Exception = exception;
		TraversalError = traversalError;
		MutationResult = mutationResult;
	}

	/// <summary>Gets the stable failure category.</summary>
	public RecursiveMutationErrorCode Code { get; }
	/// <summary>Gets the failed operation stage.</summary>
	public RecursiveMutationStage Stage { get; }
	/// <summary>Gets the affected E1-compatible continuation scope.</summary>
	public PathTraversalErrorScope Scope { get; }
	/// <summary>Gets the E1 root and operand provenance.</summary>
	public PathTraversalRoot Root { get; }
	/// <summary>Gets the affected pathname.</summary>
	public string Path { get; }
	/// <summary>Gets the consumer-independent diagnostic.</summary>
	public string Message { get; }
	/// <summary>Gets the underlying exception, when available.</summary>
	public Exception? Exception { get; }
	/// <summary>Gets the underlying E1 error, when applicable.</summary>
	public PathTraversalError? TraversalError { get; }
	/// <summary>Gets the underlying E4 result, when applicable.</summary>
	public FileSystemMutationResult? MutationResult { get; }
}

/// <summary>Represents one mutation-aware recursive event.</summary>
public sealed class RecursiveMutationEvent {
	private RecursiveMutationEvent(
		RecursiveMutationEventKind kind,
		PathTraversalRoot root,
		RecursiveMutationEntry? entry,
		RecursiveMutationError? error,
		string? relatedPath
	) {
		Kind = kind;
		Root = root;
		Entry = entry;
		Error = error;
		RelatedPath = relatedPath;
	}

	/// <summary>Gets the event kind.</summary>
	public RecursiveMutationEventKind Kind { get; }
	/// <summary>Gets the root and operand provenance.</summary>
	public PathTraversalRoot Root { get; }
	/// <summary>Gets the mutation-aware entry, when applicable.</summary>
	public RecursiveMutationEntry? Entry { get; }
	/// <summary>Gets the structured error, when applicable.</summary>
	public RecursiveMutationError? Error { get; }
	/// <summary>Gets a related pathname for cycle and boundary events.</summary>
	public string? RelatedPath { get; }

	/// <summary>Creates a root event.</summary>
	/// <param name="root">The root and operand provenance.</param>
	/// <returns>The root event.</returns>
	public static RecursiveMutationEvent CreateRoot( PathTraversalRoot root ) {
		ArgumentNullException.ThrowIfNull( root );
		return new RecursiveMutationEvent( RecursiveMutationEventKind.Root, root, null, null, null );
	}

	/// <summary>Creates an entry-phase event.</summary>
	/// <param name="kind">The entry-phase event kind.</param>
	/// <param name="entry">The mutation-aware entry.</param>
	/// <param name="relatedPath">An optional related pathname for cycle or boundary events.</param>
	/// <returns>The entry-phase event.</returns>
	public static RecursiveMutationEvent CreateEntry(
		RecursiveMutationEventKind kind,
		RecursiveMutationEntry entry,
		string? relatedPath = null
	) {
		if ( kind is RecursiveMutationEventKind.Root or RecursiveMutationEventKind.Error ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		if ( !Enum.IsDefined( typeof( RecursiveMutationEventKind ), kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		ArgumentNullException.ThrowIfNull( entry );
		return new RecursiveMutationEvent( kind, entry.TraversalEntry.Root, entry, null, relatedPath );
	}

	/// <summary>Creates an error event.</summary>
	/// <param name="error">The structured recursive-operation failure.</param>
	/// <returns>The error event.</returns>
	public static RecursiveMutationEvent CreateError( RecursiveMutationError error ) {
		ArgumentNullException.ThrowIfNull( error );
		return new RecursiveMutationEvent( RecursiveMutationEventKind.Error, error.Root, null, error, null );
	}
}

/// <summary>Builds the metadata-preservation request for one E3 observation.</summary>
public sealed class RecursiveMetadataPreservationPlan {
	private RecursiveMetadataPreservationPlan(
		RecursiveMetadataFields requested,
		RecursiveMetadataFields available,
		RecursiveMetadataFields missingRequired
	) {
		Requested = requested;
		Available = available;
		MissingRequired = missingRequired;
	}

	/// <summary>Gets requested metadata classes.</summary>
	public RecursiveMetadataFields Requested { get; }
	/// <summary>Gets metadata classes represented by the observation.</summary>
	public RecursiveMetadataFields Available { get; }
	/// <summary>Gets mandatory metadata classes absent from the observation.</summary>
	public RecursiveMetadataFields MissingRequired { get; }
	/// <summary>Gets whether every mandatory class is available.</summary>
	public bool CanProceed => MissingRequired == RecursiveMetadataFields.None;

	/// <summary>Creates a preservation plan from authoritative E3 metadata.</summary>
	/// <param name="metadata">The authoritative E3 observation.</param>
	/// <param name="requested">The metadata classes the caller would preserve when available.</param>
	/// <param name="required">The requested metadata classes that are mandatory.</param>
	/// <returns>The requested-versus-available preservation plan.</returns>
	public static RecursiveMetadataPreservationPlan Create(
		FileSystemMetadata metadata,
		RecursiveMetadataFields requested,
		RecursiveMetadataFields required
	) {
		ArgumentNullException.ThrowIfNull( metadata );
		if ( (requested & ~RecursiveMetadataFields.All) != 0 ) {
			throw new ArgumentOutOfRangeException( nameof( requested ) );
		}
		if ( (required & ~RecursiveMetadataFields.All) != 0 ) {
			throw new ArgumentOutOfRangeException( nameof( required ) );
		}
		if ( (required & ~requested) != 0 ) {
			throw new ArgumentException( "Required metadata fields must also be requested.", nameof( required ) );
		}
		var available = RecursiveMetadataFields.None;
		if ( metadata.Mode.IsAvailable ) {
			available |= RecursiveMetadataFields.Mode;
		}
		if ( metadata.UserId.IsAvailable && metadata.GroupId.IsAvailable ) {
			available |= RecursiveMetadataFields.Ownership;
		}
		if ( metadata.AccessTime.IsAvailable ) {
			available |= RecursiveMetadataFields.AccessTime;
		}
		if ( metadata.ModificationTime.IsAvailable ) {
			available |= RecursiveMetadataFields.ModificationTime;
		}
		if ( metadata.BirthTime.IsAvailable ) {
			available |= RecursiveMetadataFields.BirthTime;
		}
		if ( metadata.Attributes.IsAvailable ) {
			available |= RecursiveMetadataFields.Attributes;
		}
		if ( metadata.EntryIdentity.IsAvailable && metadata.LinkCount.IsAvailable ) {
			available |= RecursiveMetadataFields.HardLinks;
		}
		if ( metadata.Size.IsAvailable && metadata.AllocatedBytes.IsAvailable ) {
			available |= RecursiveMetadataFields.SparseLayout;
		}
		return new RecursiveMetadataPreservationPlan( requested, available, required & ~available );
	}
}
