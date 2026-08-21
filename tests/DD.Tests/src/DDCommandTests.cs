using Icod.CommandFramework.Diagnostics;
using Xunit;
using Tool = Icod.CoreUtils.DD.Command;

namespace Icod.CoreUtils.DD.Tests;

public sealed class DDCommandTests {
	[Fact]
	public async Task CopiesInjectedBinaryStandardStreams() {
		var input = new MemoryStream(
			"alpha beta"u8.ToArray()
		);
		var output = new MemoryStream();
		var error = new StringWriter();
		var exitCode = await Tool.RunAsync(
			[ "status=none" ],
			Context( input, output, error )
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal(
			"alpha beta"u8.ToArray(),
			output.ToArray()
		);
		Assert.Equal(
			string.Empty,
			error.ToString()
		);
	}

	[Fact]
	public async Task AppliesSkipCountAndIndependentBlockSizes() {
		var input = new MemoryStream(
			Enumerable.Range( 0, 10 ).Select( value => (byte)value ).ToArray()
		);
		var output = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "ibs=2", "obs=3", "skip=1", "count=2", "status=none" ],
				Context( input, output )
			)
		);
		Assert.Equal(
			new byte[] { 2, 3, 4, 5 },
			output.ToArray()
		);
	}

	[Fact]
	public async Task CountEndingInBCountsBytes() {
		var output = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "bs=2", "count=3B", "status=none" ],
				Context(
					new MemoryStream( "abcdef"u8.ToArray() ),
					output
				)
			)
		);
		Assert.Equal(
			"abc"u8.ToArray(),
			output.ToArray()
		);
	}

	[Fact]
	public async Task FullBlockAccumulatesShortReads() {
		var output = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "ibs=4", "count=1", "iflag=fullblock", "status=none" ],
				Context(
					new ChunkedReadStream( "abcdef"u8.ToArray(), 1 ),
					output
				)
			)
		);
		Assert.Equal(
			"abcd"u8.ToArray(),
			output.ToArray()
		);
	}

	[Fact]
	public async Task SwabSwapsPairsAndPreservesOddByte() {
		var output = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "ibs=3", "conv=swab", "status=none" ],
				Context(
					new MemoryStream( "abcde"u8.ToArray() ),
					output
				)
			)
		);
		Assert.Equal(
			"badce"u8.ToArray(),
			output.ToArray()
		);
	}

	[Fact]
	public async Task BlockAndUnblockHonorConversionBlockSize() {
		var blocked = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "conv=block", "cbs=4", "status=none" ],
				Context(
					new MemoryStream( [ (byte)'a', (byte)'b', (byte)'c', 0x0A, (byte)'x', (byte)'y', 0x0A ] ),
					blocked
				)
			)
		);
		Assert.Equal(
			"abc xy  "u8.ToArray(),
			blocked.ToArray()
		);

		blocked.Position = 0L;
		var unblocked = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "conv=unblock", "cbs=4", "status=none" ],
				Context( blocked, unblocked )
			)
		);
		Assert.Equal(
			new byte[] { (byte)'a', (byte)'b', (byte)'c', 0x0A, (byte)'x', (byte)'y', 0x0A },
			unblocked.ToArray()
		);
	}

	[Fact]
	public async Task SyncPadsPartialInputBlock() {
		var output = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "ibs=4", "conv=sync", "status=none" ],
				Context(
					new MemoryStream( "ab"u8.ToArray() ),
					output
				)
			)
		);
		Assert.Equal(
			new byte[] { (byte)'a', (byte)'b', 0, 0 },
			output.ToArray()
		);
	}

	[Fact]
	public async Task CaseAndEbcdicConversionsRoundTrip() {
		var encoded = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "conv=ucase,ebcdic", "status=none" ],
				Context(
					new MemoryStream( "Abz"u8.ToArray() ),
					encoded
				)
			)
		);
		encoded.Position = 0L;
		var decoded = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "conv=ascii", "status=none" ],
				Context( encoded, decoded )
			)
		);
		Assert.Equal(
			"ABZ"u8.ToArray(),
			decoded.ToArray()
		);
	}

	[Fact]
	public async Task OutputSeekAndNoTruncatePreserveExistingData() {
		var path = TemporaryPath();
		try {
			await File.WriteAllBytesAsync(
				path,
				"abcdefgh"u8.ToArray()
			);
			Assert.Equal(
				0,
				await Tool.RunAsync(
					[
						string.Concat( "of=", path ),
						"obs=2",
						"seek=1",
						"conv=notrunc",
						"status=none",
					],
					Context(
						new MemoryStream( "XY"u8.ToArray() ),
						new MemoryStream()
					)
				)
			);
			Assert.Equal(
				"abXYefgh"u8.ToArray(),
				await File.ReadAllBytesAsync( path )
			);
		} finally {
			File.Delete( path );
		}
	}

	[Fact]
	public async Task ExclusiveOutputRefusesExistingFile() {
		var path = TemporaryPath();
		try {
			await File.WriteAllTextAsync( path, "existing" );
			var error = new StringWriter();
			Assert.Equal(
				1,
				await Tool.RunAsync(
					[ string.Concat( "of=", path ), "conv=excl", "status=none" ],
					Context(
						new MemoryStream( "x"u8.ToArray() ),
						new MemoryStream(),
						error
					)
				)
			);
			Assert.NotEmpty( error.ToString() );
		} finally {
			File.Delete( path );
		}
	}

	[Fact]
	public async Task SparseCreatesCorrectLogicalLength() {
		var path = TemporaryPath();
		try {
			var input = new byte[ 128 * 1024 ];
			Assert.Equal(
				0,
				await Tool.RunAsync(
					[
						string.Concat( "of=", path ),
						"bs=4096",
						"conv=sparse",
						"status=none",
					],
					Context(
						new MemoryStream( input ),
						new MemoryStream()
					)
				)
			);
			Assert.Equal(
				input.LongLength,
				new FileInfo( path ).Length
			);
		} finally {
			File.Delete( path );
		}
	}

	[Fact]
	public async Task StatusModesControlStatistics() {
		var noneError = new StringWriter();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "status=none" ],
				Context(
					new MemoryStream( "abc"u8.ToArray() ),
					new MemoryStream(),
					noneError
				)
			)
		);
		Assert.Equal( string.Empty, noneError.ToString() );

		var noTransferError = new StringWriter();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "status=noxfer" ],
				Context(
					new MemoryStream( "abc"u8.ToArray() ),
					new MemoryStream(),
					noTransferError
				)
			)
		);
		Assert.Contains( "records in", noTransferError.ToString(), StringComparison.Ordinal );
		Assert.DoesNotContain( "bytes copied", noTransferError.ToString(), StringComparison.Ordinal );
	}

	[Fact]
	public async Task StreamsLargeInputWithoutChangingBytes() {
		var data = new byte[ 4 * 1024 * 1024 ];
		new Random( 42 ).NextBytes( data );
		var output = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "ibs=64K", "obs=32K", "status=none" ],
				Context(
					new MemoryStream( data ),
					output
				)
			)
		);
		Assert.Equal( data, output.ToArray() );
	}

	[Fact]
	public async Task CancellationReturnsConventionalExitCode() {
		using var cancellation = new CancellationTokenSource();
		var task = Tool.RunAsync(
			[ "status=none" ],
			Context(
				new CancellableInputStream(),
				new MemoryStream(),
				cancellationToken: cancellation.Token
			)
		);
		cancellation.Cancel();
		Assert.Equal( 130, await task );
	}

	[Fact]
	public async Task HelpVersionAndInvalidOperandsAreDiagnosed() {
		var help = new StringWriter();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "--help" ],
				new CommandContext( "dd", TextReader.Null, help, new StringWriter() )
			)
		);
		Assert.Contains( "Usage: dd [OPERAND]...", help.ToString(), StringComparison.Ordinal );
		Assert.Contains( "conv=CONVS", help.ToString(), StringComparison.Ordinal );

		var version = new StringWriter();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "--version" ],
				new CommandContext( "dd", TextReader.Null, version, new StringWriter() )
			)
		);
		Assert.Contains( "Icod.CoreUtils", version.ToString(), StringComparison.Ordinal );

		var error = new StringWriter();
		Assert.Equal(
			1,
			await Tool.RunAsync(
				[ "unknown=value" ],
				Context( new MemoryStream(), new MemoryStream(), error )
			)
		);
		Assert.Contains( "unrecognized operand", error.ToString(), StringComparison.Ordinal );
	}

	[Fact]
	public async Task BlockSizeOverridesIbsAndObsRegardlessOfOperandOrder() {
		var error = new StringWriter();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "ibs=1", "bs=2", "ibs=3", "obs=4", "status=noxfer" ],
				Context(
					new MemoryStream( "abcde"u8.ToArray() ),
					new MemoryStream(),
					error
				)
			)
		);
		Assert.Contains( "2+1 records in", error.ToString(), StringComparison.Ordinal );
	}

	[Fact]
	public async Task BlockSizePreservesShortInputRecordBoundaries() {
		var error = new StringWriter();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "bs=4", "status=noxfer" ],
				Context(
					new ChunkedReadStream( "abcdef"u8.ToArray(), 2 ),
					new MemoryStream(),
					error
				)
			)
		);
		Assert.Contains( "0+3 records out", error.ToString(), StringComparison.Ordinal );
	}

	[Fact]
	public async Task SeekWithoutNoTruncatePreservesPrefixAndTruncatesSuffix() {
		var path = TemporaryPath();
		try {
			await File.WriteAllBytesAsync(
				path,
				"abcdefgh"u8.ToArray()
			);
			Assert.Equal(
				0,
				await Tool.RunAsync(
					[ string.Concat( "of=", path ), "obs=2", "seek=1", "status=none" ],
					Context(
						new MemoryStream( "XY"u8.ToArray() ),
						new MemoryStream()
					)
				)
			);
			Assert.Equal(
				"abXY"u8.ToArray(),
				await File.ReadAllBytesAsync( path )
			);
		} finally {
			File.Delete( path );
		}
	}

	[Fact]
	public async Task BlockAndUnblockAreRecordNoOpsWithoutCbs() {
		var source = new byte[] { (byte)'a', 0x0A };
		foreach ( var conversion in new[] { "block", "unblock" } ) {
			var output = new MemoryStream();
			Assert.Equal(
				0,
				await Tool.RunAsync(
					[ string.Concat( "conv=", conversion ), "status=none" ],
					Context( new MemoryStream( source ), output )
				)
			);
			Assert.Equal( source, output.ToArray() );
		}
	}

	[Fact]
	public async Task CharacterSetConversionsImplyRecordConversionWhenCbsIsSet() {
		var encoded = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "cbs=4", "conv=ebcdic", "status=none" ],
				Context(
					new MemoryStream( new byte[] { (byte)'A', 0x0A } ),
					encoded
				)
			)
		);
		Assert.Equal( 4L, encoded.Length );

		encoded.Position = 0L;
		var decoded = new MemoryStream();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "cbs=4", "conv=ascii", "status=none" ],
				Context( encoded, decoded )
			)
		);
		Assert.Equal(
			new byte[] { (byte)'A', 0x0A },
			decoded.ToArray()
		);
	}

	[Fact]
	public async Task NoErrorAdvancesPastASeekableReadFailure() {
		var output = new MemoryStream();
		var error = new StringWriter();
		Assert.Equal(
			0,
			await Tool.RunAsync(
				[ "ibs=4", "conv=noerror", "status=none" ],
				Context(
					new OneFailureReadStream( "BAD!GOOD"u8.ToArray() ),
					output,
					error
				)
			)
		);
		Assert.Equal( "GOOD"u8.ToArray(), output.ToArray() );
		Assert.Contains( "error reading input", error.ToString(), StringComparison.Ordinal );
	}

	[Fact]
	public async Task OpenFailureDoesNotPrintTransferStatistics() {
		var error = new StringWriter();
		Assert.Equal(
			1,
			await Tool.RunAsync(
				[ string.Concat( "if=", TemporaryPath() ) ],
				Context( new MemoryStream(), new MemoryStream(), error )
			)
		);
		Assert.DoesNotContain( "records in", error.ToString(), StringComparison.Ordinal );
		Assert.DoesNotContain( "bytes copied", error.ToString(), StringComparison.Ordinal );
	}

	[Fact]
	public async Task DirectoryInputFlagAllowsAZeroCountDirectoryOpen() {
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			string.Concat( "dd-directory-", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( path );
		try {
			Assert.Equal(
				0,
				await Tool.RunAsync(
					[ string.Concat( "if=", path ), "iflag=directory", "count=0", "status=none" ],
					Context( new MemoryStream(), new MemoryStream() )
				)
			);
		} finally {
			Directory.Delete( path );
		}
	}

	[Fact]
	public async Task UnsupportedPortableFlagFailsCleanly() {
		var error = new StringWriter();
		Assert.Equal(
			1,
			await Tool.RunAsync(
				[ "iflag=direct", "status=none" ],
				Context( new MemoryStream(), new MemoryStream(), error )
			)
		);
		Assert.Contains( "not supported", error.ToString(), StringComparison.Ordinal );
	}

	private static CommandContext Context(
		Stream input,
		Stream output,
		TextWriter? error = null,
		CancellationToken cancellationToken = default
	) => new(
		"dd",
		TextReader.Null,
		TextWriter.Null,
		error ?? new StringWriter(),
		input,
		output,
		null,
		cancellationToken
	);

	private static string TemporaryPath() => System.IO.Path.Combine(
		System.IO.Path.GetTempPath(),
		string.Concat(
			"dd-tests-",
			Guid.NewGuid().ToString( "N" )
		)
	);

	private sealed class ChunkedReadStream : MemoryStream {
		private readonly int myMaximumRead;

		public ChunkedReadStream(
			byte[] data,
			int maximumRead
		) : base( data ) {
			this.myMaximumRead = maximumRead;
		}

		public override ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) => base.ReadAsync(
			buffer.Slice(
				0,
				Math.Min(
					buffer.Length,
					this.myMaximumRead
				)
			),
			cancellationToken
		);
	}

	private sealed class OneFailureReadStream : MemoryStream {
		private bool myFailed;

		public OneFailureReadStream(
			byte[] data
		) : base( data ) {
		}

		public override ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			if ( !this.myFailed ) {
				this.myFailed = true;
				throw new IOException(
					"synthetic read failure"
				);
			}
			return base.ReadAsync(
				buffer,
				cancellationToken
			);
		}
	}

	private sealed class CancellableInputStream : Stream {
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position {
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override void Flush() {
		}

		public override int Read(
			byte[] buffer,
			int offset,
			int count
		) => throw new NotSupportedException();

		public override async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			await Task.Delay(
				Timeout.InfiniteTimeSpan,
				cancellationToken
			);
			return 0;
		}

		public override long Seek(
			long offset,
			SeekOrigin origin
		) => throw new NotSupportedException();

		public override void SetLength(
			long value
		) => throw new NotSupportedException();

		public override void Write(
			byte[] buffer,
			int offset,
			int count
		) => throw new NotSupportedException();
	}
}
