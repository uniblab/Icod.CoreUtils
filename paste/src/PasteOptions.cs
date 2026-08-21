namespace Icod.CoreUtils.Paste;

using Icod.CommandFramework.Delimiters;

/// <summary>Contains validated options for one <c>paste</c> execution.</summary>
internal sealed class PasteOptions {
	/// <summary>Initializes validated command options.</summary>
	internal PasteOptions(
		bool serial,
		byte recordSeparator,
		byte[] outputRecordSeparator,
		SeparatorCycle delimiters,
		IReadOnlyList<string> operands
	) {
		this.Serial = serial;
		this.RecordSeparator = recordSeparator;
		this.OutputRecordSeparator = outputRecordSeparator;
		this.Delimiters = delimiters;
		this.Operands = operands;
	}
	/// <summary>Gets whether each operand is pasted one at a time.</summary>
	internal bool Serial { get; }
	/// <summary>Gets the input and output record separator.</summary>
	internal byte RecordSeparator { get; }
	/// <summary>Gets the generated output record terminator.</summary>
	internal byte[] OutputRecordSeparator { get; }
	/// <summary>Gets the cyclic separator list.</summary>
	internal SeparatorCycle Delimiters { get; }
	/// <summary>Gets ordered file operands.</summary>
	internal IReadOnlyList<string> Operands { get; }
}
