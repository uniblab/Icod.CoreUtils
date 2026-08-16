namespace Icod.CoreUtils.Shared.Codecs;

/// <summary>
/// Represents malformed encoded input or an invalid source length.
/// </summary>
public sealed class BaseEncodingException : Exception {

	/// <summary>
	/// Initializes an encoding exception.
	/// </summary>
	public BaseEncodingException( string message ) : base( message ) {
	}

}
