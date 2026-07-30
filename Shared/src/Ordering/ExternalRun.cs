namespace Icod.CoreUtils.Shared.Ordering;

/// <summary>Describes one sorted temporary run.</summary>
public sealed class ExternalRun {
	/// <summary>Initializes a sorted-run descriptor.</summary>
	/// <param name="path">The run pathname.</param>
	/// <param name="itemCount">The exact number of serialized items.</param>
	public ExternalRun(
		string path,
		long itemCount
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		ArgumentOutOfRangeException.ThrowIfNegative( itemCount );
		this.Path = path;
		this.ItemCount = itemCount;
	}

	/// <summary>Gets the run pathname.</summary>
	public string Path { get; }

	/// <summary>Gets the exact number of serialized items.</summary>
	public long ItemCount { get; }
}
