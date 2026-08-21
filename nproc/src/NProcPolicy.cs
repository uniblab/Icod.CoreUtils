namespace Icod.CoreUtils.NProc;

using Icod.CommandFramework.Host;
using System.Globalization;

/// <summary>Identifies the primary fact used for one <c>nproc</c> decision.</summary>
public enum NProcDecisionBasis {
	/// <summary>No provider fact was available, so the required minimum was used.</summary>
	MinimumFallback = 0,
	/// <summary>An OpenMP thread-count override supplied the result.</summary>
	OpenMpThreadCount = 1,
	/// <summary>Installed or configured host processors supplied an <c>--all</c> result.</summary>
	InstalledProcessors = 2,
	/// <summary>Current-process affinity supplied or limited the result.</summary>
	ProcessAffinity = 3,
	/// <summary>The managed current-process count supplied or limited the result.</summary>
	ProcessAvailable = 4,
	/// <summary>The online processor count supplied the fallback result.</summary>
	OnlineProcessors = 5,
	/// <summary>The installed processor count supplied the fallback result.</summary>
	InstalledFallback = 6,
	/// <summary>The configured processor count supplied the fallback result.</summary>
	ConfiguredFallback = 7
}

/// <summary>Represents one resolved processor-count decision.</summary>
public sealed record NProcDecision {
	/// <summary>Initializes a decision.</summary>
	/// <param name="processorCount">The positive processor count.</param>
	/// <param name="basis">The primary fact used.</param>
	/// <param name="openMpThreadLimitApplied">Whether <c>OMP_THREAD_LIMIT</c> reduced the result.</param>
	/// <param name="quotaApplied">Whether a host quota reduced the result.</param>
	/// <param name="ignoreApplied">The effective ignored processor count.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="processorCount"/> is zero.</exception>
	public NProcDecision(
		ulong processorCount,
		NProcDecisionBasis basis,
		bool openMpThreadLimitApplied,
		bool quotaApplied,
		ulong ignoreApplied
	) {
		if ( processorCount == 0 ) {
			throw new ArgumentOutOfRangeException( nameof( processorCount ) );
		}
		ProcessorCount = processorCount;
		Basis = basis;
		OpenMpThreadLimitApplied = openMpThreadLimitApplied;
		QuotaApplied = quotaApplied;
		IgnoreApplied = ignoreApplied;
	}

	/// <summary>Gets the positive processor count.</summary>
	public ulong ProcessorCount { get; }

	/// <summary>Gets the primary fact used.</summary>
	public NProcDecisionBasis Basis { get; }

	/// <summary>Gets whether <c>OMP_THREAD_LIMIT</c> reduced the result.</summary>
	public bool OpenMpThreadLimitApplied { get; }

	/// <summary>Gets whether a host quota reduced the result.</summary>
	public bool QuotaApplied { get; }

	/// <summary>Gets the effective ignored processor count.</summary>
	public ulong IgnoreApplied { get; }
}

/// <summary>Applies GNU-specific processor-count policy to factual F2 observations.</summary>
public static class NProcPolicy {
	private const string OpenMpNumThreads = "OMP_NUM_THREADS";
	private const string OpenMpThreadLimit = "OMP_THREAD_LIMIT";

	/// <summary>Calculates the processor count printed by <c>nproc</c>.</summary>
	/// <param name="snapshot">The factual host and process observations.</param>
	/// <param name="options">The parsed command options.</param>
	/// <param name="environment">The OpenMP environment reader.</param>
	/// <returns>The resolved positive count and decision metadata.</returns>
	public static NProcDecision Calculate(
		ProcessorResourceSnapshot snapshot,
		NProcOptions options,
		INProcEnvironment environment
	) {
		ArgumentNullException.ThrowIfNull( snapshot );
		ArgumentNullException.ThrowIfNull( options );
		ArgumentNullException.ThrowIfNull( environment );

		ulong count;
		NProcDecisionBasis basis;
		var threadLimitApplied = false;
		var quotaApplied = false;
		if ( options.All ) {
			(count, basis) = SelectAllCount( snapshot );
		} else {
			var threadCount = ParseOpenMpCount( environment.GetVariable( OpenMpNumThreads ) );
			var threadLimit = ParseOpenMpCount( environment.GetVariable( OpenMpThreadLimit ) );
			if ( threadCount.HasValue ) {
				count = threadCount.Value;
				basis = NProcDecisionBasis.OpenMpThreadCount;
				if ( threadLimit.HasValue && threadLimit.Value < count ) {
					count = threadLimit.Value;
					threadLimitApplied = true;
				}
			} else {
				(count, basis) = SelectCurrentCount( snapshot );
				if ( threadLimit.HasValue && threadLimit.Value < count ) {
					count = threadLimit.Value;
					threadLimitApplied = true;
				}
				if ( snapshot.Quota.IsAvailable ) {
					var quota = RoundQuota( snapshot.Quota.GetRequiredValue().ProcessorLimit );
					if ( quota < count ) {
						count = quota;
						quotaApplied = true;
					}
				}
			}
		}

		count = Math.Max( 1UL, count );
		var ignored = options.Ignore < count ? options.Ignore : count - 1;
		count -= ignored;
		return new NProcDecision( count, basis, threadLimitApplied, quotaApplied, ignored );
	}

