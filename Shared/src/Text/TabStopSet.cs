namespace Icod.CoreUtils.Shared.Text;

using System.Collections.ObjectModel;

/// <summary>Represents explicit and recurring zero-based display-column tab stops.</summary>
public sealed class TabStopSet {
	private readonly ulong[] myExplicitStops;
	private readonly ReadOnlyCollection<ulong> myReadOnlyExplicitStops;

	private TabStopSet(
		ulong[] explicitStops,
		TabStopContinuation continuation
	) {
		this.myExplicitStops = explicitStops;
		this.myReadOnlyExplicitStops = Array.AsReadOnly( explicitStops );
		this.Continuation = continuation;
		this.MaximumDistance = CalculateMaximumDistance(
			explicitStops,
			continuation
		);
	}

	/// <summary>Gets the default GNU tab-stop model of one stop every eight columns.</summary>
	public static TabStopSet Default {
		get;
	} = Every( 8 );

	/// <summary>Gets the recurring continuation after the explicit list.</summary>
	public TabStopContinuation Continuation {
		get;
	}

	/// <summary>Gets the strictly increasing explicit tab stops.</summary>
	public IReadOnlyList<ulong> ExplicitStops => this.myReadOnlyExplicitStops;

	/// <summary>Gets the greatest configured distance between consecutive tab stops.</summary>
	/// <remarks>For an explicit-only model, column zero is treated as the origin before the first stop.</remarks>
	public ulong MaximumDistance {
		get;
	}

	/// <summary>Creates a tab-stop model with a globally aligned recurring interval.</summary>
	/// <param name="interval">The positive interval.</param>
	/// <returns>The recurring tab-stop model.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The interval is zero.</exception>
	public static TabStopSet Every( ulong interval ) => new(
		Array.Empty<ulong>(),
		TabStopContinuation.Absolute( interval )
	);

	/// <summary>Creates a validated explicit and recurring tab-stop model.</summary>
	/// <param name="explicitStops">The explicit positive tab stops in strictly increasing order.</param>
	/// <param name="continuation">The optional recurring continuation.</param>
	/// <returns>The validated tab-stop model.</returns>
	/// <exception cref="ArgumentNullException">The explicit-stop sequence is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">No stops are supplied, or the explicit stops are not strictly increasing.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An explicit stop is zero.</exception>
	public static TabStopSet Create(
		IEnumerable<ulong> explicitStops,
		TabStopContinuation continuation
	) {
		ArgumentNullException.ThrowIfNull( explicitStops );
		var values = explicitStops.ToArray();
		for ( var index = 0; index < values.Length; index++ ) {
			if ( values[index] == 0 ) {
				throw new ArgumentOutOfRangeException(
					nameof( explicitStops ),
					"Tab stops must be positive."
				);
			}
			if ( (index > 0) && (values[index] <= values[index - 1]) ) {
				throw new ArgumentException(
					"Explicit tab stops must be strictly increasing.",
					nameof( explicitStops )
				);
			}
		}
		if ( (values.Length == 0) && (continuation.Kind == TabStopContinuationKind.None) ) {
			throw new ArgumentException(
				"At least one explicit or recurring tab stop is required.",
				nameof( explicitStops )
			);
		}
		return new( values, continuation );
	}

	/// <summary>Gets the first configured tab stop strictly greater than a display column.</summary>
	/// <param name="column">The current zero-based display column.</param>
	/// <returns>The next stop, or <see langword="null"/> when an explicit list is exhausted.</returns>
	/// <exception cref="OverflowException">A recurring next stop cannot be represented.</exception>
	public ulong? GetNextStop( ulong column ) {
		var lower = 0;
		var upper = this.myExplicitStops.Length;
		while ( lower < upper ) {
			var middle = lower + ((upper - lower) / 2);
			if ( this.myExplicitStops[middle] <= column ) {
				lower = middle + 1;
			} else {
				upper = middle;
			}
		}
		if ( lower < this.myExplicitStops.Length ) {
			return this.myExplicitStops[lower];
		}
		return this.Continuation.Kind switch {
			TabStopContinuationKind.None => null,
			TabStopContinuationKind.Absolute => GetAbsoluteNextStop(
				column,
				this.Continuation.Interval
			),
			TabStopContinuationKind.Relative => this.GetRelativeNextStop(
				column,
				this.Continuation.Interval
			),
			_ => throw new InvalidOperationException( "Unknown tab-stop continuation kind." )
		};
	}

	private static ulong CalculateMaximumDistance(
		IReadOnlyList<ulong> explicitStops,
		TabStopContinuation continuation
	) {
		ulong maximum = continuation.Kind == TabStopContinuationKind.None
			? 0
			: continuation.Interval;
		ulong previous = 0;
		for ( var index = 0; index < explicitStops.Count; index++ ) {
			var distance = explicitStops[index] - previous;
			if ( distance > maximum ) {
				maximum = distance;
			}
			previous = explicitStops[index];
		}
		return maximum;
	}

	private static ulong GetAbsoluteNextStop(
		ulong column,
		ulong interval
	) {
		var multiple = checked((column / interval) + 1);
		return checked(multiple * interval);
	}

	private ulong GetRelativeNextStop(
		ulong column,
		ulong interval
	) {
		var origin = this.myExplicitStops.Length == 0
			? 0
			: this.myExplicitStops[^1];
		var distance = column - origin;
		var multiple = checked((distance / interval) + 1);
		return checked(origin + checked(multiple * interval));
	}
}
