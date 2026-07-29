namespace Icod.CoreUtils.Shared.Tests.Text;

using System.Text;
using Icod.CoreUtils.Shared.Text;
using Xunit;

/// <summary>Tests byte-preserving text-unit iteration.</summary>
public sealed class TextUnitReaderTests {
	/// <summary>Verifies that byte mode returns every source byte without decoding.</summary>
	[Fact]
	public void ByteModeReturnsEverySourceByte() {
		using var input = new MemoryStream(
			new byte[] { 0x41, 0xC3, 0xA9 },
			writable: false
		);
		var reader = new TextUnitReader(
			input,
			TextDecodingMode.Bytes
		);
		var units = new List<TextUnit>();
		while ( reader.Read() is { } unit ) {
			units.Add( unit );
		}
		Assert.Equal( 3, units.Count );
		Assert.All(
			units,
			value => Assert.Equal( TextUnitKind.Byte, value.Kind )
		);
		Assert.Equal(
			new byte[] { 0x41, 0xC3, 0xA9 },
			Reconstruct( units )
		);
		Assert.Equal( 3, reader.ByteOffset );
	}

	/// <summary>Verifies that decoded scalar units retain their exact UTF-8 source bytes.</summary>
	[Fact]
	public async Task Utf8ModePreservesExactSourceBytes() {
		var source = Encoding.UTF8.GetBytes( "Aé界😀" );
		using var input = new MemoryStream( source, writable: false );
		var reader = new TextUnitReader( input );
		var units = await ReadAllAsync( reader );
		Assert.Equal( 4, units.Count );
		Assert.All(
			units,
			value => Assert.Equal( TextUnitKind.Scalar, value.Kind )
		);
		Assert.Equal( source, Reconstruct( units ) );
		Assert.Equal( source.LongLength, reader.ByteOffset );
	}

	/// <summary>Verifies that UTF-8 scalars decode correctly across short underlying reads.</summary>
	[Fact]
	public async Task Utf8ModeDecodesAcrossShortReads() {
		var source = Encoding.UTF8.GetBytes( "é界😀" );
		using var input = new ChunkedReadStream( source, 1 );
		var reader = new TextUnitReader(
			input,
			bufferSize: 1
		);
		var units = await ReadAllAsync( reader );
		Assert.Equal( 3, units.Count );
		Assert.Equal<Rune?>( new Rune( 0x00E9 ), units[0].Scalar );
		Assert.Equal<Rune?>( new Rune( 0x754C ), units[1].Scalar );
		Assert.Equal<Rune?>( new Rune( 0x1F600 ), units[2].Scalar );
		Assert.Equal( source, Reconstruct( units ) );
	}

	/// <summary>Verifies synchronous UTF-8 decoding across short underlying reads.</summary>
	[Fact]
	public void SynchronousUtf8ModeDecodesAcrossShortReads() {
		var source = Encoding.UTF8.GetBytes( "é界😀" );
		using var input = new ChunkedReadStream( source, 1 );
		var reader = new TextUnitReader(
			input,
			bufferSize: 1
		);
		var units = new List<TextUnit>();
		while ( reader.Read() is { } unit ) {
			units.Add( unit );
		}
		Assert.Equal( source, Reconstruct( units ) );
		Assert.Equal<Rune?>( new Rune( 0x00E9 ), units[0].Scalar );
		Assert.Equal<Rune?>( new Rune( 0x754C ), units[1].Scalar );
		Assert.Equal<Rune?>( new Rune( 0x1F600 ), units[2].Scalar );
	}

	/// <summary>Verifies that malformed UTF-8 bytes are independently preserved by the preserve policy.</summary>
	[Fact]
	public async Task PreservePolicyReturnsInvalidBytesIndividually() {
		var source = new byte[] { 0xC0, 0xAF, 0x41 };
		using var input = new MemoryStream( source, writable: false );
		var reader = new TextUnitReader(
			input,
			invalidEncodingPolicy: InvalidEncodingPolicy.PreserveBytes
		);
		var units = await ReadAllAsync( reader );
		Assert.Equal(
			new[] {
				TextUnitKind.InvalidByte,
				TextUnitKind.InvalidByte,
				TextUnitKind.Scalar
			},
			units.Select( value => value.Kind ).ToArray()
		);
		Assert.Equal( source, Reconstruct( units ) );
	}

