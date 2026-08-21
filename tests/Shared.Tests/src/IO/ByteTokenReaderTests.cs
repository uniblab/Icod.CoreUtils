namespace Icod.CoreUtils.Shared.Tests.IO;

using System.Text;
using Icod.CoreUtils.Shared.IO;
using Xunit;

/// <summary>Verifies separator-set byte tokenization.</summary>
public sealed class ByteTokenReaderTests {
	private static readonly byte[] Separators = [ (byte)' ', (byte)'\t', (byte)'\n' ];

	/// <summary>Verifies that only configured bytes separate tokens.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task OnlyConfiguredBytesSeparateTokens() {
		using var input = new MemoryStream( Encoding.UTF8.GetBytes( "a\rb a\vb a\fb" ) );
		using var reader = new ByteTokenReader( input, Separators, bufferSize: 2 );
		Assert.Equal( "a\rb", Encoding.UTF8.GetString( (await reader.ReadTokenAsync())! ) );
		Assert.Equal( "a\vb", Encoding.UTF8.GetString( (await reader.ReadTokenAsync())! ) );
		Assert.Equal( "a\fb", Encoding.UTF8.GetString( (await reader.ReadTokenAsync())! ) );
		Assert.Null( await reader.ReadTokenAsync() );
	}

	/// <summary>Verifies tokens that cross one-byte read boundaries.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TokensCrossReadBoundaries() {
		using var input = new MemoryStream( Encoding.UTF8.GetBytes( "  alpha\tbeta\n" ) );
		using var reader = new ByteTokenReader( input, Separators, bufferSize: 1 );
		Assert.Equal( "alpha", Encoding.UTF8.GetString( (await reader.ReadTokenAsync())! ) );
		Assert.Equal( "beta", Encoding.UTF8.GetString( (await reader.ReadTokenAsync())! ) );
		Assert.Null( await reader.ReadTokenAsync() );
	}

	/// <summary>Verifies exact preservation of arbitrary non-separator bytes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task PreservesArbitraryBytes() {
		byte[] value = [ 0, 0x80, 0xff, (byte)'x' ];
		using var input = new MemoryStream( value );
		using var reader = new ByteTokenReader( input, Separators );
		Assert.Equal( value, (await reader.ReadTokenAsync())! );
	}

	/// <summary>Verifies that an empty separator set returns the remaining stream as one token.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task EmptySeparatorSetReturnsOneToken() {
		byte[] expected = [ (byte)'a', (byte)' ', (byte)'b', (byte)'\n' ];
		using var input = new MemoryStream( expected );
		using var reader = new ByteTokenReader( input, Array.Empty<byte>(), bufferSize: 2 );
		Assert.Equal( expected, (await reader.ReadTokenAsync())! );
		Assert.Null( await reader.ReadTokenAsync() );
	}

	/// <summary>Verifies cancellation before a read begins.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task HonorsCancellation() {
		using var input = new MemoryStream( Encoding.UTF8.GetBytes( "a" ) );
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		await Assert.ThrowsAsync<OperationCanceledException>( async () => {
			_ = await reader.ReadTokenAsync( cancellation.Token );
		} );
	}

	/// <summary>Verifies cancellation interrupts an active asynchronous stream read.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CancelsBlockedRead() {
		using var input = new BlockingReadStream();
		var read = reader.ReadTokenAsync( cancellation.Token ).AsTask();
		await input.ReadStarted;
		cancellation.Cancel();
		var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>( async () => {
			_ = await read;
		} );
		Assert.Equal( cancellation.Token, exception.CancellationToken );
	}

	/// <summary>Verifies bounded incremental reading of a token much larger than the read buffer.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReadsLongTokenIncrementally() {
		var expected = new string( 'x', 100_000 );
		using var input = new MemoryStream( Encoding.UTF8.GetBytes( expected ) );
		using var reader = new ByteTokenReader( input, Separators, bufferSize: 31 );
		Assert.Equal( expected, Encoding.UTF8.GetString( (await reader.ReadTokenAsync())! ) );
		Assert.Null( await reader.ReadTokenAsync() );
	}

	/// <summary>Verifies that reads after disposal fail predictably.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RejectsReadsAfterDisposal() {
		var reader = new ByteTokenReader( input, Separators );
		reader.Dispose();
		await Assert.ThrowsAsync<ObjectDisposedException>( async () => {
			_ = await reader.ReadTokenAsync();
		} );
	}

	/// <summary>Verifies that disposing the reader does not dispose its input.</summary>
	[Fact]
	public void DoesNotOwnInputStream() {
		using var input = new MemoryStream();
		var reader = new ByteTokenReader( input, Separators );
		reader.Dispose();
		Assert.True( input.CanRead );
	}

	private sealed class BlockingReadStream : Stream {
		private readonly TaskCompletionSource<bool> myReadStarted = new( TaskCreationOptions.RunContinuationsAsynchronously );

		/// <summary>Gets a task completed when an asynchronous read begins.</summary>
		internal Task ReadStarted => this.myReadStarted.Task;

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
		public override int Read( byte[] buffer, int offset, int count ) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override async ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) {
			this.myReadStarted.TrySetResult( true );
			await Task.Delay( Timeout.Infinite, cancellationToken );
			return 0;
		}

		/// <inheritdoc/>
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void SetLength( long value ) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void Write( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
	}
}
