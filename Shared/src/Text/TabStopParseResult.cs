namespace Icod.CoreUtils.Shared.Text;

using Icod.CommandFramework.Text;

/// <summary>Represents the controlled result of parsing GNU-style tab stops.</summary>
public sealed class TabStopParseResult {
	private TabStopParseResult(
		TabStopSet? tabStops,
		TabStopParseError? error
	) {
		this.TabStops = tabStops;
		this.Error = error;
	}

	/// <summary>Gets the parse error when parsing failed.</summary>
	public TabStopParseError? Error {
		get;
	}

	/// <summary>Gets whether parsing succeeded.</summary>
	public bool IsSuccess => this.TabStops is not null;

	/// <summary>Gets the parsed tab-stop model when parsing succeeded.</summary>
	public TabStopSet? TabStops {
		get;
	}

	/// <summary>Creates a successful parse result.</summary>
	/// <param name="tabStops">The parsed tab-stop model.</param>
	/// <returns>The successful result.</returns>
	internal static TabStopParseResult Succeeded( TabStopSet tabStops ) {
		ArgumentNullException.ThrowIfNull( tabStops );
		return new( tabStops, null );
	}

	/// <summary>Creates a failed parse result.</summary>
	/// <param name="error">The deterministic parse error.</param>
	/// <returns>The failed result.</returns>
	internal static TabStopParseResult Failed( TabStopParseError error ) {
		ArgumentNullException.ThrowIfNull( error );
		return new( null, error );
	}
}
