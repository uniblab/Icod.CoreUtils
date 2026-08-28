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

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

/// <summary>
/// Defines the incremental contract used by checksum implementations.
/// </summary>
internal interface IChecksumAccumulator : IDisposable {

	/// <summary>Appends source bytes to the checksum state.</summary>
	/// <param name="data">The source bytes.</param>
	void Append(
		ReadOnlySpan<byte> data
	);

	/// <summary>Completes the checksum and returns its binary representation.</summary>
	/// <param name="length">The total number of source bytes processed.</param>
	/// <returns>The completed checksum bytes.</returns>
	byte[] Complete(
		long length
	);

}

/// <summary>
/// Provides the incremental hash accumulator implementation.
/// </summary>
internal sealed class IncrementalHashAccumulator : IChecksumAccumulator {

	private readonly IncrementalHash myHash;

	/// <summary>
	/// Initializes a new instance of the IncrementalHashAccumulator class.
	/// </summary>
	public IncrementalHashAccumulator(
		HashAlgorithmName algorithm
	) {
		this.myHash = IncrementalHash.CreateHash(
			algorithm
		);
	}

	/// <summary>
	/// Performs the append operation.
	/// </summary>
	public void Append(
		ReadOnlySpan<byte> data
	) {
		this.myHash.AppendData(
			data
		);
	}

	/// <summary>
	/// Completes the operation and returns its result.
	/// </summary>
	public byte[] Complete(
		long length
	) {
		return this.myHash.GetHashAndReset();
	}

	/// <summary>
	/// Releases resources used by this instance.
	/// </summary>
	public void Dispose() {
		this.myHash.Dispose();
	}

}

/// <summary>
/// Provides the posix crc accumulator implementation.
/// </summary>
internal sealed class PosixCrcAccumulator : IChecksumAccumulator {

	private static readonly uint[] Table = CreateTable();
	private uint myCrc;

	/// <summary>
	/// Performs the append operation.
	/// </summary>
	public void Append(
		ReadOnlySpan<byte> data
	) {
		foreach ( var value in data ) {
			this.myCrc = unchecked(
				this.myCrc << 8
			) ^ Table[
				checked(
					(int)(
						(
							this.myCrc >> 24
						) ^ value
					)
				)
			];
		}
	}

	/// <summary>
	/// Completes the operation and returns its result.
	/// </summary>
	public byte[] Complete(
		long length
	) {
		var remaining = checked(
			(ulong)length
		);
		while ( 0 != remaining ) {
			var value = checked(
				(byte)(
					remaining & 0xFF
				)
			);
			this.myCrc = unchecked(
				this.myCrc << 8
			) ^ Table[
				checked(
					(int)(
						(
							this.myCrc >> 24
						) ^ value
					)
				)
			];
			remaining >>= 8;
		}
		var result = ~this.myCrc;
		var output = new byte[ 4 ];
		BinaryPrimitives.WriteUInt32BigEndian(
			output,
			result
		);
		return output;
	}

	/// <summary>
	/// Releases resources used by this instance.
	/// </summary>
	public void Dispose() {
	}

	private static uint[] CreateTable() {
		const uint polynomial = 0x04C11DB7;
		var table = new uint[ 256 ];
		for (
			var index = 0;
			index < table.Length;
			index++
		) {
			var value = checked(
				(uint)index << 24
			);
			for (
				var bit = 0;
				bit < 8;
				bit++
			) {
				value = 0 != (
					value & 0x80000000
				)
					? unchecked(
						value << 1
					) ^ polynomial
					: unchecked(
						value << 1
					)
				;
			}
			table[ index ] = value;
		}
		return table;
	}

}

/// <summary>
/// Provides the crc32b accumulator implementation.
/// </summary>
internal sealed class Crc32bAccumulator : IChecksumAccumulator {

	private static readonly uint[] Table = CreateTable();
	private uint myCrc = uint.MaxValue;

	/// <summary>
	/// Performs the append operation.
	/// </summary>
	public void Append(
		ReadOnlySpan<byte> data
	) {
		foreach ( var value in data ) {
			this.myCrc = (
				this.myCrc >> 8
			) ^ Table[
				checked(
					(int)(
						(
							this.myCrc ^ value
						) & 0xFF
					)
				)
			];
		}
	}

	/// <summary>
	/// Completes the operation and returns its result.
	/// </summary>
	public byte[] Complete(
		long length
	) {
		var output = new byte[ 4 ];
		BinaryPrimitives.WriteUInt32BigEndian(
			output,
			~this.myCrc
		);
		return output;
	}

	/// <summary>
	/// Releases resources used by this instance.
	/// </summary>
	public void Dispose() {
	}

