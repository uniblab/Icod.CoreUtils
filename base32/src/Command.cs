// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Base32;

using System.Text;
using Icod.CoreUtils.Shared.Codecs;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

/// <summary>
/// Encodes or decodes data and writes the result to standard output.
/// </summary>
public static class Command {

	private static readonly BaseEncodingCommandSettings Settings = new() {
		ProgramName = "base32",
		VersionText = "Icod.CoreUtils.Base32 1.0",
		FixedEncoding = BaseEncodingKind.Base32,
		PrintUsage = PrintUsage
	};

	/// <summary>
	/// Runs the command synchronously.
	/// </summary>
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
	/// Runs the command asynchronously with optionally injected streams.
	/// </summary>
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
					"base32",
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

	/// <summary>
	/// Runs the command using a shared command context.
	/// </summary>
	public static Task<int> RunAsync(
		string[] args,
		CommandContext context
	) {
		return BaseEncodingCommand.RunAsync(
			args,
			context,
			Settings
		);
	}

	private static void PrintUsage(
		TextWriter output
	) {
		output.WriteLine(
			"Usage: base32 [OPTION]... [FILE]"
		);
		output.WriteLine(
			"Base32 encode or decode FILE, or standard input, to standard output."
		);
		output.WriteLine(
			""
		);
		output.WriteLine(
			"  -d, --decode          decode data"
		);
		output.WriteLine(
			"  -i, --ignore-garbage  when decoding, ignore non-alphabet characters"
		);
		output.WriteLine(
			"  -w, --wrap=COLS       wrap encoded lines after COLS characters"
		);
		output.WriteLine(
			"                         (default 76; 0 disables wrapping)"
		);
		output.WriteLine(
			"      --help            display this help and exit"
		);
		output.WriteLine(
			"      --version         output version information and exit"
		);
	}

}
