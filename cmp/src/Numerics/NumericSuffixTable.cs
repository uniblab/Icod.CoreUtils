namespace Icod.DiffUtils.Cmp.Numerics;

using System.Collections.ObjectModel;
using System.Numerics;

/// <summary>Provides exact, culture-independent integer suffix multipliers.</summary>
public sealed class NumericSuffixTable {
	private readonly ReadOnlyDictionary<string, BigInteger> myMultipliers;

	/// <summary>Gets a suffix table containing only the empty suffix.</summary>
	public static NumericSuffixTable None { get; } = new NumericSuffixTable(
		new NumericSuffix( string.Empty, BigInteger.One )
	);

	/// <summary>Initializes a suffix table.</summary>
	public NumericSuffixTable( params NumericSuffix[] suffixes ) : this( (IEnumerable<NumericSuffix>)suffixes ) {
	}

	/// <summary>Initializes a suffix table.</summary>
	public NumericSuffixTable( IEnumerable<NumericSuffix> suffixes ) {
		ArgumentNullException.ThrowIfNull( suffixes );
		var output = new Dictionary<string, BigInteger>( StringComparer.Ordinal );
		foreach ( var suffix in suffixes ) {
			ArgumentNullException.ThrowIfNull( suffix );
			if ( suffix.Multiplier <= BigInteger.Zero ) {
				throw new ArgumentOutOfRangeException( nameof( suffixes ), $"Suffix '{suffix.Name}' must have a positive multiplier." );
			}
			if ( !output.TryAdd( suffix.Name, suffix.Multiplier ) ) {
				throw new ArgumentException( $"Duplicate suffix '{suffix.Name}'.", nameof( suffixes ) );
			}
		}
		this.myMultipliers = new ReadOnlyDictionary<string, BigInteger>( output );
	}

	/// <summary>Looks up an exact suffix.</summary>
	public bool TryGetMultiplier( string suffix, out BigInteger multiplier ) {
		return this.myMultipliers.TryGetValue( suffix, out multiplier );
	}
}
