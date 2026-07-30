namespace Icod.CoreUtils.Shared.Tests.Records;

using Icod.CoreUtils.Shared.Records;
using Xunit;

/// <summary>Tests propagation of record I/O failures and cancellation.</summary>
public sealed class RecordFailureTests {

	/// <summary>Verifies that source read failures are not converted into end of input.</summary>
	[Fact]
	public async Task ReaderPropagatesReadFailure() {
		using var input = new ReadFailureStream();
		using var reader = new DelimitedByteRecordSegmentReader( input );
		await Assert.ThrowsAsync<IOException>(
			async () => {
				await reader.ReadAsync();
			}
		);
	}

	/// <summary>Verifies that destination write failures propagate to the caller.</summary>
	[Fact]
	public async Task WriterPropagatesWriteFailure() {
		using var output = new WriteFailureStream();
		var writer = new DelimitedByteRecordWriter( output );
		await Assert.ThrowsAsync<IOException>(
			async () => {
				await writer.WriteContentAsync( new byte[] { 1 } );
			}
		);
	}

	/// <summary>Verifies that writer operations honor pre-requested cancellation.</summary>
	[Fact]
	public async Task WriterHonorsCancellation() {
		using var output = new MemoryStream();
		var writer = new DelimitedByteRecordWriter( output );
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			async () => {
				await writer.WriteSeparatorAsync( cancellation.Token );
			}
		);
		Assert.Empty( output.ToArray() );
	}

	private sealed class ReadFailureStream : Stream {
		/// <inheritdoc/>
		public override bool CanRead => true;
		/// <inheritdoc/>
		public override bool CanSeek => false;
		/// <inheritdoc/>
		public override bool CanWrite => false;
		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();
		/// <inheritdoc/>
		public override long Position {
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}
		/// <inheritdoc/>
		public override void Flush() {
		}
		/// <inheritdoc/>
		public override int Read( byte[] buffer, int offset, int count ) => throw new IOException( "read failure" );
		/// <inheritdoc/>
		public override ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) => ValueTask.FromException<int>( new IOException( "read failure" ) );
		/// <inheritdoc/>
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override void SetLength( long value ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override void Write( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
	}

	private sealed class WriteFailureStream : Stream {
		/// <inheritdoc/>
		public override bool CanRead => false;
		/// <inheritdoc/>
		public override bool CanSeek => false;
		/// <inheritdoc/>
		public override bool CanWrite => true;
		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();
		/// <inheritdoc/>
		public override long Position {
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}
		/// <inheritdoc/>
		public override void Flush() {
		}
		/// <inheritdoc/>
		public override int Read( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override void SetLength( long value ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override void Write( byte[] buffer, int offset, int count ) => throw new IOException( "write failure" );
		/// <inheritdoc/>
		public override ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) => ValueTask.FromException( new IOException( "write failure" ) );
	}

}
