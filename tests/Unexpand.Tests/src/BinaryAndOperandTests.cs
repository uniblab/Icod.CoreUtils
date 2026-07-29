namespace Icod.CoreUtils.Unexpand.Tests;

using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests binary fidelity, operand continuity, ownership, and failures for <c>unexpand</c>.</summary>
public sealed class BinaryAndOperandTests {
	/// <summary>Verifies exact preservation of BOM, malformed bytes, NUL, and final termination.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task BomMalformedBytesAndNulArePreserved() {
		var input = new byte[] { 0xEF, 0xBB, 0xBF, 0xFF, 0x00, (byte)'x' };
		var result = await RunAsync( [ ], input );
		Assert.Equal( input, result.Output );
	}

	/// <summary>Verifies that backspace repositions later blank conversion.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task BackspaceRepositionsSubsequentBlankConversion() {
		var result = await RunAsync( [ "--all" ], "1234\b     x"u8.ToArray() );
		Assert.Equal( "1234\b\tx"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies that unterminated operands share one logical line and pending run.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task UnterminatedOperandsShareLogicalLineState() {
		var directory = Directory.CreateTempSubdirectory();
		try {
			var first = Path.Combine( directory.FullName, "first" );
			var second = Path.Combine( directory.FullName, "second" );
			await File.WriteAllBytesAsync( first, "    "u8.ToArray() );
			await File.WriteAllBytesAsync( second, "    x"u8.ToArray() );
			var result = await RunAsync( [ first, second ], [ ] );
			Assert.Equal( "\tx"u8.ToArray(), result.Output );
		} finally {
			directory.Delete( recursive: true );
		}
	}

	/// <summary>Verifies that an unreadable operand does not prevent later operands from running.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task MissingFileReportsFailureButLaterOperandsContinue() {
		var file = Path.GetTempFileName();
		try {
			await File.WriteAllTextAsync( file, "        x" );
			using var input = new MemoryStream();
			using var output = new MemoryStream();
			var error = new StringWriter();
			var context = new CommandContext( "unexpand", new StringReader( string.Empty ), new StringWriter(), error, input, output );
			var status = await Command.RunAsync( [ string.Concat( file, ".missing" ), file ], context );
			Assert.Equal( CommandExitCodes.Failure, status );
			Assert.Equal( "\tx"u8.ToArray(), output.ToArray() );
			Assert.Contains( ".missing", error.ToString() );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies that file-only execution does not require a binary standard-input stream.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task FileOnlyExecutionDoesNotRequireStandardInputStream() {
		var file = Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, "        x"u8.ToArray() );
			using var output = new MemoryStream();
			var context = new CommandContext( "unexpand", new StringReader( string.Empty ), new StringWriter(), new StringWriter(), null, output );
			var status = await Command.RunAsync( [ file ], context );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.Equal( "\tx"u8.ToArray(), output.ToArray() );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies that caller-owned streams remain open.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task CallerOwnedStreamsRemainOpen() {
		using var input = new MemoryStream( "        x"u8.ToArray() );
		using var output = new MemoryStream();
		var context = new CommandContext( "unexpand", new StringReader( string.Empty ), new StringWriter(), new StringWriter(), input, output );
		await Command.RunAsync( [ ], context );
		Assert.True( input.CanRead );
		Assert.True( output.CanWrite );
	}

	/// <summary>Verifies controlled read and write failure diagnostics.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ReadAndWriteFailuresReturnFailure() {
		using var failingInput = new FailingReadStream( "        "u8.ToArray() );
		using var partialOutput = new MemoryStream();
		var readError = new StringWriter();
		var readContext = new CommandContext( "unexpand", new StringReader( string.Empty ), new StringWriter(), readError, failingInput, partialOutput );
		var readStatus = await Command.RunAsync( [ ], readContext );
		Assert.Equal( CommandExitCodes.Failure, readStatus );
		Assert.Equal( "\t"u8.ToArray(), partialOutput.ToArray() );
		Assert.Contains( "simulated read failure", readError.ToString() );

		using var input = new MemoryStream( "        "u8.ToArray() );
		using var output = new ThrowingWriteStream();
		var writeError = new StringWriter();
		var writeContext = new CommandContext( "unexpand", new StringReader( string.Empty ), new StringWriter(), writeError, input, output );
		var writeStatus = await Command.RunAsync( [ ], writeContext );
		Assert.Equal( CommandExitCodes.Failure, writeStatus );
		Assert.Contains( "write failed", writeError.ToString() );
	}

	private static async Task<(int Status, byte[] Output)> RunAsync( string[] args, byte[] input ) {
		using var inputStream = new MemoryStream( input, writable: false );
		using var outputStream = new MemoryStream();
		var context = new CommandContext( "unexpand", new StringReader( string.Empty ), new StringWriter(), new StringWriter(), inputStream, outputStream );
		var status = await Command.RunAsync( args, context );
		return ( status, outputStream.ToArray() );
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
