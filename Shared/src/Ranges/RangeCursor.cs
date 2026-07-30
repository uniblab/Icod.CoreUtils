namespace Icod.CoreUtils.Shared.Ranges;

/// <summary>Provides sequential membership and range-boundary lookup over a normalized range set.</summary>
public struct RangeCursor {

	private int myIndex;
	private bool myInitialized;
	private ulong myLastPosition;
	private readonly RangeSet mySet;

	/// <summary>Initializes a sequential range cursor.</summary>
	/// <param name="set">The immutable range set to traverse.</param>
	public RangeCursor( RangeSet set ) {
		ArgumentNullException.ThrowIfNull( set );
		this.mySet = set;
		this.myIndex = 0;
		this.myInitialized = false;
		this.myLastPosition = 0;
	}

	/// <summary>Evaluates membership and boundary state at one position.</summary>
	/// <param name="position">The position to evaluate.</param>
	/// <returns>The membership and normalized-range-start state.</returns>
	public RangeMatch Match( ulong position ) {
		if ( this.myInitialized && position < this.myLastPosition ) {
			this.myIndex = 0;
		}
		this.myInitialized = true;
		this.myLastPosition = position;
		while ( this.myIndex < this.mySet.Count ) {
			var range = this.mySet.GetRange( this.myIndex );
			if ( range.End.HasValue && range.End.Value < position ) {
				this.myIndex++;
				continue;
			}
			if ( position < range.Start ) {
				return new RangeMatch( false, false );
			}
			return new RangeMatch(
				isSelected: true,
				isRangeStart: position == range.Start
			);
		}
		return new RangeMatch( false, false );
	}

	/// <summary>Determines whether one position is selected.</summary>
	/// <param name="position">The position to evaluate.</param>
	/// <returns><see langword="true"/> when selected.</returns>
	public bool Contains( ulong position ) => this.Match( position ).IsSelected;

	/// <summary>Determines whether one position begins a normalized range.</summary>
	/// <param name="position">The position to evaluate.</param>
	/// <returns><see langword="true"/> when the position begins a range.</returns>
	public bool IsRangeStart( ulong position ) => this.Match( position ).IsRangeStart;

	/// <summary>Resets traversal to the beginning of the range set.</summary>
	public void Reset() {
		this.myIndex = 0;
		this.myInitialized = false;
		this.myLastPosition = 0;
	}

}
