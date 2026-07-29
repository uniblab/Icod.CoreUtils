namespace Icod.CoreUtils.Shared.IO;

/// <summary>
/// Represents a file operand, including the conventional standard-input marker.
/// </summary>
/// <param name="Value">The value value.</param>
public readonly record struct InputOperand(
	string Value
) {
	/// <summary>Gets whether the operand denotes standard input.</summary>
	public bool IsStandardInput {
		get {
			return "-" == this.Value;
		}
	}

	/// <summary>Gets a user-facing source name.</summary>
	public string DisplayName {
		get {
			return this.IsStandardInput
				? "standard input"
				: this.Value
			;
		}
	}

	/// <summary>Creates an operand and normalizes an empty value to standard input.</summary>
	public static InputOperand Create(
		string? value
	) {
		return new InputOperand(
			string.IsNullOrEmpty( value )
				? "-"
				: value
		);
	}
}
