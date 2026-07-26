namespace Icod.CoreUtils.Shared.Numerics;

using System.Collections.ObjectModel;

/// <summary>
/// Provides exact suffix multipliers for floating-point operands.
/// </summary>
public sealed class FloatingSuffixTable {

	private readonly ReadOnlyDictionary<string, double> myMultipliers;

	/// <summary>Gets a suffix table for seconds, minutes, hours, and days.</summary>
	public static FloatingSuffixTable TimeSeconds {
		get;
	} = new FloatingSuffixTable(
		new Dictionary<string, double>( StringComparer.Ordinal ) {
			[ string.Empty ] = 1.0,
			[ "s" ] = 1.0,
			[ "m" ] = 60.0,
			[ "h" ] = 3600.0,
			[ "d" ] = 86400.0
		}
	);

	/// <summary>
	/// Initializes a floating suffix table.
	/// </summary>
	public FloatingSuffixTable(
		IReadOnlyDictionary<string, double> multipliers
	) {
		ArgumentNullException.ThrowIfNull(
			multipliers
		);
		var output = new Dictionary<string, double>(
			StringComparer.Ordinal
		);
		foreach ( var pair in multipliers ) {
			if (
				!double.IsFinite( pair.Value )
				|| pair.Value <= 0.0
			) {
				throw new ArgumentOutOfRangeException(
					nameof( multipliers ),
					$"Suffix '{pair.Key}' must have a positive finite multiplier."
				);
			}
			output.Add(
				pair.Key,
				pair.Value
			);
		}
		this.myMultipliers = new ReadOnlyDictionary<string, double>(
			output
		);
	}

	/// <summary>Looks up an exact suffix.</summary>
	public bool TryGetMultiplier(
		string suffix,
		out double multiplier
	) {
		return this.myMultipliers.TryGetValue(
			suffix,
			out multiplier
		);
	}
}
