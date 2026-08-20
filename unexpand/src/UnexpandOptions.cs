namespace Icod.CoreUtils.Unexpand;

using Icod.CommandFramework.Text;

/// <summary>Contains validated options for one <c>unexpand</c> invocation.</summary>
internal sealed class UnexpandOptions {
	/// <summary>Initializes a validated option set.</summary>
	/// <param name="convertAll">Whether blank runs throughout each line are converted.</param>
	/// <param name="tabStops">The configured tab stops.</param>
	/// <param name="operands">The input operands.</param>
	internal UnexpandOptions(
		bool convertAll,
		TabStopSet tabStops,
		IReadOnlyList<string> operands
	) {
		this.ConvertAll = convertAll;
		this.TabStops = tabStops ?? throw new ArgumentNullException( nameof( tabStops ) );
		this.Operands = operands ?? throw new ArgumentNullException( nameof( operands ) );
	}

	/// <summary>Gets whether conversion continues after the initial blank region.</summary>
	internal bool ConvertAll { get; }

	/// <summary>Gets the input operands in encounter order.</summary>
	internal IReadOnlyList<string> Operands { get; }

	/// <summary>Gets the configured tab-stop model.</summary>
	internal TabStopSet TabStops { get; }
}
