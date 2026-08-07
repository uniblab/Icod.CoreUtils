namespace Icod.CoreUtils.Shared.Time;

using System.Runtime.CompilerServices;

/// <summary>
/// Describes one fixed-rate periodic scheduling observation.
/// </summary>
public sealed class PeriodicTick {
	/// <summary>Gets how late the observation occurred relative to the fixed-rate schedule.</summary>
	public TimeSpan Lateness => this.ObservedElapsed > this.ScheduledElapsed
		? this.ObservedElapsed - this.ScheduledElapsed
		: TimeSpan.Zero
	;

	/// <summary>Gets the elapsed duration observed when the tick was emitted.</summary>
	public TimeSpan ObservedElapsed {
		get;
	}

	/// <summary>Gets the elapsed duration at which the tick was scheduled.</summary>
	public TimeSpan ScheduledElapsed {
		get;
	}

	/// <summary>Gets the zero-based tick sequence.</summary>
	public long Sequence {
		get;
	}

	/// <summary>Initializes a periodic tick.</summary>
	public PeriodicTick(
		long sequence,
		TimeSpan scheduledElapsed,
		TimeSpan observedElapsed
	) {
		ArgumentOutOfRangeException.ThrowIfNegative(
			sequence
		);
		this.Sequence = sequence;
		this.ScheduledElapsed = scheduledElapsed;
		this.ObservedElapsed = observedElapsed;
	}
}

/// <summary>
/// Produces cancellable fixed-rate periodic ticks without using wall-clock time.
/// </summary>
public interface IPeriodicScheduler {
	/// <summary>Schedules periodic ticks.</summary>
	IAsyncEnumerable<PeriodicTick> ScheduleAsync(
		TimeSpan interval,
		bool fireImmediately = false,
		CancellationToken cancellationToken = default
	);
}

/// <summary>
/// Implements drift-resistant fixed-rate scheduling over an injectable monotonic clock.
/// </summary>
public sealed class MonotonicPeriodicScheduler : IPeriodicScheduler {
	private readonly IMonotonicClock _clock;

	/// <summary>Gets the shared system periodic scheduler.</summary>
	public static MonotonicPeriodicScheduler Instance {
		get;
	} = new(
		SystemMonotonicClock.Instance
	);

	/// <summary>Initializes a monotonic periodic scheduler.</summary>
	public MonotonicPeriodicScheduler(
		IMonotonicClock clock
	) {
		ArgumentNullException.ThrowIfNull(
			clock
		);
		this._clock = clock;
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<PeriodicTick> ScheduleAsync(
		TimeSpan interval,
		bool fireImmediately = false,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	) {
		if ( TimeSpan.Zero >= interval ) {
			throw new ArgumentOutOfRangeException(
				nameof( interval )
			);
		}
		var started = this._clock.GetTimestamp();
		var sequence = 0L;
		if ( fireImmediately ) {
			yield return new PeriodicTick(
				sequence++,
				TimeSpan.Zero,
				TimeSpan.Zero
			);
		}
		while ( true ) {
			cancellationToken.ThrowIfCancellationRequested();
			var scheduledElapsed = TimeSpan.FromTicks(
				checked( interval.Ticks * ( sequence + ( fireImmediately ? 0L : 1L ) ) )
			);
			var now = this._clock.GetTimestamp();
			var observedElapsed = this._clock.GetElapsedTime(
				started,
				now
			);
			var remaining = scheduledElapsed - observedElapsed;
			if ( TimeSpan.Zero < remaining ) {
				await this._clock.DelayAsync(
					remaining,
					cancellationToken
				).ConfigureAwait( false );
			}
			now = this._clock.GetTimestamp();
			observedElapsed = this._clock.GetElapsedTime(
				started,
				now
			);
			yield return new PeriodicTick(
				sequence++,
				scheduledElapsed,
				observedElapsed
			);
		}
	}
}
