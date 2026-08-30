namespace Icod.CoreUtils.Tty.Tests;

using Icod.Terminal;
using Xunit;

/// <summary>Tests the <c>tty</c> command boundary.</summary>
public sealed class CommandTests {
	/// <summary>Verifies terminal pathname output and success.</summary>
	[Fact]
	public async Task ReportsTerminalPathname() {
		var provider = new FakeTerminalProvider {
			Observation = TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					"/dev/pts/7",
					TerminalPlatformKind.PosixTermios,
					TerminalControlCapabilities.Attachment | TerminalControlCapabilities.Pathname
				)
			)
		};
		var output = new StringWriter();
		var exitCode = await Command.RunAsync( Array.Empty<string>(), output, new StringWriter(), provider );
		Assert.Equal( 0, exitCode );
		Assert.Equal( string.Concat( "/dev/pts/7", Environment.NewLine ), output.ToString() );
	}

	/// <summary>Verifies nonterminal output and status.</summary>
	[Fact]
	public async Task ReportsNonTerminalInput() {
		var output = new StringWriter();
		var exitCode = await Command.RunAsync(
			Array.Empty<string>(),
			output,
			new StringWriter(),
			new FakeTerminalProvider()
		);
		Assert.Equal( 1, exitCode );
		Assert.Equal( string.Concat( "not a tty", Environment.NewLine ), output.ToString() );
	}

	/// <summary>Verifies silent mode suppresses terminal and nonterminal output.</summary>
	[Theory]
	[InlineData( "-s" )]
	[InlineData( "--silent" )]
	[InlineData( "--quiet" )]
	public async Task SilentAliasesSuppressOutput( string option ) {
		var output = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { option },
			output,
			new StringWriter(),
			new FakeTerminalProvider()
		);
		Assert.Equal( 1, exitCode );
		Assert.Equal( string.Empty, output.ToString() );
	}

	/// <summary>Verifies invalid usage uses GNU's usage status.</summary>
	[Fact]
	public async Task InvalidUsageReturnsTwo() {
		var exitCode = await Command.RunAsync(
			new[] { "operand" },
			new StringWriter(),
			new StringWriter(),
			new FakeTerminalProvider()
		);
		Assert.Equal( 2, exitCode );
	}

	/// <summary>Verifies an attached terminal without a name is indeterminate.</summary>
	[Fact]
	public async Task MissingTerminalNameReturnsFour() {
		var provider = new FakeTerminalProvider {
			Observation = TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.PosixTermios,
					TerminalControlCapabilities.Attachment
				)
			)
		};
		var exitCode = await Command.RunAsync(
			Array.Empty<string>(),
			new StringWriter(),
			new StringWriter(),
			provider
		);
		Assert.Equal( 4, exitCode );
	}
}
