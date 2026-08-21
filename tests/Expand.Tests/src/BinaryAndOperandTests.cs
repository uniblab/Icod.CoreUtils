namespace Icod.CoreUtils.Expand.Tests;

using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests binary fidelity, operand continuity, ownership, and failures for <c>expand</c>.</summary>
public sealed class BinaryAndOperandTests {
	/// <summary>Verifies preservation of byte-order marks, malformed bytes, NUL, and final termination.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task BomMalformedBytesAndNulArePreserved() {
		var input = new byte[] { 0xEF, 0xBB, 0xBF, 0xFF, 0x00, (byte)'x' };
		var result = await RunAsync( [ ], input );
		Assert.Equal( input, result.Output );
	}

	/// <summary>Verifies that unterminated operands share one logical line.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task UnterminatedOperandsShareLogicalLineState() {
		var directory = Directory.CreateTempSubdirectory();
		try {
			var first = System.IO.Path.Combine( directory.FullName, "first" );
			var second = System.IO.Path.Combine( directory.FullName, "second" );
			await File.WriteAllBytesAsync( first, "1234"u8.ToArray() );
			await File.WriteAllBytesAsync( second, "\tX"u8.ToArray() );
			var result = await RunAsync( [ first, second ], [ ] );
			Assert.Equal( "1234    X"u8.ToArray(), result.Output );
		} finally {
			directory.Delete( recursive: true );
		}
	}

	/// <summary>Verifies that an unreadable operand does not prevent later operands from running.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task MissingFileReportsFailureButLaterOperandsContinue() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllTextAsync( file, "x" );
			var result = await RunAsync( [ string.Concat( file, ".missing" ), file ], [ ] );
			Assert.Equal( CommandExitCodes.Failure, result.Status );
			Assert.Equal( "x"u8.ToArray(), result.Output );
			Assert.Contains( ".missing", result.Error );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies that file-only execution does not require a binary standard-input stream.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task FileOnlyExecutionDoesNotRequireStandardInputStream() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, "x\ty"u8.ToArray() );
			using var output = new MemoryStream();
			var context = new CommandContext( "expand", new StringReader( string.Empty ), new StringWriter(), new StringWriter(), null, output );
			var status = await Command.RunAsync( [ file ], context );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.Equal( "x       y"u8.ToArray(), output.ToArray() );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies that caller-owned binary streams remain open.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task CallerOwnedBinaryStreamsRemainOpen() {
		using var input = new MemoryStream( "x"u8.ToArray() );
		using var output = new MemoryStream();
		var context = new CommandContext( "expand", new StringReader( string.Empty ), new StringWriter(), new StringWriter(), input, output );
		await Command.RunAsync( [ ], context );
		Assert.True( input.CanRead );
		Assert.True( output.CanWrite );
	}

	/// <summary>Verifies controlled read and write failure diagnostics.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ReadAndWriteFailuresReturnFailure() {
		using var failingInput = new FailingReadStream( "x"u8.ToArray() );
		using var partialOutput = new MemoryStream();
		var readError = new StringWriter();
		var readContext = new CommandContext( "expand", new StringReader( string.Empty ), new StringWriter(), readError, failingInput, partialOutput );
		var readStatus = await Command.RunAsync( [ ], readContext );
		Assert.Equal( CommandExitCodes.Failure, readStatus );
		Assert.Equal( "x"u8.ToArray(), partialOutput.ToArray() );
		Assert.Contains( "simulated read failure", readError.ToString() );

		using var input = new MemoryStream( "\t"u8.ToArray() );
		using var output = new ThrowingWriteStream();
		var writeError = new StringWriter();
		var writeContext = new CommandContext( "expand", new StringReader( string.Empty ), new StringWriter(), writeError, input, output );
		var writeStatus = await Command.RunAsync( [ ], writeContext );
		Assert.Equal( CommandExitCodes.Failure, writeStatus );
		Assert.Contains( "write failed", writeError.ToString() );
	}

	private static async Task<(int Status, byte[] Output, string Error)> RunAsync( string[] args, byte[] input ) {
		using var inputStream = new MemoryStream( input, writable: false );
		using var outputStream = new MemoryStream();
		var error = new StringWriter();
		var context = new CommandContext( "expand", new StringReader( string.Empty ), new StringWriter(), error, inputStream, outputStream );
		var status = await Command.RunAsync( args, context );
		return ( status, outputStream.ToArray(), error.ToString() );
	}

	private sealed class FailingReadStream : MemoryStream {
		/// <summary>Initializes a stream that returns a prefix and then fails.</summary>
		/// <param name="prefix">The readable prefix.</param>
		internal FailingReadStream( byte[] prefix ) : base( prefix, writable: false ) {
		}

		/// <inheritdoc/>
		public override ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) {
			return this.Position < this.Length
				? base.ReadAsync( buffer, cancellationToken )
				: ValueTask.FromException<int>( new IOException( "simulated read failure" ) );
		}
	}

	private sealed class ThrowingWriteStream : MemoryStream {
		/// <inheritdoc/>
		public override ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) {
			return ValueTask.FromException( new IOException( "simulated write failure" ) );
		}
	}
}
