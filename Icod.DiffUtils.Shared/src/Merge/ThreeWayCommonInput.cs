namespace Icod.DiffUtils.Shared.Merge;

/// <summary>Identifies which external input anchors a GNU-compatible three-way comparison.</summary>
public enum ThreeWayCommonInput {
	/// <summary>Use the second input as the common file, as merge and edit-script modes normally do.</summary>
	Second,
	/// <summary>Use the third input as the common file, as the historical normal report normally does.</summary>
	Third
}
