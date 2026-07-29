namespace Icod.CoreUtils.Yes;

using System.Text;

/// <summary>
/// Implements the yes utility.
/// </summary>
public static class Command {
	private const int TargetBufferSize = 64 * 1024;
	private const string VersionText = "yes (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>yes</c> synchronously with optional standard-stream substitution.
	/// </summary>
	/// <remarks>
	/// This compatibility entry point blocks on the TAP implementation. A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream; caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
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

	/// <summary>
	/// Executes <c>yes</c> asynchronously with optional injected standard streams.
	/// </summary>
	/// <remarks>
	/// A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream. Caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
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

			var line = System.String.Concat(
				string.Join(
					" ",
					0 == args.Length
						? new string[] { "y" }
						: args
				),
				Environment.NewLine
			);
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
				System.String.Concat(
					"yes: standard output: ",
					exception.Message,
					Environment.NewLine
				)
			).ConfigureAwait( false );
			return 1;
		} catch ( ObjectDisposedException exception ) {
			await TryWriteErrorAsync(
				stderr,
				System.String.Concat(
					"yes: standard output: ",
					exception.Message,
					Environment.NewLine
				)
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
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}
}
