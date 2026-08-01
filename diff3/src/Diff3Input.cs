namespace Icod.DiffUtils.Diff3;

using Icod.DiffUtils.Shared.Lines;

/// <summary>Contains one materialized <c>diff3</c> input and its conflict label.</summary>
internal sealed class Diff3Input {
	/// <summary>Initializes a materialized input.</summary>
	public Diff3Input( string operand, string displayName, string label, ComparisonDocument document ) {
		ArgumentException.ThrowIfNullOrEmpty( operand );
		ArgumentException.ThrowIfNullOrEmpty( displayName );
		ArgumentNullException.ThrowIfNull( label );
		ArgumentNullException.ThrowIfNull( document );
		this.Operand = operand;
		this.DisplayName = displayName;
		this.Label = label;
		this.Document = document;
	}

	/// <summary>Gets the operational operand.</summary>
	public string Operand { get; }
	/// <summary>Gets the diagnostic display name.</summary>
	public string DisplayName { get; }
	/// <summary>Gets the conflict-marker label.</summary>
	public string Label { get; }
	/// <summary>Gets the materialized comparison document.</summary>
	public ComparisonDocument Document { get; }
}
