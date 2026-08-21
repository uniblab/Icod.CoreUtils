namespace Icod.CoreUtils.Shared.Tests.Records;

using System.Text;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Records;
using Xunit;

/// <summary>Tests byte-record writing and compatibility with the existing materializing reader.</summary>
public sealed class RecordWriterAndCompatibilityTests {

	/// <summary>Verifies caller-selected terminated and unterminated output records.</summary>
	[Fact]
	public async Task WriterDoesNotChooseTerminationPolicy() {
		using var output = new MemoryStream();
		var writer = new DelimitedByteRecordWriter( output, RecordSeparator.Null );
		await writer.WriteRecordAsync( Encoding.ASCII.GetBytes( "one" ), terminate: true );
		await writer.WriteRecordAsync( Encoding.ASCII.GetBytes( "two" ), terminate: false );
		await writer.FlushAsync();
		Assert.Equal(
			new byte[] { (byte)'o', (byte)'n', (byte)'e', 0, (byte)'t', (byte)'w', (byte)'o' },
			output.ToArray()
		);
	}

	/// <summary>Verifies separate content and separator operations.</summary>
	[Fact]
	public async Task WriterExposesContentAndSeparatorOperations() {
		var writer = new DelimitedByteRecordWriter( output );
		await writer.WriteContentAsync( Encoding.ASCII.GetBytes( "value" ) );
		await writer.WriteSeparatorAsync();
		Assert.Equal( Encoding.ASCII.GetBytes( "value\n" ), output.ToArray() );
	}

	/// <summary>Verifies that the writer does not own the destination stream.</summary>
	[Fact]
	public async Task WriterDoesNotOwnDestinationStream() {
		var writer = new DelimitedByteRecordWriter( output );
		await writer.WriteRecordAsync( ReadOnlyMemory<byte>.Empty, terminate: true );
		Assert.True( output.CanWrite );
	}

	/// <summary>Verifies the new materializing reader separates content from termination metadata.</summary>
	[Fact]
	public async Task MaterializingReaderReturnsExplicitTerminationMetadata() {
		using var input = new MemoryStream( Encoding.ASCII.GetBytes( "a\n\nb" ), writable: false );
		using var reader = new ByteRecordReader( input, bufferSize: 1 );

		var first = Assert.IsType<ByteRecord>( await reader.ReadAsync() );
		Assert.Equal( Encoding.ASCII.GetBytes( "a" ), first.Content.ToArray() );
		Assert.True( first.IsTerminated );

		var second = Assert.IsType<ByteRecord>( await reader.ReadAsync() );
		Assert.Empty( second.Content.ToArray() );
		Assert.True( second.IsTerminated );

		var third = Assert.IsType<ByteRecord>( await reader.ReadAsync() );
		Assert.Equal( Encoding.ASCII.GetBytes( "b" ), third.Content.ToArray() );
		Assert.False( third.IsTerminated );

		Assert.Null( await reader.ReadAsync() );
	}

	/// <summary>Verifies that the compatibility reader still includes present separators.</summary>
	[Fact]
	public async Task CompatibilityReaderPreservesExistingReturnContract() {
		using var reader = new DelimitedByteRecordReader( input, bufferSize: 1 );
		Assert.Equal( Encoding.ASCII.GetBytes( "a\n" ), await reader.ReadAsync() );
		Assert.Equal( Encoding.ASCII.GetBytes( "\n" ), await reader.ReadAsync() );
		Assert.Equal( Encoding.ASCII.GetBytes( "b" ), await reader.ReadAsync() );
		Assert.Null( await reader.ReadAsync() );
		Assert.Null( await reader.ReadAsync() );
	}

	/// <summary>Verifies compatibility-reader NUL records and exact final-record behavior.</summary>
	[Fact]
	public async Task CompatibilityReaderSupportsNullDelimiter() {
		using var input = new MemoryStream( new byte[] { 1, 0, 2 }, writable: false );
		using var reader = new DelimitedByteRecordReader( input, separator: 0, bufferSize: 1 );
		Assert.Equal( new byte[] { 1, 0 }, await reader.ReadAsync() );
		Assert.Equal( new byte[] { 2 }, await reader.ReadAsync() );
		Assert.Null( await reader.ReadAsync() );
	}

	/// <summary>Verifies writer construction validation.</summary>
	[Fact]
	public void WriterValidatesDestination() {
		Assert.Throws<ArgumentNullException>( () => new DelimitedByteRecordWriter( null! ) );
		using var inputOnly = new MemoryStream( new byte[1], writable: false );
		Assert.Throws<ArgumentException>( () => new DelimitedByteRecordWriter( inputOnly ) );
	}

}
