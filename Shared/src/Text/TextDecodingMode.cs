namespace Icod.CoreUtils.Shared.Text;

/// <summary>Specifies how a <see cref="TextUnitReader"/> divides source bytes into text units.</summary>
public enum TextDecodingMode {
	/// <summary>Returns every source byte as an independent opaque unit.</summary>
	Bytes,
	/// <summary>Decodes well-formed UTF-8 into Unicode scalars while applying an explicit invalid-input policy.</summary>
	Utf8
}
