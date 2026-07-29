namespace Icod.CoreUtils.Fold;

/// <summary>Contains validated options for one <c>fold</c> invocation.</summary>
internal sealed class FoldOptions {
	/// <summary>Initializes a validated option set.</summary>
	/// <param name="countingMode">The active counting mode.</param>
	/// <param name="breakAtBlanks">Whether folds prefer locale blank boundaries.</param>
	/// <param name="width">The positive maximum width.</param>
	/// <param name="operands">The input operands.</param>
	internal FoldOptions(
		FoldCountingMode countingMode,
		bool breakAtBlanks,
		ulong width,
		IReadOnlyList<string> operands
	) {
		this.CountingMode = countingMode;
		this.BreakAtBlanks = breakAtBlanks;
		this.Width = width;
		this.Operands = operands ?? throw new ArgumentNullException( nameof( operands ) );
	}

	/// <summary>Gets whether folding prefers the last eligible blank.</summary>
	internal bool BreakAtBlanks { get; }

	/// <summary>Gets the active counting mode.</summary>
	internal FoldCountingMode CountingMode { get; }

	/// <summary>Gets the input operands in encounter order.</summary>
	internal IReadOnlyList<string> Operands { get; }

	/// <summary>Gets the positive maximum line width.</summary>
	internal ulong Width { get; }
}
