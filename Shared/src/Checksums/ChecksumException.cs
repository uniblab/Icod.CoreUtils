namespace Icod.CoreUtils.Shared.Checksums;

/// <summary>
/// Represents an invalid checksum option, unsupported algorithm, or malformed
/// checksum record.
/// </summary>
public sealed class ChecksumException : Exception {

	/// <summary>
	/// Initializes a checksum exception.
	/// </summary>
	public ChecksumException(
		string message
	) : base(
		message
	) {
	}

}
