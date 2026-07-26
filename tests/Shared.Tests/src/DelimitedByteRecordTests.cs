namespace Icod.CoreUtils.Shared.Tests;

using System.Text;
using Icod.CoreUtils.Shared.IO;
using Xunit;

public sealed class DelimitedByteRecordTests {

	[Fact]
	public async Task ReaderPreservesTerminatorsAndFinalUnterminatedRecord() {
		var input = Encoding.UTF8.GetBytes(
			"alpha\r\nbeta\ngamma"
		);
		using var stream = new MemoryStream(
			input,
			writable: false
		);
		using var reader = new DelimitedByteRecordReader(
			stream,
			bufferSize: 3
		);

		Assert.Equal( "alpha\r\n", Encoding.UTF8.GetString( ( await reader.ReadAsync() )! ) );
		Assert.Equal( "beta\n", Encoding.UTF8.GetString( ( await reader.ReadAsync() )! ) );
		Assert.Equal( "gamma", Encoding.UTF8.GetString( ( await reader.ReadAsync() )! ) );
		Assert.Null( await reader.ReadAsync() );
	}

	[Fact]
	public async Task ReaderPreservesEmptyNulRecords() {
		using var stream = new MemoryStream(
			Encoding.UTF8.GetBytes( "alpha\0\0beta\0" ),
			writable: false
		);
		using var reader = new DelimitedByteRecordReader(
			stream,
			separator: 0,
			bufferSize: 2
		);

		Assert.Equal( "alpha\0", Encoding.UTF8.GetString( ( await reader.ReadAsync() )! ) );
		Assert.Equal( "\0", Encoding.UTF8.GetString( ( await reader.ReadAsync() )! ) );
		Assert.Equal( "beta\0", Encoding.UTF8.GetString( ( await reader.ReadAsync() )! ) );
		Assert.Null( await reader.ReadAsync() );
	}

	[Theory]
	[InlineData( "a\nb\nc\n", 1, 4 )]
	[InlineData( "a\nb\nc\n", 2, 2 )]
	[InlineData( "a\nb\nc\n", 3, 0 )]
	[InlineData( "a\nb\nc", 1, 4 )]
	[InlineData( "a\nb\nc", 2, 2 )]
	[InlineData( "\n", 1, 0 )]
	[InlineData( "", 1, 0 )]
	[InlineData( "a\nb\n", 0, 4 )]
	public async Task FindsStartOfLastDelimitedRecords(
		string value,
		long count,
		long expected
	) {
		using var stream = new MemoryStream(
			Encoding.UTF8.GetBytes( value ),
			writable: false
		);
		stream.Position = Math.Min( 1, stream.Length );
		var originalPosition = stream.Position;

		var actual = await StreamOperations.FindStartOfLastDelimitedRecordsAsync(
			stream,
			(byte)'\n',
			count,
			bufferSize: 2
		);

		Assert.Equal( expected, actual );
		Assert.Equal( originalPosition, stream.Position );
	}

	[Fact]
	public async Task ByteOutputFallbackDecodesSplitUtf8Sequences() {
		using var text = new StringWriter();
		using var output = new ByteOutputStream(
			text
		);
		var bytes = Encoding.UTF8.GetBytes(
			"A€B"
		);

		await output.WriteAsync( bytes.AsMemory( 0, 2 ) );
		await output.WriteAsync( bytes.AsMemory( 2, 2 ) );
		await output.WriteAsync( bytes.AsMemory( 4 ) );
		await output.CompleteAsync();

		Assert.Equal( "A€B", text.ToString() );
	}

}
