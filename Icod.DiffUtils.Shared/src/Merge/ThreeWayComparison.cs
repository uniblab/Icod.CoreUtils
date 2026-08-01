namespace Icod.DiffUtils.Shared.Merge;

/// <summary>Contains the connected regions found by a three-way comparison.</summary>
public sealed class ThreeWayComparison {
	/// <summary>Initializes a three-way comparison.</summary>
	public ThreeWayComparison( IReadOnlyList<ThreeWayChangeRegion> regions ) {
		ArgumentNullException.ThrowIfNull( regions );
		this.Regions = regions;
	}

	/// <summary>Gets the regions in common-ancestor order.</summary>
	public IReadOnlyList<ThreeWayChangeRegion> Regions { get; }
	/// <summary>Gets whether any conflict was found.</summary>
	public bool HasConflicts => this.Regions.Any( region => region.IsConflict );
	/// <summary>Gets whether any true overlap was found.</summary>
	public bool HasOverlaps => this.Regions.Any( region => region.IsOverlap );
}
