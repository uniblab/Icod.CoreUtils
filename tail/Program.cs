namespace Icod.CoreUtils.Tail;

using System;
using System.Threading;
using System.Threading.Tasks;

public static class Program {

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