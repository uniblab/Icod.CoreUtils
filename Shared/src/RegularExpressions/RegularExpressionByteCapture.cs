namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Represents one participating or nonparticipating byte-preserving subexpression.</summary>
/// <param name="Success">Whether the subexpression participated in the selected match.</param>
/// <param name="ByteIndex">The zero-based source-byte offset, or -1 when the subexpression did not participate.</param>
/// <param name="ByteLength">The source-byte capture length.</param>
/// <param name="Value">The exact captured source bytes, or an empty memory region when the subexpression did not participate.</param>
public sealed record RegularExpressionByteCapture(
	bool Success,
	int ByteIndex,
	int ByteLength,
	ReadOnlyMemory<byte> Value
);
