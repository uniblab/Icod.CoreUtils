namespace Icod.CoreUtils.Cksum;

using System.Text;
using Icod.CoreUtils.Shared.Checksums;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

/// <summary>Computes and verifies checksums.</summary>
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
					"cksum",
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
		return CksumCommand.RunAsync(
			args,
			context,
			PrintUsage,
			"Icod.CoreUtils.Cksum 1.0"
		);
	}

	private static void PrintUsage(
		TextWriter output
	) {
		output.WriteLine(
			"Usage: cksum [OPTION]... [FILE]..."
		);
		output.WriteLine(
			"Print or verify checksums."
		);
		output.WriteLine();
		output.WriteLine(
			"  -a, --algorithm=TYPE  select sysv, bsd, crc, crc32b, md5, sha1,"
		);
		output.WriteLine(
			"                        sha2, sha3, blake2b, or sm3"
		);
		output.WriteLine(
			"      --base64         emit base64 digests"
		);
		output.WriteLine(
			"  -c, --check          verify checksum files"
		);
		output.WriteLine(
			"  -l, --length=BITS    digest length for sha2, sha3, or blake2b"
		);
		output.WriteLine(
			"      --raw            emit raw digest bytes"
		);
		output.WriteLine(
			"      --tag            emit BSD-style tagged output"
		);
		output.WriteLine(
			"      --untagged       emit digest followed by file name"
		);
		output.WriteLine(
			"  -z, --zero           terminate output records with NUL"
		);
		output.WriteLine(
			"      --debug          explain the selected implementation"
		);
		output.WriteLine(
			"      --help           display this help and exit"
		);
		output.WriteLine(
			"      --version        output version information and exit"
		);
	}

}
