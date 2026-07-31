namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Identifies how a traversal root was produced.
/// </summary>
public enum PathTraversalRootKind {
	/// <summary>The root is an unexpanded literal operand.</summary>
	Literal = 0,
	/// <summary>The root was selected by pathname expansion.</summary>
	Expanded = 1
}

/// <summary>
/// Preserves the original-operand provenance of one traversal root.
/// </summary>
public sealed class PathTraversalRoot {
	/// <summary>
	/// Initializes a traversal root.
	/// </summary>
	/// <param name="originalOperand">The original operand text.</param>
	/// <param name="operandIndex">The zero-based original operand index.</param>
	/// <param name="rootOrdinal">The zero-based result ordinal across all operands.</param>
	/// <param name="accessPath">The operational pathname.</param>
	/// <param name="displayPath">The user-facing pathname.</param>
	/// <param name="kind">How the root was produced.</param>
	public PathTraversalRoot(
		string originalOperand,
		int operandIndex,
		long rootOrdinal,
		string accessPath,
		string displayPath,
		PathTraversalRootKind kind
	) {
		ArgumentException.ThrowIfNullOrEmpty( originalOperand );
		ArgumentOutOfRangeException.ThrowIfNegative( operandIndex );
		ArgumentOutOfRangeException.ThrowIfNegative( rootOrdinal );
		ArgumentException.ThrowIfNullOrEmpty( accessPath );
		ArgumentException.ThrowIfNullOrEmpty( displayPath );
		if ( !Enum.IsDefined( typeof( PathTraversalRootKind ), kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		OriginalOperand = originalOperand;
		OperandIndex = operandIndex;
		RootOrdinal = rootOrdinal;
		AccessPath = accessPath;
		DisplayPath = displayPath;
		Kind = kind;
	}

	/// <summary>Gets the original operand text.</summary>
	public string OriginalOperand { get; }

	/// <summary>Gets the zero-based original operand index.</summary>
	public int OperandIndex { get; }

	/// <summary>Gets the zero-based result ordinal across all operands.</summary>
	public long RootOrdinal { get; }

	/// <summary>Gets the operational pathname.</summary>
	public string AccessPath { get; }

	/// <summary>Gets the user-facing pathname.</summary>
	public string DisplayPath { get; }

	/// <summary>Gets how the root was produced.</summary>
	public PathTraversalRootKind Kind { get; }
}
