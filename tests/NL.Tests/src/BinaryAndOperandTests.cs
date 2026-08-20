namespace Icod.CoreUtils.NL.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests exact bytes, multiple operands, failures, overflow, and ownership.</summary>
public sealed class BinaryAndOperandTests {
	/// <summary>Verifies content-byte preservation and GNU normalization of a missing final line feed.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task RetainsOriginalLineBytes() {
		var input = new byte[] { 0xEF, 0xBB, 0xBF, 0xFF };
		var result = await RunAsync( [ "-w1", "-s:" ], input );
		Assert.Equal( new byte[] { (byte)'1', (byte)':' }.Concat( input ).Concat( Encoding.UTF8.GetBytes( Environment.NewLine ) ).ToArray(), result.Output );
	}

	/// <summary>Verifies that numbering continues across file operands.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task MultipleFilesFormOneDocument() {
		var first = System.IO.Path.GetTempFileName();
		var second = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( first, "a\n"u8.ToArray() );
			await File.WriteAllBytesAsync( second, "b\n"u8.ToArray() );
			var result = await RunAsync( [ "-w1", "-s:", first, second ], [ ] );
			Assert.Equal( "1:a\n2:b\n"u8.ToArray(), result.Output );
		} finally {
			File.Delete( first );
			File.Delete( second );
		}
	}

	/// <summary>Verifies that a missing file does not reset or suppress later operands.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task MissingFileContinuesWithLaterOperands() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, "x\n"u8.ToArray() );
			var result = await RunAsync( [ "-w1", "-s:", string.Concat( file, ".missing" ), file ], [ ] );
			Assert.Equal( CommandExitCodes.Failure, result.Status );
			Assert.Equal( "1:x\n"u8.ToArray(), result.Output );
			Assert.Contains( ".missing", result.Error );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies ordered mixing of a file and standard input.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task FileAndStandardInputPreserveEncounterOrder() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, "a\n"u8.ToArray() );
			var result = await RunAsync( [ "-w1", "-s:", file, "-" ], "b\n"u8.ToArray() );
			Assert.Equal( "1:a\n2:b\n"u8.ToArray(), result.Output );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies deferred diagnosis of line-number overflow.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task OverflowIsReportedWhenAnotherNumberIsNeeded() {
		var oneLine = await RunAsync( [ "-v9223372036854775807", "-i1" ], "a\n"u8.ToArray() );
		var twoLines = await RunAsync( [ "-v9223372036854775807", "-i1" ], "a\nb\n"u8.ToArray() );
		Assert.Equal( CommandExitCodes.Success, oneLine.Status );
		Assert.Equal( CommandExitCodes.Failure, twoLines.Status );
		Assert.Contains( "line number overflow", twoLines.Error );
	}

	/// <summary>Verifies that caller-owned streams remain open.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task LeavesInjectedStreamsOpen() {
		using var input = new MemoryStream( "x"u8.ToArray() );
		using var output = new MemoryStream();
		var context = new CommandContext( "nl", new StringReader( string.Empty ), new StringWriter(), new StringWriter(), input, output );
		_ = await Command.RunAsync( [ ], context );
		Assert.True( input.CanRead );
		Assert.True( output.CanWrite );
	}

	/// <summary>Verifies controlled write-failure diagnostics.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ReportsWriteFailures() {
		using var input = new MemoryStream( "x\n"u8.ToArray() );
		using var output = new ThrowingWriteStream();
		var error = new StringWriter();
		var context = new CommandContext( "nl", new StringReader( string.Empty ), new StringWriter(), error, input, output );
		var status = await Command.RunAsync( [ ], context );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "write failed", error.ToString() );
	}

	/// <summary>Verifies that repeated standard-input operands observe one shared stream position.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task RepeatedStandardInputIsNotRewound() {
		var result = await RunAsync( [ "-w1", "-s:", "-", "-" ], "x\n"u8.ToArray() );
		Assert.Equal( "1:x\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies controlled diagnostics when standard input fails while being read.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ReportsReadFailures() {
		using var input = new ThrowingReadStream();
		using var output = new MemoryStream();
		var error = new StringWriter();
		var context = new CommandContext( "nl", new StringReader( string.Empty ), new StringWriter(), error, input, output );
		var status = await Command.RunAsync( [ ], context );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "simulated read failure", error.ToString() );
	}

	private static async Task<(int Status, byte[] Output, string Error)> RunAsync( string[] args, byte[] input ) {
		var original = Environment.GetEnvironmentVariable( "LC_ALL" );
		Environment.SetEnvironmentVariable( "LC_ALL", "C.UTF-8" );
		try {
			using var inputStream = new MemoryStream( input, writable: false );
			using var outputStream = new MemoryStream();
			var error = new StringWriter();
			var context = new CommandContext( "nl", new StringReader( string.Empty ), new StringWriter(), error, inputStream, outputStream );
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