	private static uint[] CreateTable() {
		const uint polynomial = 0xEDB88320;
		var table = new uint[ 256 ];
		for (
			var index = 0;
			index < table.Length;
			index++
		) {
			var value = checked(
				(uint)index
			);
			for (
				var bit = 0;
				bit < 8;
				bit++
			) {
				value = 0 != (
					value & 1
				)
					? (
						value >> 1
					) ^ polynomial
					: value >> 1
				;
			}
			table[ index ] = value;
		}
		return table;
	}

}

/// <summary>
/// Provides the bsd sum accumulator implementation.
/// </summary>
internal sealed class BsdSumAccumulator : IChecksumAccumulator {

	private ushort myValue;

	/// <summary>
	/// Performs the append operation.
	/// </summary>
	public void Append(
		ReadOnlySpan<byte> data
	) {
		foreach ( var value in data ) {
			this.myValue = unchecked(
				(ushort)(
					(
						this.myValue >> 1
					) | (
						this.myValue << 15
					)
				)
			);
			this.myValue = unchecked(
				(ushort)(
					this.myValue + value
				)
			);
		}
	}

	/// <summary>
	/// Completes the operation and returns its result.
	/// </summary>
	public byte[] Complete(
		long length
	) {
		var output = new byte[ 2 ];
		BinaryPrimitives.WriteUInt16BigEndian(
			output,
			this.myValue
		);
		return output;
	}

	/// <summary>
	/// Releases resources used by this instance.
	/// </summary>
	public void Dispose() {
	}

}

/// <summary>
/// Provides the sys v sum accumulator implementation.
/// </summary>
internal sealed class SysVSumAccumulator : IChecksumAccumulator {

	private ulong myValue;

	/// <summary>
	/// Performs the append operation.
	/// </summary>
	public void Append(
		ReadOnlySpan<byte> data
	) {
		foreach ( var value in data ) {
			this.myValue += value;
		}
	}

	/// <summary>
	/// Completes the operation and returns its result.
	/// </summary>
	public byte[] Complete(
		long length
	) {
		var folded = (
			this.myValue & 0xFFFF
		) + (
			this.myValue >> 16
		);
		folded = (
			folded & 0xFFFF
		) + (
			folded >> 16
		);
		var output = new byte[ 2 ];
		BinaryPrimitives.WriteUInt16BigEndian(
			output,
			checked(
				(ushort)folded
			)
		);
		return output;
	}

	/// <summary>
	/// Releases resources used by this instance.
	/// </summary>
	public void Dispose() {
	}

}

/// <summary>
/// Computes checksum and digest values in one asynchronous streaming pass.
/// </summary>
public static class ChecksumProcessor {

	/// <summary>
	/// Computes a checksum from the supplied stream.
	/// </summary>
	public static async Task<ChecksumComputation> ComputeAsync(
		Stream input,
		ChecksumAlgorithmKind algorithm,
		int? lengthBits = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			input
		);
		if ( !input.CanRead ) {
			throw new ArgumentException(
				"The input stream must be readable.",
				nameof( input )
			);
		}

		using var accumulator = CreateAccumulator(
			algorithm,
			lengthBits
		);
		var buffer = ArrayPool<byte>.Shared.Rent(
			64 * 1024
		);
		long length = 0;
		try {
			while ( true ) {
				var count = await input.ReadAsync(
					buffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == count ) {
					break;
				}
				length = checked(
					length + count
				);
				accumulator.Append(
					buffer.AsSpan(
						0,
						count
					)
				);
			}
		} finally {
			ArrayPool<byte>.Shared.Return(
				buffer
			);
		}

