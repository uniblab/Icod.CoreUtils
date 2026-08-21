namespace Icod.CoreUtils.Base64.Tests;

using System.Text;
using Icod.CommandFramework.Diagnostics;
using Base64Command = Icod.CoreUtils.Base64.Command;
using Xunit;

public sealed class Base64CommandTests {

	[Fact]
	public async Task EncodesAndDecodesRawBytes() {
		var bytes = new byte[] {
			0,
			0xFF
		};
		var encoded = await RunAsync(
			Array.Empty<string>(),
			bytes
		);
		Assert.Equal(
			"AP8=\n",
			Encoding.ASCII.GetString(
				encoded.Output
			)
		);

		var decoded = await RunAsync(
			new string[] {
				"-d"
			},
			Encoding.ASCII.GetBytes(
				"AP8=\n"
			)
		);
		Assert.Equal(
			bytes,
			decoded.Output
		);
	}

	[Fact]
	public async Task WrapOptionUsesExactColumnWidth() {
		var result = await RunAsync(
			new string[] {
				"-w",
				"3"
			},
			Encoding.ASCII.GetBytes(
				"foobar"
			)
		);
		Assert.Equal(
			"Zm9\nvYm\nFy\n",
			Encoding.ASCII.GetString(
				result.Output
			)
		);
	}

	[Fact]
	public async Task WrapAcceptsAnExplicitPositiveSign() {
		var result = await RunAsync(
			new string[] {
				"-w",
				"+3"
			},
			Encoding.ASCII.GetBytes(
				"foobar"
			)
		);
		Assert.Equal(
			"Zm9\nvYm\nFy\n",
			Encoding.ASCII.GetString(
				result.Output
			)
		);
	}

	[Fact]
	public async Task WrapAcceptsValuesBeyondInt32() {
		var result = await RunAsync(
			new string[] {
				"--wrap=2147483648"
			},
			Encoding.ASCII.GetBytes(
				"a"
			)
		);
		Assert.Equal(
			"YQ==\n",
			Encoding.ASCII.GetString(
				result.Output
			)
		);
	}

	[Fact]
	public async Task ZeroWrapDisablesInteriorNewlines() {
		var result = await RunAsync(
			new string[] {
				"--wrap=0"
			},
			Encoding.ASCII.GetBytes(
				"foobar"
			)
		);
		var text = Encoding.ASCII.GetString(
			result.Output
		);
		Assert.DoesNotContain(
			"\n",
			text
		);
	}

	[Fact]
	public async Task IgnoreGarbageOnlyChangesDecodeBehavior() {
		var recovered = await RunAsync(
			new string[] {
				"-d",
				"-i"
			},
			Encoding.ASCII.GetBytes(
				"AP #8=\r"
			)
		);
		Assert.Equal(
			new byte[] {
				0,
				0xFF
			},
			recovered.Output
		);

		var rejected = await RunAsync(
			new string[] {
				"-d"
			},
			Encoding.ASCII.GetBytes(
				"AP #8=\r"
			)
		);
		Assert.Equal(
			1,
			rejected.ExitCode
		);
		Assert.Contains(
			"invalid input",
			rejected.Error
		);
	}

	[Fact]
	public async Task ReadsOneNamedFile() {
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-base64-{Guid.NewGuid():N}.bin"
		);
		await File.WriteAllBytesAsync(
			path,
			Encoding.ASCII.GetBytes(
				"file"
			)
		);
		try {
			var result = await RunAsync(
				new string[] {
					path
				},
				Array.Empty<byte>()
			);
			Assert.Equal(
				0,
				result.ExitCode
			);
			Assert.NotEmpty(
				result.Output
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task ExtraOperandAndInvalidWrapFail() {
		var extra = await RunAsync(
			new string[] {
				"first",
				"second"
			},
			Array.Empty<byte>()
		);
		var wrap = await RunAsync(
			new string[] {
				"--wrap=-1"
			},
			Array.Empty<byte>()
		);
		Assert.Equal(
			1,
			extra.ExitCode
		);
		Assert.Contains(
			"extra operand",
			extra.Error
		);
		Assert.Equal(
			1,
			wrap.ExitCode
		);
		Assert.Contains(
			"invalid wrap size",
			wrap.Error
		);
	}

	[Fact]
	public async Task HelpVersionAndCancellationAreHandled() {
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
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var canceled = await RunAsync(
			Array.Empty<string>(),
			Encoding.ASCII.GetBytes(
				"data"
			),
			cancellation.Token
		);

		Assert.Equal(
			0,
			help.ExitCode
		);
		Assert.Contains(
			"Usage: base64",
			help.TextOutput
		);
		Assert.Equal(
			0,
			version.ExitCode
		);
		Assert.Contains(
			"Icod.CoreUtils.Base64",
			version.TextOutput
		);
		Assert.Equal(
			130,
			canceled.ExitCode
		);
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
		var exitCode = await Base64Command.RunAsync(
			args,
			new CommandContext(
				"base64",
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
