namespace Icod.CoreUtils.Shared.Tests.Records;

using System.Text;
using Icod.CoreUtils.Shared.Records;
using Xunit;

/// <summary>Tests bounded line-feed and NUL byte-record segmentation.</summary>
public sealed class SegmentedRecordTests {

	/// <summary>Verifies empty, terminated, and final unterminated records.</summary>
	[Fact]
	public async Task ReaderDistinguishesTerminationAndEmptyRecords() {
		using var input = new MemoryStream(
			Encoding.ASCII.GetBytes( "alpha\n\nomega" ),
			writable: false
		);
		using var reader = new DelimitedByteRecordSegmentReader(
			input,
			RecordSeparator.LineFeed,
			bufferSize: 3
		);
		var records = await ReadRecordsAsync( reader );
		Assert.Equal( 3, records.Count );
		Assert.Equal( "alpha", Encoding.ASCII.GetString( records[0].Content.Span ) );
		Assert.True( records[0].IsTerminated );
		Assert.Empty( records[1].Content.ToArray() );
		Assert.True( records[1].IsTerminated );
		Assert.Equal( "omega", Encoding.ASCII.GetString( records[2].Content.Span ) );
		Assert.False( records[2].IsTerminated );
	}

	/// <summary>Verifies NUL-delimited records without treating embedded line feeds specially.</summary>
	[Fact]
	public async Task ReaderSupportsNullRecords() {
		using var input = new MemoryStream(
			new byte[] { (byte)'a', (byte)'\n', 0, (byte)'b', 0 },
			writable: false
		);
		using var reader = new DelimitedByteRecordSegmentReader(
			input,
			RecordSeparator.Null,
			bufferSize: 2
		);
		var records = await ReadRecordsAsync( reader );
		Assert.Equal( 2, records.Count );
		Assert.Equal( new byte[] { (byte)'a', (byte)'\n' }, records[0].Content.ToArray() );
		Assert.Equal( new byte[] { (byte)'b' }, records[1].Content.ToArray() );
		Assert.All( records, value => Assert.True( value.IsTerminated ) );
	}

	/// <summary>Verifies that every nonfinal segment respects the configured read bound.</summary>
	[Fact]
	public async Task LargeRecordsRemainSegmentedAndBounded() {
		var source = Enumerable.Range( 0, 41 ).Select( value => (byte)value ).Concat( new byte[] { 0 } ).ToArray();
		using var input = new MemoryStream( source, writable: false );
		using var reader = new DelimitedByteRecordSegmentReader(
			input,
			RecordSeparator.Null,
			bufferSize: 7
		);
		var segments = new List<ByteRecordSegment>();
		while ( await reader.ReadAsync() is { } segment ) {
			segments.Add( segment );
		}
		Assert.True( 1 < segments.Count );
		Assert.All( segments, value => Assert.InRange( value.Data.Length, 0, 7 ) );
		Assert.True( segments[^1].EndsRecord );
		Assert.True( segments[^1].IsTerminated );
		Assert.Equal(
			source[..^1],
			segments.SelectMany( value => value.Data.ToArray() ).ToArray()
		);
	}

	/// <summary>Verifies that a separator immediately after a full buffer terminates the preceding segment directly.</summary>
	[Fact]
	public async Task BufferBoundarySeparatorDoesNotCreateSyntheticEmptySegment() {
		using var input = new MemoryStream( Encoding.ASCII.GetBytes( "abcd\n" ), writable: false );
		using var reader = new DelimitedByteRecordSegmentReader( input, bufferSize: 2 );
		var first = Assert.IsType<ByteRecordSegment>( await reader.ReadAsync() );
		var second = Assert.IsType<ByteRecordSegment>( await reader.ReadAsync() );
		Assert.Equal( "ab", Encoding.ASCII.GetString( first.Data.Span ) );
		Assert.False( first.EndsRecord );
		Assert.Equal( "cd", Encoding.ASCII.GetString( second.Data.Span ) );
		Assert.True( second.EndsRecord );
		Assert.True( second.IsTerminated );
		Assert.Null( await reader.ReadAsync() );
	}

	/// <summary>Verifies that cancellation is honored before asynchronous input is consumed.</summary>
	[Fact]
	public async Task ReaderHonorsCancellation() {
		using var input = new MemoryStream( Encoding.ASCII.GetBytes( "value\n" ), writable: false );
		using var reader = new DelimitedByteRecordSegmentReader( input );
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			async () => {
				await reader.ReadAsync( cancellation.Token );
			}
		);
	}

	/// <summary>Verifies that disposing the reader does not dispose its caller-owned stream.</summary>
	[Fact]
	public void ReaderDoesNotOwnSourceStream() {
		using var input = new MemoryStream( Encoding.ASCII.GetBytes( "value" ), writable: false );
		var reader = new DelimitedByteRecordSegmentReader( input );
		reader.Dispose();
		Assert.True( input.CanRead );
	}

	/// <summary>Verifies construction validation and disposed-reader behavior.</summary>
	[Fact]
	public async Task ReaderValidatesConstructionAndDisposal() {
		using var input = new MemoryStream();
		Assert.Throws<ArgumentNullException>( () => new DelimitedByteRecordSegmentReader( null! ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => new DelimitedByteRecordSegmentReader( input, bufferSize: 0 ) );
		Assert.Throws<ArgumentException>( () => new ByteRecordSegment( Array.Empty<byte>(), false, true ) );
		var reader = new DelimitedByteRecordSegmentReader( input );
		reader.Dispose();
		await Assert.ThrowsAsync<ObjectDisposedException>(
			async () => {
				await reader.ReadAsync();
			}
		);
	}

	private static async Task<List<ByteRecord>> ReadRecordsAsync( DelimitedByteRecordSegmentReader reader ) {
		var records = new List<ByteRecord>();
		using var content = new MemoryStream();
		while ( await reader.ReadAsync() is { } segment ) {
			content.Write( segment.Data.ToArray() );
			if ( !segment.EndsRecord ) {
				continue;
			}
			records.Add( new ByteRecord( content.ToArray(), segment.IsTerminated ) );
			content.SetLength( 0 );
		}
		return records;
	}

}
