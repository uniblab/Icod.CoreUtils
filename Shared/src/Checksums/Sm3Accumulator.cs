namespace Icod.CoreUtils.Shared.Checksums;

using System.Buffers.Binary;
using System.Numerics;

/// <summary>
/// Provides the sm3 accumulator implementation.
/// </summary>
internal sealed class Sm3Accumulator : IChecksumAccumulator {

	private readonly byte[] myBuffer = new byte[ 64 ];
	private int myBufferCount;
	private bool myCompleted;
	private readonly uint[] myState = new uint[] {
		0x7380166F,
		0x4914B2B9,
		0x172442D7,
		0xDA8A0600,
		0xA96F30BC,
		0x163138AA,
		0xE38DEE4D,
		0xB0FB0E4E
	};
	private ulong myTotalLength;

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
		this.myTotalLength += checked(
			(ulong)data.Length
		);
		while ( !data.IsEmpty ) {
			var count = Math.Min(
				64 - this.myBufferCount,
				data.Length
			);
			data.Slice(
				0,
				count
			).CopyTo(
				this.myBuffer.AsSpan(
					this.myBufferCount
				)
			);
			this.myBufferCount += count;
			data = data.Slice(
				count
			);
			if ( 64 == this.myBufferCount ) {
				this.Transform(
					this.myBuffer
				);
				this.myBufferCount = 0;
			}
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
		var bitLength = checked(
			this.myTotalLength * 8
		);
		this.myBuffer[ this.myBufferCount++ ] = 0x80;
		if ( 56 < this.myBufferCount ) {
			this.myBuffer.AsSpan(
				this.myBufferCount
			).Clear();
			this.Transform(
				this.myBuffer
			);
			this.myBufferCount = 0;
		}
		this.myBuffer.AsSpan(
			this.myBufferCount,
			56 - this.myBufferCount
		).Clear();
		BinaryPrimitives.WriteUInt64BigEndian(
			this.myBuffer.AsSpan(
				56,
				8
			),
			bitLength
		);
		this.Transform(
			this.myBuffer
		);

		var output = new byte[ 32 ];
		for (
			var index = 0;
			index < 8;
			index++
		) {
			BinaryPrimitives.WriteUInt32BigEndian(
				output.AsSpan(
					index * 4,
					4
				),
				this.myState[ index ]
			);
		}
		return output;
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

	private void Transform(
		ReadOnlySpan<byte> block
	) {
		Span<uint> words = stackalloc uint[ 68 ];
		Span<uint> derived = stackalloc uint[ 64 ];
		for (
			var index = 0;
			index < 16;
			index++
		) {
			words[ index ] = BinaryPrimitives.ReadUInt32BigEndian(
				block.Slice(
					index * 4,
					4
				)
			);
		}
		for (
			var index = 16;
			index < 68;
			index++
		) {
			words[ index ] = P1(
				words[ index - 16 ]
					^ words[ index - 9 ]
					^ BitOperations.RotateLeft(
						words[ index - 3 ],
						15
					)
			) ^ BitOperations.RotateLeft(
				words[ index - 13 ],
				7
			) ^ words[ index - 6 ];
		}
		for (
			var index = 0;
			index < 64;
			index++
		) {
			derived[ index ] = words[ index ] ^ words[ index + 4 ];
		}

		var a = this.myState[ 0 ];
		var b = this.myState[ 1 ];
		var c = this.myState[ 2 ];
		var d = this.myState[ 3 ];
		var e = this.myState[ 4 ];
		var f = this.myState[ 5 ];
		var g = this.myState[ 6 ];
		var h = this.myState[ 7 ];

		for (
			var round = 0;
			round < 64;
			round++
		) {
			var constant = round < 16
				? 0x79CC4519U
				: 0x7A879D8AU
			;
			var ss1 = BitOperations.RotateLeft(
				unchecked(
					BitOperations.RotateLeft(
						a,
						12
					) + e + BitOperations.RotateLeft(
						constant,
						round
					)
				),
				7
			);
			var ss2 = ss1 ^ BitOperations.RotateLeft(
				a,
				12
			);
			var tt1 = unchecked(
				FF(
					a,
					b,
					c,
					round
				) + d + ss2 + derived[ round ]
			);
			var tt2 = unchecked(
				GG(
					e,
					f,
					g,
					round
				) + h + ss1 + words[ round ]
			);
			d = c;
			c = BitOperations.RotateLeft(
				b,
				9
			);
			b = a;
			a = tt1;
			h = g;
			g = BitOperations.RotateLeft(
				f,
				19
			);
			f = e;
			e = P0(
				tt2
			);
		}

		this.myState[ 0 ] ^= a;
		this.myState[ 1 ] ^= b;
		this.myState[ 2 ] ^= c;
		this.myState[ 3 ] ^= d;
		this.myState[ 4 ] ^= e;
		this.myState[ 5 ] ^= f;
		this.myState[ 6 ] ^= g;
		this.myState[ 7 ] ^= h;
	}

	private static uint FF(
		uint x,
		uint y,
		uint z,
		int round
	) {
		return round < 16
			? x ^ y ^ z
			: (
				x & y
			) | (
				x & z
			) | (
				y & z
			)
		;
	}

	private static uint GG(
		uint x,
		uint y,
		uint z,
		int round
	) {
		return round < 16
			? x ^ y ^ z
			: (
				x & y
			) | (
				~x & z
			)
		;
	}

	private static uint P0(
		uint value
	) {
		return value ^ BitOperations.RotateLeft(
			value,
			9
		) ^ BitOperations.RotateLeft(
			value,
			17
		);
	}

	private static uint P1(
		uint value
	) {
		return value ^ BitOperations.RotateLeft(
			value,
			15
		) ^ BitOperations.RotateLeft(
			value,
			23
		);
	}

}
