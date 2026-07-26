namespace Icod.CoreUtils.Wc.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using WcCommand = Icod.CoreUtils.Wc.Command;
using Xunit;

public sealed class WcCommandTests {

	[Fact]
	public async Task DefaultStandardInputUsesTraditionalFieldWidths() {
		var result = await RunAsync(
			Array.Empty<string>(),
			Encoding.UTF8.GetBytes(
				"a b\n"
			)
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			"      1       2       4\n",
			result.Output
		);
	}

	[Fact]
	public async Task CharacterAndByteCountsDifferForUtf8() {
		var result = await RunAsync(
			new string[] { "-cm" },
			Encoding.UTF8.GetBytes(
				"é\n"
			)
		);
		Assert.Equal(
			"      2       3\n",
			result.Output
		);
	}

	[Fact]
	public async Task InvalidUtf8IsNotCountedAsACharacter() {
		var result = await RunAsync(
			new string[] { "-wmc" },
			new byte[] {
				0xFF,
				(byte)'A',
				(byte)'\n'
			}
		);
		Assert.Equal(
			"      1       2       3\n",
			result.Output
		);
	}

	[Fact]
	public async Task MaximumLineLengthHandlesTabsWideAndCombiningRunes() {
		var result = await RunAsync(
			new string[] { "-L" },
			Encoding.UTF8.GetBytes(
				"a\t界e\u0301\n"
			)
		);
		Assert.Equal(
			"     11\n",
			result.Output
		);
	}

	[Theory]
	[InlineData( "abc\rx\n", "      3\n" )]
	[InlineData( "abc\b\bX\n", "      4\n" )]
	public async Task MaximumLineLengthTracksDisplayCursorRules(
		string input,
		string expected
	) {
		var result = await RunAsync(
			new string[] { "-L" },
			Encoding.UTF8.GetBytes(
				input
			)
		);
		Assert.Equal(
			expected,
			result.Output
		);
	}

