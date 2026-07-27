namespace Icod.CoreUtils.Echo.Tests;

using EchoCommand = Icod.CoreUtils.Echo.Command;
using Xunit;

public sealed class EchoCommandTests {
	[Fact]
	public async Task SupportsShortOptionClusters() {
		var result = await RunAsync(
			new string[] { "-nEne", "alpha\\nbeta" }
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( System.String.Concat( "alpha", Environment.NewLine, "beta" ), result.Output );
	}

	[Fact]
	public async Task ImplementsDocumentedEscapes() {
		var result = await RunAsync(
			new string[] {
				"-e",
				"\\a\\b\\e\\f\\n\\r\\t\\v\\0101\\x42\\\\"
			}
		);
		Assert.Equal(
			System.String.Concat(
				"\a\b\u001B\f",
				Environment.NewLine,
				"\r\t\vAB\\",
				Environment.NewLine
			),
			result.Output
		);
	}

	[Fact]
	public async Task CStopsFurtherOutputAndSuppressesNewline() {
		var result = await RunAsync(
			new string[] { "-e", "before\\cafter", "ignored" }
		);
		Assert.Equal( "before", result.Output );
	}

	[Fact]
	public async Task PreservesUnknownEscapesAndLiteralDoubleDash() {
		var unknown = await RunAsync(
			new string[] { "-e", "a\\qb" }
		);
		var doubleDash = await RunAsync(
			new string[] { "--", "-n" }
		);
		Assert.Equal( System.String.Concat( "a\\qb", Environment.NewLine ), unknown.Output );
		Assert.Equal( System.String.Concat( "-- -n", Environment.NewLine ), doubleDash.Output );
	}

	[Fact]
	public async Task HonorsPosixlyCorrectOptionRules() {
		using var environment = new EnvironmentVariableScope(
			"POSIXLY_CORRECT",
			"1"
		);
		var literalOption = await RunAsync(
			new string[] { "-e", "alpha\\nbeta" }
		);
		var leadingN = await RunAsync(
			new string[] { "-n", "-E", "alpha\\nbeta" }
		);
		var literalHelp = await RunAsync(
			new string[] { "--help" }
		);
		Assert.Equal( System.String.Concat( "-e alpha", Environment.NewLine, "beta", Environment.NewLine ), literalOption.Output );
		Assert.Equal( System.String.Concat( "alpha", Environment.NewLine, "beta" ), leadingN.Output );
		Assert.Equal( System.String.Concat( "--help", Environment.NewLine ), literalHelp.Output );
	}

	[Fact]
	public async Task HandlesLargeOutputWithoutChangingContents() {
		var payload = new string( 'x', 2 * 1024 * 1024 );
		var result = await RunAsync(
			new string[] { "-n", payload }
		);
		Assert.Equal( payload, result.Output );
	}

	[Fact]
	public async Task HelpVersionAndCancellationAreHandled() {
		var help = await RunAsync(
			new string[] { "--help" }
		);
		var version = await RunAsync(
			new string[] { "--version" }
		);
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var cancelled = await RunAsync(
			new string[] { "text" },
			cancellation.Token
		);
		Assert.Equal( 0, help.ExitCode );
		Assert.Contains( "Usage: echo", help.Output );
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains( "Icod.CoreUtils", version.Output );
		Assert.Equal( 130, cancelled.ExitCode );
	}

	private static async Task<CommandResult> RunAsync(
		string[] args,
		CancellationToken cancellationToken = default
	) {
		using var output = new StringWriter { NewLine = Environment.NewLine };
		using var error = new StringWriter { NewLine = Environment.NewLine };
		var exitCode = await EchoCommand.RunAsync(
			args,
			TextReader.Null,
			output,
			error,
			cancellationToken
		);
		return new CommandResult(
			exitCode,
			output.ToString(),
			error.ToString()
		);
	}

	private sealed record CommandResult(
		int ExitCode,
		string Output,
		string Error
	);

	private sealed class EnvironmentVariableScope : IDisposable {
		private readonly string myName;
		private readonly string? myValue;

		public EnvironmentVariableScope(
			string name,
			string? value
		) {
			this.myName = name;
			this.myValue = Environment.GetEnvironmentVariable( name );
			Environment.SetEnvironmentVariable( name, value );
		}

		public void Dispose() {
			Environment.SetEnvironmentVariable(
				this.myName,
				this.myValue
			);
		}
	}
}
