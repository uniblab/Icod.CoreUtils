namespace Icod.CoreUtils.Checksum.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Checksums;
using Xunit;

public sealed class ChecksumProcessorTests {

	[Theory]
	[InlineData( ChecksumAlgorithmKind.Md5, "900150983cd24fb0d6963f7d28e17f72" )]
	[InlineData( ChecksumAlgorithmKind.Sha1, "a9993e364706816aba3e25717850c26c9cd0d89d" )]
	[InlineData( ChecksumAlgorithmKind.Sha224, "23097d223405d8228642a477bda255b32aadbce4bda0b3f7e36c9da7" )]
	[InlineData( ChecksumAlgorithmKind.Sha256, "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad" )]
	[InlineData( ChecksumAlgorithmKind.Sha384, "cb00753f45a35e8bb5a03d699ac65007272c32ab0ded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7" )]
	[InlineData( ChecksumAlgorithmKind.Sha512, "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f" )]
	[InlineData( ChecksumAlgorithmKind.Blake2b, "ba80a53f981c4d0d6a2797b69f12f6e94c212f14685ac4b74b12bb6fdbffa2d17d87c5392aab792dc252d5de4533cc9518d38aa8dbf1925ab92386edd4009923" )]
	[InlineData( ChecksumAlgorithmKind.Sha3_224, "e642824c3f8cf24ad09234ee7d3c766fc9a3a5168d0c94ad73b46fdf" )]
	[InlineData( ChecksumAlgorithmKind.Sha3_256, "3a985da74fe225b2045c172d6bd390bd855f086e3e9d525b46bfe24511431532" )]
	[InlineData( ChecksumAlgorithmKind.Sha3_384, "ec01498288516fc926459f58e2c6ad8df9b473cb0fc08c2596da7cf0e49be4b298d88cea927ac7f539f1edf228376d25" )]
	[InlineData( ChecksumAlgorithmKind.Sha3_512, "b751850b1a57168a5693cd924b6b096e08f621827444f70d884f5d0240d2712e10e116e9192af3c91a7ec57647e3934057340b4cf408d5a56592f8274eec53f0" )]
	[InlineData( ChecksumAlgorithmKind.Sm3, "66c7f0f462eeedd9d1f2d46bdc10e4e24167c4875cf2f7a2297da02b8f4ba8e0" )]
	public async Task MatchesKnownAbcVectors(
		ChecksumAlgorithmKind algorithm,
		string expected
	) {
		await using var input = new MemoryStream(
			Encoding.ASCII.GetBytes(
				"abc"
			),
			writable: false
		);
		var result = await ChecksumProcessor.ComputeAsync(
			input,
			algorithm
		);
		Assert.Equal(
			expected,
			Convert.ToHexString(
				result.Digest!
			).ToLowerInvariant()
		);
		Assert.Equal(
			3,
			result.Length
		);
	}

	[Fact]
	public async Task Blake2bSupportsVariableDigestLength() {
		await using var input = new MemoryStream(
			Encoding.ASCII.GetBytes(
				"abc"
			),
			writable: false
		);
		var result = await ChecksumProcessor.ComputeAsync(
			input,
			ChecksumAlgorithmKind.Blake2b,
			256
		);
		Assert.Equal(
			"bddd813c634239723171ef3fee98579b94964e3bb1cb3e427262c8c068d52319",
			Convert.ToHexString(
				result.Digest!
			).ToLowerInvariant()
		);
	}

	[Theory]
	[InlineData( ChecksumAlgorithmKind.Crc, 1219131554UL, 0L )]
	[InlineData( ChecksumAlgorithmKind.Crc32b, 891568578UL, 0L )]
	[InlineData( ChecksumAlgorithmKind.Bsd, 16556UL, 1L )]
	[InlineData( ChecksumAlgorithmKind.SysV, 294UL, 1L )]
	public async Task MatchesNativeChecksumVectors(
		ChecksumAlgorithmKind algorithm,
		ulong expected,
		long expectedBlocks
	) {
		await using var input = new MemoryStream(
			Encoding.ASCII.GetBytes(
				"abc"
			),
			writable: false
		);
		var result = await ChecksumProcessor.ComputeAsync(
			input,
			algorithm
		);
		Assert.Equal(
			expected,
			result.NumericValue
		);
		Assert.Equal(
			expectedBlocks,
			result.BlockCount
		);
	}

	[Fact]
	public async Task ProcessesShortReadsWithoutMaterializingInput() {
		var data = Enumerable.Range(
			0,
			10000
		).Select(
			value => checked( (byte)value )
		).ToArray();
		await using var chunked = new ChunkedReadStream(
			data,
			3
		);
		await using var normal = new MemoryStream(
			data,
			writable: false
		);
		var chunkedResult = await ChecksumProcessor.ComputeAsync(
			chunked,
			ChecksumAlgorithmKind.Sha256
		);
		var normalResult = await ChecksumProcessor.ComputeAsync(
			normal,
			ChecksumAlgorithmKind.Sha256
		);
		Assert.Equal(
			normalResult.Digest,
			chunkedResult.Digest
		);
	}

	[Fact]
	public async Task CancellationIsObserved() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		await using var input = new MemoryStream(
			Encoding.ASCII.GetBytes(
				"abc"
			),
			writable: false
		);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			async () => await ChecksumProcessor.ComputeAsync(
				input,
				ChecksumAlgorithmKind.Sha256,
				cancellationToken: cancellation.Token
			)
		);
	}

	private sealed class ChunkedReadStream : Stream {

		private readonly int myChunkSize;
		private readonly MemoryStream myInner;

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
			this.myInner = new MemoryStream(
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
			return this.myInner.Read(
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
			return this.myInner.ReadAsync(
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
				this.myInner.Dispose();
			}
			base.Dispose(
				disposing
			);
		}

	}

}