	[Fact]
	public async Task NamedFilePrintsItsNameWithoutDefaultStdinPadding() {
		var path = await CreateFileAsync(
			"a b\n"
		);
		try {
			var result = await RunAsync(
				Array.Empty<string>(),
				Array.Empty<byte>(),
				additionalArguments: new string[] { path }
			);
			Assert.Equal(
				$"1 2 4 {path}\n",
				result.Output
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task MultipleFilesProduceAlignedTotal() {
		var first = await CreateFileAsync(
			"a\n"
		);
		var second = await CreateFileAsync(
			"b c\n"
		);
		try {
			var result = await RunAsync(
				new string[] { first, second },
				Array.Empty<byte>()
			);
			var lines = result.Output.Split(
				'\n',
				StringSplitOptions.RemoveEmptyEntries
			);
			Assert.Equal( 3, lines.Length );
			Assert.EndsWith( first, lines[ 0 ] );
			Assert.EndsWith( second, lines[ 1 ] );
			Assert.EndsWith( "total", lines[ 2 ] );
			Assert.Equal(
				new string[] { "2", "3", "6", "total" },
				lines[ 2 ].Split(
					' ',
					StringSplitOptions.RemoveEmptyEntries
				)
			);
		} finally {
			File.Delete( first );
			File.Delete( second );
		}
	}

	[Fact]
	public async Task FilesZeroFromReadsNamesContainingWhitespace() {
		var first = await CreateFileAsync(
			"one\n",
			"with space"
		);
		var second = await CreateFileAsync(
			"two\n",
			"second"
		);
		var list = Path.Combine(
			Path.GetTempPath(),
			$"icod-wc-list-{Guid.NewGuid():N}.bin"
		);
		await File.WriteAllBytesAsync(
			list,
			Encoding.UTF8.GetBytes(
				string.Concat(
					first,
					"\0",
					second,
					"\0"
				)
			)
		);
		try {
			var result = await RunAsync(
				new string[] { $"--files0-from={list}", "-l" },
				Array.Empty<byte>()
			);
			Assert.Equal( 0, result.ExitCode );
			Assert.Contains( $"1 {first}", result.Output );
			Assert.Contains( $"1 {second}", result.Output );
			Assert.Contains( "2 total", result.Output );
		} finally {
			File.Delete( first );
			File.Delete( second );
			File.Delete( list );
		}
	}

	[Fact]
	public async Task FilesZeroFromStandardInputRejectsStandardInputOperand() {
		var result = await RunAsync(
			new string[] { "--files0-from=-" },
			Encoding.UTF8.GetBytes(
				"-\0"
			)
		);
		Assert.Equal( 1, result.ExitCode );
		Assert.Contains(
			"no file name of '-' is allowed",
			result.Error
		);
	}

	[Theory]
	[InlineData( "always", true, true )]
	[InlineData( "never", true, false )]
	[InlineData( "only", false, false )]
	public async Task TotalModesControlIndividualAndTotalRows(
		string mode,
		bool includesFile,
		bool includesTotalLabel
	) {
		var path = await CreateFileAsync(
			"one\n"
		);
		try {
			var result = await RunAsync(
				new string[] { $"--total={mode}", "-l", path },
				Array.Empty<byte>()
			);
			Assert.Equal(
				includesFile,
				result.Output.Contains(
					path,
					StringComparison.Ordinal
				)
			);
			Assert.Equal(
				includesTotalLabel,
				result.Output.Contains(
					"total",
					StringComparison.Ordinal
				)
			);
			Assert.Contains(
				"1",
				result.Output
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task DebugDescribesStreamingImplementation() {
		var result = await RunAsync(
			new string[] { "--debug", "-c" },
			Encoding.UTF8.GetBytes(
				"abc"
			)
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Contains(
			"streaming byte counts",
			result.Error
		);
	}

	[Fact]
	public async Task MissingFileDoesNotPreventLaterFileCounts() {
		var valid = await CreateFileAsync(
			"a\n"
		);
		var missing = Path.Combine(
			Path.GetTempPath(),
			$"icod-wc-missing-{Guid.NewGuid():N}"
		);
		try {
			var result = await RunAsync(
				new string[] { "-l", missing, valid },
				Array.Empty<byte>()
			);
			Assert.Equal( 1, result.ExitCode );
			Assert.Contains( $"1 {valid}", result.Output );
			Assert.Contains( missing, result.Error );
		} finally {
			File.Delete(
				valid
			);
		}
	}

	[Fact]
	public async Task ChunkedNonSeekableInputIsCountedIncrementally() {
		await using var input = new ChunkedReadStream(
			Encoding.UTF8.GetBytes(
				"alpha beta\n界\n"
			),
			2
		);
		using var output = new StringWriter();
		using var error = new StringWriter();
		var exitCode = await WcCommand.RunAsync(
			new string[] { "-lwmcL" },
			new CommandContext(
				"wc",
				TextReader.Null,
				output,
				error,
				input
			)
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal(
			new string[] { "2", "3", "13", "15", "10" },
			output.ToString().Trim().Split(
				' ',
				StringSplitOptions.RemoveEmptyEntries
			)
		);
	}

	[Fact]
	public async Task HelpVersionInvalidTotalAndExtraFiles0OperandAreHandled() {
		var help = await RunAsync(
			new string[] { "--help" },
			Array.Empty<byte>()
		);
		var version = await RunAsync(
			new string[] { "--version" },
			Array.Empty<byte>()
		);
		var invalid = await RunAsync(
			new string[] { "--total=invalid" },
			Array.Empty<byte>()
		);
		var extra = await RunAsync(
			new string[] { "--files0-from=-", "file" },
			Array.Empty<byte>()
		);
		Assert.Equal( 0, help.ExitCode );
		Assert.Contains( "Usage: wc", help.Output );
		Assert.Equal( 0, version.ExitCode );
		Assert.Equal( 1, invalid.ExitCode );
		Assert.Equal( 1, extra.ExitCode );
	}

	[Fact]
	public async Task CancellationReturnsConventionalExitCode() {
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

	private static async Task<string> CreateFileAsync(
		string contents,
		string? stem = null
	) {
		var path = Path.Combine(
			Path.GetTempPath(),
			$"icod-wc-{stem ?? "file"}-{Guid.NewGuid():N}.txt"
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

	private static Task<CommandResult> RunAsync(
		string[] args,
		byte[] input,
		CancellationToken cancellationToken = default,
		string[]? additionalArguments = null
	) {
		if ( null != additionalArguments ) {
			args = args.Concat(
				additionalArguments
			).ToArray();
		}
		return RunCoreAsync(
			args,
			input,
			cancellationToken
		);
	}

	private static async Task<CommandResult> RunCoreAsync(
		string[] args,
		byte[] input,
		CancellationToken cancellationToken
	) {
		await using var inputStream = new MemoryStream(
			input,
			writable: false
		);
		using var output = new StringWriter();
		using var error = new StringWriter();
		var exitCode = await WcCommand.RunAsync(
			args,
			new CommandContext(
				"wc",
				TextReader.Null,
				output,
				error,
				inputStream,
				cancellationToken: cancellationToken
			)
		);
		return new CommandResult(
			exitCode,
			output.ToString(),
			error.ToString()
		);
	}

	private sealed class ChunkedReadStream : Stream {

		private readonly int myChunkSize;
		private readonly MemoryStream myInner;

		public override bool CanRead {
			get {
				return true;
			}
		}

		public override bool CanSeek {
			get {
				return false;
			}
		}

		public override bool CanWrite {
			get {
				return false;
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

		public ChunkedReadStream(
			byte[] data,
			int chunkSize
		) {
			this.myInner = new MemoryStream(
				data,
				writable: false
			);
			this.myChunkSize = chunkSize;
		}

		public override void Flush() {
		}

		public override int Read(
			byte[] buffer,
			int offset,
			int count
		) {
			return this.myInner.Read(
				buffer,
				offset,
				Math.Min(
					count,
					this.myChunkSize
				)
			);
		}

		public override ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			return this.myInner.ReadAsync(
				buffer.Slice(
					0,
					Math.Min(
						buffer.Length,
						this.myChunkSize
					)
				),
				cancellationToken
			);
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
			throw new NotSupportedException();
		}

		protected override void Dispose(
			bool disposing
		) {
			if ( disposing ) {
				this.myInner.Dispose();
			}
			base.Dispose(
				disposing
			);
		}

	}

	private sealed record CommandResult(
		int ExitCode,
		string Output,
		string Error
	);

}
