namespace Icod.CoreUtils.Shared.Checksums;

using System.Buffers.Binary;
using System.Numerics;

internal sealed class Sha224Accumulator : IChecksumAccumulator {

	private static readonly uint[] Constants = new uint[] {
		0x428A2F98, 0x71374491, 0xB5C0FBCF, 0xE9B5DBA5,
		0x3956C25B, 0x59F111F1, 0x923F82A4, 0xAB1C5ED5,
		0xD807AA98, 0x12835B01, 0x243185BE, 0x550C7DC3,
		0x72BE5D74, 0x80DEB1FE, 0x9BDC06A7, 0xC19BF174,
		0xE49B69C1, 0xEFBE4786, 0x0FC19DC6, 0x240CA1CC,
		0x2DE92C6F, 0x4A7484AA, 0x5CB0A9DC, 0x76F988DA,
		0x983E5152, 0xA831C66D, 0xB00327C8, 0xBF597FC7,
		0xC6E00BF3, 0xD5A79147, 0x06CA6351, 0x14292967,
		0x27B70A85, 0x2E1B2138, 0x4D2C6DFC, 0x53380D13,
		0x650A7354, 0x766A0ABB, 0x81C2C92E, 0x92722C85,
		0xA2BFE8A1, 0xA81A664B, 0xC24B8B70, 0xC76C51A3,
		0xD192E819, 0xD6990624, 0xF40E3585, 0x106AA070,
		0x19A4C116, 0x1E376C08, 0x2748774C, 0x34B0BCB5,
		0x391C0CB3, 0x4ED8AA4A, 0x5B9CCA4F, 0x682E6FF3,
		0x748F82EE, 0x78A5636F, 0x84C87814, 0x8CC70208,
		0x90BEFFFA, 0xA4506CEB, 0xBEF9A3F7, 0xC67178F2
	};

	private readonly byte[] myBuffer = new byte[ 64 ];
	private int myBufferCount;
	private bool myCompleted;
	private readonly uint[] myState = new uint[] {
		0xC1059ED8,
		0x367CD507,
		0x3070DD17,
		0xF70E5939,
		0xFFC00B31,
		0x68581511,
		0x64F98FA7,
		0xBEFA4FA4
	};
	private ulong myTotalLength;

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

		var output = new byte[ 28 ];
		for (
			var index = 0;
			index < 7;
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
		Span<uint> words = stackalloc uint[ 64 ];
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
			index < 64;
			index++
		) {
			var s0 = BitOperations.RotateRight(
				words[ index - 15 ],
				7
			) ^ BitOperations.RotateRight(
				words[ index - 15 ],
				18
			) ^ (
				words[ index - 15 ] >> 3
			);
			var s1 = BitOperations.RotateRight(
				words[ index - 2 ],
				17
			) ^ BitOperations.RotateRight(
				words[ index - 2 ],
				19
			) ^ (
				words[ index - 2 ] >> 10
			);
			words[ index ] = unchecked(
				words[ index - 16 ]
				+ s0
				+ words[ index - 7 ]
				+ s1
			);
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
			var index = 0;
			index < 64;
			index++
		) {
			var s1 = BitOperations.RotateRight(
				e,
				6
			) ^ BitOperations.RotateRight(
				e,
				11
			) ^ BitOperations.RotateRight(
				e,
				25
			);
			var choice = (
				e & f
			) ^ (
				~e & g
			);
			var temporary1 = unchecked(
				h
					+ s1
					+ choice
					+ Constants[ index ]
					+ words[ index ]
			);
			var s0 = BitOperations.RotateRight(
				a,
				2
			) ^ BitOperations.RotateRight(
				a,
				13
			) ^ BitOperations.RotateRight(
				a,
				22
			);
			var majority = (
				a & b
			) ^ (
				a & c
			) ^ (
				b & c
			);
			var temporary2 = unchecked(
				s0 + majority
			);

			h = g;
			g = f;
			f = e;
			e = unchecked(
				d + temporary1
			);
			d = c;
			c = b;
			b = a;
			a = unchecked(
				temporary1 + temporary2
			);
		}

		this.myState[ 0 ] = unchecked( this.myState[ 0 ] + a );
		this.myState[ 1 ] = unchecked( this.myState[ 1 ] + b );
		this.myState[ 2 ] = unchecked( this.myState[ 2 ] + c );
		this.myState[ 3 ] = unchecked( this.myState[ 3 ] + d );
		this.myState[ 4 ] = unchecked( this.myState[ 4 ] + e );
		this.myState[ 5 ] = unchecked( this.myState[ 5 ] + f );
		this.myState[ 6 ] = unchecked( this.myState[ 6 ] + g );
		this.myState[ 7 ] = unchecked( this.myState[ 7 ] + h );
	}

}
