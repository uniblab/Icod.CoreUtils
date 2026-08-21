namespace Icod.CoreUtils.Stty.Tests;

using Icod.CommandFramework.Terminal;
using Xunit;

/// <summary>Tests the <c>stty</c> command boundary.</summary>
public sealed class CommandTests {
	/// <summary>Verifies machine-readable reporting uses the shared codec.</summary>
	[Fact]
	public async Task SavePrintsSerializedMode() {
		var provider = new FakeTerminalProvider();
		var output = new StringWriter();
		var exitCode = await Command.RunAsync( new[] { "-g" }, output, new StringWriter(), provider );
		Assert.Equal( 0, exitCode );
		Assert.Equal(
			string.Concat( TerminalModeCodec.Serialize( provider.ModeResult.GetRequiredValue() ), Environment.NewLine ),
			output.ToString()
		);
	}

	/// <summary>Verifies selected-device paths reach the provider.</summary>
	[Fact]
	public async Task FileOptionSelectsNamedEndpoint() {
		var provider = new FakeTerminalProvider();
		var exitCode = await Command.RunAsync(
			new[] { "--file=/dev/ttyS0", "echo" },
			new StringWriter(),
			new StringWriter(),
			provider
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( TerminalEndpointKind.Path, provider.LastEndpoint!.Kind );
		Assert.Equal( "/dev/ttyS0", provider.LastEndpoint.Path );
	}

	/// <summary>Verifies POSIX mutations use output-drain timing by default.</summary>
	[Fact]
	public async Task PosixMutationUsesDrainTiming() {
		var provider = new FakeTerminalProvider();
		var exitCode = await Command.RunAsync(
			new[] { "-echo" },
			new StringWriter(),
			new StringWriter(),
			provider
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( TerminalModeApplyTiming.AfterOutputDrained, provider.LastTiming );
	}

	/// <summary>Verifies incompatible output modes are rejected during parsing.</summary>
	[Fact]
	public void AllAndSaveConflict() {
		Assert.Throws<SttyUsageException>( () => SttyOptions.Parse( new[] { "-a", "-g" } ) );
	}

	/// <summary>Verifies unavailable terminal state produces a controlled failure.</summary>
	[Fact]
	public async Task UnavailableModeFailsCleanly() {
		var provider = new FakeTerminalProvider {
			ModeResult = TerminalControlResult<TerminalModeSnapshot>.Unavailable( "not a terminal" )
		};
		var error = new StringWriter();
		var exitCode = await Command.RunAsync( Array.Empty<string>(), new StringWriter(), error, provider );
		Assert.Equal( 1, exitCode );
		Assert.Contains( "not a terminal", error.ToString(), StringComparison.Ordinal );
	}
}
