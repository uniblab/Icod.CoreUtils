namespace Icod.CoreUtils.Shared.Tests.Integration;

using System.Text;
using Icod.CoreUtils.Shared.Escapes;
using Icod.CoreUtils.Shared.Ranges;
using Icod.CoreUtils.Shared.Records;
using Icod.CommandFramework.Text;
using Xunit;

/// <summary>
/// Verifies that the Completion Gate C3 components compose with one another and with the existing
/// Completion Gate C2 text-unit layer without introducing hidden normalization.
/// </summary>
public sealed class C3CompositionTests {
	/// <summary>
	/// Verifies that positional ranges can be applied incrementally across bounded record segments.
	/// </summary>
	[Fact]
	public async Task SegmentedRecordsComposeWithRangeCursor() {
		await using var input = new MemoryStream( "abcdef\n"u8.ToArray() );
		using var reader = new DelimitedByteRecordSegmentReader(
			input,
			RecordSeparator.LineFeed,
			bufferSize: 2
		);
		var parsed = RangeListParser.Parse( "2-3,5" );
		Assert.True( parsed.IsSuccess );
		var cursor = parsed.Value!.CreateCursor();
		await using var output = new MemoryStream();
		ulong position = 0;

		while ( true ) {
			var segment = await reader.ReadAsync();
			if ( null == segment ) {
				break;
			}
			foreach ( var value in segment.Data.ToArray() ) {
				position++;
				if ( cursor.Contains( position ) ) {
					output.WriteByte( value );
				}
			}
			if ( segment.EndsRecord ) {
				break;
			}
		}

		Assert.Equal( "bce"u8.ToArray(), output.ToArray() );
	}

	/// <summary>
	/// Verifies that character-position ranges select exact UTF-8 source bytes from the C2 text-unit reader.
	/// </summary>
	[Fact]
	public async Task TextUnitsComposeWithCharacterRangesWithoutReencoding() {
		var source = Encoding.UTF8.GetBytes( "Aé界" );
		await using var input = new MemoryStream( source );
		var reader = new TextUnitReader(
			input,
			TextDecodingMode.Utf8,
			InvalidEncodingPolicy.PreserveBytes,
			bufferSize: 1
		);
		var parsed = RangeListParser.Parse( "2" );
		Assert.True( parsed.IsSuccess );
		var cursor = parsed.Value!.CreateCursor();
		await using var output = new MemoryStream();
		ulong position = 0;

		while ( await reader.ReadAsync() is { } unit ) {
			position++;
			if ( cursor.Contains( position ) ) {
				output.Write( unit.ToByteArray() );
			}
		}

		Assert.Equal( Encoding.UTF8.GetBytes( "é" ), output.ToArray() );
	}

	/// <summary>
	/// Verifies that paste delimiter parsing preserves empty entries and that the resulting cycle repeats them.
	/// </summary>
	[Fact]
	public void PasteDelimiterParsingComposesWithSeparatorCycles() {
		var parsed = PasteDelimiterParser.Parse( @"\0,界" );
		Assert.True( parsed.IsSuccess );
		var cursor = parsed.Value!.CreateCursor();

		Assert.Empty( cursor.Next().Bytes.ToArray() );
		Assert.Equal( ","u8.ToArray(), cursor.Next().Bytes.ToArray() );
		Assert.Equal( Encoding.UTF8.GetBytes( "界" ), cursor.Next().Bytes.ToArray() );
		Assert.Empty( cursor.Next().Bytes.ToArray() );
	}
}
