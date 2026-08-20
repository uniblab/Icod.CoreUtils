namespace Icod.CoreUtils.Head.Tests;

using System.Text;
using HeadCommand = Icod.CoreUtils.Head.Command;
using Xunit;

public sealed class HeadCommandTests {

	[Fact]
	public async Task DefaultsToFirstTenRecords() {
		var input = string.Join(
			"\n",
			Enumerable.Range( 1, 12 ).Select( value => $"line-{value}" )
		) + "\n";

		var result = await RunAsync(
			Array.Empty<string>(),
			Encoding.UTF8.GetBytes( input )
		);

		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			string.Join(
				"\n",
				Enumerable.Range( 1, 10 ).Select( value => $"line-{value}" )
			) + "\n",
			Encoding.UTF8.GetString( result.Output )
		);
	}

	[Fact]
	public async Task PreservesLineEndingsAndFinalUnterminatedRecord() {
		var input = Encoding.UTF8.GetBytes(
			"alpha\r\nbeta\ngamma"
		);

		var result = await RunAsync(
			new string[] { "-n", "3" },
			input
		);

		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( input, result.Output );
	}

	[Fact]
	public async Task NegativeLineCountExcludesFinalRecords() {
		var result = await RunAsync(
			new string[] { "-n", "-1" },
			Encoding.UTF8.GetBytes(
				"alpha\nbeta\ngamma"
			)
		);

		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			"alpha\nbeta\n",
			Encoding.UTF8.GetString( result.Output )
		);
	}

	[Fact]
	public async Task ByteModePreservesArbitraryBytes() {
		var input = new byte[] {
			0x00, 0x01, 0x7F, 0x80, 0xFF
		};

		var first = await RunAsync(
			new string[] { "-c3" },
			input
		);
		var allButLast = await RunAsync(
			new string[] { "--bytes=-2" },
			input
		);

		Assert.Equal( input.Take( 3 ).ToArray(), first.Output );
		Assert.Equal( input.Take( 3 ).ToArray(), allButLast.Output );
	}

	[Fact]
	public async Task ZeroTerminatedModeUsesNulRecords() {
		var result = await RunAsync(
			new string[] { "-z", "-n", "2" },
			Encoding.UTF8.GetBytes(
				"alpha\0beta\0gamma\0"
			)
		);

		Assert.Equal(
			"alpha\0beta\0",
			Encoding.UTF8.GetString( result.Output )
		);
	}

	[Fact]
	public async Task LegacyAndAbbreviatedFormsAreAccepted() {
		var legacy = await RunAsync(
			new string[] { "-2" },
			Encoding.UTF8.GetBytes(
				"one\ntwo\nthree\n"
			)
		);
		var abbreviation = await RunAsync(
			new string[] { "--lin=1" },
			Encoding.UTF8.GetBytes(
				"one\ntwo\n"
			)
		);

		Assert.Equal( "one\ntwo\n", Encoding.UTF8.GetString( legacy.Output ) );
		Assert.Equal( "one\n", Encoding.UTF8.GetString( abbreviation.Output ) );
	}

	[Fact]
	public async Task OptionsMayFollowFileOperands() {
		var path = await CreateFileAsync(
			"one\ntwo\n"
		);
		try {
			var result = await RunAsync(
				new string[] { path, "-n", "1" },
				Array.Empty<byte>()
			);

			Assert.Equal( "one\n", Encoding.UTF8.GetString( result.Output ) );
		} finally {
			File.Delete( path );
		}
	}

	[Fact]
	public async Task MultipleFilesUseHeadersAndQuietSuppressesThem() {
		var first = await CreateFileAsync( "alpha\n" );
		var second = await CreateFileAsync( "beta\n" );
		try {
			var normal = await RunAsync(
				new string[] { first, second },
				Array.Empty<byte>()
			);
			var quiet = await RunAsync(
				new string[] { "-v", "-q", first, second },
				Array.Empty<byte>()
			);

			var normalText = Encoding.UTF8.GetString( normal.Output );
			Assert.Contains( $"==> {first} <==\n", normalText );
			Assert.Contains( $"==> {second} <==\n", normalText );
			Assert.Equal( "alpha\nbeta\n", Encoding.UTF8.GetString( quiet.Output ) );
		} finally {
			File.Delete( first );
			File.Delete( second );
		}
	}

	[Fact]
	public async Task InvalidCountsProduceUsageFailure() {
		var result = await RunAsync(
			new string[] { "--lines=bogus" },
			Array.Empty<byte>()
		);

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "invalid number of lines", result.Error );
	}

	[Fact]
	public async Task NonSeekableNegativeByteCountUsesSpool() {
		var input = new byte[] {
			0x00, 0x01, 0x02, 0x03, 0x04
		};

		var result = await RunAsync(
			new string[] { "--bytes=-2" },
			input,
			nonSeekable: true
		);

		Assert.Equal( input.Take( 3 ).ToArray(), result.Output );
	}

	[Fact]
	public async Task LargeNegativeRecordCountUsesSpool() {
		var result = await RunAsync(
			new string[] { "--lines=-65537" },
			Encoding.UTF8.GetBytes( "one\ntwo\n" ),
			nonSeekable: true
		);

		Assert.Empty( result.Output );
	}

	[Fact]
	public async Task CancellationReturnsConventionalExitCode() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		var result = await RunAsync(
			new string[] { "-n", "1" },
			Encoding.UTF8.GetBytes( "alpha\n" ),
			cancellation.Token
		);

		Assert.Equal( 130, result.ExitCode );
	}

	private static async Task<string> CreateFileAsync(
		string contents
	) {
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-head-test-{Guid.NewGuid():N}.txt"
		);
		await File.WriteAllTextAsync(
			path,
			contents,
			new UTF8Encoding( false )
		);
		return path;
	}

	private static async Task<CommandResult> RunAsync(
		string[] args,
		byte[] input,
		CancellationToken cancellationToken = default,
		bool nonSeekable = false
	) {
		using Stream inputStream = nonSeekable
			? new NonSeekableReadStream( input )
			: new MemoryStream(
				input,
				writable: false
			)
		;
		using var outputStream = new MemoryStream();
		using var outputText = new StringWriter();
		using var error = new StringWriter();
		var exitCode = await HeadCommand.RunAsync(
			args,
			stdin: new StringReader( string.Empty ),
			stdout: outputText,
			stderr: error,
			stdinStream: inputStream,
			stdoutStream: outputStream,
			cancellationToken: cancellationToken
		);
		return new CommandResult(
			exitCode,
			outputStream.ToArray(),
			error.ToString()
		);
	}

	private sealed class NonSeekableReadStream : Stream {

		private readonly MemoryStream myInner;

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position {
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public NonSeekableReadStream(
			byte[] value
		) {
			this.myInner = new MemoryStream(
				value,
				writable: false
			);
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
				count
			);
		}

		public override Task<int> ReadAsync(
			byte[] buffer,
			int offset,
			int count,
			CancellationToken cancellationToken
		) {
			return this.myInner.ReadAsync(
				buffer,
				offset,
				count,
				cancellationToken
			);
		}

		public override ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			return this.myInner.ReadAsync(
				buffer,
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
		byte[] Output,
		string Error
	);

}
