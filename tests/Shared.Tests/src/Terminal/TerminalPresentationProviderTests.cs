namespace Icod.CoreUtils.Shared.Tests.Terminal;

using Icod.CoreUtils.Shared.Terminal;
using Xunit;

/// <summary>
/// Verifies injectable terminal observation, environment capture, geometry
/// precedence, and controlled fallback behavior.
/// </summary>
public sealed class TerminalPresentationProviderTests {
	/// <summary>
	/// Verifies that environment dimensions override terminal dimensions.
	/// </summary>
	[Fact]
	public void EnvironmentDimensionsOverrideTerminalDimensions() {
		var provider = CreateProvider(
			TerminalDeviceObservation.Attached(
				new TerminalDimensions( 120, 40 )
			),
			new Dictionary<string, string?> {
				[ "COLUMNS" ] = "92",
				[ "LINES" ] = "31",
				[ "TERM" ] = "xterm-256color"
			}
		);

		var observation = provider.Observe(
			TerminalStreamKind.StandardOutput
		);

		Assert.True( observation.IsTerminal );
		Assert.Equal( 92, observation.Width );
		Assert.Equal( 31, observation.Height );
		Assert.Equal(
			TerminalDimensionSource.Environment,
			observation.WidthSource
		);
		Assert.Equal(
			TerminalDimensionSource.Environment,
			observation.HeightSource
		);
	}

	/// <summary>
	/// Verifies that terminal dimensions are used when environment values are
	/// absent or malformed.
	/// </summary>
	[Fact]
	public void TerminalDimensionsFollowInvalidEnvironmentValues() {
		var provider = CreateProvider(
			TerminalDeviceObservation.Attached(
				new TerminalDimensions( 101, 37 )
			),
			new Dictionary<string, string?> {
				[ "COLUMNS" ] = "zero",
				[ "LINES" ] = "-4"
			}
		);

		var observation = provider.Observe(
			TerminalStreamKind.StandardOutput
		);

		Assert.Equal( 101, observation.Width );
		Assert.Equal( 37, observation.Height );
		Assert.Equal(
			TerminalDimensionSource.Terminal,
			observation.WidthSource
		);
		Assert.Equal(
			TerminalDimensionSource.Terminal,
			observation.HeightSource
		);
	}

	/// <summary>
	/// Verifies deterministic fallback for redirected streams without usable
	/// environment geometry.
	/// </summary>
	[Fact]
	public void RedirectedStreamUsesConfiguredFallback() {
		var provider = new TerminalPresentationProvider(
			new FakeTerminalDeviceProvider(
				TerminalDeviceObservation.Redirected()
			),
			new FakeEnvironmentVariableProvider(
				new Dictionary<string, string?>()
			),
			new TerminalPresentationOptions {
				FallbackWidth = 77,
				FallbackHeight = 19
			}
		);

		var observation = provider.Observe(
			TerminalStreamKind.StandardOutput
		);

		Assert.False( observation.IsTerminal );
		Assert.Equal(
			TerminalProbeStatus.Redirected,
			observation.Device.Status
		);
		Assert.Equal( 77, observation.Width );
		Assert.Equal( 19, observation.Height );
		Assert.Equal(
			TerminalDimensionSource.Fallback,
			observation.WidthSource
		);
	}

	/// <summary>
	/// Verifies controlled fallback when the host cannot expose terminal data.
	/// </summary>
	[Fact]
	public void UnavailableTerminalUsesFallbackWithoutThrowing() {
		var provider = CreateProvider(
			TerminalDeviceObservation.Unavailable( "not supported" ),
			new Dictionary<string, string?>()
		);

		var observation = provider.Observe(
			TerminalStreamKind.StandardError
		);

		Assert.Equal(
			TerminalProbeStatus.Unavailable,
			observation.Device.Status
		);
		Assert.Equal( "not supported", observation.Device.Message );
		Assert.Equal( 80, observation.Width );
		Assert.Equal( 24, observation.Height );
	}

