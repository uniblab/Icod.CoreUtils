namespace Icod.CoreUtils.Shared.Checksums;

/// <summary>
/// Represents a completed checksum computation.
/// </summary>
/// <param name="Algorithm">The algorithm value.</param>
/// <param name="Digest">The digest value.</param>
/// <param name="NumericValue">The numeric value value.</param>
/// <param name="Length">The length value.</param>
/// <param name="BlockCount">The block count value.</param>
public sealed record ChecksumComputation(
	ChecksumAlgorithmKind Algorithm,
	byte[]? Digest,
	ulong? NumericValue,
	long Length,
	long BlockCount
);
