namespace Icod.CoreUtils.Sha1Sum;

using System.Text;
using Icod.CoreUtils.Shared.Checksums;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

/// <summary>
/// Computes or verifies SHA1 message digests.
/// </summary>
public static class Command {

	private static readonly DigestCommandSettings Settings = new() {
		Algorithm = ChecksumAlgorithmKind.Sha1,
		DefaultLengthBits = 160,
		DisplayName = "SHA1",
		PrintUsage = PrintUsage,
		ProgramName = "sha1sum",
		SupportsLength = false,
		VersionText = "Icod.CoreUtils.Sha1Sum 1.0"
	};

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
					"sha1sum",
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
			"Usage: sha1sum [OPTION]... [FILE]..."
		);
		output.WriteLine(
			"Print or check SHA1 checksums."
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
