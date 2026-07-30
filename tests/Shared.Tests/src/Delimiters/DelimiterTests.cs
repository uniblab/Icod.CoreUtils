namespace Icod.CoreUtils.Shared.Tests.Delimiters;

using System.Text;
using Icod.CoreUtils.Shared.Delimiters;
using Xunit;

/// <summary>Tests immutable delimiters, empty separators, cycles, and incremental matching.</summary>
public sealed class DelimiterTests {

	/// <summary>Verifies that delimiter and separator constructors copy caller-owned bytes.</summary>
	[Fact]
	public void ValuesCopyCallerOwnedBytes() {
		var source = Encoding.UTF8.GetBytes( "é" );
		var delimiter = new ByteDelimiter( source );
		var separator = new ByteSeparator( source );
		source[0] = 0;
		Assert.Equal( Encoding.UTF8.GetBytes( "é" ), delimiter.Bytes.ToArray() );
		Assert.Equal( Encoding.UTF8.GetBytes( "é" ), separator.Bytes.ToArray() );
	}

	/// <summary>Verifies the deliberate empty-separator and nonempty-delimiter distinction.</summary>
	[Fact]
	public void EmptySeparatorIsValidButEmptyDelimiterIsNot() {
		Assert.True( ByteSeparator.Empty.IsEmpty );
		Assert.Throws<ArgumentException>( () => new ByteDelimiter( Array.Empty<byte>() ) );
	}

	/// <summary>Verifies separator-cycle repetition including an empty slot.</summary>
	[Fact]
	public void CycleRepeatsPossiblyEmptySeparators() {
		var comma = new ByteSeparator( new byte[] { (byte)',' } );
		var cycle = new SeparatorCycle( new[] { ByteSeparator.Empty, comma } );
		var cursor = cycle.CreateCursor();
		Assert.True( cursor.Next().IsEmpty );
		Assert.Equal( comma, cursor.Next() );
		Assert.True( cursor.Next().IsEmpty );
		cursor.Reset();
		Assert.True( cursor.Next().IsEmpty );
	}

	/// <summary>Verifies validation of separator-cycle construction.</summary>
	[Fact]
	public void CycleRequiresAnElement() {
		Assert.Throws<ArgumentNullException>( () => new SeparatorCycle( null! ) );
		Assert.Throws<ArgumentException>( () => new SeparatorCycle( Array.Empty<ByteSeparator>() ) );
	}

	/// <summary>Verifies a multibyte match across separately supplied bytes.</summary>
	[Fact]
	public void MatcherSpansArbitraryInputBoundaries() {
		var matcher = new ByteSequenceMatcher(
			new ByteDelimiter( Encoding.UTF8.GetBytes( "é" ) )
		);
		Assert.False( matcher.Advance( 0xC3 ) );
		Assert.Equal( 1, matcher.MatchedLength );
		Assert.True( matcher.Advance( 0xA9 ) );
		Assert.Equal( 0, matcher.MatchedLength );
	}

	/// <summary>Verifies overlapping delimiter occurrences.</summary>
	[Fact]
	public void MatcherRecognizesOverlappingPatterns() {
		var matcher = new ByteSequenceMatcher(
			new ByteDelimiter( Encoding.ASCII.GetBytes( "aba" ) )
		);
		var matches = 0;
		foreach ( var value in Encoding.ASCII.GetBytes( "ababa" ) ) {
			if ( matcher.Advance( value ) ) {
				matches++;
			}
		}
		Assert.Equal( 2, matches );
	}

	/// <summary>Verifies mismatch fallback and explicit reset.</summary>
	[Fact]
	public void MatcherFallsBackAndResets() {
		var matcher = new ByteSequenceMatcher(
			new ByteDelimiter( Encoding.ASCII.GetBytes( "aab" ) )
		);
		Assert.False( matcher.Advance( (byte)'a' ) );
		Assert.False( matcher.Advance( (byte)'a' ) );
		Assert.False( matcher.Advance( (byte)'a' ) );
		Assert.True( matcher.Advance( (byte)'b' ) );
		matcher.Advance( (byte)'a' );
		matcher.Reset();
		Assert.Equal( 0, matcher.MatchedLength );
	}

}
