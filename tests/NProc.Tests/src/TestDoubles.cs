namespace Icod.CoreUtils.NProc.Tests;

using Icod.CoreUtils.NProc;
using Icod.CoreUtils.Shared.Host;

/// <summary>Supplies deterministic processor snapshots to tests.</summary>
public sealed class TestProcessorResourceProvider : IProcessorResourceProvider {
	private readonly Func<CancellationToken, ValueTask<ProcessorResourceSnapshot>> factory;

	/// <summary>Initializes a provider from one snapshot factory.</summary>
	/// <param name="factory">The snapshot factory.</param>
	public TestProcessorResourceProvider(
		Func<CancellationToken, ValueTask<ProcessorResourceSnapshot>> factory
	) {
		this.factory = factory ?? throw new ArgumentNullException( nameof( factory ) );
	}

	/// <summary>Gets the number of provider calls.</summary>
	public int CallCount { get; private set; }

	/// <inheritdoc />
	public ValueTask<ProcessorResourceSnapshot> GetProcessorResourcesAsync(
		CancellationToken cancellationToken = default
	) {
		CallCount++;
		return factory( cancellationToken );
	}
}

/// <summary>Supplies deterministic OpenMP environment values to tests.</summary>
public sealed class TestNProcEnvironment : INProcEnvironment {
	private readonly IReadOnlyDictionary<string, string?> values;

	/// <summary>Initializes an environment.</summary>
	/// <param name="values">The environment values.</param>
	public TestNProcEnvironment( IReadOnlyDictionary<string, string?>? values = null ) {
		this.values = values ?? new Dictionary<string, string?>();
	}

	/// <inheritdoc />
	public string? GetVariable( string name ) {
		return values.TryGetValue( name, out var value ) ? value : null;
	}
}

/// <summary>Creates concise deterministic processor snapshots.</summary>
public static class ProcessorSnapshotFactory {
	/// <summary>Creates a snapshot from optional positive facts.</summary>
	/// <param name="configured">The configured count.</param>
	/// <param name="installed">The installed count.</param>
	/// <param name="online">The online count.</param>
	/// <param name="available">The managed process-available count.</param>
	/// <param name="affinity">The selected affinity count.</param>
	/// <param name="quota">The fractional quota.</param>
	/// <returns>The snapshot.</returns>
	public static ProcessorResourceSnapshot Create(
		int? configured = null,
		int? installed = null,
		int? online = null,
		int? available = null,
		int? affinity = null,
		double? quota = null
	) {
		return new ProcessorResourceSnapshot(
			Count( configured ),
			Count( installed ),
			Count( online ),
			Count( available ),
			affinity.HasValue
				? HostResourceValue<ProcessorAffinityDescriptor>.Available(
					new ProcessorAffinityDescriptor(
						Enumerable.Range( 0, affinity.Value ).Select( static value => (long)value ),
						isComplete: true
					),
					HostResourceProvenance.Derived
				)
				: HostResourceValue<ProcessorAffinityDescriptor>.Unavailable(),
			quota.HasValue
				? HostResourceValue<ProcessorQuotaDescriptor>.Available(
					new ProcessorQuotaDescriptor( quota.Value, null, null, "test" ),
					HostResourceProvenance.Derived
				)
				: HostResourceValue<ProcessorQuotaDescriptor>.NotApplicable(),
			HostResourceValue<ProcessorTopologyDescriptor>.Unavailable()
		);
	}

	private static HostResourceValue<int> Count( int? value ) {
		return value.HasValue
			? HostResourceValue<int>.Available( value.Value, HostResourceProvenance.Derived )
			: HostResourceValue<int>.Unavailable();
	}
}
