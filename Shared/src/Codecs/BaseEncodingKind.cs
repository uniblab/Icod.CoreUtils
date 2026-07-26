namespace Icod.CoreUtils.Shared.Codecs;

/// <summary>
/// Identifies an encoding supported by the base-encoding command family.
/// </summary>
public enum BaseEncodingKind {

	/// <summary>RFC 4648 Base16.</summary>
	Base16,

	/// <summary>RFC 4648 Base32.</summary>
	Base32,

	/// <summary>RFC 4648 extended-hex Base32.</summary>
	Base32Hex,

	/// <summary>Visually unambiguous Base58.</summary>
	Base58,

	/// <summary>RFC 4648 Base64.</summary>
	Base64,

	/// <summary>RFC 4648 URL-safe Base64.</summary>
	Base64Url,

	/// <summary>Binary text with the least-significant bit first.</summary>
	Base2Lsbf,

	/// <summary>Binary text with the most-significant bit first.</summary>
	Base2Msbf,

	/// <summary>ZeroMQ Z85.</summary>
	Z85

}
