namespace Icod.CoreUtils.Tty.Tests;

using Icod.Terminal;

/// <summary>Provides deterministic terminal observations for command tests.</summary>
public sealed class FakeTerminalProvider : ITerminalControlProvider {
	/// <summary>Gets or sets the observation returned by <see cref="Observe"/>.</summary>
	public TerminalControlResult<TerminalEndpointObservation> Observation { get; set; } =
		TerminalControlResult<TerminalEndpointObservation>.Available(
			new TerminalEndpointObservation( false, null, null, TerminalControlCapabilities.None )
		);

	/// <inheritdoc />
	public TerminalControlResult<TerminalEndpointObservation> Observe( TerminalEndpoint endpoint ) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return this.Observation;
	}

	/// <inheritdoc />
	public TerminalControlResult<Icod.TermInfo.TerminalSize> GetSize(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<Icod.TermInfo.TerminalSize>.Unsupported( "not used" );
	}

	/// <inheritdoc />
	public TerminalControlResult<TerminalModeSnapshot> GetMode( TerminalEndpoint endpoint ) =>
		TerminalControlResult<TerminalModeSnapshot>.Unsupported( "not used" );

	/// <inheritdoc />
	public TerminalControlMutationResult SetMode(
		TerminalEndpoint endpoint,
		TerminalModeSnapshot mode,
		TerminalModeApplyTiming timing
	) => TerminalControlMutationResult.Unsupported( "not used" );
}
