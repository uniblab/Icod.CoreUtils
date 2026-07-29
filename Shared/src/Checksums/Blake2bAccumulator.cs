namespace Icod.CoreUtils.Shared.Checksums;

using System.Buffers.Binary;
using System.Numerics;

/// <summary>
/// Provides the blake2b accumulator implementation.
/// </summary>
internal sealed class Blake2bAccumulator : IChecksumAccumulator {

	private static readonly ulong[] InitializationVector = new ulong[] {
		0x6A09E667F3BCC908,
		0xBB67AE8584CAA73B,
		0x3C6EF372FE94F82B,
		0xA54FF53A5F1D36F1,
		0x510E527FADE682D1,
		0x9B05688C2B3E6C1F,
		0x1F83D9ABFB41BD6B,
		0x5BE0CD19137E2179
	};

	private static readonly byte[,] Sigma = new byte[,] {
		{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
		{ 14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3 },
		{ 11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4 },
		{ 7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8 },
		{ 9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13 },
		{ 2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9 },
		{ 12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11 },
		{ 13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10 },
		{ 6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5 },
		{ 10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0 },
		{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
		{ 14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3 }
	};

	private readonly byte[] myBuffer = new byte[ 128 ];
	private int myBufferCount;
	private bool myCompleted;
	private readonly int myDigestLength;
	private readonly ulong[] myState = new ulong[ 8 ];
	private ulong myTotalHigh;
	private ulong myTotalLow;

	/// <summary>
	/// Initializes a new instance of the Blake2bAccumulator class.
	/// </summary>
	public Blake2bAccumulator(
		int digestLength
	) {
		if (
			digestLength < 1
			|| 64 < digestLength
		) {
			throw new ArgumentOutOfRangeException(
				nameof( digestLength )
			);
		}
		this.myDigestLength = digestLength;
		InitializationVector.CopyTo(
			this.myState,
			0
		);
		this.myState[ 0 ] ^= 0x01010000UL ^ checked(
			(uint)digestLength
		);
	}

	/// <summary>
	/// Performs the append operation.
	/// </summary>
	public void Append(
		ReadOnlySpan<byte> data
	) {
		if ( this.myCompleted ) {
			throw new InvalidOperationException(
				"The checksum has already been completed."
			);
		}

		if ( 0 < this.myBufferCount ) {
			var fill = Math.Min(
				128 - this.myBufferCount,
				data.Length
			);
			data.Slice(
				0,
				fill
			).CopyTo(
				this.myBuffer.AsSpan(
					this.myBufferCount
				)
			);
			this.myBufferCount += fill;
			data = data.Slice(
				fill
			);
			if (
				128 == this.myBufferCount
				&& !data.IsEmpty
			) {
				this.IncrementCounter(
					128
				);
				this.Compress(
					this.myBuffer,
					isFinal: false
				);
				this.myBufferCount = 0;
			}
		}

		while ( 128 < data.Length ) {
			this.IncrementCounter(
				128
			);
			this.Compress(
				data.Slice(
					0,
					128
				),
				isFinal: false
			);
			data = data.Slice(
				128
			);
		}

		if ( !data.IsEmpty ) {
			data.CopyTo(
				this.myBuffer.AsSpan(
					this.myBufferCount
				)
			);
			this.myBufferCount += data.Length;
		}
	}

	/// <summary>
	/// Completes the operation and returns its result.
	/// </summary>
	public byte[] Complete(
		long length
	) {
		if ( this.myCompleted ) {
			throw new InvalidOperationException(
				"The checksum has already been completed."
			);
		}
		this.myCompleted = true;
		this.IncrementCounter(
			checked(
				(ulong)this.myBufferCount
			)
		);
		this.myBuffer.AsSpan(
			this.myBufferCount
		).Clear();
		this.Compress(
			this.myBuffer,
			isFinal: true
		);

		var fullDigest = new byte[ 64 ];
		for (
			var index = 0;
			index < this.myState.Length;
			index++
		) {
			BinaryPrimitives.WriteUInt64LittleEndian(
				fullDigest.AsSpan(
					index * 8,
					8
				),
				this.myState[ index ]
			);
		}
		return fullDigest.AsSpan(
			0,
			this.myDigestLength
		).ToArray();
	}

	/// <summary>
	/// Releases resources used by this instance.
	/// </summary>
	public void Dispose() {
		Array.Clear(
			this.myBuffer
		);
		Array.Clear(
			this.myState
		);
	}

	private void IncrementCounter(
		ulong value
	) {
		var previous = this.myTotalLow;
		this.myTotalLow += value;
		if ( this.myTotalLow < previous ) {
			this.myTotalHigh++;
		}
	}

	private void Compress(
		ReadOnlySpan<byte> block,
		bool isFinal
	) {
		Span<ulong> message = stackalloc ulong[ 16 ];
		Span<ulong> working = stackalloc ulong[ 16 ];
		for (
			var index = 0;
			index < 16;
			index++
		) {
			message[ index ] = BinaryPrimitives.ReadUInt64LittleEndian(
				block.Slice(
					index * 8,
					8
				)
			);
		}
		for (
			var index = 0;
			index < 8;
			index++
		) {
			working[ index ] = this.myState[ index ];
			working[ index + 8 ] = InitializationVector[ index ];
		}
		working[ 12 ] ^= this.myTotalLow;
		working[ 13 ] ^= this.myTotalHigh;
		if ( isFinal ) {
			working[ 14 ] = ~working[ 14 ];
		}

		for (
			var round = 0;
			round < 12;
			round++
		) {
			Mix( working, 0, 4, 8, 12, message[ Sigma[ round, 0 ] ], message[ Sigma[ round, 1 ] ] );
			Mix( working, 1, 5, 9, 13, message[ Sigma[ round, 2 ] ], message[ Sigma[ round, 3 ] ] );
			Mix( working, 2, 6, 10, 14, message[ Sigma[ round, 4 ] ], message[ Sigma[ round, 5 ] ] );
			Mix( working, 3, 7, 11, 15, message[ Sigma[ round, 6 ] ], message[ Sigma[ round, 7 ] ] );
			Mix( working, 0, 5, 10, 15, message[ Sigma[ round, 8 ] ], message[ Sigma[ round, 9 ] ] );
			Mix( working, 1, 6, 11, 12, message[ Sigma[ round, 10 ] ], message[ Sigma[ round, 11 ] ] );
			Mix( working, 2, 7, 8, 13, message[ Sigma[ round, 12 ] ], message[ Sigma[ round, 13 ] ] );
			Mix( working, 3, 4, 9, 14, message[ Sigma[ round, 14 ] ], message[ Sigma[ round, 15 ] ] );
		}

		for (
			var index = 0;
			index < 8;
			index++
		) {
			this.myState[ index ] ^= working[ index ] ^ working[ index + 8 ];
		}
	}

	private static void Mix(
		Span<ulong> state,
		int a,
		int b,
		int c,
		int d,
		ulong x,
		ulong y
	) {
		state[ a ] = unchecked( state[ a ] + state[ b ] + x );
		state[ d ] = BitOperations.RotateRight( state[ d ] ^ state[ a ], 32 );
		state[ c ] = unchecked( state[ c ] + state[ d ] );
		state[ b ] = BitOperations.RotateRight( state[ b ] ^ state[ c ], 24 );
		state[ a ] = unchecked( state[ a ] + state[ b ] + y );
		state[ d ] = BitOperations.RotateRight( state[ d ] ^ state[ a ], 16 );
		state[ c ] = unchecked( state[ c ] + state[ d ] );
		state[ b ] = BitOperations.RotateRight( state[ b ] ^ state[ c ], 63 );
	}

}
