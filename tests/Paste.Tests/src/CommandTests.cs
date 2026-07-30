namespace Icod.CoreUtils.Paste.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests parallel, serial, delimiter-cycle, and control-path behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies corresponding records from two files are pasted in parallel.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task PastesFilesInParallel() {
		var first = Path.GetTempFileName();
		var second = Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( first, "a\nb\n"u8.ToArray() );
			await File.WriteAllBytesAsync( second, "1\n2\n"u8.ToArray() );
			var result = await RunAsync( [ first, second ], [] );
			Assert.Equal( Generated( "a\t1", "b\t2" ), result.Output );
		} finally {
			File.Delete( first );
			File.Delete( second );
		}
	}

	/// <summary>Verifies uneven files retain leading columns but omit trailing unused delimiters.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task HandlesUnevenParallelInputs() {
		var first = Path.GetTempFileName();
		var second = Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( first, "a\n"u8.ToArray() );
			await File.WriteAllBytesAsync( second, "1\n2\n"u8.ToArray() );
			var result = await RunAsync( [ first, second ], [] );
			Assert.Equal( Generated( "a\t1", "\t2" ), result.Output );
		} finally {
			File.Delete( first );
			File.Delete( second );
		}
	}

	/// <summary>Verifies serial mode joins each operand independently.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task PastesOneFileAtATime() {
		var file = Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, "a\nb\nc\n"u8.ToArray() );
			var result = await RunAsync( [ "-s", file ], [] );
			Assert.Equal( Generated( "a\tb\tc" ), result.Output );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies delimiters cycle and reset for each output row.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CyclesDelimitersPerOutputRow() {
		var files = Enumerable.Range( 0, 3 ).Select( _ => Path.GetTempFileName() ).ToArray();
		try {
			await File.WriteAllBytesAsync( files[0], "a\nd\n"u8.ToArray() );
			await File.WriteAllBytesAsync( files[1], "b\ne\n"u8.ToArray() );
			await File.WriteAllBytesAsync( files[2], "c\nf\n"u8.ToArray() );
			var result = await RunAsync( [ "-d", ",;", .. files ], [] );
			Assert.Equal( Generated( "a,b;c", "d,e;f" ), result.Output );
		} finally {
			foreach ( var file in files ) { File.Delete( file ); }
		}
	}

	/// <summary>Verifies <c>\0</c> denotes an empty cycle element rather than a NUL byte.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task EmptyDelimiterSlotsProduceNoBytes() {
		var result = await RunAsync( [ "-d", "\\0,", "-", "-", "-" ], "a\nb\nc\n"u8.ToArray() );
		Assert.Equal( Generated( "ab,c" ), result.Output );
	}

	/// <summary>Verifies NUL-delimited records and output.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsNullTerminatedRecords() {
		var result = await RunAsync( [ "-z", "-", "-" ], [ (byte)'a', 0, (byte)'b', 0 ] );
		Assert.Equal( new byte[] { (byte)'a', (byte)'\t', (byte)'b', 0 }, result.Output );
	}

	/// <summary>Verifies help, version, and malformed delimiter diagnostics.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], [] );
		var version = await RunAsync( [ "--version" ], [] );
		var invalid = await RunAsync( [ "-d", "\\" ], [] );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: paste", help.TextOutput );
		Assert.Equal( CommandExitCodes.Success, version.Status );
		Assert.Contains( "paste (Icod.CoreUtils)", version.TextOutput );
		Assert.Equal( CommandExitCodes.Failure, invalid.Status );
		Assert.Contains( "backslash", invalid.Error.ToLowerInvariant() );
	}

	private static byte[] Generated( params string[] lines ) => Encoding.UTF8.GetBytes( string.Concat( string.Join( Environment.NewLine, lines ), Environment.NewLine ) );

	private static async Task<(int Status, byte[] Output, string TextOutput, string Error)> RunAsync( string[] args, byte[] input ) {
		using var inputStream = new MemoryStream( input, writable: false );
		using var outputStream = new MemoryStream();
		var textOutput = new StringWriter();
		var error = new StringWriter();
		var context = new CommandContext( "paste", new StringReader( string.Empty ), textOutput, error, inputStream, outputStream );
		var status = await Command.RunAsync( args, context );
		return ( status, outputStream.ToArray(), textOutput.ToString(), error.ToString() );
	}
}