	/// <summary>Verifies preservation of representative malformed UTF-8 forms.</summary>
	[Fact]
	public async Task PreservePolicyRoundTripsRepresentativeMalformedForms() {
		var sources = new byte[][] {
			new byte[] { 0x80 },
			new byte[] { 0xC0, 0xAF },
			new byte[] { 0xE2, 0x28, 0xA1 },
			new byte[] { 0xED, 0xA0, 0x80 },
			new byte[] { 0xF4, 0x90, 0x80, 0x80 },
			new byte[] { 0xF5, 0x80, 0x80, 0x80 },
			new byte[] { 0xE2, 0x82 }
		};
		foreach ( var source in sources ) {
			using var input = new ChunkedReadStream( source, 1 );
			var reader = new TextUnitReader( input );
			var units = await ReadAllAsync( reader );
			Assert.Equal( source, Reconstruct( units ) );
		}
	}

	/// <summary>Verifies that replacement units retain each invalid source byte.</summary>
	[Fact]
	public async Task ReplacePolicyRetainsReplacedBytes() {
		var source = new byte[] { 0xC0, 0xAF };
		using var input = new MemoryStream( source, writable: false );
		var reader = new TextUnitReader(
			input,
			invalidEncodingPolicy: InvalidEncodingPolicy.Replace
		);
		var units = await ReadAllAsync( reader );
		Assert.Equal( 2, units.Count );
		Assert.All(
			units,
			value => {
				Assert.Equal( TextUnitKind.Replacement, value.Kind );
				Assert.Equal<Rune?>( Rune.ReplacementChar, value.Scalar );
			}
		);
		Assert.Equal( source, Reconstruct( units ) );
	}

	/// <summary>Verifies that the throw policy reports the first invalid source-byte offset.</summary>
	[Fact]
	public void ThrowPolicyReportsStableByteOffset() {
		using var input = new MemoryStream(
			new byte[] { 0x41, 0xC0 },
			writable: false
		);
		var reader = new TextUnitReader(
			input,
			invalidEncodingPolicy: InvalidEncodingPolicy.Throw
		);
		Assert.NotNull( reader.Read() );
		var exception = Assert.Throws<DecoderFallbackException>(
			() => reader.Read()
		);
		Assert.Contains( "byte offset 1", exception.Message );
		Assert.Equal( 1, reader.ByteOffset );
	}

	/// <summary>Verifies that an incomplete final UTF-8 sequence is preserved byte by byte.</summary>
	[Fact]
	public async Task IncompleteFinalSequenceIsPreserved() {
		var source = new byte[] { 0xE2, 0x82 };
		using var input = new ChunkedReadStream( source, 1 );
		var reader = new TextUnitReader( input );
		var units = await ReadAllAsync( reader );
		Assert.Equal( 2, units.Count );
		Assert.All(
			units,
			value => Assert.Equal( TextUnitKind.InvalidByte, value.Kind )
		);
		Assert.Equal( source, Reconstruct( units ) );
	}

	/// <summary>Verifies that a UTF-8 byte-order mark remains an ordinary preserved scalar.</summary>
	[Fact]
	public async Task ByteOrderMarkIsNotRemoved() {
		var source = new byte[] { 0xEF, 0xBB, 0xBF, 0x41 };
		using var input = new MemoryStream( source, writable: false );
		var reader = new TextUnitReader( input );
		var units = await ReadAllAsync( reader );
		Assert.Equal( 2, units.Count );
		Assert.Equal<Rune?>( new Rune( 0xFEFF ), units[0].Scalar );
		Assert.Equal( source, Reconstruct( units ) );
	}

