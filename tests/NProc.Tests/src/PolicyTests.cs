namespace Icod.CoreUtils.NProc.Tests;

using Icod.CoreUtils.NProc;
using Xunit;

/// <summary>Exercises GNU-specific processor-count policy independently of the host.</summary>
public sealed class PolicyTests {
	/// <summary>Verifies that the smaller process-scoped observation is used.</summary>
	[Theory]
	[InlineData( 12, 4, 4 )]
	[InlineData( 3, 8, 3 )]
	public void UsesSmallestProcessScopedObservation( int affinity, int available, int expected ) {
		var decision = Calculate(
			ProcessorSnapshotFactory.Create( online: 32, available: available, affinity: affinity )
		);

		Assert.Equal( checked((ulong)expected), decision.ProcessorCount );
	}

	/// <summary>Verifies that OpenMP thread count supersedes host and quota facts.</summary>
	[Fact]
	public void OpenMpThreadCountOverridesHostAndQuota() {
		var decision = Calculate(
			ProcessorSnapshotFactory.Create( available: 16, affinity: 16, quota: 2 ),
			new Dictionary<string, string?> { ["OMP_NUM_THREADS"] = " 7,3 " }
		);

		Assert.Equal( 7UL, decision.ProcessorCount );
		Assert.Equal( NProcDecisionBasis.OpenMpThreadCount, decision.Basis );
		Assert.False( decision.QuotaApplied );
	}

	/// <summary>Verifies the OpenMP upper limit in both override modes.</summary>
	[Theory]
	[InlineData( "9", "4", 4 )]
	[InlineData( null, "3", 3 )]
	public void OpenMpThreadLimitCapsResult( string? threads, string limit, int expected ) {
		var values = new Dictionary<string, string?> {
			["OMP_NUM_THREADS"] = threads,
			["OMP_THREAD_LIMIT"] = limit
		};
		var decision = Calculate(
			ProcessorSnapshotFactory.Create( available: 12, affinity: 12 ),
			values
		);

		Assert.Equal( checked((ulong)expected), decision.ProcessorCount );
		Assert.True( decision.OpenMpThreadLimitApplied );
	}

	/// <summary>Verifies invalid OpenMP values are ignored.</summary>
	[Fact]
	public void IgnoresInvalidOpenMpValues() {
		var decision = Calculate(
			ProcessorSnapshotFactory.Create( available: 6 ),
			new Dictionary<string, string?> {
				["OMP_NUM_THREADS"] = "0",
				["OMP_THREAD_LIMIT"] = "not-a-number"
			}
		);

		Assert.Equal( 6UL, decision.ProcessorCount );
	}

	/// <summary>Verifies the ordered host-count fallbacks when process facts are absent.</summary>
	[Theory]
	[InlineData( 9, null, null, 9, NProcDecisionBasis.OnlineProcessors )]
	[InlineData( null, 7, null, 7, NProcDecisionBasis.InstalledFallback )]
	[InlineData( null, null, 5, 5, NProcDecisionBasis.ConfiguredFallback )]
	public void UsesHostCountFallbacks(
		int? online,
		int? installed,
		int? configured,
		int expected,
		NProcDecisionBasis expectedBasis
	) {
		var decision = Calculate(
			ProcessorSnapshotFactory.Create(
				configured: configured,
				installed: installed,
				online: online
			)
		);

		Assert.Equal( checked((ulong)expected), decision.ProcessorCount );
		Assert.Equal( expectedBasis, decision.Basis );
	}

	/// <summary>Verifies nearest-unit quota rounding and the required minimum.</summary>
	[Theory]
	[InlineData( 2.49, 2 )]
	[InlineData( 2.50, 3 )]
	[InlineData( 0.10, 1 )]
	public void AppliesRoundedQuota( double quota, int expected ) {
		var decision = Calculate(
			ProcessorSnapshotFactory.Create( available: 16, quota: quota )
		);

		Assert.Equal( checked((ulong)expected), decision.ProcessorCount );
		Assert.True( decision.QuotaApplied );
	}

	/// <summary>Verifies that all mode ignores OpenMP and quota policy.</summary>
	[Fact]
	public void AllUsesInstalledCountAndIgnoresLimits() {
		var options = NProcOptions.Parse( ["--all"] );
		var decision = NProcPolicy.Calculate(
			ProcessorSnapshotFactory.Create( configured: 24, installed: 20, available: 2, quota: 1 ),
			options,
			new TestNProcEnvironment(
				new Dictionary<string, string?> {
					["OMP_NUM_THREADS"] = "3",
					["OMP_THREAD_LIMIT"] = "2"
				}
			)
		);

		Assert.Equal( 20UL, decision.ProcessorCount );
		Assert.False( decision.OpenMpThreadLimitApplied );
		Assert.False( decision.QuotaApplied );
	}

	/// <summary>Verifies that ignore is applied last and saturates at one.</summary>
	[Theory]
	[InlineData( "2", 6 )]
	[InlineData( "8", 1 )]
	[InlineData( "999", 1 )]
	public void IgnoreNeverReducesBelowOne( string ignored, int expected ) {
		var options = NProcOptions.Parse( [string.Concat( "--ignore=", ignored )] );
		var decision = NProcPolicy.Calculate(
			ProcessorSnapshotFactory.Create( available: 8 ),
			options,
			new TestNProcEnvironment()
		);

		Assert.Equal( checked((ulong)expected), decision.ProcessorCount );
	}

	/// <summary>Verifies the controlled minimum when every fact is unavailable.</summary>
	[Fact]
	public void FallsBackToOne() {
		var decision = Calculate( ProcessorSnapshotFactory.Create() );

		Assert.Equal( 1UL, decision.ProcessorCount );
		Assert.Equal( NProcDecisionBasis.MinimumFallback, decision.Basis );
	}

	private static NProcDecision Calculate(
		Icod.Host.ProcessorResourceSnapshot snapshot,
		IReadOnlyDictionary<string, string?>? values = null
	) {
		return NProcPolicy.Calculate(
			snapshot,
			NProcOptions.Parse( [] ),
			new TestNProcEnvironment( values )
		);
	}
}