	/// <summary>Parses a positive OpenMP count, accepting the first item of a list.</summary>
	/// <param name="text">The environment value.</param>
	/// <returns>The positive value, or <see langword="null"/> when invalid or unset.</returns>
	public static ulong? ParseOpenMpCount( string? text ) {
		if ( string.IsNullOrWhiteSpace( text ) ) {
			return null;
		}
		var trimmed = text.Trim();
		var comma = trimmed.IndexOf( ',' );
		if ( comma >= 0 ) {
			trimmed = trimmed[..comma].Trim();
		}
		if (
			trimmed.Length == 0
			|| !trimmed.All( static character => character is >= '0' and <= '9' )
			|| !ulong.TryParse( trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var value )
			|| value == 0
		) {
			return null;
		}
		return value;
	}

	/// <summary>Rounds a fractional processor quota using the GNU nearest-unit rule.</summary>
	/// <param name="processorLimit">The positive fractional capacity.</param>
	/// <returns>A positive integral processor limit.</returns>
	public static ulong RoundQuota( double processorLimit ) {
		if ( !double.IsFinite( processorLimit ) || processorLimit <= 0 ) {
			return 1;
		}
		var rounded = Math.Floor( processorLimit + 0.5 );
		if ( rounded >= ulong.MaxValue ) {
			return ulong.MaxValue;
		}
		return Math.Max( 1UL, checked((ulong)rounded) );
	}

	private static (ulong Count, NProcDecisionBasis Basis) SelectAllCount(
		ProcessorResourceSnapshot snapshot
	) {
		if ( TryGetPositive( snapshot.InstalledProcessorCount, out var installed ) ) {
			return (installed, NProcDecisionBasis.InstalledProcessors);
		}
		if ( TryGetPositive( snapshot.ConfiguredProcessorCount, out var configured ) ) {
			return (configured, NProcDecisionBasis.InstalledProcessors);
		}
		if ( TryGetPositive( snapshot.OnlineProcessorCount, out var online ) ) {
			return (online, NProcDecisionBasis.OnlineProcessors);
		}
		if ( TryGetPositive( snapshot.ProcessAvailableProcessorCount, out var available ) ) {
			return (available, NProcDecisionBasis.ProcessAvailable);
		}
		if ( snapshot.Affinity.IsAvailable && snapshot.Affinity.GetRequiredValue().Count > 0 ) {
			return (checked((ulong)snapshot.Affinity.GetRequiredValue().Count), NProcDecisionBasis.ProcessAffinity);
		}
		return (1, NProcDecisionBasis.MinimumFallback);
	}

	private static (ulong Count, NProcDecisionBasis Basis) SelectCurrentCount(
		ProcessorResourceSnapshot snapshot
	) {
		ulong? affinity = null;
		ulong? available = null;
		if ( snapshot.Affinity.IsAvailable && snapshot.Affinity.GetRequiredValue().Count > 0 ) {
			affinity = checked((ulong)snapshot.Affinity.GetRequiredValue().Count);
		}
		if ( TryGetPositive( snapshot.ProcessAvailableProcessorCount, out var processCount ) ) {
			available = processCount;
		}
		if ( affinity.HasValue && available.HasValue ) {
			return affinity.Value <= available.Value
				? (affinity.Value, NProcDecisionBasis.ProcessAffinity)
				: (available.Value, NProcDecisionBasis.ProcessAvailable);
		}
		if ( affinity.HasValue ) {
			return (affinity.Value, NProcDecisionBasis.ProcessAffinity);
		}
		if ( available.HasValue ) {
			return (available.Value, NProcDecisionBasis.ProcessAvailable);
		}
		if ( TryGetPositive( snapshot.OnlineProcessorCount, out var online ) ) {
			return (online, NProcDecisionBasis.OnlineProcessors);
		}
		if ( TryGetPositive( snapshot.InstalledProcessorCount, out var installed ) ) {
			return (installed, NProcDecisionBasis.InstalledFallback);
		}
		if ( TryGetPositive( snapshot.ConfiguredProcessorCount, out var configured ) ) {
			return (configured, NProcDecisionBasis.ConfiguredFallback);
		}
		return (1, NProcDecisionBasis.MinimumFallback);
	}

	private static bool TryGetPositive( HostResourceValue<int> observation, out ulong value ) {
		if ( observation.IsAvailable && observation.GetRequiredValue() > 0 ) {
			value = checked((ulong)observation.GetRequiredValue());
			return true;
		}
		value = 0;
		return false;
	}
}
