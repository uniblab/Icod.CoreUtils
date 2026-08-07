namespace Icod.CoreUtils.Shared.Time;

using System.Diagnostics;

/// <summary>
/// Supplies monotonic timestamps and cancellable delays for timeout and scheduling logic.
/// </summary>
public interface IMonotonicClock {
	/// <summary>Gets a monotonic timestamp in provider-defined units.</summary>
	long GetTimestamp();

	/// <summary>Gets the elapsed duration between two timestamps from this clock.</summary>
	TimeSpan GetElapsedTime(
		long startingTimestamp,
		long endingTimestamp
	);

	/// <summary>Waits for a duration without depending on wall-clock adjustments.</summary>
	ValueTask DelayAsync(
		TimeSpan delay,
		CancellationToken cancellationToken = default
	);
}

/// <summary>
/// Provides monotonic timestamps through <see cref="Stopwatch"/> and delays through the task scheduler.
/// </summary>
public sealed class SystemMonotonicClock : IMonotonicClock {
	private static readonly TimeSpan MaximumDelaySlice = TimeSpan.FromDays(
		7
	);

	/// <summary>Gets the shared system monotonic clock.</summary>
	public static SystemMonotonicClock Instance {
		get;
	} = new();

	private SystemMonotonicClock() {
	}

	/// <inheritdoc />
	public long GetTimestamp() => Stopwatch.GetTimestamp();

	/// <inheritdoc />
	public TimeSpan GetElapsedTime(
		long startingTimestamp,
		long endingTimestamp
	) => Stopwatch.GetElapsedTime(
		startingTimestamp,
		endingTimestamp
	);

	/// <inheritdoc />
	public ValueTask DelayAsync(
		TimeSpan delay,
		CancellationToken cancellationToken = default
	) {
		if ( TimeSpan.Zero > delay ) {
			throw new ArgumentOutOfRangeException(
				nameof( delay )
			);
		}
		if ( TimeSpan.Zero == delay ) {
			return ValueTask.CompletedTask;
		}
		return new ValueTask(
			DelayCoreAsync(
				delay,
				cancellationToken
			)
		);
	}

	private static async Task DelayCoreAsync(
		TimeSpan delay,
		CancellationToken cancellationToken
	) {
		var started = Stopwatch.GetTimestamp();
		while ( true ) {
			var elapsed = Stopwatch.GetElapsedTime(
				started,
				Stopwatch.GetTimestamp()
			);
			var remaining = delay - elapsed;
			if ( TimeSpan.Zero >= remaining ) {
				return;
			}
			await Task.Delay(
				remaining < MaximumDelaySlice
					? remaining
					: MaximumDelaySlice,
				cancellationToken
			).ConfigureAwait( false );
		}
	}
}
