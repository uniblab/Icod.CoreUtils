namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Specifies how an operand that contains metacharacters is handled when it produces no matches.
/// </summary>
public enum UnmatchedPathnamePatternBehavior {
	/// <summary>Returns the original operand as a literal traversal root.</summary>
	PreserveAsLiteral = 0,
	/// <summary>Returns a no-match event without producing a root.</summary>
	ReturnNoMatches = 1,
	/// <summary>Returns a structured error without producing a root.</summary>
	ReportError = 2
}

/// <summary>
/// Specifies the ordering of matches discovered in one directory.
/// </summary>
public enum PathnameExpansionMatchOrder {
	/// <summary>Preserves provider enumeration order.</summary>
	Provider = 0,
	/// <summary>Uses ordinal basename ordering.</summary>
	Ordinal = 1,
	/// <summary>Uses ordinal case-insensitive basename ordering.</summary>
	OrdinalIgnoreCase = 2
}

/// <summary>
/// Configures pathname-operand expansion independently from later traversal.
/// </summary>
public sealed class PathnameExpansionOptions {
	/// <summary>Gets a default immutable-by-convention option instance.</summary>
	public static PathnameExpansionOptions Default { get; } = new();

	/// <summary>
	/// Gets or initializes the operational base directory for relative operands.
	/// </summary>
	public string BaseDirectory { get; init; } = Directory.GetCurrentDirectory();

	/// <summary>Gets or initializes pathname-pattern syntax and comparison policy.</summary>
	public PathnamePatternOptions PatternOptions { get; init; } = PathnamePatternOptions.Default;

	/// <summary>Gets or initializes unmatched-pattern behavior.</summary>
	public UnmatchedPathnamePatternBehavior UnmatchedPatternBehavior { get; init; } =
		UnmatchedPathnamePatternBehavior.PreserveAsLiteral;

	/// <summary>Gets or initializes match ordering within each directory.</summary>
	public PathnameExpansionMatchOrder MatchOrder { get; init; } = PathnameExpansionMatchOrder.Provider;

	/// <summary>
	/// Gets or initializes which links may be followed while resolving intermediate pattern segments.
	/// <see cref="SymbolicLinkTraversalMode.RootsOnly"/> follows only explicitly named intermediate segments;
	/// wildcard-discovered directories remain physical.
	/// </summary>
	public SymbolicLinkTraversalMode SymbolicLinkMode { get; init; } = SymbolicLinkTraversalMode.Never;

	/// <summary>Gets or initializes whether expansion may cross the filesystem of its starting directory.</summary>
	public FileSystemBoundaryMode FileSystemBoundaryMode { get; init; } = FileSystemBoundaryMode.CrossFileSystems;

	/// <summary>Gets or initializes the maximum number of directory transitions from the expansion starting directory, or <see langword="null"/> for no configured limit.</summary>
	public int? MaximumDepth { get; init; }

	/// <summary>Gets or initializes the maximum number of children retained from one directory.</summary>
	public int MaximumEntriesPerDirectory { get; init; } = int.MaxValue;

	/// <summary>Gets or initializes whether expansion stops after its first structured error.</summary>
	public PathTraversalErrorMode ErrorMode { get; init; } = PathTraversalErrorMode.Continue;

	/// <summary>Validates the option values.</summary>
	/// <exception cref="ArgumentException"><see cref="BaseDirectory"/> is empty.</exception>
	/// <exception cref="ArgumentNullException">A required option object is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A numeric limit is invalid.</exception>
	internal void Validate() {
		ArgumentException.ThrowIfNullOrEmpty( BaseDirectory );
		ArgumentNullException.ThrowIfNull( PatternOptions );
		PatternOptions.Validate();
		if ( !Enum.IsDefined( typeof( UnmatchedPathnamePatternBehavior ), UnmatchedPatternBehavior ) ) {
			throw new ArgumentOutOfRangeException( nameof( UnmatchedPatternBehavior ) );
		}
		if ( !Enum.IsDefined( typeof( PathnameExpansionMatchOrder ), MatchOrder ) ) {
			throw new ArgumentOutOfRangeException( nameof( MatchOrder ) );
		}
		if ( !Enum.IsDefined( typeof( SymbolicLinkTraversalMode ), SymbolicLinkMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( SymbolicLinkMode ) );
		}
		if ( !Enum.IsDefined( typeof( FileSystemBoundaryMode ), FileSystemBoundaryMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( FileSystemBoundaryMode ) );
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
	}
}
