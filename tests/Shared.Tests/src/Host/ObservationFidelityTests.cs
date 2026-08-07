namespace Icod.CoreUtils.Shared.Tests.Host;

using Icod.CoreUtils.Shared.Host;
using Xunit;

/// <summary>
/// Verifies the neutral semantic-fidelity vocabulary established by Completion Gate P1.
/// </summary>
public sealed class ObservationFidelityTests {
	/// <summary>Verifies that every required semantic-fidelity category remains explicit.</summary>
	[Fact]
	public void DefinesRequiredFidelityCategories() {
		ObservationFidelity[] expected = [
			ObservationFidelity.Exact,
			ObservationFidelity.Equivalent,
			ObservationFidelity.Approximated,
			ObservationFidelity.Synthesized,
			ObservationFidelity.Unavailable
		];
		Assert.Equal( expected, Enum.GetValues<ObservationFidelity>() );
	}
}
