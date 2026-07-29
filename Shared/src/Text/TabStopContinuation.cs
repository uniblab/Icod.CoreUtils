namespace Icod.CoreUtils.Shared.Text;

/// <summary>Describes an optional recurring tab-stop interval.</summary>
public readonly struct TabStopContinuation {
	private TabStopContinuation(
		TabStopContinuationKind kind,
		ulong interval
	) {
		this.Kind = kind;
		this.Interval = interval;
	}

	/// <summary>Gets a continuation that supplies no recurring tab stops.</summary>
	public static TabStopContinuation None => default;

	/// <summary>Gets the recurring interval, or zero when <see cref="Kind"/> is <see cref="TabStopContinuationKind.None"/>.</summary>
	public ulong Interval {
		get;
	}

	/// <summary>Gets the continuation kind.</summary>
	public TabStopContinuationKind Kind {
		get;
	}

	/// <summary>Creates a continuation aligned to global multiples of an interval.</summary>
	/// <param name="interval">The positive recurring interval.</param>
	/// <returns>The absolute continuation.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The interval is zero.</exception>
	public static TabStopContinuation Absolute( ulong interval ) {
		if ( interval == 0 ) {
			throw new ArgumentOutOfRangeException( nameof( interval ) );
		}
		return new(
			TabStopContinuationKind.Absolute,
			interval
		);
	}

	/// <summary>Creates a continuation aligned relative to the final explicit stop.</summary>
	/// <param name="interval">The positive recurring interval.</param>
	/// <returns>The relative continuation.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The interval is zero.</exception>
	public static TabStopContinuation Relative( ulong interval ) {
		if ( interval == 0 ) {
			throw new ArgumentOutOfRangeException( nameof( interval ) );
		}
		return new(
			TabStopContinuationKind.Relative,
			interval
		);
	}
}
