namespace Icod.CoreUtils.Shared.Tests.Terminal;

using Icod.CoreUtils.Shared.Terminal;
using Xunit;

/// <summary>
/// Verifies terminal color-capability inference and mode policy.
/// </summary>
public sealed class TerminalColorPolicyTests {
	/// <summary>
	/// Verifies true-color inference from <c>COLORTERM</c>.
	/// </summary>
	[Fact]
	public void AutoUsesTrueColorOnAttachedTerminal() {
		var presentation = CreateSnapshot(
			true,
			"xterm-256color",
			"truecolor"
		);

		var decision = TerminalColorPolicy.Resolve(
			TerminalColorMode.Auto,
			presentation
		);

		Assert.True( decision.UseColor );
		Assert.Equal(
			TerminalColorCapability.TrueColor,
			decision.Capability
		);
	}

	/// <summary>
	/// Verifies that automatic color is disabled for redirected output.
	/// </summary>
	[Fact]
	public void AutoDisablesColorForRedirectedOutput() {
		var presentation = CreateSnapshot(
			false,
			"xterm-256color",
			null
		);

		var decision = TerminalColorPolicy.Resolve(
			TerminalColorMode.Auto,
			presentation
		);

		Assert.False( decision.UseColor );
		Assert.Equal(
			TerminalColorCapability.Ansi256,
			decision.Capability
		);
	}

	/// <summary>
	/// Verifies that a dumb terminal disables automatic color.
	/// </summary>
	[Fact]
	public void DumbTerminalDisablesAutomaticColor() {
		var presentation = CreateSnapshot(
			true,
			"dumb",
			null
		);

		var decision = TerminalColorPolicy.Resolve(
			TerminalColorMode.Auto,
			presentation
		);

		Assert.False( decision.UseColor );
		Assert.Equal(
			TerminalColorCapability.None,
			decision.Capability
		);
	}

	/// <summary>
	/// Verifies that always mode emits a conservative ANSI capability even when
	/// output is redirected and no terminal name exists.
	/// </summary>
	[Fact]
	public void AlwaysUsesConservativeAnsiFallback() {
		var presentation = CreateSnapshot( false, null, null );

		var decision = TerminalColorPolicy.Resolve(
			TerminalColorMode.Always,
			presentation
		);

		Assert.True( decision.UseColor );
		Assert.Equal(
			TerminalColorCapability.Ansi16,
			decision.Capability
		);
	}

	/// <summary>
	/// Verifies that never mode suppresses output while retaining the inferred
	/// capability for diagnostics and planning.
	/// </summary>
	[Fact]
	public void NeverSuppressesKnownCapability() {
		var presentation = CreateSnapshot(
			true,
			"screen-256color",
			null
		);

		var decision = TerminalColorPolicy.Resolve(
			TerminalColorMode.Never,
			presentation
		);

		Assert.False( decision.UseColor );
		Assert.Equal(
			TerminalColorCapability.Ansi256,
			decision.Capability
		);
	}

	/// <summary>
	/// Verifies early rejection of undefined color-policy values.
	/// </summary>
	[Fact]
	public void RejectsUndefinedColorPolicyValues() {
		var presentation = CreateSnapshot( true, "xterm", null );

		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalColorPolicy.Resolve(
				(TerminalColorMode)int.MaxValue,
				presentation
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalColorDecision(
				true,
				(TerminalColorCapability)int.MaxValue
			)
		);
	}

	private static TerminalPresentationSnapshot CreateSnapshot(
		bool terminal,
		string? term,
		string? colorTerm
	) {
		var provider = new TerminalPresentationProvider(
			new FakeTerminalDeviceProvider(
				terminal
					? TerminalDeviceObservation.Attached(
						new TerminalDimensions( 80, 24 )
					)
					: TerminalDeviceObservation.Redirected()
			),
			new FakeEnvironmentVariableProvider(
				new Dictionary<string, string?> {
					[ "TERM" ] = term,
					[ "COLORTERM" ] = colorTerm
				}
			)
		);
		return provider.Observe( TerminalStreamKind.StandardOutput );
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