		var digest = accumulator.Complete(
			length
		);
		return algorithm switch {
			ChecksumAlgorithmKind.Bsd => new ChecksumComputation(
				algorithm,
				null,
				BinaryPrimitives.ReadUInt16BigEndian(
					digest
				),
				length,
				checked(
					(
						length + 1023
					) / 1024
				)
			),
			ChecksumAlgorithmKind.SysV => new ChecksumComputation(
				algorithm,
				null,
				BinaryPrimitives.ReadUInt16BigEndian(
					digest
				),
				length,
				checked(
					(
						length + 511
					) / 512
				)
			),
			ChecksumAlgorithmKind.Crc
			or ChecksumAlgorithmKind.Crc32b => new ChecksumComputation(
				algorithm,
				digest,
				BinaryPrimitives.ReadUInt32BigEndian(
					digest
				),
				length,
				0
			),
			_ => new ChecksumComputation(
				algorithm,
				digest,
				null,
				length,
				0
			)
		};
	}

	/// <summary>
	/// Gets the canonical digest length for an algorithm.
	/// </summary>
	public static int GetDefaultLengthBits(
		ChecksumAlgorithmKind algorithm
	) {
		return algorithm switch {
			ChecksumAlgorithmKind.Blake2b => 512,
			ChecksumAlgorithmKind.Crc => 32,
			ChecksumAlgorithmKind.Crc32b => 32,
			ChecksumAlgorithmKind.Md5 => 128,
			ChecksumAlgorithmKind.Sha1 => 160,
			ChecksumAlgorithmKind.Sha224 => 224,
			ChecksumAlgorithmKind.Sha256 => 256,
			ChecksumAlgorithmKind.Sha384 => 384,
			ChecksumAlgorithmKind.Sha512 => 512,
			ChecksumAlgorithmKind.Sha3_224 => 224,
			ChecksumAlgorithmKind.Sha3_256 => 256,
			ChecksumAlgorithmKind.Sha3_384 => 384,
			ChecksumAlgorithmKind.Sha3_512 => 512,
			ChecksumAlgorithmKind.Sm3 => 256,
			_ => 16
		};
	}

	/// <summary>
	/// Gets the canonical BSD-style digest label.
	/// </summary>
	public static string GetDisplayName(
		ChecksumAlgorithmKind algorithm
	) {
		return algorithm switch {
			ChecksumAlgorithmKind.Blake2b => "BLAKE2b",
			ChecksumAlgorithmKind.Crc => "CRC",
			ChecksumAlgorithmKind.Crc32b => "CRC32B",
			ChecksumAlgorithmKind.Md5 => "MD5",
			ChecksumAlgorithmKind.Sha1 => "SHA1",
			ChecksumAlgorithmKind.Sha224 => "SHA224",
			ChecksumAlgorithmKind.Sha256 => "SHA256",
			ChecksumAlgorithmKind.Sha384 => "SHA384",
			ChecksumAlgorithmKind.Sha512 => "SHA512",
			ChecksumAlgorithmKind.Sha3_224 => "SHA3-224",
			ChecksumAlgorithmKind.Sha3_256 => "SHA3-256",
			ChecksumAlgorithmKind.Sha3_384 => "SHA3-384",
			ChecksumAlgorithmKind.Sha3_512 => "SHA3-512",
			ChecksumAlgorithmKind.Sm3 => "SM3",
			ChecksumAlgorithmKind.Bsd => "BSD",
			ChecksumAlgorithmKind.SysV => "SYSV",
			_ => algorithm.ToString()
		};
	}

	private static IChecksumAccumulator CreateAccumulator(
		ChecksumAlgorithmKind algorithm,
		int? lengthBits
	) {
		return algorithm switch {
			ChecksumAlgorithmKind.Blake2b => new Blake2bAccumulator(
				ValidateBlake2Length(
					lengthBits ?? 512
				) / 8
			),
			ChecksumAlgorithmKind.Bsd => new BsdSumAccumulator(),
			ChecksumAlgorithmKind.Crc => new PosixCrcAccumulator(),
			ChecksumAlgorithmKind.Crc32b => new Crc32bAccumulator(),
			ChecksumAlgorithmKind.Md5 => new IncrementalHashAccumulator(
				HashAlgorithmName.MD5
			),
			ChecksumAlgorithmKind.Sha1 => new IncrementalHashAccumulator(
				HashAlgorithmName.SHA1
			),
			ChecksumAlgorithmKind.Sha224 => new Sha224Accumulator(),
			ChecksumAlgorithmKind.Sha256 => new IncrementalHashAccumulator(
				HashAlgorithmName.SHA256
			),
			ChecksumAlgorithmKind.Sha384 => new IncrementalHashAccumulator(
				HashAlgorithmName.SHA384
			),
			ChecksumAlgorithmKind.Sha512 => new IncrementalHashAccumulator(
				HashAlgorithmName.SHA512
			),
			ChecksumAlgorithmKind.Sha3_224 => new Sha3Accumulator(
				28
			),
			ChecksumAlgorithmKind.Sha3_256 => new Sha3Accumulator(
				32
			),
			ChecksumAlgorithmKind.Sha3_384 => new Sha3Accumulator(
				48
			),
			ChecksumAlgorithmKind.Sha3_512 => new Sha3Accumulator(
				64
			),
			ChecksumAlgorithmKind.Sm3 => new Sm3Accumulator(),
			ChecksumAlgorithmKind.SysV => new SysVSumAccumulator(),
			_ => throw new ChecksumException(
				$"unsupported checksum algorithm: {algorithm}"
			)
		};
	}

	private static int ValidateBlake2Length(
		int lengthBits
	) {
		if (
			lengthBits < 8
			|| 512 < lengthBits
			|| 0 != lengthBits % 8
		) {
			throw new ChecksumException(
				"BLAKE2b length must be a multiple of 8 from 8 through 512"
			);
		}
		return lengthBits;
	}

}
