namespace Icod.CoreUtils.Cat.Tests;

using System.Text;
using Icod.CommandFramework.Diagnostics;
using CatCommand = Icod.CoreUtils.Cat.Command;
using Xunit;

public sealed class CatCommandTests {

	[Fact]
	public async Task RawCopyPreservesEveryByteAndFinalRecordState() {
		var input = new byte[] {
			0x00,
			0xFF,
			(byte)'a',
			(byte)'\r',
			(byte)'\n',
			(byte)'z'
		};
		var result = await RunAsync(
			Array.Empty<string>(),
			input
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( input, result.Output );
	}

	[Fact]
	public async Task NumberNonblankOverridesNumberAll() {
		var result = await RunAsync(
			new string[] { "-n", "-b" },
			Encoding.UTF8.GetBytes(
				"\nalpha\n"
			)
		);
		Assert.Equal(
			"\n     1\talpha\n",
			Encoding.UTF8.GetString(
				result.Output
			)
		);
	}

	[Fact]
	public async Task NumberAllIncludesBlankLines() {
		var result = await RunAsync(
			new string[] { "--number" },
			Encoding.UTF8.GetBytes(
				"\nalpha\n"
			)
		);
		Assert.Equal(
			"     1\t\n     2\talpha\n",
			Encoding.UTF8.GetString(
				result.Output
			)
		);
	}

	[Fact]
	public async Task ShowAllUsesCaretAndMetaNotation() {
		var result = await RunAsync(
			new string[] { "-A" },
			new byte[] {
				(byte)'\t',
				0x01,
				0x80,
				(byte)'\n'
			}
		);
		Assert.Equal(
			"^I^AM-^@$\n",
			Encoding.ASCII.GetString(
				result.Output
			)
		);
	}

	[Fact]
	public async Task ShowEndsRecognizesCarriageReturnLineFeed() {
		var result = await RunAsync(
			new string[] { "-E" },
			Encoding.ASCII.GetBytes(
				"a\r\nb\r"
			)
		);
		Assert.Equal(
			"a^M$\nb\r",
			Encoding.ASCII.GetString(
				result.Output
			)
		);
	}

	[Fact]
	public async Task SqueezeBlankStateContinuesAcrossFiles() {
		var first = await CreateFileAsync(
			"a\n\n"
		);
		var second = await CreateFileAsync(
			"\n\nb\n"
		);
		try {
			var result = await RunAsync(
				new string[] { "-s", first, second },
				Array.Empty<byte>()
			);
			Assert.Equal(
				"a\n\nb\n",
				Encoding.UTF8.GetString(
					result.Output
				)
			);
		} finally {
			File.Delete(
				first
			);
			File.Delete(
				second
			);
		}
	}

	[Fact]
	public async Task MissingFileDoesNotPreventLaterOperands() {
		var existing = await CreateFileAsync(
			"present\n"
		);
		var missing = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-cat-missing-{Guid.NewGuid():N}"
		);
		try {
			var result = await RunAsync(
				new string[] { missing, existing },
				Array.Empty<byte>()
			);
			Assert.Equal( 1, result.ExitCode );
			Assert.Equal(
				"present\n",
				Encoding.UTF8.GetString(
					result.Output
				)
			);
			Assert.Contains(
				missing,
				result.Error
			);
		} finally {
			File.Delete(
				existing
			);
		}
	}

	[Fact]
	public async Task StandardInputMayAppearAmongFileOperands() {
		var first = await CreateFileAsync(
			"file\n"
		);
		try {
			var result = await RunAsync(
				new string[] { first, "-" },
				Encoding.UTF8.GetBytes(
					"stdin"
				)
			);
			Assert.Equal(
				"file\nstdin",
				Encoding.UTF8.GetString(
					result.Output
				)
			);
		} finally {
			File.Delete(
				first
			);
		}
	}

	[Fact]
	public async Task HelpVersionAndInvalidOptionsAreHandled() {
		var help = await RunAsync(
			new string[] { "--help" },
			Array.Empty<byte>()
		);
		var version = await RunAsync(
			new string[] { "--version" },
			Array.Empty<byte>()
		);
		var invalid = await RunAsync(
			new string[] { "--unknown" },
			Array.Empty<byte>()
		);
		Assert.Equal( 0, help.ExitCode );
		Assert.Contains(
			"Usage: cat",
			help.TextOutput
		);
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains(
			"Icod.CoreUtils.Cat",
			version.TextOutput
		);
		Assert.Equal( 1, invalid.ExitCode );
		Assert.Contains(
			"unrecognized option",
			invalid.Error
		);
	}

	[Fact]
	public async Task CancellationReturnsConventionalExitCode() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var result = await RunAsync(
			Array.Empty<string>(),
			Encoding.UTF8.GetBytes(
				"alpha\n"
			),
			cancellation.Token
		);
		Assert.Equal( 130, result.ExitCode );
	}

	private static async Task<string> CreateFileAsync(
		string contents
	) {
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-cat-{Guid.NewGuid():N}.txt"
		);
		await File.WriteAllTextAsync(
			path,
			contents,
			new UTF8Encoding(
				encoderShouldEmitUTF8Identifier: false
			)
		);
		return path;
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
		var context = new CommandContext(
			"cat",
			TextReader.Null,
			outputText,
			errorText,
			inputStream,
			outputStream,
			cancellationToken: cancellationToken
		);
		var exitCode = await CatCommand.RunAsync(
			args,
			context
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
