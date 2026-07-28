namespace Icod.CoreUtils.Basenc.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using BasencCommand = Icod.CoreUtils.BasEnc.Command;
using Xunit;

public sealed class BasencCommandTests {

	[Theory]
	[InlineData( "--base64", "/k+C" )]
	[InlineData( "--base64url", "_k-C" )]
	[InlineData( "--base32", "7ZHYE===" )]
	[InlineData( "--base32hex", "VP7O4===" )]
	[InlineData( "--base16", "FE4F82" )]
	[InlineData( "--base2lsbf", "011111111111001001000001" )]
	[InlineData( "--base2msbf", "111111100100111110000010" )]
	public async Task ImplementsDocumentedEncodingExamples(
		string option,
		string expected
	) {
		var input = new byte[] {
			0xFE,
			0x4F,
			0x82
		};
		var encoded = await RunAsync(
			new string[] {
				option,
				"-w0"
			},
			input
		);
		Assert.Equal(
			expected,
			Encoding.ASCII.GetString(
				encoded.Output
			)
		);
		var decoded = await RunAsync(
			new string[] {
				option,
				"-d"
			},
			Encoding.ASCII.GetBytes(
				expected
			)
		);
		Assert.Equal(
			input,
			decoded.Output
		);
	}

	[Fact]
	public async Task ImplementsZ85AndBase58() {
		var z85Input = new byte[] {
			0xFE,
			0x4F,
			0x82,
			0
		};
		var z85 = await RunAsync(
			new string[] {
				"--z85",
				"-w0"
			},
			z85Input
		);
		Assert.Equal(
			"@.FaC",
			Encoding.ASCII.GetString(
				z85.Output
			)
		);

		var base58 = await RunAsync(
			new string[] {
				"--base58",
				"-w0"
			},
			Encoding.ASCII.GetBytes(
				"Hello World!"
			)
		);
		Assert.Equal(
			"2NEpo7TZRRrLZSi2U",
			Encoding.ASCII.GetString(
				base58.Output
			)
		);
	}

	[Fact]
	public async Task LastEncodingOptionWins() {
		var result = await RunAsync(
			new string[] {
				"--base64",
				"--base32",
				"-w0"
			},
			Encoding.ASCII.GetBytes(
				"a"
			)
		);
		Assert.Equal(
			"ME======",
			Encoding.ASCII.GetString(
				result.Output
			)
		);
	}

	[Fact]
	public async Task MissingEncodingFailsWithHelpHint() {
		var result = await RunAsync(
			Array.Empty<string>(),
			Array.Empty<byte>()
		);
		Assert.Equal(
			1,
			result.ExitCode
		);
		Assert.Contains(
			"missing encoding type",
			result.Error
		);
		Assert.Contains(
			"basenc --help",
			result.Error
		);
	}

	[Fact]
	public async Task Z85RejectsIncompleteGroups() {
		var result = await RunAsync(
			new string[] {
				"--z85"
			},
			new byte[] {
				1,
				2,
				3
			}
		);
		Assert.Equal(
			1,
			result.ExitCode
		);
		Assert.Contains(
			"invalid input",
			result.Error
		);
	}

	[Fact]
	public async Task IgnoreGarbageWorksForUnpaddedEncodings() {
		var result = await RunAsync(
			new string[] {
				"--base16",
				"-d",
				"-i"
			},
			Encoding.ASCII.GetBytes(
				"FE=4F 82"
			)
		);
		Assert.Equal(
			new byte[] {
				0xFE,
				0x4F,
				0x82
			},
			result.Output
		);
	}

	[Fact]
	public async Task LongEncodingNamesMayBeUniquelyAbbreviated() {
		var result = await RunAsync(
			new string[] {
				"--base32h",
				"-w0"
			},
			new byte[] {
				0xFE,
				0x4F,
				0x82
			}
		);
		Assert.Equal(
			"VP7O4===",
			Encoding.ASCII.GetString(
				result.Output
			)
		);
	}

	[Fact]
	public async Task HelpVersionExtraOperandAndCancellationAreHandled() {
		var help = await RunAsync(
			new string[] {
				"--help"
			},
			Array.Empty<byte>()
		);
		var version = await RunAsync(
			new string[] {
				"--version"
			},
			Array.Empty<byte>()
		);
		var extra = await RunAsync(
			new string[] {
				"--base64",
				"first",
				"second"
			},
			Array.Empty<byte>()
		);
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var canceled = await RunAsync(
			new string[] {
				"--base64"
			},
			Encoding.ASCII.GetBytes(
				"data"
			),
			cancellation.Token
		);

		Assert.Equal( 0, help.ExitCode );
		Assert.Contains(
			"Usage: basenc",
			help.TextOutput
		);
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains(
			"Icod.CoreUtils.Basenc",
			version.TextOutput
		);
		Assert.Equal( 1, extra.ExitCode );
		Assert.Contains(
			"extra operand",
			extra.Error
		);
		Assert.Equal( 130, canceled.ExitCode );
	}

	private static async Task<CommandResult> RunAsync(
		string[] args,
		byte[] input,
		CancellationToken cancellationToken = default
	) {
		await using var inputStream = new MemoryStream(
			input,
			writable: false
		);
		await using var outputStream = new MemoryStream();
		using var outputText = new StringWriter();
		using var errorText = new StringWriter();
		var exitCode = await BasencCommand.RunAsync(
			args,
			new CommandContext(
				"basenc",
				TextReader.Null,
				outputText,
				errorText,
				inputStream,
				outputStream,
				cancellationToken: cancellationToken
			)
		);
		return new CommandResult(
			exitCode,
			outputStream.ToArray(),
			outputText.ToString(),
			errorText.ToString()
		);
	}

	private sealed record CommandResult(
		int ExitCode,
		byte[] Output,
		string TextOutput,
		string Error
	);

}
