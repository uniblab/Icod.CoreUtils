namespace Icod.DiffUtils.SDiff;

/// <summary>Contains aligned groups and the effective comparison result.</summary>
internal sealed class SDiffComparison {
	/// <summary>Initializes a comparison.</summary>
	/// <param name="groups">The aligned common and differing groups.</param>
	public SDiffComparison( IReadOnlyList<SDiffGroup> groups ) {
		this.Groups = groups;
	}

	/// <summary>Gets all common and differing groups in source order.</summary>
	public IReadOnlyList<SDiffGroup> Groups { get; }
	/// <summary>Gets whether any nonignored difference remains.</summary>
	public bool HasDifferences => this.Groups.Any( group => group.IsDifferent );
}
