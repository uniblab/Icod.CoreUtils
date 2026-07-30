namespace Icod.CoreUtils.Shared.Ordering;

/// <summary>Associates a value with its original zero-based input ordinal.</summary>
/// <typeparam name="T">The ordered value type.</typeparam>
/// <param name="Value">The value.</param>
/// <param name="OriginalOrdinal">The original zero-based input ordinal.</param>
public sealed record StableItem<T>(
	T Value,
	long OriginalOrdinal
);

/// <summary>Preserves original input order whenever the primary comparer reports equality.</summary>
/// <typeparam name="T">The ordered value type.</typeparam>
public sealed class StableComparer<T> : IComparer<StableItem<T>> {
	private readonly IComparer<T> valueComparer;

	/// <summary>Initializes a stable comparer.</summary>
	/// <param name="valueComparer">The primary value comparer.</param>
	public StableComparer( IComparer<T> valueComparer ) {
		ArgumentNullException.ThrowIfNull( valueComparer );
		this.valueComparer = valueComparer;
	}

	/// <summary>Gets the primary value comparer.</summary>
	public IComparer<T> ValueComparer => this.valueComparer;

	/// <inheritdoc/>
	public int Compare( StableItem<T>? x, StableItem<T>? y ) {
		if ( ReferenceEquals( x, y ) ) {
			return 0;
		}
		if ( null == x ) {
			return -1;
		}
		if ( null == y ) {
			return 1;
		}
		var result = this.valueComparer.Compare( x.Value, y.Value );
		return 0 != result
			? result
			: x.OriginalOrdinal.CompareTo( y.OriginalOrdinal );
	}
}
