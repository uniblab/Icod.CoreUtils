namespace Icod.CoreUtils.Shared.Host;

/// <summary>
/// Describes how faithfully one host observation represents the authoritative source semantics.
/// </summary>
public enum ObservationFidelity {
	/// <summary>The value is obtained from the authoritative platform source with matching semantics.</summary>
	Exact,
	/// <summary>The value comes from a different platform source whose semantics are demonstrably equivalent.</summary>
	Equivalent,
	/// <summary>The value is a documented approximation and may differ from the authoritative semantics.</summary>
	Approximated,
	/// <summary>The value is synthesized from other observations rather than exposed directly by the host.</summary>
	Synthesized,
	/// <summary>No defensible value is available on the current host or with the current privileges.</summary>
	Unavailable
}
