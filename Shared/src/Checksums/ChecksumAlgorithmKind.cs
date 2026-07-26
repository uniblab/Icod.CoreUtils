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
