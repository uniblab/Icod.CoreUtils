namespace Icod.CoreUtils.Shared.Numerics;

using System.Numerics;

/// <summary>
/// Associates an exact suffix with a positive integer multiplier.
/// </summary>
public sealed class NumericSuffix {

	/// <summary>Gets the exact suffix text.</summary>
	public string Name {
		get;
	}

	/// <summary>Gets the positive multiplier.</summary>
	public BigInteger Multiplier {
		get;
	}

	/// <summary>
	/// Creates a validated suffix.
	/// </summary>
	public NumericSuffix(
		string name,
		BigInteger multiplier
	) {
		ArgumentNullException.ThrowIfNull(
			name
		);
		if ( multiplier <= BigInteger.Zero ) {
			throw new ArgumentOutOfRangeException(
				nameof( multiplier ),
				"A suffix multiplier must be positive."
			);
		}

		this.Name = name;
		this.Multiplier = multiplier;
	}

	/// <summary>
	/// Creates a validated suffix.
	/// </summary>
	public NumericSuffix(
		string name,
		long multiplier
	) : this(
		name,
		new BigInteger( multiplier )
	) {
	}

}
