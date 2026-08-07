namespace Icod.CoreUtils.Shared.Tests.Terminal;

using Icod.CoreUtils.Shared.Terminal;

using Xunit;

/// <summary>
/// Verifies the policy-neutral terminal-control contracts used by future
/// command projects and injectable test providers.
/// </summary>
public sealed class TerminalControlContractsTests {
	/// <summary>Verifies endpoint construction and standard descriptor identities.</summary>
	[Fact]
	public void CreatesDescriptorAndPathEndpoints() {
		Assert.Equal( 0, TerminalEndpoint.StandardInput.FileDescriptor );
		Assert.Equal( 1, TerminalEndpoint.StandardOutput.FileDescriptor );
		Assert.Equal( 2, TerminalEndpoint.StandardError.FileDescriptor );
		var named = TerminalEndpoint.ForPath( "/dev/tty" );
		Assert.Equal( TerminalEndpointKind.Path, named.Kind );
		Assert.Equal( "/dev/tty", named.Path );
		Assert.Equal( "/dev/tty", named.DisplayName );
	}

	/// <summary>Verifies that invalid endpoint values are rejected immediately.</summary>
	[Fact]
	public void RejectsInvalidEndpoints() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalEndpoint.ForFileDescriptor( -1 )
		);
		Assert.Throws<ArgumentException>(
			() => TerminalEndpoint.ForPath( " " )
		);
	}

	/// <summary>
	/// Verifies that attachment observations cannot fabricate platforms or
	/// capabilities for a nonterminal endpoint.
	/// </summary>
	[Fact]
	public void EnforcesObservationInvariants() {
		Assert.Throws<ArgumentException>(
			() => new TerminalEndpointObservation(
				false,
				null,
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.None
			)
		);
		Assert.Throws<ArgumentException>(
			() => new TerminalEndpointObservation(
				true,
				"/dev/tty",
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.ModeRead
			)
		);
		Assert.Throws<ArgumentException>(
			() => new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.Attachment | TerminalControlCapabilities.Pathname
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.Attachment | (TerminalControlCapabilities)( 1 << 20 )
			)
		);
	}

	/// <summary>
	/// Verifies that controlled results retain distinct unavailable,
	/// unsupported, and failed states.
	/// </summary>
	[Fact]
	public void PreservesControlledResultStates() {
		var available = TerminalControlResult<string>.Available( "value" );
		Assert.True( available.IsAvailable );
		Assert.Equal( "value", available.GetRequiredValue() );

		var unavailable = TerminalControlResult<string>.Unavailable( null, 25 );
		Assert.Equal( TerminalControlStatus.Unavailable, unavailable.Status );
		Assert.Equal( 25, unavailable.NativeErrorCode );
		Assert.False( string.IsNullOrWhiteSpace( unavailable.Message ) );
		Assert.Throws<InvalidOperationException>( unavailable.GetRequiredValue );

		Assert.Equal(
			TerminalControlStatus.Unsupported,
			TerminalControlMutationResult.Unsupported( null ).Status
		);
		Assert.Equal(
			TerminalControlStatus.Failed,
			TerminalControlMutationResult.Failed( "failed" ).Status
		);
	}

	/// <summary>Verifies that Windows snapshots do not expose fabricated POSIX fields.</summary>
	[Fact]
	public void KeepsWindowsSnapshotPlatformFieldsExplicit() {
		var mode = TerminalModeSnapshot.CreateWindowsConsole(
			TerminalConsoleDirection.Input,
			0x1234
		);
		Assert.Equal( 0, mode.NativeFlagWidth );
		Assert.Empty( mode.ControlCharacters );
		Assert.Null( mode.LineDiscipline );
		Assert.Null( mode.InputSpeed );
		Assert.Null( mode.OutputSpeed );
	}

	/// <summary>Verifies that undefined mutation timing values are rejected.</summary>
	[Fact]
	public void RejectsUndefinedMutationTiming() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => SystemTerminalControlProvider.Instance.SetMode(
				TerminalEndpoint.StandardInput,
				TerminalModeSnapshot.CreateWindowsConsole(
					TerminalConsoleDirection.Input,
					0
				),
				(TerminalModeApplyTiming)int.MaxValue
			)
		);
	}

	/// <summary>
	/// Verifies that command tests can inject a provider without reaching
	/// process-global handles or native APIs.
	/// </summary>
	[Fact]
	public void SupportsDeterministicProviderInjection() {
		var expected = new TerminalEndpointObservation(
			true,
			"test-terminal",
			TerminalPlatformKind.PosixTermios,
			TerminalControlCapabilities.Attachment | TerminalControlCapabilities.Pathname
		);
		ITerminalControlProvider provider = new FakeTerminalControlProvider( expected );
		Assert.Same(
			expected,
			provider.Observe( TerminalEndpoint.StandardInput ).GetRequiredValue()
		);
	}

	private sealed class FakeTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalEndpointObservation observation;

		/// <summary>Initializes a provider with one fixed observation.</summary>
		/// <param name="observation">The observation to return.</param>
		public FakeTerminalControlProvider(
			TerminalEndpointObservation observation
		) {
			this.observation = observation;
		}

		/// <inheritdoc />
		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				this.observation
			);
		}

		/// <inheritdoc />
		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			return TerminalControlResult<TerminalModeSnapshot>.Unavailable(
				"No mode was configured."
			);
		}

		/// <inheritdoc />
		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			return TerminalControlMutationResult.Unavailable(
				"No mutation was configured."
			);
		}
	}
}
