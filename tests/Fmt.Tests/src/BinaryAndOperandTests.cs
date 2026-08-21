namespace Icod.CoreUtils.Fmt.Tests;

using System.Text;
using Icod.CommandFramework.Diagnostics;
using Xunit;

/// <summary>Tests binary preservation, operands, failures, and stream ownership.</summary>
public sealed class BinaryAndOperandTests {
	/// <summary>Verifies preservation of a BOM, malformed bytes, and Unicode word bytes.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task RetainsWordBytesExactly() {
		var input = new byte[] { 0xEF, 0xBB, 0xBF, 0xFF, (byte)' ', 0xE7, 0x95, 0x8C };
		var result = await RunAsync( [ ], input );
		Assert.Equal( input.Concat( Encoding.UTF8.GetBytes( Environment.NewLine ) ).ToArray(), result.Output );
	}

	/// <summary>Verifies that an unformatted nonmatching final line retains its missing line feed.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task NonmatchingFinalLineRetainsMissingTerminator() {
		var result = await RunAsync( [ "--prefix=>" ], "plain"u8.ToArray() );
		Assert.Equal( "plain"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies GNU's normalization of blank and prefix-only unterminated input.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task NormalizesBlankAndPrefixOnlyInput() {
		var blank = await RunAsync( [ ], "   "u8.ToArray() );
		var prefixOnly = await RunAsync( [ "--prefix=> " ], "> "u8.ToArray() );
		var strictPrefix = await RunAsync( [ "--prefix=foo" ], "fo"u8.ToArray() );
		Assert.Equal( Encoding.UTF8.GetBytes( Environment.NewLine ), blank.Output );
		Assert.Equal( Encoding.UTF8.GetBytes( string.Concat( ">", Environment.NewLine ) ), prefixOnly.Output );
		Assert.Equal( "fo"u8.ToArray(), strictPrefix.Output );
	}

	/// <summary>Verifies ordered file operands and standard input.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ProcessesFilesAndStandardInputInOrder() {
		var first = System.IO.Path.GetTempFileName();
		var second = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( first, "file one\n"u8.ToArray() );
			await File.WriteAllBytesAsync( second, "file two\n"u8.ToArray() );
			var result = await RunAsync( [ first, "-", second ], "stdin words\n"u8.ToArray() );
			Assert.Equal(
				Generated( "file one", "stdin words", "file two" ),
				result.Output
			);
		} finally {
			File.Delete( first );
			File.Delete( second );
		}
	}

	/// <summary>Verifies that a missing operand fails without suppressing later output.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task MissingFileDoesNotSuppressLaterOperands() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, "later\n"u8.ToArray() );
			var result = await RunAsync( [ string.Concat( file, ".missing" ), file ], [ ] );
			Assert.Equal( CommandExitCodes.Failure, result.Status );
			Assert.Equal( Generated( "later" ), result.Output );
			Assert.Contains( ".missing", result.Error );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies that caller-owned standard streams remain open.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task LeavesInjectedStreamsOpen() {
		using var input = new MemoryStream( "x"u8.ToArray() );
		using var output = new MemoryStream();
		var context = new CommandContext( "fmt", new StringReader( string.Empty ), new StringWriter(), new StringWriter(), input, output );
		_ = await Command.RunAsync( [ ], context );
		Assert.True( input.CanRead );
		Assert.True( output.CanWrite );
	}

	/// <summary>Verifies controlled output-failure diagnostics.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ReportsWriteFailures() {
		using var input = new MemoryStream( "x"u8.ToArray() );
		using var output = new ThrowingWriteStream();
		var error = new StringWriter();
		var context = new CommandContext( "fmt", new StringReader( string.Empty ), new StringWriter(), error, input, output );
		var status = await Command.RunAsync( [ ], context );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "write failed", error.ToString() );
	}

	/// <summary>Verifies that repeated standard-input operands observe one shared stream position.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task RepeatedStandardInputIsNotRewound() {
		var result = await RunAsync( [ "-", "-" ], "once\n"u8.ToArray() );
		Assert.Equal( Generated( "once" ), result.Output );
	}

	/// <summary>Verifies controlled diagnostics when standard input fails while being read.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ReportsReadFailures() {
		using var input = new ThrowingReadStream();
		using var output = new MemoryStream();
		var error = new StringWriter();
		var context = new CommandContext( "fmt", new StringReader( string.Empty ), new StringWriter(), error, input, output );
		var status = await Command.RunAsync( [ ], context );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "simulated read failure", error.ToString() );
	}

	private static byte[] Generated( params string[] lines ) {
		return Encoding.UTF8.GetBytes( string.Concat( string.Join( Environment.NewLine, lines ), Environment.NewLine ) );
	}

	private static async Task<(int Status, byte[] Output, string Error)> RunAsync( string[] args, byte[] input ) {
		var original = Environment.GetEnvironmentVariable( "LC_ALL" );
		Environment.SetEnvironmentVariable( "LC_ALL", "C.UTF-8" );
		try {
			using var inputStream = new MemoryStream( input, writable: false );
			using var outputStream = new MemoryStream();
			var error = new StringWriter();
			var context = new CommandContext( "fmt", new StringReader( string.Empty ), new StringWriter(), error, inputStream, outputStream );
			var status = await Command.RunAsync( args, context );
			return ( status, outputStream.ToArray(), error.ToString() );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", original );
		}
	}

	private sealed class ThrowingReadStream : MemoryStream {
		/// <inheritdoc/>
		public override ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			return ValueTask.FromException<int>( new IOException( "simulated read failure" ) );
		}
	}

	private sealed class ThrowingWriteStream : MemoryStream {
		/// <inheritdoc/>
		public override ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			return ValueTask.FromException( new IOException( "simulated write failure" ) );
		}
	}
}
