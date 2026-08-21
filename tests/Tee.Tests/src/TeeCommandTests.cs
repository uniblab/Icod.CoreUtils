namespace Icod.CoreUtils.Tee.Tests;

using System.Text;
using Icod.CommandFramework.Diagnostics;
using TeeCommand = Icod.CoreUtils.Tee.Command;
using Xunit;

public sealed class TeeCommandTests {

	[Fact]
	public async Task CopiesRawBytesToStandardOutputAndFile() {
		var path = CreatePath();
		var input = new byte[] {
			0x00,
			0xFF,
			(byte)'a',
			(byte)'\n'
		};
		try {
			var result = await RunAsync(
				new string[] { path },
				input
			);
			Assert.Equal( 0, result.ExitCode );
			Assert.Equal( input, result.Output );
			Assert.Equal(
				input,
				await File.ReadAllBytesAsync(
					path
				)
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task AppendDoesNotOverwriteExistingData() {
		var path = CreatePath();
		await File.WriteAllBytesAsync(
			path,
			Encoding.ASCII.GetBytes(
				"first"
			)
		);
		try {
			var result = await RunAsync(
				new string[] { "-a", path },
				Encoding.ASCII.GetBytes(
					"second"
				)
			);
			Assert.Equal( 0, result.ExitCode );
			Assert.Equal(
				"firstsecond",
				Encoding.ASCII.GetString(
					await File.ReadAllBytesAsync(
						path
					)
				)
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task WritesEveryNamedDestination() {
		var first = CreatePath();
		var second = CreatePath();
		try {
			var input = Encoding.UTF8.GetBytes(
				"shared\n"
			);
			var result = await RunAsync(
				new string[] { first, second },
				input
			);
			Assert.Equal( 0, result.ExitCode );
			Assert.Equal( input, await File.ReadAllBytesAsync( first ) );
			Assert.Equal( input, await File.ReadAllBytesAsync( second ) );
		} finally {
			File.Delete( first );
			File.Delete( second );
		}
	}

	[Fact]
	public async Task OpenFailureDoesNotPreventOtherOutputs() {
		var good = CreatePath();
		var directory = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-tee-dir-{Guid.NewGuid():N}"
		);
		Directory.CreateDirectory(
			directory
		);
		try {
			var input = Encoding.UTF8.GetBytes(
				"data"
			);
			var result = await RunAsync(
				new string[] { directory, good },
				input
			);
			Assert.Equal( 1, result.ExitCode );
			Assert.Equal( input, result.Output );
			Assert.Equal( input, await File.ReadAllBytesAsync( good ) );
			Assert.Contains( directory, result.Error );
		} finally {
			Directory.Delete( directory );
			File.Delete( good );
		}
	}

	[Fact]
	public async Task WarnModeDiagnosesPipeErrorAndContinuesFiles() {
		var good = CreatePath();
		await using var input = new MemoryStream(
			Encoding.UTF8.GetBytes(
				"continued"
			),
			writable: false
		);
		await using var failingOutput = new ThrowingWriteStream();
		using var outputText = new StringWriter();
		using var errorText = new StringWriter();
		try {
			var exitCode = await TeeCommand.RunAsync(
				new string[] { "--output-error=warn", good },
				new CommandContext(
					"tee",
					TextReader.Null,
					outputText,
					errorText,
					input,
					failingOutput
				)
			);
			Assert.Equal( 1, exitCode );
			Assert.Equal(
				"continued",
				Encoding.UTF8.GetString(
					await File.ReadAllBytesAsync(
						good
					)
				)
			);
			Assert.Contains(
				"standard output",
				errorText.ToString()
			);
		} finally {
			File.Delete(
				good
			);
		}
	}

	[Fact]
	public async Task DefaultModeStopsOnPipeError() {
		await using var input = new MemoryStream(
			Encoding.UTF8.GetBytes(
				"data"
			),
			writable: false
		);
		await using var failingOutput = new ThrowingWriteStream();
		using var outputText = new StringWriter();
		using var errorText = new StringWriter();
		var exitCode = await TeeCommand.RunAsync(
			Array.Empty<string>(),
			new CommandContext(
				"tee",
				TextReader.Null,
				outputText,
				errorText,
				input,
				failingOutput
			)
		);
		Assert.Equal( 1, exitCode );
	}

	[Fact]
	public async Task IgnoreInterruptsUsesAnUncancelledIoToken() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var result = await RunAsync(
			new string[] { "-i" },
			Encoding.UTF8.GetBytes(
				"survives"
			),
			cancellation.Token
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			"survives",
			Encoding.UTF8.GetString(
				result.Output
			)
		);
	}

	[Fact]
	public void DetectsIgnoredInterruptOption() {
		Assert.True(
			TeeCommand.RequestsIgnoredInterrupts(
				new string[] { "-i" }
			)
		);
		Assert.True(
			TeeCommand.RequestsIgnoredInterrupts(
				new string[] { "-ai" }
			)
		);
		Assert.True(
			TeeCommand.RequestsIgnoredInterrupts(
				new string[] { "--ignore-interrupts" }
			)
		);
		Assert.True(
			TeeCommand.RequestsIgnoredInterrupts(
				new string[] { "--ignore" }
			)
		);
		Assert.False(
			TeeCommand.RequestsIgnoredInterrupts(
				new string[] { "--", "-i" }
			)
		);
	}

	[Fact]
	public async Task CancellationWithoutIgnoreReturnsConventionalExitCode() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var result = await RunAsync(
			Array.Empty<string>(),
			Encoding.UTF8.GetBytes(
				"alpha"
			),
			cancellation.Token
		);
		Assert.Equal( 130, result.ExitCode );
	}

	[Fact]
	public async Task HelpVersionAndInvalidModeAreHandled() {
		var help = await RunAsync(
			new string[] { "--help" },
			Array.Empty<byte>()
		);
		var version = await RunAsync(
			new string[] { "--version" },
			Array.Empty<byte>()
		);
		var invalid = await RunAsync(
			new string[] { "--output-error=invalid" },
			Array.Empty<byte>()
		);
		Assert.Equal( 0, help.ExitCode );
		Assert.Contains(
			"Usage: tee",
			help.TextOutput
		);
		Assert.Equal( 0, version.ExitCode );
		Assert.Equal( 1, invalid.ExitCode );
		Assert.Contains(
			"invalid argument",
			invalid.Error
		);
	}

	private static string CreatePath() {
		return System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-tee-{Guid.NewGuid():N}.bin"
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
		var exitCode = await TeeCommand.RunAsync(
			args,
			new CommandContext(
				"tee",
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

	private sealed class ThrowingWriteStream : Stream {

		public override bool CanRead {
			get {
				return false;
			}
		}

		public override bool CanSeek {
			get {
				return false;
			}
		}

		public override bool CanWrite {
			get {
				return true;
			}
		}

		public override long Length {
			get {
				throw new NotSupportedException();
			}
		}

		public override long Position {
			get {
				throw new NotSupportedException();
			}
			set {
				throw new NotSupportedException();
			}
		}

		public override void Flush() {
		}

		public override int Read(
			byte[] buffer,
			int offset,
			int count
		) {
			throw new NotSupportedException();
		}

		public override long Seek(
			long offset,
			SeekOrigin origin
		) {
			throw new NotSupportedException();
		}

		public override void SetLength(
			long value
		) {
			throw new NotSupportedException();
		}

		public override void Write(
			byte[] buffer,
			int offset,
			int count
		) {
			throw new IOException(
				"simulated write failure"
			);
		}

		public override ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			return ValueTask.FromException(
				new IOException(
					"simulated write failure"
				)
			);
		}

	}

	private sealed record CommandResult(
		int ExitCode,
		byte[] Output,
		string TextOutput,
		string Error
	);

}
