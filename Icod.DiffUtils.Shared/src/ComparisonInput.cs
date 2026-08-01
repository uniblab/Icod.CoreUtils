namespace Icod.DiffUtils.Shared;

using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

/// <summary>
/// Represents a Diffutils input while delegating general operand and stream
/// mechanics to the current shared command-framework incubation project.
/// </summary>
/// <param name="Operand">The underlying general-purpose input operand.</param>
public readonly record struct ComparisonInput( InputOperand Operand ) {

	/// <summary>Gets the original command-line value.</summary>
	public string Value => this.Operand.Value;

	/// <summary>Gets the user-facing name used by comparison diagnostics.</summary>
	public string DisplayName => this.Operand.DisplayName;

	/// <summary>Gets whether the input denotes standard input.</summary>
	public bool IsStandardInput => this.Operand.IsStandardInput;

	/// <summary>Creates an input and normalizes an empty value to standard input.</summary>
	public static ComparisonInput Create( string? value ) {
		return new ComparisonInput( InputOperand.Create( value ) );
	}

	/// <summary>Opens the input for bounded asynchronous byte access.</summary>
	public InputSource OpenBinary(
		CommandContext context,
		int bufferSize = StreamOperations.DefaultBufferSize
	) {
		return InputSource.OpenBinary( this.Operand, context, bufferSize );
	}
}
