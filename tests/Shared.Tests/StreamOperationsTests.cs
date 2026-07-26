namespace Icod.CoreUtils.Shared.Tests;

using Icod.CoreUtils.Shared.IO;
using Xunit;

public sealed class StreamOperationsTests {

	[Fact]
	public async Task CopyAsyncHandlesShortReads() {
		var bytes = Enumerable.Range(
			0,
			10000
		).Select(
			value => (byte)( value % 251 )
		).ToArray();
		await using var input = new ChunkedReadStream(
			bytes,
			maximumChunk: 7
		);
		await using var output = new MemoryStream();

		var copied = await StreamOperations.CopyAsync(
			input,
			output,
			bufferSize: 113
		);

		Assert.Equal( bytes.Length, copied );
		Assert.Equal( bytes, output.ToArray() );
	}

	[Fact]
	public async Task CopyCountStopsAtRequestedCount() {
		await using var input = new MemoryStream(
			Enumerable.Range( 0, 100 ).Select( value => (byte)value ).ToArray()
		);
		await using var output = new MemoryStream();

		var copied = await StreamOperations.CopyCountAsync(
			input,
			output,
			25
		);

		Assert.Equal( 25, copied );
		Assert.Equal( 25, output.Length );
		Assert.Equal( 25, input.Position );
	}

	[Fact]
	public async Task SkipUsesSeekWhenAvailable() {
		await using var input = new MemoryStream(
			new byte[ 100 ]
		);

		var skipped = await StreamOperations.SkipAsync(
			input,
			40
		);

		Assert.Equal( 40, skipped );
		Assert.Equal( 40, input.Position );
	}

	[Fact]
	public async Task SkipStreamsWhenNotSeekable() {
		await using var input = new ChunkedReadStream(
			new byte[ 100 ],
			maximumChunk: 3
		);

		var skipped = await StreamOperations.SkipAsync(
			input,
			40,
			bufferSize: 11
		);

		Assert.Equal( 40, skipped );
	}

	[Fact]
	public async Task CopyRangeStreamsRequestedRange() {
		var bytes = Enumerable.Range(
			0,
			100
		).Select(
			value => (byte)value
		).ToArray();
		await using var input = new MemoryStream(
			bytes
		);
		await using var output = new MemoryStream();

		var copied = await StreamOperations.CopyRangeAsync(
			input,
			output,
			10,
			15
		);

		Assert.Equal( 15, copied );
		Assert.Equal( bytes.Skip( 10 ).Take( 15 ), output.ToArray() );
	}

	[Fact]
	public async Task ReadAtMostReturnsOnlyAvailableBytes() {
		await using var input = new MemoryStream(
			new byte[ 7 ] { 1, 2, 3, 4, 5, 6, 7 }
		);

		var result = await StreamOperations.ReadAtMostAsync(
			input,
			20
		);

		Assert.Equal( new byte[ 7 ] { 1, 2, 3, 4, 5, 6, 7 }, result );
	}

	[Fact]
	public async Task CopyHonorsCancellation() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		await using var input = new MemoryStream(
			new byte[ 1024 ]
		);
		await using var output = new MemoryStream();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => StreamOperations.CopyAsync(
				input,
				output,
				cancellationToken: cancellation.Token
			)
		);
	}

	private sealed class ChunkedReadStream : Stream {

		private readonly byte[] myBytes;
		private readonly int myMaximumChunk;
		private int myOffset;

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => this.myBytes.Length;
		public override long Position {
			get => this.myOffset;
			set => throw new NotSupportedException();
		}

		public ChunkedReadStream(
			byte[] bytes,
			int maximumChunk
		) {
			this.myBytes = bytes;
			this.myMaximumChunk = maximumChunk;
		}

		public override int Read(
			byte[] buffer,
			int offset,
			int count
		) {
			var read = Math.Min(
				Math.Min( count, this.myMaximumChunk ),
				this.myBytes.Length - this.myOffset
			);
			if ( 0 < read ) {
				Array.Copy(
					this.myBytes,
					this.myOffset,
					buffer,
					offset,
					read
				);
				this.myOffset += read;
			}
			return read;
		}

		public override ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			var read = Math.Min(
				Math.Min( buffer.Length, this.myMaximumChunk ),
				this.myBytes.Length - this.myOffset
			);
			if ( 0 < read ) {
				this.myBytes.AsMemory(
					this.myOffset,
					read
				).CopyTo(
					buffer
				);
				this.myOffset += read;
			}
			return ValueTask.FromResult(
				read
			);
		}

		public override void Flush() {
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
	}

}
