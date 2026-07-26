namespace Icod.CoreUtils.Shared.Numerics;

/// <summary>
/// Controls handling of numeric values outside the destination range.
/// </summary>
public enum OverflowBehavior {
	/// <summary>Report overflow as an error.</summary>
	Reject,
	/// <summary>Clamp overflow to the nearest representable value.</summary>
	Clamp
}
