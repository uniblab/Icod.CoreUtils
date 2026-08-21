using Path = global::System.IO.Path;
using Icod.CommandFramework.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;
using Icod.CommandFramework.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.CopyMove;

/// <summary>Identifies whether the shared engine copies or moves its operands.</summary>
public enum CopyMoveOperationKind {
	/// <summary>Copy each source while retaining it.</summary>
	Copy = 0,
	/// <summary>Move each source, preferring a direct rename.</summary>
	Move = 1
}

/// <summary>Controls replacement of an existing destination.</summary>
public enum CopyMoveOverwriteMode {
	/// <summary>Replace an existing destination.</summary>
	Replace = 0,
	/// <summary>Silently retain an existing destination.</summary>
	NoClobber = 1,
	/// <summary>Ask the caller before replacing an existing destination.</summary>
	Interactive = 2,
	/// <summary>Replace only when the source is newer than the destination.</summary>
	Update = 3
}

/// <summary>Controls interpretation of the final destination operand.</summary>
public enum CopyMoveDestinationMode {
	/// <summary>Infer whether the destination is a directory.</summary>
	Auto = 0,
	/// <summary>Require the destination to be a directory.</summary>
	TargetDirectory = 1,
	/// <summary>Treat the destination as one exact pathname.</summary>
	NoTargetDirectory = 2
}

/// <summary>Controls clone/reflink requests for ordinary files.</summary>
public enum CopyMoveReflinkPolicy {
	/// <summary>Do not request a clone.</summary>
	Never = 0,
	/// <summary>Attempt a clone and fall back to another copy mechanism.</summary>
	Auto = 1,
	/// <summary>Require a clone and fail when the host cannot supply one.</summary>
	Always = 2
}

/// <summary>Identifies the terminal state of one source operand.</summary>
public enum CopyMoveItemOutcome {
	/// <summary>The source was copied or moved.</summary>
	Completed = 0,
	/// <summary>The source was intentionally skipped by overwrite policy.</summary>
	Skipped = 1,
	/// <summary>The source failed.</summary>
	Failed = 2
}

/// <summary>Asks whether an existing destination may be replaced.</summary>
/// <param name="sourcePath">The source pathname.</param>
/// <param name="destinationPath">The destination pathname.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns><see langword="true"/> when replacement is permitted.</returns>
public delegate ValueTask<bool> CopyMoveOverwritePrompt(
	string sourcePath,
	string destinationPath,
	CancellationToken cancellationToken
);

/// <summary>Controls one shared copy or move operation.</summary>
public sealed class CopyMoveOptions {
	/// <summary>Gets or initializes the operation kind.</summary>
	public CopyMoveOperationKind Operation { get; init; } = CopyMoveOperationKind.Copy;
	/// <summary>Gets or initializes whether directory operands may be traversed.</summary>
	public bool Recursive { get; init; }
	/// <summary>Gets or initializes destination interpretation.</summary>
	public CopyMoveDestinationMode DestinationMode { get; init; } = CopyMoveDestinationMode.Auto;
	/// <summary>Gets or initializes symbolic-link traversal policy.</summary>
	public SymbolicLinkTraversalMode SymbolicLinkMode { get; init; } = SymbolicLinkTraversalMode.Never;
	/// <summary>Gets or initializes filesystem-boundary traversal policy.</summary>
	public FileSystemBoundaryMode FileSystemBoundaryMode { get; init; } = FileSystemBoundaryMode.CrossFileSystems;
	/// <summary>Gets or initializes requested metadata preservation.</summary>
	public RecursiveMetadataFields MetadataFields { get; init; } = RecursiveMetadataFields.None;
	/// <summary>Gets or initializes mandatory metadata preservation.</summary>
	public RecursiveMetadataFields RequiredMetadataFields { get; init; } = RecursiveMetadataFields.None;
	/// <summary>Gets or initializes sparse-file policy.</summary>
	public RecursiveSparseFilePolicy SparseFilePolicy { get; init; } = RecursiveSparseFilePolicy.WhenSupported;
	/// <summary>Gets or initializes clone/reflink policy.</summary>
	public CopyMoveReflinkPolicy ReflinkPolicy { get; init; } = CopyMoveReflinkPolicy.Auto;
	/// <summary>Gets or initializes overwrite policy.</summary>
	public CopyMoveOverwriteMode OverwriteMode { get; init; } = CopyMoveOverwriteMode.Replace;
	/// <summary>Gets or initializes GNU backup naming.</summary>
	public TransactionalReplacementBackupMode BackupMode { get; init; } = TransactionalReplacementBackupMode.None;
	/// <summary>Gets or initializes the simple backup suffix.</summary>
	public string BackupSuffix { get; init; } = "~";
	/// <summary>Gets or initializes whether repeated hard links should remain linked.</summary>
	public bool PreserveHardLinks { get; init; }
	/// <summary>Gets or initializes whether copies are created as hard links.</summary>
	public bool CopyAsHardLink { get; init; }
	/// <summary>Gets or initializes whether copies are created as symbolic links to their sources.</summary>
	public bool CopyAsSymbolicLink { get; init; }
	/// <summary>Gets or initializes whether an existing non-directory destination is removed before link creation.</summary>
	public bool RemoveDestination { get; init; }
	/// <summary>Gets or initializes whether a failed direct rename may fall back to copy and removal.</summary>
	public bool NoCopyFallback { get; init; }
	/// <summary>Gets or initializes whether successful operations are reported.</summary>
	public bool Verbose { get; init; }
	/// <summary>Gets or initializes the overwrite prompt callback.</summary>
	public CopyMoveOverwritePrompt? Prompt { get; init; }

