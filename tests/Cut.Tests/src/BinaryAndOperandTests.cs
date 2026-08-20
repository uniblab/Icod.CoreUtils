namespace Icod.CoreUtils.Cut.Tests;

using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests exact bytes, ordered operands, ownership, cancellation, and failures.</summary>
public sealed class BinaryAndOperandTests {
	/// <summary>Verifies malformed bytes remain selectable character units.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task PreservesMalformedCharacterBytes() {
		var result = await RunAsync( [ "-c", "2" ], [ (byte)'a', 0xFF, (byte)'b', (byte)'\n' ] );
		Assert.Equal( new byte[] { 0xFF, (byte)'\n' }, result.Output );
	}

	/// <summary>Verifies ordered file and standard-input operands.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ProcessesFilesAndStandardInputInOrder() {
		var first = System.IO.Path.GetTempFileName();
		var second = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( first, "a1\n"u8.ToArray() );
			await File.WriteAllBytesAsync( second, "c3\n"u8.ToArray() );
			var result = await RunAsync( [ "-b", "1", first, "-", second ], "b2\n"u8.ToArray() );
			Assert.Equal( "a\nb\nc\n"u8.ToArray(), result.Output );
		} finally {
			File.Delete( first );
			File.Delete( second );
		}
	}

	/// <summary>Verifies repeated standard input is not rewound.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RepeatedStandardInputUsesOnePosition() {
		var result = await RunAsync( [ "-b", "1-", "-", "-" ], "once\n"u8.ToArray() );
		Assert.Equal( "once\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies a missing file does not prevent later operands.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task MissingFileDoesNotSuppressLaterOutput() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, "later\n"u8.ToArray() );
			var result = await RunAsync( [ "-b", "1-", string.Concat( file, ".missing" ), file ], [] );
			Assert.Equal( CommandExitCodes.Failure, result.Status );
			Assert.Equal( "later\n"u8.ToArray(), result.Output );
			Assert.Contains( ".missing", result.Error );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies caller-owned standard streams remain open.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task LeavesInjectedStreamsOpen() {
		using var input = new MemoryStream( "x\n"u8.ToArray() );
		using var output = new MemoryStream();
		var context = new CommandContext( "cut", new StringReader( string.Empty ), new StringWriter(), new StringWriter(), input, output );
		_ = await Command.RunAsync( [ "-b", "1" ], context );
		Assert.True( input.CanRead );
		Assert.True( output.CanWrite );
	}

	/// <summary>Verifies controlled read-failure diagnostics.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsReadFailures() {
		using var input = new ThrowingReadStream();
		using var output = new MemoryStream();
		var error = new StringWriter();
		var context = new CommandContext( "cut", new StringReader( string.Empty ), new StringWriter(), error, input, output );
		var status = await Command.RunAsync( [ "-b", "1" ], context );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "simulated read failure", error.ToString() );
	}

	/// <summary>Verifies controlled output-failure diagnostics.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsWriteFailures() {
		using var input = new MemoryStream( "x\n"u8.ToArray() );
		using var output = new ThrowingWriteStream();
		var error = new StringWriter();
		var context = new CommandContext( "cut", new StringReader( string.Empty ), new StringWriter(), error, input, output );
		var status = await Command.RunAsync( [ "-b", "1" ], context );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "write failed", error.ToString() );
	}

	/// <summary>Verifies cancellation returns the repository cancellation status.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task HonorsCancellation() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		using var input = new MemoryStream( "x\n"u8.ToArray() );
		using var output = new MemoryStream();
		var context = new CommandContext( "cut", new StringReader( string.Empty ), new StringWriter(), new StringWriter(), input, output, cancellationToken: cancellation.Token );
		var status = await Command.RunAsync( [ "-b", "1" ], context );
		Assert.Equal( CommandExitCodes.Canceled, status );
	}

	private static async Task<(int Status, byte[] Output, string Error)> RunAsync( string[] args, byte[] input ) {
		using var inputStream = new MemoryStream( input, writable: false );
		using var outputStream = new MemoryStream();
		var error = new StringWriter();
		var context = new CommandContext( "cut", new StringReader( string.Empty ), new StringWriter(), error, inputStream, outputStream );
		var status = await Command.RunAsync( args, context );
		return (status, outputStream.ToArray(), error.ToString());
	}

	private sealed class ThrowingReadStream : Stream {
		/// <inheritdoc/>
		public override bool CanRead => true;
		/// <inheritdoc/>
		public override bool CanSeek => false;
		/// <inheritdoc/>
		public override bool CanWrite => false;
		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();
		/// <inheritdoc/>
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		/// <inheritdoc/>
		public override void Flush() { }
		/// <inheritdoc/>
		public override int Read( byte[] buffer, int offset, int count ) => throw new IOException( "simulated read failure" );
		/// <inheritdoc/>
		public override ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) => ValueTask.FromException<int>( new IOException( "simulated read failure" ) );
		/// <inheritdoc/>
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override void SetLength( long value ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override void Write( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
	}

	private sealed class ThrowingWriteStream : Stream {
		/// <inheritdoc/>
		public override bool CanRead => false;
		/// <inheritdoc/>
		public override bool CanSeek => false;
		/// <inheritdoc/>
		public override bool CanWrite => true;
		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();
		/// <inheritdoc/>
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		/// <inheritdoc/>
		public override void Flush() { }
		/// <inheritdoc/>
		public override int Read( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override void SetLength( long value ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override void Write( byte[] buffer, int offset, int count ) => throw new IOException( "simulated write failure" );
		/// <inheritdoc/>
		public override ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) => ValueTask.FromException( new IOException( "simulated write failure" ) );
	}
}
