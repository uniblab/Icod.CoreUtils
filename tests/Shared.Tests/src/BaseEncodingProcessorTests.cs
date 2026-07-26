namespace Icod.CoreUtils.Shared.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Codecs;
using Xunit;

public sealed class BaseEncodingProcessorTests {

	[Theory]
	[InlineData( "", "" )]
	[InlineData( "f", "Zg==" )]
	[InlineData( "fo", "Zm8=" )]
	[InlineData( "foo", "Zm9v" )]
	[InlineData( "foob", "Zm9vYg==" )]
	[InlineData( "fooba", "Zm9vYmE=" )]
	[InlineData( "foobar", "Zm9vYmFy" )]
	public async Task Base64MatchesRfc4648Vectors(
		string plain,
		string encoded
	) {
		Assert.Equal(
			encoded,
			TrimEncodedNewline(
				await EncodeAsync(
					BaseEncodingKind.Base64,
					Encoding.ASCII.GetBytes(
						plain
					)
				)
			)
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				plain
			),
			await DecodeAsync(
				BaseEncodingKind.Base64,
				Encoding.ASCII.GetBytes(
					encoded
				)
			)
		);
	}

	[Theory]
	[InlineData( "", "" )]
	[InlineData( "f", "MY======" )]
	[InlineData( "fo", "MZXQ====" )]
	[InlineData( "foo", "MZXW6===" )]
	[InlineData( "foob", "MZXW6YQ=" )]
	[InlineData( "fooba", "MZXW6YTB" )]
	[InlineData( "foobar", "MZXW6YTBOI======" )]
	public async Task Base32MatchesRfc4648Vectors(
		string plain,
		string encoded
	) {
		Assert.Equal(
			encoded,
			TrimEncodedNewline(
				await EncodeAsync(
					BaseEncodingKind.Base32,
					Encoding.ASCII.GetBytes(
						plain
					)
				)
			)
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				plain
			),
			await DecodeAsync(
				BaseEncodingKind.Base32,
				Encoding.ASCII.GetBytes(
					encoded
				)
			)
		);
	}

	[Theory]
	[InlineData( BaseEncodingKind.Base64, "/k+C" )]
	[InlineData( BaseEncodingKind.Base64Url, "_k-C" )]
	[InlineData( BaseEncodingKind.Base32, "7ZHYE===" )]
	[InlineData( BaseEncodingKind.Base32Hex, "VP7O4===" )]
	[InlineData( BaseEncodingKind.Base16, "FE4F82" )]
	[InlineData( BaseEncodingKind.Base2Lsbf, "011111111111001001000001" )]
	[InlineData( BaseEncodingKind.Base2Msbf, "111111100100111110000010" )]
	public async Task BasencExamplesMatchGnuDocumentation(
		BaseEncodingKind kind,
		string expected
	) {
		var input = new byte[] {
			0xFE,
			0x4F,
			0x82
		};
		Assert.Equal(
			expected,
			TrimEncodedNewline(
				await EncodeAsync(
					kind,
					input
				)
			)
		);
		Assert.Equal(
			input,
			await DecodeAsync(
				kind,
				Encoding.ASCII.GetBytes(
					expected
				)
			)
		);
	}

	[Fact]
	public async Task Z85MatchesPublishedVector() {
		var input = new byte[] {
			0xFE,
			0x4F,
			0x82,
			0x00
		};
		Assert.Equal(
			"@.FaC",
			TrimEncodedNewline(
				await EncodeAsync(
					BaseEncodingKind.Z85,
					input
				)
			)
		);
		Assert.Equal(
			input,
			await DecodeAsync(
				BaseEncodingKind.Z85,
				Encoding.ASCII.GetBytes(
					"@.FaC"
				)
			)
		);
	}

	[Fact]
	public async Task Base58PreservesLeadingZeroBytes() {
		var input = Encoding.ASCII.GetBytes(
			"Hello World!"
		);
		Assert.Equal(
			"2NEpo7TZRRrLZSi2U",
			TrimEncodedNewline(
				await EncodeAsync(
					BaseEncodingKind.Base58,
					input
				)
			)
		);
		var leadingZeroInput = new byte[] {
			0,
			0,
			0x28,
			0x7F,
			0xB4,
			0xCD
		};
		Assert.Equal(
			"11233QC4",
			TrimEncodedNewline(
				await EncodeAsync(
					BaseEncodingKind.Base58,
					leadingZeroInput
				)
			)
		);
		Assert.Equal(
			leadingZeroInput,
			await DecodeAsync(
				BaseEncodingKind.Base58,
				Encoding.ASCII.GetBytes(
					"11233QC4"
				)
			)
		);
	}

	[Fact]
	public async Task EncodingWrapsAtArbitraryColumnsAndEndsWithNewline() {
		Assert.Equal(
			"Zm9\nvYm\nFy\n",
			Encoding.ASCII.GetString(
				await EncodeAsync(
					BaseEncodingKind.Base64,
					Encoding.ASCII.GetBytes(
						"foobar"
					),
					wrapColumns: 3
				)
			)
		);
		Assert.Equal(
			"Zm9vYmFy",
			Encoding.ASCII.GetString(
				await EncodeAsync(
					BaseEncodingKind.Base64,
					Encoding.ASCII.GetBytes(
						"foobar"
					),
					wrapColumns: 0
				)
			)
		);
	}

	[Fact]
	public async Task EmptyInputProducesNoEncodedOutput() {
		Assert.Empty(
			await EncodeAsync(
				BaseEncodingKind.Base64,
				Array.Empty<byte>()
			)
		);
	}

	[Fact]
	public async Task DecoderAcceptsUnpaddedAndConcatenatedPaddedQuanta() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"i"
			),
			await DecodeAsync(
				BaseEncodingKind.Base64,
				Encoding.ASCII.GetBytes(
					"aQ"
				)
			)
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"12341234"
			),
			await DecodeAsync(
				BaseEncodingKind.Base64,
				Encoding.ASCII.GetBytes(
					"MTIzNA==\nMTIzNA"
				)
			)
		);
	}

	[Fact]
	public async Task DecoderRejectsPartialPaddingAndNonzeroUnusedBits() {
		await Assert.ThrowsAsync<BaseEncodingException>(
			async () => await DecodeAsync(
				BaseEncodingKind.Base64,
				Encoding.ASCII.GetBytes(
					"MTIzNA=\n"
				)
			)
		);
		await Assert.ThrowsAsync<BaseEncodingException>(
			async () => await DecodeAsync(
				BaseEncodingKind.Base32,
				Encoding.ASCII.GetBytes(
					"MZ======"
				)
			)
		);
	}

	[Fact]
	public async Task IgnoreGarbageSkipsUnrecognizedBytes() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"f"
			),
			await DecodeAsync(
				BaseEncodingKind.Base64,
				Encoding.ASCII.GetBytes(
					"Z #g==\r"
				),
				ignoreGarbage: true
			)
		);
		await Assert.ThrowsAsync<BaseEncodingException>(
			async () => await DecodeAsync(
				BaseEncodingKind.Base64,
				Encoding.ASCII.GetBytes(
					"Z #g==\r"
				)
			)
		);
	}

	[Fact]
	public async Task ChunkBoundariesDoNotChangeResults() {
		var plain = Enumerable.Range(
			0,
			1025
		).Select(
			value => unchecked( (byte)value )
		).ToArray();
		await using var chunked = new ChunkedReadStream(
			plain,
			3
		);
		await using var encoded = new MemoryStream();
		await BaseEncodingProcessor.EncodeAsync(
			chunked,
			encoded,
			BaseEncodingKind.Base32,
			wrapColumns: 0
		);
		encoded.Position = 0;
		await using var chunkedEncoded = new ChunkedReadStream(
			encoded.ToArray(),
			2
		);
		await using var decoded = new MemoryStream();
		await BaseEncodingProcessor.DecodeAsync(
			chunkedEncoded,
			decoded,
			BaseEncodingKind.Base32
		);
		Assert.Equal(
			plain,
			decoded.ToArray()
		);
	}

	[Fact]
	public async Task Z85RequiresCompleteGroups() {
		await Assert.ThrowsAsync<BaseEncodingException>(
			async () => await EncodeAsync(
				BaseEncodingKind.Z85,
				new byte[] {
					1,
					2,
					3
				}
			)
		);
		await Assert.ThrowsAsync<BaseEncodingException>(
			async () => await DecodeAsync(
				BaseEncodingKind.Z85,
				Encoding.ASCII.GetBytes(
					"1234"
				)
			)
		);
	}

	private static async Task<byte[]> EncodeAsync(
		BaseEncodingKind kind,
		byte[] input,
		long wrapColumns = 76
	) {
		await using var source = new MemoryStream(
			input,
			writable: false
		);
		await using var destination = new MemoryStream();
		await BaseEncodingProcessor.EncodeAsync(
			source,
			destination,
			kind,
			wrapColumns
		);
		return destination.ToArray();
	}

	private static async Task<byte[]> DecodeAsync(
		BaseEncodingKind kind,
		byte[] input,
		bool ignoreGarbage = false
	) {
		await using var source = new MemoryStream(
			input,
			writable: false
		);
		await using var destination = new MemoryStream();
		await BaseEncodingProcessor.DecodeAsync(
			source,
			destination,
			kind,
			ignoreGarbage
		);
		return destination.ToArray();
	}

	private static string TrimEncodedNewline(
		byte[] value
	) {
		return Encoding.ASCII.GetString(
			value
		).TrimEnd(
			'\n'
		);
	}

	private sealed class ChunkedReadStream : Stream {

		private readonly int myChunkSize;
		private readonly MemoryStream myStream;

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();

		public override long Position {
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public ChunkedReadStream(
			byte[] data,
			int chunkSize
		) {
			this.myStream = new MemoryStream(
				data,
				writable: false
			);
			this.myChunkSize = chunkSize;
		}

		public override void Flush() {
		}

		public override int Read(
			byte[] buffer,
			int offset,
			int count
		) {
			return this.myStream.Read(
				buffer,
				offset,
				Math.Min(
					count,
					this.myChunkSize
				)
			);
		}

		public override ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			return this.myStream.ReadAsync(
				buffer.Slice(
					0,
					Math.Min(
						buffer.Length,
						this.myChunkSize
					)
				),
				cancellationToken
			);
		}

		public override long Seek(
			long offset,
			SeekOrigin origin
		) {
			throw new NotSupportedException();
		}

		public override void SetLength(
			long value
		) {
			throw new NotSupportedException();
		}

		public override void Write(
			byte[] buffer,
			int offset,
			int count
		) {
			throw new NotSupportedException();
		}

		protected override void Dispose(
			bool disposing
		) {
			if ( disposing ) {
				this.myStream.Dispose();
			}
			base.Dispose(
				disposing
			);
		}

	}

}
