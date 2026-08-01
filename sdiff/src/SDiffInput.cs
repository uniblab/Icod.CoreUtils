namespace Icod.DiffUtils.SDiff;

using Icod.DiffUtils.Shared.Lines;

/// <summary>Contains one materialized <c>sdiff</c> input.</summary>
internal sealed class SDiffInput {
	/// <summary>Initializes an input.</summary>
	/// <param name="path">The operational path or standard-input marker.</param>
	/// <param name="displayName">The user-facing operand name.</param>
	/// <param name="document">The materialized comparison document.</param>
	public SDiffInput( string path, string displayName, ComparisonDocument document ) {
		this.Path = path;
		this.DisplayName = displayName;
		this.Document = document;
	}

	/// <summary>Gets the operational path or standard-input marker.</summary>
	public string Path { get; }
	/// <summary>Gets the user-facing operand name.</summary>
	public string DisplayName { get; }
	/// <summary>Gets the materialized comparison document.</summary>
	public ComparisonDocument Document { get; }
}
