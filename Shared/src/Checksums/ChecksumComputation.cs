namespace Icod.CoreUtils.Shared.Checksums;

/// <summary>
/// Represents a completed checksum computation.
/// </summary>
public sealed record ChecksumComputation(
	ChecksumAlgorithmKind Algorithm,
	byte[]? Digest,
	ulong? NumericValue,
	long Length,
	long BlockCount
);
