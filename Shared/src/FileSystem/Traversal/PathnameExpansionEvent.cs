namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Identifies the result represented by one pathname-expansion event.
/// </summary>
public enum PathnameExpansionEventKind {
	/// <summary>A traversal root was produced.</summary>
	Root = 0,
	/// <summary>A pattern produced no matches under a non-error policy.</summary>
	NoMatch = 1,
	/// <summary>A structured expansion error occurred.</summary>
	Error = 2,
	/// <summary>A followed directory would revisit an active ancestor.</summary>
	Cycle = 3,
	/// <summary>A matching directory lies beyond the configured root filesystem.</summary>
	FileSystemBoundary = 4
}

/// <summary>
/// Represents one provenance-preserving pathname-expansion result.
/// </summary>
public sealed class PathnameExpansionEvent {
	private PathnameExpansionEvent(
		PathnameExpansionEventKind kind,
		string originalOperand,
		int operandIndex,
		PathTraversalRoot? root,
		PathTraversalError? error,
		string? path,
		string? relatedPath
	) {
		Kind = kind;
		OriginalOperand = originalOperand;
		OperandIndex = operandIndex;
		Root = root;
		Error = error;
		Path = path;
		RelatedPath = relatedPath;
	}

	/// <summary>Gets the event kind.</summary>
	public PathnameExpansionEventKind Kind { get; }

	/// <summary>Gets the original operand text.</summary>
	public string OriginalOperand { get; }

	/// <summary>Gets the zero-based original operand index.</summary>
	public int OperandIndex { get; }

	/// <summary>Gets the produced root, when <see cref="Kind"/> is <see cref="PathnameExpansionEventKind.Root"/>.</summary>
	public PathTraversalRoot? Root { get; }

	/// <summary>Gets the structured error, when <see cref="Kind"/> is <see cref="PathnameExpansionEventKind.Error"/>.</summary>
	public PathTraversalError? Error { get; }

	/// <summary>Gets the associated operational pathname for a no-match, cycle, or boundary event.</summary>
	public string? Path { get; }

	/// <summary>Gets an associated ancestor pathname for a cycle event.</summary>
	public string? RelatedPath { get; }

	/// <summary>Creates a root event.</summary>
	/// <param name="root">The produced root.</param>
	/// <returns>The event.</returns>
	public static PathnameExpansionEvent CreateRoot( PathTraversalRoot root ) {
		ArgumentNullException.ThrowIfNull( root );
		return new PathnameExpansionEvent(
			PathnameExpansionEventKind.Root,
			root.OriginalOperand,
			root.OperandIndex,
			root,
			null,
			root.AccessPath,
			null
		);
	}

	/// <summary>Creates a no-match event.</summary>
	/// <param name="operand">The original operand.</param>
	/// <param name="operandIndex">The operand index.</param>
	/// <returns>The event.</returns>
	public static PathnameExpansionEvent CreateNoMatch( string operand, int operandIndex ) {
		ArgumentNullException.ThrowIfNull( operand );
		ArgumentOutOfRangeException.ThrowIfNegative( operandIndex );
		return new PathnameExpansionEvent(
			PathnameExpansionEventKind.NoMatch,
			operand,
			operandIndex,
			null,
			null,
			operand,
			null
		);
	}

	/// <summary>Creates an error event.</summary>
	/// <param name="operand">The original operand.</param>
	/// <param name="operandIndex">The operand index.</param>
	/// <param name="error">The structured error.</param>
	/// <returns>The event.</returns>
	public static PathnameExpansionEvent CreateError(
		string operand,
		int operandIndex,
		PathTraversalError error
	) {
		ArgumentNullException.ThrowIfNull( operand );
		ArgumentOutOfRangeException.ThrowIfNegative( operandIndex );
		ArgumentNullException.ThrowIfNull( error );
		return new PathnameExpansionEvent(
			PathnameExpansionEventKind.Error,
			operand,
			operandIndex,
			null,
			error,
			error.Path,
			null
		);
	}

	/// <summary>Creates a cycle event.</summary>
	/// <param name="operand">The original operand.</param>
	/// <param name="operandIndex">The operand index.</param>
	/// <param name="path">The matching directory path.</param>
	/// <param name="ancestorPath">The active ancestor path with the same identity.</param>
	/// <returns>The event.</returns>
	public static PathnameExpansionEvent CreateCycle(
		string operand,
		int operandIndex,
		string path,
		string ancestorPath
	) {
		ArgumentNullException.ThrowIfNull( operand );
		ArgumentOutOfRangeException.ThrowIfNegative( operandIndex );
		ArgumentNullException.ThrowIfNull( path );
		ArgumentNullException.ThrowIfNull( ancestorPath );
		return new PathnameExpansionEvent(
			PathnameExpansionEventKind.Cycle,
			operand,
			operandIndex,
			null,
			null,
			path,
			ancestorPath
		);
	}

	/// <summary>Creates a filesystem-boundary event.</summary>
	/// <param name="operand">The original operand.</param>
	/// <param name="operandIndex">The operand index.</param>
	/// <param name="path">The directory path beyond the root filesystem.</param>
	/// <returns>The event.</returns>
	public static PathnameExpansionEvent CreateFileSystemBoundary(
		string operand,
		int operandIndex,
		string path
	) {
		ArgumentNullException.ThrowIfNull( operand );
		ArgumentOutOfRangeException.ThrowIfNegative( operandIndex );
		ArgumentNullException.ThrowIfNull( path );
		return new PathnameExpansionEvent(
			PathnameExpansionEventKind.FileSystemBoundary,
			operand,
			operandIndex,
			null,
			null,
			path,
			null
		);
	}
}