	/// <summary>Validates the option combination.</summary>
	public void Validate() {
		if ( !Enum.IsDefined( typeof( CopyMoveOperationKind ), Operation ) ) throw new ArgumentOutOfRangeException( nameof( Operation ) );
		if ( !Enum.IsDefined( typeof( CopyMoveDestinationMode ), DestinationMode ) ) throw new ArgumentOutOfRangeException( nameof( DestinationMode ) );
		if ( !Enum.IsDefined( typeof( SymbolicLinkTraversalMode ), SymbolicLinkMode ) ) throw new ArgumentOutOfRangeException( nameof( SymbolicLinkMode ) );
		if ( !Enum.IsDefined( typeof( FileSystemBoundaryMode ), FileSystemBoundaryMode ) ) throw new ArgumentOutOfRangeException( nameof( FileSystemBoundaryMode ) );
		if ( !Enum.IsDefined( typeof( RecursiveSparseFilePolicy ), SparseFilePolicy ) ) throw new ArgumentOutOfRangeException( nameof( SparseFilePolicy ) );
		if ( !Enum.IsDefined( typeof( CopyMoveReflinkPolicy ), ReflinkPolicy ) ) throw new ArgumentOutOfRangeException( nameof( ReflinkPolicy ) );
		if ( !Enum.IsDefined( typeof( CopyMoveOverwriteMode ), OverwriteMode ) ) throw new ArgumentOutOfRangeException( nameof( OverwriteMode ) );
		if ( !Enum.IsDefined( typeof( TransactionalReplacementBackupMode ), BackupMode ) ) throw new ArgumentOutOfRangeException( nameof( BackupMode ) );
		if ( (MetadataFields & ~RecursiveMetadataFields.All) != 0 ) throw new ArgumentOutOfRangeException( nameof( MetadataFields ) );
		if ( (RequiredMetadataFields & ~MetadataFields) != 0 ) throw new ArgumentException( "Required metadata fields must also be requested." );
		if ( string.IsNullOrEmpty( BackupSuffix ) || BackupSuffix.IndexOfAny( new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar } ) >= 0 ) {
			throw new ArgumentException( "The backup suffix must be one nonempty filename suffix.", nameof( BackupSuffix ) );
		}
		if ( CopyMoveOverwriteMode.Interactive == OverwriteMode && Prompt is null ) throw new ArgumentException( "Interactive overwrite mode requires a prompt callback.", nameof( Prompt ) );
		if ( CopyAsHardLink && CopyAsSymbolicLink ) throw new ArgumentException( "Hard-link and symbolic-link copy modes are mutually exclusive." );
		if ( CopyMoveOperationKind.Move == Operation && (CopyAsHardLink || CopyAsSymbolicLink) ) throw new ArgumentException( "Move operations cannot request link-copy modes." );
	}
}

/// <summary>Reports one source operand.</summary>
/// <param name="SourcePath">The source pathname.</param>
/// <param name="DestinationPath">The resolved destination pathname.</param>
/// <param name="Outcome">The terminal item outcome.</param>
/// <param name="Message">An optional controlled diagnostic.</param>
public sealed record CopyMoveItemResult(
	string SourcePath,
	string DestinationPath,
	CopyMoveItemOutcome Outcome,
	string? Message = null
);

/// <summary>Reports one complete shared copy or move request.</summary>
public sealed class CopyMoveResult {
	/// <summary>Initializes a result.</summary>
	/// <param name="items">The source results in operand order.</param>
	public CopyMoveResult( IReadOnlyList<CopyMoveItemResult> items ) {
		ArgumentNullException.ThrowIfNull( items );
		Items = Array.AsReadOnly( items.ToArray() );
	}
	/// <summary>Gets the source results in operand order.</summary>
	public IReadOnlyList<CopyMoveItemResult> Items { get; }
	/// <summary>Gets whether no source failed.</summary>
	public bool Succeeded => Items.All( item => CopyMoveItemOutcome.Failed != item.Outcome );
}
