namespace Icod.CoreUtils.Yes.Tests;

using System.Text;
using YesCommand = Icod.CoreUtils.Yes.Command;
using Xunit;

public sealed class YesCommandTests {
	[Fact]
	public async Task DefaultsToYAndIsCancellableAfterLargeOutput() {
		using var cancellation = new CancellationTokenSource();
		var output = new CancellingTextWriter(
			cancellation,
			70_000
		);
		using var error = new StringWriter { NewLine = "\n" };
		var exitCode = await YesCommand.RunAsync(
			Array.Empty<string>(),
			TextReader.Null,
			output,
			error,
			cancellation.Token
		);
		Assert.Equal( 130, exitCode );
		Assert.StartsWith( "y\ny\ny\n", output.Output );
		Assert.True( output.Output.Length >= 70_000 );
	}

	[Fact]
	public async Task JoinsOperandsWithSpaces() {
		using var cancellation = new CancellationTokenSource();
		var output = new CancellingTextWriter(
			cancellation,
			80
		);
		using var error = new StringWriter { NewLine = "\n" };
		var exitCode = await YesCommand.RunAsync(
			new string[] { "alpha", "beta" },
			TextReader.Null,
			output,
			error,
			cancellation.Token
		);
		Assert.Equal( 130, exitCode );
		Assert.StartsWith( "alpha beta\nalpha beta\n", output.Output );
	}

	[Fact]
	public async Task BrokenPipeReturnsFailureAndDiagnostic() {
		using var output = new ThrowingTextWriter();
		using var error = new StringWriter { NewLine = "\n" };
		var exitCode = await YesCommand.RunAsync(
			Array.Empty<string>(),
			TextReader.Null,
			output,
			error,
			CancellationToken.None
		);
		Assert.Equal( 1, exitCode );
		Assert.Contains( "simulated broken pipe", error.ToString() );
	}

	[Fact]
	public async Task HelpAndVersionAreHandled() {
		var help = await RunFiniteAsync(
			new string[] { "--help" }
		);
		var version = await RunFiniteAsync(
			new string[] { "--version" }
		);
		Assert.Equal( 0, help.ExitCode );
		Assert.Contains( "Usage: yes", help.Output );
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains( "Icod.CoreUtils", version.Output );
	}

	private static async Task<CommandResult> RunFiniteAsync(
		string[] args
	) {
		using var output = new StringWriter { NewLine = "\n" };
		using var error = new StringWriter { NewLine = "\n" };
		var exitCode = await YesCommand.RunAsync(
			args,
			TextReader.Null,
			output,
			error,
			CancellationToken.None
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

	private sealed class CancellingTextWriter : TextWriter {
		private readonly CancellationTokenSource myCancellation;
		private readonly int myLimit;
		private readonly StringBuilder myOutput = new();

		public override Encoding Encoding => Encoding.UTF8;
		public string Output => this.myOutput.ToString();

		public CancellingTextWriter(
			CancellationTokenSource cancellation,
			int limit
		) {
			this.myCancellation = cancellation;
			this.myLimit = limit;
		}

		public override Task WriteAsync(
			char[] buffer,
			int index,
			int count
		) {
			this.AppendAndMaybeCancel(
				buffer.AsSpan( index, count )
			);
			return Task.CompletedTask;
		}

		public override Task WriteAsync(
			ReadOnlyMemory<char> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.AppendAndMaybeCancel( buffer.Span );
			return Task.CompletedTask;
		}

		private void AppendAndMaybeCancel(
			ReadOnlySpan<char> value
		) {
			var remaining = this.myLimit - this.myOutput.Length;
			if ( 0 < remaining ) {
				this.myOutput.Append(
					value[ ..Math.Min( remaining, value.Length ) ]
				);
			}
			if ( this.myOutput.Length >= this.myLimit ) {
				this.myCancellation.Cancel();
			}
		}
	}

	private sealed class ThrowingTextWriter : TextWriter {
		public override Encoding Encoding => Encoding.UTF8;

		public override Task WriteAsync(
			ReadOnlyMemory<char> buffer,
			CancellationToken cancellationToken = default
		) {
			throw new IOException( "simulated broken pipe" );
		}
	}
}
