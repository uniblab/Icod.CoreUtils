// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Basenc;

using System.Text;
using Icod.CoreUtils.Shared.Codecs;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

/// <summary>
/// Encodes or decodes data and writes the result to standard output.
/// </summary>
public static class Command {

	private static readonly BaseEncodingCommandSettings Settings = new() {
		ProgramName = "basenc",
		VersionText = "Icod.CoreUtils.Basenc 1.0",
		EncodingSelections = new BaseEncodingSelection[] {
			new BaseEncodingSelection(
				"base64",
				"base64",
				BaseEncodingKind.Base64
			),
			new BaseEncodingSelection(
				"base64url",
				"base64url",
				BaseEncodingKind.Base64Url
			),
			new BaseEncodingSelection(
				"base58",
				"base58",
				BaseEncodingKind.Base58
			),
			new BaseEncodingSelection(
				"base32",
				"base32",
				BaseEncodingKind.Base32
			),
			new BaseEncodingSelection(
				"base32hex",
				"base32hex",
				BaseEncodingKind.Base32Hex
			),
			new BaseEncodingSelection(
				"base16",
				"base16",
				BaseEncodingKind.Base16
			),
			new BaseEncodingSelection(
				"base2msbf",
				"base2msbf",
				BaseEncodingKind.Base2Msbf
			),
			new BaseEncodingSelection(
				"base2lsbf",
				"base2lsbf",
				BaseEncodingKind.Base2Lsbf
			),
			new BaseEncodingSelection(
				"z85",
				"z85",
				BaseEncodingKind.Z85
			)
		},
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
					"basenc",
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
			"Usage: basenc ENCODING [OPTION]... [FILE]"
		);
		output.WriteLine(
			"Encode or decode FILE, or standard input, to standard output."
		);
		output.WriteLine(
			""
		);
		output.WriteLine(
			"      --base64          RFC 4648 Base64"
		);
		output.WriteLine(
			"      --base64url       file- and URL-safe Base64"
		);
		output.WriteLine(
			"      --base58          visually unambiguous Base58"
		);
		output.WriteLine(
			"      --base32          RFC 4648 Base32"
		);
		output.WriteLine(
			"      --base32hex       extended-hex Base32"
		);
		output.WriteLine(
			"      --base16          hexadecimal"
		);
		output.WriteLine(
			"      --base2msbf       bit string, most-significant bit first"
		);
		output.WriteLine(
			"      --base2lsbf       bit string, least-significant bit first"
		);
		output.WriteLine(
			"      --z85             ZeroMQ Z85"
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
