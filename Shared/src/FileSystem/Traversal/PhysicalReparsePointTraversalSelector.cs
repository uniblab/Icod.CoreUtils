namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Yields physical reparse-point objects while preventing traversal from enumerating their apparent directory contents.
/// </summary>
/// <remarks>
/// This selector is intended for physical-object operations such as removal, non-dereferencing copy, and move cleanup.
/// A reparse point that was explicitly dereferenced by traversal policy remains eligible for descent.
/// </remarks>
public sealed class PhysicalReparsePointTraversalSelector : IPathTraversalSelector {
	/// <summary>Gets the reusable selector instance.</summary>
	public static PhysicalReparsePointTraversalSelector Instance { get; } = new();

	private PhysicalReparsePointTraversalSelector() {
	}

	/// <inheritdoc/>
	public PathTraversalSelection Select( PathTraversalEntry entry ) {
		ArgumentNullException.ThrowIfNull( entry );
		return new PathTraversalSelection(
			Yield: true,
			Descend: !(entry.IsReparsePoint && !entry.WasDereferenced)
		);
	}
}
