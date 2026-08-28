/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace Icod.CoreUtils.Shared.Checksums;

using System.Buffers.Binary;
using System.Numerics;

/// <summary>
/// Provides the sha3 accumulator implementation.
/// </summary>
internal sealed class Sha3Accumulator : IChecksumAccumulator {

	private static readonly ulong[] RoundConstants = new ulong[] {
		0x0000000000000001,
		0x0000000000008082,
		0x800000000000808A,
		0x8000000080008000,
		0x000000000000808B,
		0x0000000080000001,
		0x8000000080008081,
		0x8000000000008009,
		0x000000000000008A,
		0x0000000000000088,
		0x0000000080008009,
		0x000000008000000A,
		0x000000008000808B,
		0x800000000000008B,
		0x8000000000008089,
		0x8000000000008003,
		0x8000000000008002,
		0x8000000000000080,
		0x000000000000800A,
		0x800000008000000A,
		0x8000000080008081,
		0x8000000000008080,
		0x0000000080000001,
		0x8000000080008008
	};

	private static readonly int[] RotationOffsets = new int[] {
		0, 1, 62, 28, 27,
		36, 44, 6, 55, 20,
		3, 10, 43, 25, 39,
		41, 45, 15, 21, 8,
		18, 2, 61, 56, 14
	};

	private readonly byte[] myBuffer;
	private int myBufferCount;
	private bool myCompleted;
	private readonly int myDigestLength;
	private readonly int myRate;
	private readonly ulong[] myState = new ulong[ 25 ];

	/// <summary>
	/// Initializes a new instance of the Sha3Accumulator class.
	/// </summary>
	public Sha3Accumulator(
		int digestLength
	) {
		this.myDigestLength = digestLength;
		this.myRate = digestLength switch {
			28 => 144,
			32 => 136,
			48 => 104,
			64 => 72,
			_ => throw new ArgumentOutOfRangeException(
				nameof( digestLength )
			)
		};
		this.myBuffer = new byte[
			this.myRate
		];
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
		while ( !data.IsEmpty ) {
			var count = Math.Min(
				this.myRate - this.myBufferCount,
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
			if ( this.myRate == this.myBufferCount ) {
				this.Absorb(
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
		this.myBuffer.AsSpan(
			this.myBufferCount
		).Clear();
		this.myBuffer[ this.myBufferCount ] ^= 0x06;
		this.myBuffer[ this.myRate - 1 ] ^= 0x80;
		this.Absorb(
			this.myBuffer
		);

		var output = new byte[
			this.myDigestLength
		];
		var outputIndex = 0;
		while ( outputIndex < output.Length ) {
			var count = Math.Min(
				this.myRate,
				output.Length - outputIndex
			);
			for (
				var offset = 0;
				offset < count;
				offset++
			) {
				var lane = offset / 8;
				var shift = offset % 8 * 8;
				output[ outputIndex + offset ] = checked(
					(byte)(
						this.myState[ lane ] >> shift
						& 0xFF
					)
				);
			}
			outputIndex += count;
			if ( outputIndex < output.Length ) {
				Permute(
					this.myState
				);
			}
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

	private void Absorb(
		ReadOnlySpan<byte> block
	) {
		for (
			var offset = 0;
			offset < this.myRate;
			offset += 8
		) {
			this.myState[ offset / 8 ] ^= BinaryPrimitives.ReadUInt64LittleEndian(
				block.Slice(
					offset,
					8
				)
			);
		}
		Permute(
			this.myState
		);
	}

	private static void Permute(
		Span<ulong> state
	) {
		Span<ulong> c = stackalloc ulong[ 5 ];
		Span<ulong> d = stackalloc ulong[ 5 ];
		Span<ulong> b = stackalloc ulong[ 25 ];

		for (
			var round = 0;
			round < RoundConstants.Length;
			round++
		) {
			for (
				var x = 0;
				x < 5;
				x++
			) {
				c[ x ] = state[ x ]
					^ state[ x + 5 ]
					^ state[ x + 10 ]
					^ state[ x + 15 ]
					^ state[ x + 20 ]
				;
			}
			for (
				var x = 0;
				x < 5;
				x++
			) {
				d[ x ] = c[ ( x + 4 ) % 5 ] ^ BitOperations.RotateLeft(
					c[ ( x + 1 ) % 5 ],
					1
				);
			}
			for (
				var y = 0;
				y < 5;
				y++
			) {
				for (
					var x = 0;
					x < 5;
					x++
				) {
					state[ x + 5 * y ] ^= d[ x ];
				}
			}

			for (
				var y = 0;
				y < 5;
				y++
			) {
				for (
					var x = 0;
					x < 5;
					x++
				) {
					var sourceIndex = x + 5 * y;
					var targetX = y;
					var targetY = (
						2 * x + 3 * y
					) % 5;
					b[ targetX + 5 * targetY ] = BitOperations.RotateLeft(
						state[ sourceIndex ],
						RotationOffsets[ sourceIndex ]
					);
				}
			}

			for (
				var y = 0;
				y < 5;
				y++
			) {
				for (
					var x = 0;
					x < 5;
					x++
				) {
					state[ x + 5 * y ] = b[ x + 5 * y ] ^ (
						~b[ ( x + 1 ) % 5 + 5 * y ]
						& b[ ( x + 2 ) % 5 + 5 * y ]
					);
				}
			}

			state[ 0 ] ^= RoundConstants[ round ];
		}
	}

}
