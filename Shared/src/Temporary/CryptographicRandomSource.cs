namespace Icod.CoreUtils.Shared.Temporary;

using System.Security.Cryptography;

/// <summary>Uses the operating system cryptographic random-number generator.</summary>
public sealed class CryptographicRandomSource : ISecureRandomSource {
	/// <summary>Gets the shared cryptographic random source.</summary>
	public static CryptographicRandomSource Instance { get; } = new();

	private CryptographicRandomSource() {
	}

	/// <inheritdoc/>
	public int GetInt32( int exclusiveUpperBound ) {
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( exclusiveUpperBound );
		return RandomNumberGenerator.GetInt32( exclusiveUpperBound );
	}
}
