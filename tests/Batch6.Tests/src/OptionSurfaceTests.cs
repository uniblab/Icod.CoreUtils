namespace Icod.CoreUtils.Batch6.Tests;

using EchoCommand = Icod.CoreUtils.Echo.Command;
using FactorCommand = Icod.CoreUtils.Factor.Command;
using SeqCommand = Icod.CoreUtils.Seq.Command;
using SleepCommand = Icod.CoreUtils.Sleep.Command;
using YesCommand = Icod.CoreUtils.Yes.Command;
using Xunit;

public sealed class OptionSurfaceTests {
	[Theory]
	[InlineData( "echo" )]
	[InlineData( "yes" )]
	[InlineData( "sleep" )]
	[InlineData( "seq" )]
	[InlineData( "factor" )]
	public async Task HelpAndVersionOptionsAreImplemented(
		string commandName
	) {
		var command = GetCommand( commandName );
		var help = await TestSupport.RunAsync(
			command,
			new string[] { "--help" }
		);
		var version = await TestSupport.RunAsync(
			command,
			new string[] { "--version" }
		);
		Assert.Equal( 0, help.ExitCode );
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains( $"Usage: {commandName}", help.Output );
		Assert.Contains( "Icod.CoreUtils", version.Output );
	}

	private static CommandRunner GetCommand(
		string commandName
	) {
		return commandName switch {
			"echo" => EchoCommand.RunAsync,
			"yes" => YesCommand.RunAsync,
			"sleep" => SleepCommand.RunAsync,
			"seq" => SeqCommand.RunAsync,
			"factor" => FactorCommand.RunAsync,
			_ => throw new ArgumentOutOfRangeException(
				nameof( commandName )
			)
		};
	}
}
