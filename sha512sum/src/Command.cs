namespace Icod.CoreUtils.Sha512Sum;

using System.Text;
using Icod.CoreUtils.Shared.Checksums;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

/// <summary>
/// Computes or verifies SHA512 message digests.
/// </summary>
public static class Command {

	private static readonly DigestCommandSettings Settings = new() {
		Algorithm = ChecksumAlgorithmKind.Sha512,
		DefaultLengthBits = 512,
		DisplayName = "SHA512",
		PrintUsage = PrintUsage,
		ProgramName = "sha512sum",
		SupportsLength = false,
		VersionText = "Icod.CoreUtils.Sha512Sum 1.0"
	};

	/// <summary>
	/// Executes <c>sha512sum</c> synchronously with optional standard-stream substitution.
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
	/// Executes <c>sha512sum</c> asynchronously with optional injected standard streams.
	/// </summary>
	/// <remarks>
	/// Binary streams are used for byte-preserving command data when supplied; text streams remain available for diagnostics and textual fallbacks. Caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <param name="stdinStream">The binary standard-input stream, or <see langword="null"/> to derive one from the selected text input.</param>
	/// <param name="stdoutStream">The binary standard-output stream, or <see langword="null"/> to derive one from the selected text output.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
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
					"sha512sum",
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
	/// Executes <c>sha512sum</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
	public static Task<int> RunAsync(
		string[] args,
		CommandContext context
	) {
		return DigestCommand.RunAsync(
			args,
			context,
			Settings
		);
	}

	private static void PrintUsage(
		TextWriter output
	) {
		output.WriteLine(
			"Usage: sha512sum [OPTION]... [FILE]..."
		);
		output.WriteLine(
			"Print or check SHA512 checksums."
		);
		output.WriteLine();
		output.WriteLine(
			"  -b, --binary          read in binary mode"
		);
		output.WriteLine(
			"  -c, --check           read checksums from the FILEs and check them"
		);
		output.WriteLine(
			"      --tag             create a BSD-style checksum"
		);
		output.WriteLine(
			"  -t, --text            read in text mode"
		);
		output.WriteLine(
			"  -z, --zero            end each output line with NUL"
		);
		output.WriteLine(
			"      --ignore-missing  do not fail for missing files"
		);
		output.WriteLine(
			"      --quiet           do not print OK for each verified file"
		);
		output.WriteLine(
			"      --status          do not output anything; status indicates success"
		);
		output.WriteLine(
			"      --strict          exit nonzero for malformed checksum lines"
		);
		output.WriteLine(
			"  -w, --warn            warn about malformed checksum lines"
		);
		output.WriteLine(
			"      --help            display this help and exit"
		);
		output.WriteLine(
			"      --version         output version information and exit"
		);
	}

}
