namespace Icod.CoreUtils.Shared.Temporary;

/// <summary>Provides cryptographically suitable bounded random integers.</summary>
public interface ISecureRandomSource {
	/// <summary>Returns a random integer greater than or equal to zero and less than <paramref name="exclusiveUpperBound"/>.</summary>
	/// <param name="exclusiveUpperBound">The exclusive upper bound.</param>
	/// <returns>A uniformly distributed bounded integer.</returns>
	int GetInt32( int exclusiveUpperBound );
}
