namespace Icod.CoreUtils.Shared.CommandLine;

/// <summary>
/// Controls how option parsing treats operands that appear before later options.
/// </summary>
public enum OptionOrdering {
	/// <summary>Stop parsing options at the first operand.</summary>
	RequireOrder,
	/// <summary>Continue recognizing options after operands.</summary>
	Permute,
	/// <summary>Continue recognizing options after operands and preserve all items in encounter order.</summary>
	ReturnInOrder
}
