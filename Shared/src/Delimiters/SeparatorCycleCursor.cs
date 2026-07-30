namespace Icod.CoreUtils.Shared.Delimiters;

/// <summary>Cycles sequentially through an immutable <see cref="SeparatorCycle"/>.</summary>
public struct SeparatorCycleCursor {

	private readonly SeparatorCycle myCycle;
	private int myIndex;

	/// <summary>Initializes a separator-cycle cursor.</summary>
	/// <param name="cycle">The immutable cycle to traverse.</param>
	public SeparatorCycleCursor( SeparatorCycle cycle ) {
		ArgumentNullException.ThrowIfNull( cycle );
		this.myCycle = cycle;
		this.myIndex = 0;
	}

	/// <summary>Gets the next separator and advances the cursor.</summary>
	/// <returns>The current separator before cyclic advancement.</returns>
	public ByteSeparator Next() {
		var value = this.myCycle[this.myIndex];
		this.myIndex++;
		if ( this.myCycle.Count == this.myIndex ) {
			this.myIndex = 0;
		}
		return value;
	}

	/// <summary>Resets the cursor to the first separator.</summary>
	public void Reset() {
		this.myIndex = 0;
	}

}
