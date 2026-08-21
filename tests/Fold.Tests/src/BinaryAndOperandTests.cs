namespace Icod.CoreUtils.Fold.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests binary fidelity, bounded buffering, operand boundaries, ownership, and failures for <c>fold</c>.</summary>
public sealed class BinaryAndOperandTests {
	/// <summary>Verifies BOM, malformed bytes, NUL, and missing final-newline fidelity.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task BinaryInputIsPreservedExactly() {
		var input = new byte[] { 0xEF, 0xBB, 0xBF, 0xFF, 0x00, (byte)'x' };
		var result = await RunAsync( [ "--width=80" ], input );
		Assert.Equal( input, result.Output );
	}

	/// <summary>Verifies that each malformed UTF-8 byte is preserved and counted as one unit.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task InvalidByteCountsAsOneDisplayColumn() {
		var result = await RunAsync( [ "--width=1" ], new byte[] { 0xFF, (byte)'x' } );
		Assert.Equal( Combine( new byte[] { 0xFF }, Newline, "x"u8.ToArray() ), result.Output );
	}

	/// <summary>Verifies that a scalar wider than the requested width is never split.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ACharacterWiderThanTheLimitIsNotSplit() {
		var input = Encoding.UTF8.GetBytes( "界界" );
		var result = await RunAsync( [ "--width=1" ], input );
		Assert.Equal( Combine( Encoding.UTF8.GetBytes( "界" ), Newline, Encoding.UTF8.GetBytes( "界" ) ), result.Output );
	}

	/// <summary>Verifies that a scalar split across input-buffer reads remains indivisible.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task SplitUtf8ScalarIsDecodedIncrementally() {
		var original = Environment.GetEnvironmentVariable( "LC_ALL" );
		Environment.SetEnvironmentVariable( "LC_ALL", "C.UTF-8" );
		try {
			using var input = new OneByteReadStream( Encoding.UTF8.GetBytes( "a界b" ) );
			using var output = new MemoryStream();
			var context = CreateContext( input, output, new StringWriter() );
			var status = await Command.RunAsync( [ "--width=3" ], context );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.Equal( Combine( Encoding.UTF8.GetBytes( "a界" ), Newline, "b"u8.ToArray() ), output.ToArray() );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", original );
		}
	}

	/// <summary>Verifies bounded buffering for a long sequence that does not advance display columns.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ZeroWidthInputUsesBoundedBufferWithoutArtificialBreaks() {
		var input = Encoding.UTF8.GetBytes( string.Concat( "a", new string( '\u0301', 70000 ) ) );
		var result = await RunAsync( [ "--width=1" ], input );
		Assert.Equal( input, result.Output );
	}

	/// <summary>Verifies fresh columns per operand while preserving GNU's last-character-width state.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task OperandColumnStateResetsButLastCharacterWidthPersists() {
		var directory = Directory.CreateTempSubdirectory();
		try {
			var first = System.IO.Path.Combine( directory.FullName, "first" );
			var second = System.IO.Path.Combine( directory.FullName, "second" );
			await File.WriteAllBytesAsync( first, Encoding.UTF8.GetBytes( "界" ) );
			await File.WriteAllBytesAsync( second, "\t\bX"u8.ToArray() );
			var result = await RunAsync( [ "--width=8", first, second ], [ ] );
			Assert.Equal( Combine( Encoding.UTF8.GetBytes( "界" ), "\t\bX"u8.ToArray() ), result.Output );
		} finally {
			directory.Delete( recursive: true );
		}
	}

	/// <summary>Verifies that missing operands report failure while later operands still run.</summary>
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

	/// <summary>Verifies that file-only invocations do not require a binary standard-input stream.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task FileOnlyInvocationDoesNotRequireStandardInput() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllTextAsync( file, "abcdef" );
			using var output = new MemoryStream();
			var context = new CommandContext(
				"fold",
				new StringReader( string.Empty ),
				new StringWriter(),
				new StringWriter(),
				null,
				output
			);
			var status = await Command.RunAsync( [ "-4", file ], context );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.Equal( Combine( "abcd"u8.ToArray(), Newline, "ef"u8.ToArray() ), output.ToArray() );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies caller ownership of injected standard streams.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task CallerOwnedStreamsRemainOpen() {
		using var input = new MemoryStream( "abcdef"u8.ToArray() );
		using var output = new MemoryStream();
		var context = CreateContext( input, output, new StringWriter() );
		var status = await Command.RunAsync( [ "-4" ], context );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.True( input.CanRead );
		Assert.True( output.CanWrite );
	}

	/// <summary>Verifies controlled diagnostics for input and output failures.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ReadAndWriteFailuresAreControlled() {
		using var failingInput = new FailingReadStream( "abc"u8.ToArray() );
		using var readOutput = new MemoryStream();
		var readError = new StringWriter();
		var readStatus = await Command.RunAsync( [ ], CreateContext( failingInput, readOutput, readError ) );
		Assert.Equal( CommandExitCodes.Failure, readStatus );
		Assert.Equal( "abc"u8.ToArray(), readOutput.ToArray() );
		Assert.Contains( "simulated read failure", readError.ToString() );

		using var input = new MemoryStream( "abcdef"u8.ToArray() );
		using var failingOutput = new ThrowingWriteStream();
		var writeError = new StringWriter();
		var writeStatus = await Command.RunAsync( [ "-4" ], CreateContext( input, failingOutput, writeError ) );
		Assert.Equal( CommandExitCodes.Failure, writeStatus );
		Assert.Contains( "write failed", writeError.ToString() );
	}

	private static byte[] Newline => Encoding.UTF8.GetBytes( Environment.NewLine );

	private static byte[] Combine( params byte[][] values ) {
		return values.SelectMany( value => value ).ToArray();
	}

	private static CommandContext CreateContext( Stream input, Stream output, TextWriter error ) {
		return new CommandContext(
			"fold",
			new StringReader( string.Empty ),
			new StringWriter(),
			error,
			input,
			output
		);
	}

	private static async Task<(int Status, byte[] Output, string Error)> RunAsync( string[] args, byte[] input ) {
		var original = Environment.GetEnvironmentVariable( "LC_ALL" );
		Environment.SetEnvironmentVariable( "LC_ALL", "C.UTF-8" );
		try {
			using var inputStream = new MemoryStream( input, writable: false );
			using var outputStream = new MemoryStream();
			var error = new StringWriter();
			var status = await Command.RunAsync( args, CreateContext( inputStream, outputStream, error ) );
			return ( status, outputStream.ToArray(), error.ToString() );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", original );
		}
	}

	private sealed class OneByteReadStream : MemoryStream {
		/// <summary>Initializes a stream that returns at most one byte per asynchronous read.</summary>
		/// <param name="value">The source bytes.</param>
		internal OneByteReadStream( byte[] value ) : base( value, writable: false ) {
		}

		/// <inheritdoc/>
		public override ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) {
			return base.ReadAsync( buffer[..Math.Min( 1, buffer.Length )], cancellationToken );
		}
	}

	private sealed class FailingReadStream : MemoryStream {
		private bool myHasReturnedPrefix;

		/// <summary>Initializes a stream that returns a prefix and then fails.</summary>
		/// <param name="value">The readable prefix.</param>
		internal FailingReadStream( byte[] value ) : base( value, writable: false ) {
		}

		/// <inheritdoc/>
		public override ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) {
			if ( this.myHasReturnedPrefix ) {
				return ValueTask.FromException<int>( new IOException( "simulated read failure" ) );
			}
			this.myHasReturnedPrefix = true;
			return base.ReadAsync( buffer, cancellationToken );
		}
	}

	private sealed class ThrowingWriteStream : MemoryStream {
		/// <inheritdoc/>
		public override ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) {
			return ValueTask.FromException( new IOException( "simulated write failure" ) );
		}
	}
}
