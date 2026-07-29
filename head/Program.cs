namespace Icod.CoreUtils.Head;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>head</c> command for writing the leading portion of files or standard input.
/// </summary>
public static class Program {

	/// <summary>
	/// Runs the <c>head</c> command using both the text and binary process console streams, and converts a console interrupt into a cancellation request.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>head</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> Main(
		string[] args
	) {
		using ( var cancellation = new CancellationTokenSource() ) {
			Console.CancelKeyPress += (
				sender,
				eventArgs
			) => {
				eventArgs.Cancel = true;
				cancellation.Cancel();
			};
			return await Command.RunAsync(
				args,
				stdin: Console.In,
				stdout: Console.Out,
				stderr: Console.Error,
				stdinStream: Console.OpenStandardInput(),
				stdoutStream: Console.OpenStandardOutput(),
				cancellationToken: cancellation.Token
			).ConfigureAwait( false );
		}
	}

}