	/// <summary>
	/// Verifies capture of the terminal name and shell inputs consumed by
	/// <c>dircolors</c>, while retaining <c>COLORTERM</c> separately for color
	/// capability inference.
	/// </summary>
	[Fact]
	public void CapturesTerminalNameAndShellInputs() {
		var provider = CreateProvider(
			TerminalDeviceObservation.Redirected(),
			new Dictionary<string, string?> {
				[ "TERM" ] = "xterm-256color",
				[ "COLORTERM" ] = "truecolor",
				[ "SHELL" ] = "/bin/bash",
				[ "QUOTING_STYLE" ] = "escape"
			}
		);

		var observation = provider.Observe(
			TerminalStreamKind.StandardOutput
		);

		Assert.Equal(
			new[] { "xterm-256color" },
			observation.Environment.TerminalNames
		);
		Assert.Equal(
			"truecolor",
			observation.Environment.ColorTerm
		);
		Assert.Equal( "/bin/bash", observation.Environment.Shell );
		Assert.Equal( "escape", observation.Environment.QuotingStyle );
	}

	/// <summary>
	/// Verifies that unavailable and failed factories retain a nonempty
	/// controlled explanation even when the host exception had no message.
	/// </summary>
	[Fact]
	public void ProbeFactoriesSupplyControlledFallbackMessages() {
		Assert.False( string.IsNullOrWhiteSpace(
			TerminalDeviceObservation.Unavailable( null ).Message
		) );
		Assert.False( string.IsNullOrWhiteSpace(
			TerminalDeviceObservation.Failed( " " ).Message
		) );
	}

	/// <summary>
	/// Verifies construction-time validation of deterministic fallback values.
	/// </summary>
	[Fact]
	public void RejectsNonpositiveFallbackDimensions() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalPresentationProvider(
				new FakeTerminalDeviceProvider(
					TerminalDeviceObservation.Redirected()
				),
				new FakeEnvironmentVariableProvider(
					new Dictionary<string, string?>()
				),
				new TerminalPresentationOptions {
					FallbackWidth = 0
				}
			)
		);
	}

	/// <summary>
	/// Verifies that undefined stream identifiers are rejected before an
	/// injectable provider is invoked.
	/// </summary>
	[Fact]
	public void RejectsUndefinedStreamIdentifiers() {
		var provider = CreateProvider(
			TerminalDeviceObservation.Redirected(),
			new Dictionary<string, string?>()
		);

		Assert.Throws<ArgumentOutOfRangeException>(
			() => provider.Observe( (TerminalStreamKind)int.MaxValue )
		);
	}

	/// <summary>
	/// Verifies that the system provider converts the current runner state into
	/// a controlled observation.
	/// </summary>
	[Fact]
	public void SystemProviderAlwaysReturnsControlledObservation() {
		var observation = SystemTerminalDeviceProvider.Instance.Observe(
			TerminalStreamKind.StandardOutput
		);

		Assert.Contains(
			observation.Status,
			new[] {
				TerminalProbeStatus.Terminal,
				TerminalProbeStatus.Redirected,
				TerminalProbeStatus.Unavailable,
				TerminalProbeStatus.Failed
			}
		);
	}

	private static TerminalPresentationProvider CreateProvider(
		TerminalDeviceObservation observation,
		IReadOnlyDictionary<string, string?> environment
	) {
		return new TerminalPresentationProvider(
			new FakeTerminalDeviceProvider( observation ),
			new FakeEnvironmentVariableProvider( environment )
		);
	}

	private sealed class FakeTerminalDeviceProvider : ITerminalDeviceProvider {
		private readonly TerminalDeviceObservation observation;

		/// <summary>Initializes the fixed terminal-device provider.</summary>
		/// <param name="observation">The observation returned by the provider.</param>
		public FakeTerminalDeviceProvider(
			TerminalDeviceObservation observation
		) {
			this.observation = observation;
		}

		/// <inheritdoc/>
		public TerminalDeviceObservation Observe(
			TerminalStreamKind stream
		) {
			return this.observation;
		}
	}

	private sealed class FakeEnvironmentVariableProvider : IEnvironmentVariableProvider {
		private readonly IReadOnlyDictionary<string, string?> values;

		/// <summary>Initializes the dictionary-backed environment provider.</summary>
		/// <param name="values">The environment values.</param>
		public FakeEnvironmentVariableProvider(
			IReadOnlyDictionary<string, string?> values
		) {
			this.values = values;
		}

		/// <inheritdoc/>
		public string? GetValue(
			string name
		) {
			return this.values.TryGetValue( name, out var value )
				? value
				: null;
		}
	}
}
