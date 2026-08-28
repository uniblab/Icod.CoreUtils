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

/// <summary>
/// Identifies a checksum or message-digest algorithm.
/// </summary>
public enum ChecksumAlgorithmKind {

	/// <summary>BSD rotating 16-bit checksum.</summary>
	Bsd,

	/// <summary>BLAKE2b.</summary>
	Blake2b,

	/// <summary>POSIX cksum CRC.</summary>
	Crc,

	/// <summary>Reflected IEEE CRC-32.</summary>
	Crc32b,

	/// <summary>MD5.</summary>
	Md5,

	/// <summary>SHA-1.</summary>
	Sha1,

	/// <summary>SHA-224.</summary>
	Sha224,

	/// <summary>SHA-256.</summary>
	Sha256,

	/// <summary>SHA-384.</summary>
	Sha384,

	/// <summary>SHA-512.</summary>
	Sha512,

	/// <summary>SHA3-224.</summary>
	Sha3_224,

	/// <summary>SHA3-256.</summary>
	Sha3_256,

	/// <summary>SHA3-384.</summary>
	Sha3_384,

	/// <summary>SHA3-512.</summary>
	Sha3_512,

	/// <summary>SM3.</summary>
	Sm3,

	/// <summary>System V additive 16-bit checksum.</summary>
	SysV

}
