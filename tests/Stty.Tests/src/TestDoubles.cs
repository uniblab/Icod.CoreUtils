namespace Icod.CoreUtils.Stty.Tests;

using Icod.Terminal;

/// <summary>Provides deterministic mode reads and mutations for <c>stty</c> tests.</summary>
public sealed class FakeTerminalProvider : ITerminalControlProvider {
	/// <summary>Gets or sets the mode result returned by <see cref="GetMode"/>.</summary>
	public TerminalControlResult<TerminalModeSnapshot> ModeResult { get; set; } =
		TerminalControlResult<TerminalModeSnapshot>.Available( CreateLinuxMode() );

	/// <summary>Gets or sets the mutation result.</summary>
	public TerminalControlMutationResult MutationResult { get; set; } = TerminalControlMutationResult.Success();

	/// <summary>Gets the last endpoint used by a provider operation.</summary>
	public TerminalEndpoint? LastEndpoint { get; private set; }

	/// <summary>Gets the last mode supplied to a mutation.</summary>
	public TerminalModeSnapshot? LastMode { get; private set; }

	/// <summary>Gets the last mutation timing.</summary>
	public TerminalModeApplyTiming? LastTiming { get; private set; }

	/// <inheritdoc />
	public TerminalControlResult<TerminalEndpointObservation> Observe( TerminalEndpoint endpoint ) =>
		TerminalControlResult<TerminalEndpointObservation>.Unsupported( "not used" );

	/// <inheritdoc />
	public TerminalControlResult<Icod.TermInfo.TerminalSize> GetSize(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<Icod.TermInfo.TerminalSize>.Unsupported( "not used" );
	}

	/// <inheritdoc />
	public TerminalControlResult<TerminalModeSnapshot> GetMode( TerminalEndpoint endpoint ) {
		this.LastEndpoint = endpoint;
		return this.ModeResult;
	}

	/// <inheritdoc />
	public TerminalControlMutationResult SetMode(
		TerminalEndpoint endpoint,
		TerminalModeSnapshot mode,
		TerminalModeApplyTiming timing
	) {
		this.LastEndpoint = endpoint;
		this.LastMode = mode;
		this.LastTiming = timing;
		return this.MutationResult;
	}

	/// <summary>Creates a deterministic Linux-shaped terminal mode.</summary>
	/// <returns>The terminal mode.</returns>
	public static TerminalModeSnapshot CreateLinuxMode() {
		return TerminalModeSnapshot.CreatePosix(
			0x500,
			0x5,
			0xbf,
			0x8a3b,
			Enumerable.Repeat( (byte)0, 32 ),
			0,
			32,
			0,
			new TerminalSpeed( 13, 9600 ),
			new TerminalSpeed( 13, 9600 )
		);
	}
}
