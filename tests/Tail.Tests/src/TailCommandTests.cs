namespace Icod.CoreUtils.Tail.Tests;

using System.Text;
using TailCommand = Icod.CoreUtils.Tail.Command;
using Xunit;

public sealed class TailCommandTests {

	[Fact]
	public async Task DefaultsToLastTenRecords() {
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
				Enumerable.Range( 3, 10 ).Select( value => $"line-{value}" )
			) + "\n",
			Encoding.UTF8.GetString( result.Output )
		);
	}

	[Fact]
	public async Task LastRecordsPreserveFinalUnterminatedRecord() {
		var result = await RunAsync(
			new string[] { "-n", "2" },
			Encoding.UTF8.GetBytes(
				"alpha\r\nbeta\ngamma"
			)
		);

		Assert.Equal(
			"beta\ngamma",
			Encoding.UTF8.GetString( result.Output )
		);
	}

	[Fact]
	public async Task PositiveLineCountStartsAtOneBasedRecord() {
		var result = await RunAsync(
			new string[] { "-n", "+2" },
			Encoding.UTF8.GetBytes(
				"one\ntwo\nthree\n"
			)
		);

		Assert.Equal( "two\nthree\n", Encoding.UTF8.GetString( result.Output ) );
	}

	[Fact]
	public async Task ByteFormsPreserveArbitraryBytes() {
		var input = new byte[] {
			0x00, 0x01, 0x7F, 0x80, 0xFF
		};

		var last = await RunAsync(
			new string[] { "-c", "2" },
			input
		);
		var startingAt = await RunAsync(
			new string[] { "--bytes=+3" },
			input
		);

		Assert.Equal( input.Skip( 3 ).ToArray(), last.Output );
		Assert.Equal( input.Skip( 2 ).ToArray(), startingAt.Output );
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
			"beta\0gamma\0",
			Encoding.UTF8.GetString( result.Output )
		);
	}

	[Fact]
	public async Task LegacyPositiveAndNegativeFormsAreAccepted() {
		var positive = await RunAsync(
			new string[] { "+2" },
			Encoding.UTF8.GetBytes(
				"one\ntwo\nthree\n"
			)
		);
		var negative = await RunAsync(
			new string[] { "-2" },
			Encoding.UTF8.GetBytes(
				"one\ntwo\nthree\n"
			)
		);

		Assert.Equal( "two\nthree\n", Encoding.UTF8.GetString( positive.Output ) );
		Assert.Equal( "two\nthree\n", Encoding.UTF8.GetString( negative.Output ) );
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

			Assert.Equal( "two\n", Encoding.UTF8.GetString( result.Output ) );
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
	public async Task InvalidFollowModeProducesUsageFailure() {
		var result = await RunAsync(
			new string[] { "--follow=invalid" },
			Array.Empty<byte>()
		);

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "invalid argument", result.Error );
	}

	[Fact]
	public async Task RetryWithoutFollowProducesWarning() {
		var result = await RunAsync(
			new string[] { "--retry", "-n", "0" },
			Array.Empty<byte>()
		);

		Assert.Equal( 0, result.ExitCode );
		Assert.Contains( "--retry is useful only when following", result.Error );
	}

	[Fact]
	public async Task NonSeekableLastBytesUseSpool() {
		var input = new byte[] {
			0x00, 0x01, 0x02, 0x03, 0x04
		};

		var result = await RunAsync(
			new string[] { "--bytes=2" },
			input,
			nonSeekable: true
		);

		Assert.Equal( input.Skip( 3 ).ToArray(), result.Output );
	}

	[Fact]
	public async Task LargeLastRecordCountUsesSpool() {
		var input = Encoding.UTF8.GetBytes( "one\ntwo\n" );

		var result = await RunAsync(
			new string[] { "--lines=65537" },
			input,
			nonSeekable: true
		);

		Assert.Equal( input, result.Output );
	}

	[Fact]
	public async Task FollowControlOptionsAreAccepted() {
		var path = await CreateFileAsync( string.Empty );
		try {
			var result = await RunAsync(
				new string[] {
					"--debug",
					"--follow=name",
					"--retry",
					"--max-unchanged-stats=2",
					"--pid=2147483647",
					"--sleep-interval=0.01",
					"--lines=0",
					path
				},
				Array.Empty<byte>()
			);

			Assert.Equal( 0, result.ExitCode );
			Assert.Contains( "asynchronous polling follow mode", result.Error );
		} finally {
			File.Delete( path );
		}
	}

	[Fact]
	public async Task FollowOutputsAppendedDataAndObservesCancellation() {
		var path = await CreateFileAsync(
			string.Empty
		);
		using var cancellation = new CancellationTokenSource(
			TimeSpan.FromSeconds( 10 )
		);
		using var outputStream = new SignalingMemoryStream();
		using var outputText = new StringWriter();
		using var error = new StringWriter();
		try {
			var commandTask = TailCommand.RunAsync(
				new string[] { "-n", "0", "-f", "-s", "0.01", path },
				stdin: new StringReader( string.Empty ),
				stdout: outputText,
				stderr: error,
				stdinStream: new MemoryStream(),
				stdoutStream: outputStream,
				cancellationToken: cancellation.Token
			);
			await File.AppendAllTextAsync(
				path,
				"alpha\n",
				new UTF8Encoding( false ),
				CancellationToken.None
			);
			await outputStream.Signal.WaitAsync(
				cancellation.Token
			);

			cancellation.Cancel();
			var exitCode = await commandTask;

			Assert.Equal( 130, exitCode );
			Assert.Equal( "alpha\n", Encoding.UTF8.GetString( outputStream.ToArray() ) );
		} finally {
			File.Delete( path );
		}
	}

	private static async Task<string> CreateFileAsync(
		string contents
	) {
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-tail-test-{Guid.NewGuid():N}.txt"
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
		var exitCode = await TailCommand.RunAsync(
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

	private sealed class SignalingMemoryStream : MemoryStream {

		private readonly TaskCompletionSource<bool> mySignal = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);

		public Task Signal => this.mySignal.Task;

		public override async ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			await base.WriteAsync(
				buffer,
				cancellationToken
			).ConfigureAwait( false );
			if ( !buffer.IsEmpty ) {
				this.mySignal.TrySetResult( true );
			}
		}

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
