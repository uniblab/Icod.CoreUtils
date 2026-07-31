namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Specifies which directory links may be followed during traversal.
/// </summary>
public enum SymbolicLinkTraversalMode {
	/// <summary>Never follows directory links.</summary>
	Never = 0,
	/// <summary>Follows links that are traversal roots but not links encountered below a root.</summary>
	RootsOnly = 1,
	/// <summary>Follows all eligible directory links.</summary>
	Always = 2
}

/// <summary>
/// Specifies whether traversal may cross the filesystem identity of a root.
/// </summary>
public enum FileSystemBoundaryMode {
	/// <summary>Allows traversal to cross filesystems, mounts, devices, or volumes.</summary>
	CrossFileSystems = 0,
	/// <summary>Prevents descent when an entry has a different available filesystem identity than its root.</summary>
	StayOnRootFileSystem = 1
}

/// <summary>
/// Specifies the ordering of children within each directory.
/// </summary>
public enum PathTraversalChildOrder {
	/// <summary>Preserves the provider enumeration order.</summary>
	Provider = 0,
	/// <summary>Uses ordinal name ordering.</summary>
	Ordinal = 1,
	/// <summary>Uses ordinal case-insensitive name ordering.</summary>
	OrdinalIgnoreCase = 2
}

/// <summary>
/// Specifies whether traversal continues after a structured error.
/// </summary>
public enum PathTraversalErrorMode {
	/// <summary>Continues according to the error's scope.</summary>
	Continue = 0,
	/// <summary>Stops after yielding the first structured error.</summary>
	Stop = 1
}

/// <summary>
/// Configures read-only traversal policy independently from filesystem observation.
/// </summary>
public sealed class PathTraversalOptions {
	/// <summary>
	/// Gets a default immutable-by-convention option instance.
	/// </summary>
	public static PathTraversalOptions Default { get; } = new();

	/// <summary>Gets or initializes the symbolic-link traversal mode.</summary>
	public SymbolicLinkTraversalMode SymbolicLinkMode { get; init; } = SymbolicLinkTraversalMode.Never;

	/// <summary>Gets or initializes the filesystem-boundary mode.</summary>
	public FileSystemBoundaryMode FileSystemBoundaryMode { get; init; } = FileSystemBoundaryMode.CrossFileSystems;

	/// <summary>Gets or initializes the child ordering.</summary>
	public PathTraversalChildOrder ChildOrder { get; init; } = PathTraversalChildOrder.Provider;

	/// <summary>Gets or initializes the maximum zero-based depth, or <see langword="null"/> for no configured depth limit.</summary>
	public int? MaximumDepth { get; init; }

	/// <summary>
	/// Gets or initializes the maximum number of children retained from one directory.
	/// This limit is also enforced in provider-order mode so deterministic resource behavior does not depend on ordering.
	/// </summary>
	public int MaximumEntriesPerDirectory { get; init; } = int.MaxValue;

	/// <summary>Gets or initializes the entry selector.</summary>
	public IPathTraversalSelector Selector { get; init; } = PathTraversalRuleSelector.AllowAll;

	/// <summary>Gets or initializes error continuation behavior.</summary>
	public PathTraversalErrorMode ErrorMode { get; init; } = PathTraversalErrorMode.Continue;

	/// <summary>
	/// Validates the option values.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">A numeric limit is invalid.</exception>
	/// <exception cref="ArgumentNullException"><see cref="Selector"/> is <see langword="null"/>.</exception>
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
		if ( MaximumDepth is < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( MaximumDepth ) );
		}
		if ( MaximumEntriesPerDirectory < 1 ) {
			throw new ArgumentOutOfRangeException( nameof( MaximumEntriesPerDirectory ) );
		}
		ArgumentNullException.ThrowIfNull( Selector );
	}
}
