namespace Icod.CoreUtils.Shared.Tests.Time;

using Xunit;
using Icod.CoreUtils.Shared.Time;

/// <summary>
/// Verifies monotonic delay and drift-resistant periodic scheduling contracts.
/// </summary>
public sealed class MonotonicSchedulingTests {
	/// <summary>Verifies fixed-rate scheduling against an injectable monotonic clock.</summary>
	[Fact]
	public async Task SchedulerUsesFixedRateDeadlines() {
		var clock = new FakeMonotonicClock();
		var scheduler = new MonotonicPeriodicScheduler(
			clock
		);
		var ticks = new List<PeriodicTick>();
		await foreach ( var tick in scheduler.ScheduleAsync(
			TimeSpan.FromMilliseconds( 10 )
		) ) {
			ticks.Add(
				tick
			);
			if ( 3 == ticks.Count ) {
				break;
			}
		}

		Assert.Equal(
			new[] {
				TimeSpan.FromMilliseconds( 10 ),
				TimeSpan.FromMilliseconds( 20 ),
				TimeSpan.FromMilliseconds( 30 )
			},
			ticks.Select(
				static tick => tick.ScheduledElapsed
			)
		);
		Assert.All(
			ticks,
			static tick => Assert.Equal(
				TimeSpan.Zero,
				tick.Lateness
			)
		);
	}

	/// <summary>Verifies immediate-first scheduling without losing the first interval deadline.</summary>
	[Fact]
	public async Task SchedulerCanFireImmediately() {
		var clock = new FakeMonotonicClock();
		var scheduler = new MonotonicPeriodicScheduler(
			clock
		);
		var ticks = new List<PeriodicTick>();
		await foreach ( var tick in scheduler.ScheduleAsync(
			TimeSpan.FromSeconds( 1 ),
			true
		) ) {
			ticks.Add(
				tick
			);
			if ( 2 == ticks.Count ) {
				break;
			}
		}

		Assert.Equal(
			TimeSpan.Zero,
			ticks[ 0 ].ScheduledElapsed
		);
		Assert.Equal(
			TimeSpan.FromSeconds( 1 ),
			ticks[ 1 ].ScheduledElapsed
		);
	}

	private sealed class FakeMonotonicClock : IMonotonicClock {
		private long _timestamp;

		/// <summary>Initializes a deterministic monotonic clock.</summary>
		public FakeMonotonicClock() {
		}

		/// <inheritdoc />
		public long GetTimestamp() => this._timestamp;

		/// <inheritdoc />
		public TimeSpan GetElapsedTime(
			long startingTimestamp,
			long endingTimestamp
		) => TimeSpan.FromTicks(
			endingTimestamp - startingTimestamp
		);

		/// <inheritdoc />
		public ValueTask DelayAsync(
			TimeSpan delay,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this._timestamp = checked(
				this._timestamp + delay.Ticks
			);
			return ValueTask.CompletedTask;
		}
	}
}
