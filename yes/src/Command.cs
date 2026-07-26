namespace Icod.CoreUtils.Yes;

using System.Text;

/// <summary>
/// Implements the yes utility.
/// </summary>
public static class Command {
	private const int TargetBufferSize = 64 * 1024;
	private const string VersionText = "yes (Icod.CoreUtils) 1.0";

	/// <summary>Runs the command synchronously.</summary>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		return RunAsync(
			args,
			stdin,
			stdout,
			stderr
		).GetAwaiter().GetResult();
	}

	/// <summary>Runs the command asynchronously.</summary>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			if (
				1 == args.Length
				&& "--help" == args[ 0 ]
			) {
				await PrintUsageAsync(
					stdout,
					cancellationToken
				).ConfigureAwait( false );
				return 0;
			}
			if (
				1 == args.Length
				&& "--version" == args[ 0 ]
			) {
				await stdout.WriteLineAsync(
					VersionText.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				return 0;
			}

			var line = string.Join(
				" ",
				0 == args.Length
					? new string[] { "y" }
					: args
			) + "\n";
			var block = CreateOutputBlock( line );
			while ( true ) {
				cancellationToken.ThrowIfCancellationRequested();
				await stdout.WriteAsync(
					block.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
			}
		} catch ( OperationCanceledException ) {
			return 130;
		} catch ( IOException exception ) {
			await TryWriteErrorAsync(
				stderr,
				$"yes: standard output: {exception.Message}\n"
			).ConfigureAwait( false );
			return 1;
		} catch ( ObjectDisposedException exception ) {
			await TryWriteErrorAsync(
				stderr,
				$"yes: standard output: {exception.Message}\n"
			).ConfigureAwait( false );
			return 1;
		}
	}

	private static string CreateOutputBlock(
		string line
	) {
		if ( line.Length >= TargetBufferSize ) {
			return line;
		}
		var repetitions = Math.Max(
			1,
			TargetBufferSize / line.Length
		);
		var buffer = new StringBuilder(
			line.Length * repetitions
		);
		for ( var index = 0; index < repetitions; index++ ) {
			buffer.Append( line );
		}
		return buffer.ToString();
	}

	private static async Task TryWriteErrorAsync(
		TextWriter error,
		string message
	) {
		try {
			await error.WriteAsync( message ).ConfigureAwait( false );
		} catch ( IOException ) {
		}
	}

	private static async Task PrintUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		const string text = """
Usage: yes [STRING]...
  or:  yes OPTION
Repeatedly output a line with all specified STRING(s), or 'y'.

      --help        display this help and exit
      --version     output version information and exit
""";
		await output.WriteAsync(
			text.AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}
}
