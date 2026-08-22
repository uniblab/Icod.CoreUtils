namespace Icod.DiffUtils.Shared;

/// <summary>
/// Defines the process-result contract shared by GNU Diffutils comparison tools.
/// </summary>
public enum ComparisonStatus {
	/// <summary>The compared inputs are equivalent for the selected operation.</summary>
	Equal = 0,
	/// <summary>The compared inputs differ, or a merge reports conflicts.</summary>
	Different = 1,
	/// <summary>The comparison could not be completed because of an error.</summary>
	Trouble = 2
}
