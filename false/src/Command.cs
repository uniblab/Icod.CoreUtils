namespace Icod.CoreUtils.False;

/// <summary>
/// Implements the false utility.
/// </summary>
public static class Command {
	private const string VersionText = "false (Icod.CoreUtils) 1.0";

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
			} else if (
				1 == args.Length
				&& "--version" == args[ 0 ]
			) {
				await stdout.WriteLineAsync(
					VersionText.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
			}
			return 1;
		} catch ( OperationCanceledException ) {
			return 130;
		} catch ( IOException ) {
			return 1;
		}
	}

	private static async Task PrintUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		const string text = """
Usage: false [ignored command line arguments]
  or:  false OPTION
Exit with a status code indicating failure.

      --help        display this help and exit
      --version     output version information and exit
""";
		await output.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}
}
