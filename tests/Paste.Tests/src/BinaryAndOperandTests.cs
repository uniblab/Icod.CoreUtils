namespace Icod.CoreUtils.Paste.Tests;

using System.Text;
using Icod.CommandFramework.Diagnostics;
using Xunit;

/// <summary>Tests binary preservation, repeated standard input, operands, failures, and ownership.</summary>
public sealed class BinaryAndOperandTests {
	/// <summary>Verifies repeated standard-input operands consume successive records in parallel.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RepeatedStandardInputSharesOneReader() {
		var result = await RunAsync( [ "-", "-" ], "a\nb\n"u8.ToArray() );
		Assert.Equal( Generated( "a\tb" ), result.Output );
	}

	/// <summary>Verifies serial repeated standard input consumes the stream once and then emits an empty operand line.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SerialRepeatedStandardInputIsNotRewound() {
		var result = await RunAsync( [ "-s", "-", "-" ], "a\nb\n"u8.ToArray() );
		Assert.Equal(
			Encoding.UTF8.GetBytes( string.Concat( "a\tb", Environment.NewLine, Environment.NewLine ) ),
			result.Output
		);
	}

	/// <summary>Verifies an empty serial operand emits one generated terminator while empty parallel input emits nothing.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task EmptyInputDiffersBetweenSerialAndParallelModes() {
		var parallel = await RunAsync( [ ], [] );
		var serial = await RunAsync( [ "-s" ], [] );
		Assert.Empty( parallel.Output );
		Assert.Equal( Encoding.UTF8.GetBytes( Environment.NewLine ), serial.Output );
	}

	/// <summary>Verifies an unterminated final input record receives a generated terminator.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TerminatesUnterminatedOutputRows() {
		var result = await RunAsync( [ ], "abc"u8.ToArray() );
		Assert.Equal( Encoding.UTF8.GetBytes( string.Concat( "abc", Environment.NewLine ) ), result.Output );
	}

	/// <summary>Verifies malformed bytes and multibyte delimiter characters remain exact.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task PreservesArbitraryBytesAndMultibyteDelimiters() {
		var result = await RunAsync( [ "-d", "界", "-", "-" ], [ 0xFF, (byte)'\n', 0xFE, (byte)'\n' ] );
		Assert.Equal(
			new byte[] { 0xFF, 0xE7, 0x95, 0x8C, 0xFE }.Concat( Encoding.UTF8.GetBytes( Environment.NewLine ) ).ToArray(),
			result.Output
		);
	}

	/// <summary>Verifies large records are copied without changing their content.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task StreamsLargeRecords() {
		var input = Enumerable.Repeat( (byte)'x', 100_000 ).Append( (byte)'\n' ).ToArray();
		var result = await RunAsync( [ ], input );
		Assert.Equal( input.AsSpan( 0, input.Length - 1 ).ToArray().Concat( Encoding.UTF8.GetBytes( Environment.NewLine ) ).ToArray(), result.Output );
	}

	/// <summary>Verifies a parallel open failure prevents output.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ParallelOpenFailureProducesNoRows() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, "later\n"u8.ToArray() );
			var result = await RunAsync( [ string.Concat( file, ".missing" ), file ], [] );
			Assert.Equal( CommandExitCodes.Failure, result.Status );
			Assert.Empty( result.Output );
			Assert.Contains( ".missing", result.Error );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies serial mode reports a missing operand and continues with later operands.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SerialOpenFailureContinues() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, "later\n"u8.ToArray() );
			var result = await RunAsync( [ "-s", string.Concat( file, ".missing" ), file ], [] );
			Assert.Equal( CommandExitCodes.Failure, result.Status );
			Assert.Equal( Generated( "later" ), result.Output );
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
		var context = new CommandContext( "paste", new StringReader( string.Empty ), new StringWriter(), new StringWriter(), input, output );
		_ = await Command.RunAsync( [ ], context );
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
		var context = new CommandContext( "paste", new StringReader( string.Empty ), new StringWriter(), error, input, output );
		var status = await Command.RunAsync( [ ], context );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "simulated read failure", error.ToString() );
	}

	/// <summary>Verifies parallel mode reports a read failure, retires that column, and continues other columns.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ParallelReadFailureContinuesOtherColumns() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, "later\n"u8.ToArray() );
			using var input = new ThrowingReadStream();
			using var output = new MemoryStream();
			var error = new StringWriter();
			var context = new CommandContext( "paste", new StringReader( string.Empty ), new StringWriter(), error, input, output );
			var status = await Command.RunAsync( [ "-", file ], context );
			Assert.Equal( CommandExitCodes.Failure, status );
			Assert.Equal( Encoding.UTF8.GetBytes( string.Concat( "\tlater", Environment.NewLine ) ), output.ToArray() );
			Assert.Contains( "simulated read failure", error.ToString() );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies serial mode terminates a failed operand and continues later operands.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SerialReadFailureTerminatesOperandAndContinues() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, "later\n"u8.ToArray() );
			using var input = new ThrowingReadStream();
			using var output = new MemoryStream();
			var error = new StringWriter();
			var context = new CommandContext( "paste", new StringReader( string.Empty ), new StringWriter(), error, input, output );
			var status = await Command.RunAsync( [ "-s", "-", file ], context );
			Assert.Equal( CommandExitCodes.Failure, status );
			Assert.Equal(
				Encoding.UTF8.GetBytes( string.Concat( Environment.NewLine, "later", Environment.NewLine ) ),
				output.ToArray()
			);
			Assert.Contains( "simulated read failure", error.ToString() );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies controlled output-failure diagnostics.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsWriteFailures() {
		using var input = new MemoryStream( "x\n"u8.ToArray() );
		using var output = new ThrowingWriteStream();
		var error = new StringWriter();
		var context = new CommandContext( "paste", new StringReader( string.Empty ), new StringWriter(), error, input, output );
		var status = await Command.RunAsync( [ ], context );
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
		var context = new CommandContext( "paste", new StringReader( string.Empty ), new StringWriter(), new StringWriter(), input, output, cancellationToken: cancellation.Token );
		var status = await Command.RunAsync( [ ], context );
		Assert.Equal( CommandExitCodes.Canceled, status );
	}

	private static byte[] Generated( params string[] lines ) => Encoding.UTF8.GetBytes( string.Concat( string.Join( Environment.NewLine, lines ), Environment.NewLine ) );

	private static async Task<(int Status, byte[] Output, string Error)> RunAsync( string[] args, byte[] input ) {
		using var inputStream = new MemoryStream( input, writable: false );
		using var outputStream = new MemoryStream();
		var error = new StringWriter();
		var context = new CommandContext( "paste", new StringReader( string.Empty ), new StringWriter(), error, inputStream, outputStream );
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
