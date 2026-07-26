namespace Icod.CoreUtils.Sum;

using System.Text;
using Icod.CoreUtils.Shared.Checksums;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

/// <summary>Computes BSD or System V checksums.</summary>
public static class Command {

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
		Stream? stdinStream = null,
		Stream? stdoutStream = null,
		CancellationToken cancellationToken = default
	) {
		var useConsoleInput = null == stdin;
		var useConsoleOutput = null == stdout;
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		TextReaderStream? inputAdapter = null;
		if ( null == stdinStream ) {
			if ( useConsoleInput ) {
				stdinStream = Console.OpenStandardInput();
			} else {
				inputAdapter = new TextReaderStream(
					stdin,
					new UTF8Encoding(
						encoderShouldEmitUTF8Identifier: false
					)
				);
				stdinStream = inputAdapter;
			}
		}
		if (
			null == stdoutStream
			&& useConsoleOutput
		) {
			stdoutStream = Console.OpenStandardOutput();
		}
		try {
			return await RunAsync(
				args,
				new CommandContext(
					"sum",
					stdin,
					stdout,
					stderr,
					stdinStream,
					stdoutStream,
					cancellationToken: cancellationToken
				)
			).ConfigureAwait( false );
		} finally {
			inputAdapter?.Dispose();
		}
	}

	/// <summary>Runs the command with a shared context.</summary>
	public static Task<int> RunAsync(
		string[] args,
		CommandContext context
	) {
		return SumCommand.RunAsync(
			args,
			context,
			PrintUsage,
			"Icod.CoreUtils.Sum 1.0"
		);
	}

	private static void PrintUsage(
		TextWriter output
	) {
		output.WriteLine(
			"Usage: sum [OPTION]... [FILE]..."
		);
		output.WriteLine(
			"Print checksum and block counts for each FILE."
		);
		output.WriteLine();
		output.WriteLine(
			"  -r                 use BSD checksum algorithm and 1K blocks"
		);
		output.WriteLine(
			"  -s, --sysv         use System V checksum algorithm and 512-byte blocks"
		);
		output.WriteLine(
			"      --help         display this help and exit"
		);
		output.WriteLine(
			"      --version      output version information and exit"
		);
	}

}
