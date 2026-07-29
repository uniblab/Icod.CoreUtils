namespace Icod.CoreUtils.Expand;

using Icod.CoreUtils.Shared.Text;

/// <summary>Contains validated options for one <c>expand</c> invocation.</summary>
internal sealed class ExpandOptions {
	/// <summary>Initializes a validated option set.</summary>
	/// <param name="initialOnly">Whether only initial tabs are expanded.</param>
	/// <param name="tabStops">The configured tab stops.</param>
	/// <param name="operands">The input operands.</param>
	internal ExpandOptions(
		bool initialOnly,
		TabStopSet tabStops,
		IReadOnlyList<string> operands
	) {
		this.InitialOnly = initialOnly;
		this.TabStops = tabStops ?? throw new ArgumentNullException( nameof( tabStops ) );
		this.Operands = operands ?? throw new ArgumentNullException( nameof( operands ) );
	}

	/// <summary>Gets whether conversion is limited to initial blank text.</summary>
	internal bool InitialOnly { get; }

	/// <summary>Gets the input operands in encounter order.</summary>
	internal IReadOnlyList<string> Operands { get; }

	/// <summary>Gets the configured tab-stop model.</summary>
	internal TabStopSet TabStops { get; }
}