	/// <summary>Verifies indexed and span-based access to retained bytes.</summary>
	[Fact]
	public void TextUnitExposesExactRetainedBytes() {
		using var input = new MemoryStream(
			Encoding.UTF8.GetBytes( "😀" ),
			writable: false
		);
		var unit = AssertNullableUnit( new TextUnitReader( input ).Read() );
		Span<byte> destination = stackalloc byte[4];
		Assert.Equal( 4, unit.CopyBytesTo( destination ) );
		Assert.Equal( new byte[] { 0xF0, 0x9F, 0x98, 0x80 }, destination.ToArray() );
		Assert.Equal( 0xF0, unit.GetByte( 0 ) );
		Assert.Equal( 0x80, unit.GetByte( 3 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => unit.GetByte( 4 ) );
		Assert.Throws<ArgumentException>(
			() => unit.CopyBytesTo( new byte[3] )
		);
	}

	/// <summary>Verifies validation of reader construction settings.</summary>
	[Fact]
	public void ReaderValidatesConstructionSettings() {
		using var readable = new MemoryStream();
		Assert.Throws<ArgumentNullException>(
			() => new TextUnitReader( null! )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TextUnitReader( readable, bufferSize: 0 )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TextUnitReader(
				readable,
				(TextDecodingMode)int.MaxValue
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TextUnitReader(
				readable,
				invalidEncodingPolicy: (InvalidEncodingPolicy)int.MaxValue
			)
		);
		var unreadable = new MemoryStream();
		unreadable.Dispose();
		Assert.Throws<ArgumentException>(
			() => new TextUnitReader( unreadable )
		);
	}

	/// <summary>Verifies that asynchronous reads honor cancellation.</summary>
	[Fact]
	public async Task AsyncReadHonorsCancellation() {
		using var input = new ChunkedReadStream(
			Encoding.UTF8.GetBytes( "value" ),
			1
		);
		var reader = new TextUnitReader( input );
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			async () => {
				await reader.ReadAsync( cancellation.Token );
			}
		);
	}

	/// <summary>Verifies that reading does not dispose the caller-owned source stream.</summary>
	[Fact]
	public async Task ReaderDoesNotOwnSourceStream() {
		using var input = new MemoryStream(
			Encoding.UTF8.GetBytes( "value" ),
			writable: false
		);
		var reader = new TextUnitReader( input );
		await ReadAllAsync( reader );
		Assert.True( input.CanRead );
		Assert.Equal( -1, input.ReadByte() );
	}

	private static TextUnit AssertNullableUnit( TextUnit? unit ) => unit
		?? throw new InvalidOperationException(
			"The test input did not produce a text unit."
		);

	private static async Task<List<TextUnit>> ReadAllAsync( TextUnitReader reader ) {
		var units = new List<TextUnit>();
		while ( (await reader.ReadAsync()) is { } unit ) {
			units.Add( unit );
		}
		return units;
	}

	private static byte[] Reconstruct( IEnumerable<TextUnit> units ) {
		using var output = new MemoryStream();
		foreach ( var unit in units ) {
			output.Write( unit.ToByteArray() );
		}
		return output.ToArray();
	}

	private sealed class ChunkedReadStream : MemoryStream {
		private readonly int myMaximumReadSize;

		/// <summary>Initializes a short-read memory stream.</summary>
		/// <param name="buffer">The source bytes.</param>
		/// <param name="maximumReadSize">The maximum bytes returned by one read.</param>
		public ChunkedReadStream(
			byte[] buffer,
			int maximumReadSize
		) : base( buffer, writable: false ) {
			this.myMaximumReadSize = maximumReadSize;
		}

		/// <inheritdoc/>
		public override int Read(
			byte[] buffer,
			int offset,
			int count
		) => base.Read(
			buffer,
			offset,
			Math.Min( count, this.myMaximumReadSize )
		);

		/// <inheritdoc/>
		public override int Read( Span<byte> buffer ) => base.Read(
			buffer[..Math.Min( buffer.Length, this.myMaximumReadSize )]
		);

		/// <inheritdoc/>
		public override ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return base.ReadAsync(
				buffer[..Math.Min( buffer.Length, this.myMaximumReadSize )],
				cancellationToken
			);
		}
	}
}
