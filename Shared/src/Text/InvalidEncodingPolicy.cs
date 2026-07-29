namespace Icod.CoreUtils.Shared.Text;

/// <summary>Specifies how invalid UTF-8 input is represented by <see cref="TextUnitReader"/>.</summary>
public enum InvalidEncodingPolicy {
	/// <summary>Returns each invalid source byte as a distinct <see cref="TextUnitKind.InvalidByte"/> unit.</summary>
	PreserveBytes,
	/// <summary>Returns U+FFFD for each invalid source byte while retaining the replaced byte in the unit.</summary>
	Replace,
	/// <summary>Throws <see cref="System.Text.DecoderFallbackException"/> at the first invalid source byte.</summary>
	Throw
}
