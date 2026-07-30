namespace Icod.CoreUtils.Shuf;

using System.Numerics;

/// <summary>Identifies the source from which <c>shuf</c> obtains input records.</summary>
internal enum ShufInputMode {
	/// <summary>Reads records from a file or standard input.</summary>
	Standard,
	/// <summary>Treats command-line operands as input records.</summary>
	Echo,
	/// <summary>Generates records from an inclusive unsigned-integer range.</summary>
	Range
}

/// <summary>Contains validated command-line options for one <c>shuf</c> invocation.</summary>
internal sealed class ShufOptions {
	/// <summary>Initializes validated <c>shuf</c> options.</summary>
	/// <param name="inputMode">The selected input mode.</param>
	/// <param name="operands">The remaining command-line operands.</param>
	/// <param name="rangeLow">The inclusive lower range endpoint.</param>
	/// <param name="rangeHigh">The inclusive upper range endpoint.</param>
	/// <param name="headCount">The requested output count, or <see langword="null"/> when unlimited.</param>
	/// <param name="outputPath">The output path, or <see langword="null"/> for standard output.</param>
	/// <param name="randomSourcePath">The random-source path, or <see langword="null"/> for cryptographic randomness.</param>
	/// <param name="repeat">Whether records may be selected repeatedly.</param>
	/// <param name="separator">The output record separator.</param>
	internal ShufOptions(
		ShufInputMode inputMode,
		IReadOnlyList<string> operands,
		ulong rangeLow,
		ulong rangeHigh,
		BigInteger? headCount,
		string? outputPath,
		string? randomSourcePath,
		bool repeat,
		byte separator
	) {
		this.InputMode = inputMode;
		this.Operands = operands;
		this.RangeLow = rangeLow;
		this.RangeHigh = rangeHigh;
		this.HeadCount = headCount;
		this.OutputPath = outputPath;
		this.RandomSourcePath = randomSourcePath;
		this.Repeat = repeat;
		this.Separator = separator;
	}

	/// <summary>Gets the requested number of output records, or <see langword="null"/> when unlimited.</summary>
	internal BigInteger? HeadCount { get; }

	/// <summary>Gets the selected input mode.</summary>
	internal ShufInputMode InputMode { get; }

	/// <summary>Gets the remaining command-line operands.</summary>
	internal IReadOnlyList<string> Operands { get; }

	/// <summary>Gets the output path, or <see langword="null"/> for standard output.</summary>
	internal string? OutputPath { get; }

	/// <summary>Gets the random-source path, or <see langword="null"/> for cryptographic randomness.</summary>
	internal string? RandomSourcePath { get; }

	/// <summary>Gets the inclusive upper range endpoint.</summary>
	internal ulong RangeHigh { get; }

	/// <summary>Gets the inclusive lower range endpoint.</summary>
	internal ulong RangeLow { get; }

	/// <summary>Gets whether records may be selected repeatedly.</summary>
	internal bool Repeat { get; }

	/// <summary>Gets the output record separator.</summary>
	internal byte Separator { get; }
}
