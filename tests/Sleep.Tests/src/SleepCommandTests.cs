namespace Icod.CoreUtils.Sleep.Tests;

using System.Globalization;
using SleepCommand = Icod.CoreUtils.Sleep.Command;
using Xunit;

public sealed class SleepCommandTests {
	[Fact]
	public async Task AcceptsAllSuffixesAndMultipleOperands() {
		var result = await RunAsync(
			new string[] { "0s", "0m", "0h", "0d" }
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Empty( result.Error );
	}

	[Fact]
	public async Task RejectsMissingAndInvalidDurations() {
		var missing = await RunAsync(
			Array.Empty<string>()
		);
		var invalid = await RunAsync(
			new string[] { "1fortnight" }
		);
		Assert.Equal( 1, missing.ExitCode );
		Assert.Contains( "missing operand", missing.Error );
		Assert.Equal( 1, invalid.ExitCode );
		Assert.Contains( "invalid time interval", invalid.Error );
	}

	[Fact]
	public async Task IsCancellableWithoutBlockingAWorkerThread() {
		using var cancellation = new CancellationTokenSource();
		cancellation.CancelAfter( TimeSpan.FromMilliseconds( 25 ) );
		var result = await RunAsync(
			new string[] { "inf" },
			cancellation.Token
		);
		Assert.Equal( 130, result.ExitCode );
	}

	[Fact]
	public async Task UsesInvariantNumericSyntax() {
		using var culture = new CultureScope( "fr-FR" );
		var result = await RunAsync(
			new string[] { "0.0" }
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Empty( result.Error );
	}

	[Fact]
	public async Task HelpAndVersionAreHandled() {
		var help = await RunAsync(
			new string[] { "--help" }
		);
		var version = await RunAsync(
			new string[] { "--version" }
		);
		Assert.Equal( 0, help.ExitCode );
		Assert.Contains( "Usage: sleep", help.Output );
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains( "Icod.CoreUtils", version.Output );
	}

	private static async Task<CommandResult> RunAsync(
		string[] args,
		CancellationToken cancellationToken = default
	) {
		using var output = new StringWriter { NewLine = "\n" };
		using var error = new StringWriter { NewLine = "\n" };
		var exitCode = await SleepCommand.RunAsync(
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

	private sealed class CultureScope : IDisposable {
		private readonly CultureInfo myCulture;
		private readonly CultureInfo myUiCulture;

		public CultureScope(
			string cultureName
		) {
			this.myCulture = CultureInfo.CurrentCulture;
			this.myUiCulture = CultureInfo.CurrentUICulture;
			CultureInfo.CurrentCulture = new CultureInfo( cultureName );
			CultureInfo.CurrentUICulture = new CultureInfo( cultureName );
		}

		public void Dispose() {
			CultureInfo.CurrentCulture = this.myCulture;
			CultureInfo.CurrentUICulture = this.myUiCulture;
		}
	}
}
