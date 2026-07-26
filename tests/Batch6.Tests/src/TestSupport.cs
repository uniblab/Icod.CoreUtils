namespace Icod.CoreUtils.Batch6.Tests;

using System.Globalization;
using System.Text;

internal delegate Task<int> CommandRunner(
	string[] args,
	TextReader? stdin,
	TextWriter? stdout,
	TextWriter? stderr,
	CancellationToken cancellationToken
);

internal sealed record CommandResult(
	int ExitCode,
	string Output,
	string Error
);

internal static class TestSupport {
	public static async Task<CommandResult> RunAsync(
		CommandRunner command,
		string[] args,
		string input = "",
		CancellationToken cancellationToken = default,
		TextWriter? output = null
	) {
		using var inputReader = new StringReader( input );
		var actualOutput = output
			?? new StringWriter( CultureInfo.InvariantCulture )
		;
		using var error = new StringWriter( CultureInfo.InvariantCulture );
		var exitCode = await command(
			args,
			inputReader,
			actualOutput,
			error,
			cancellationToken
		).ConfigureAwait( false );
		return new CommandResult(
			exitCode,
			actualOutput is StringWriter stringWriter
				? stringWriter.ToString()
				: string.Empty,
			error.ToString()
		);
	}
}

internal sealed class CancellingTextWriter : TextWriter {
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

	public override ValueTask WriteAsync(
		ReadOnlyMemory<char> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.AppendAndMaybeCancel( buffer.Span );
		return ValueTask.CompletedTask;
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

internal sealed class ThrowingTextWriter : TextWriter {
	public override Encoding Encoding => Encoding.UTF8;

	public override ValueTask WriteAsync(
		ReadOnlyMemory<char> buffer,
		CancellationToken cancellationToken = default
	) {
		throw new IOException( "simulated broken pipe" );
	}
}

internal sealed class CultureScope : IDisposable {
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

internal sealed class EnvironmentVariableScope : IDisposable {
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
