namespace Icod.CoreUtils.Shared.Text;

/// <summary>Tracks a checked display-column position using reusable control-character operations.</summary>
public sealed class DisplayColumnState {
	/// <summary>Initializes a new instance of the <see cref="DisplayColumnState"/> class.</summary>
	/// <param name="initialColumn">The initial zero-based display column.</param>
	public DisplayColumnState( ulong initialColumn = 0 ) {
		this.Column = initialColumn;
	}

	/// <summary>Gets the current zero-based display column.</summary>
	public ulong Column {
		get;
		private set;
	}

	/// <summary>Advances by a nonnegative provider width.</summary>
	/// <param name="width">The nonnegative display width.</param>
	/// <exception cref="ArgumentOutOfRangeException">The width is negative.</exception>
	/// <exception cref="OverflowException">The resulting column exceeds <see cref="ulong.MaxValue"/>.</exception>
	public void Advance( int width ) {
		if ( width < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( width ) );
		}
		this.Advance( (ulong)width );
	}

	/// <summary>Advances by an unsigned display width.</summary>
	/// <param name="width">The display width.</param>
	/// <exception cref="OverflowException">The resulting column exceeds <see cref="ulong.MaxValue"/>.</exception>
	public void Advance( ulong width ) {
		this.Column = checked(this.Column + width);
	}

	/// <summary>Applies the ordinary one-column backspace behavior without moving before column zero.</summary>
	public void Backspace() {
		this.Backspace( 1 );
	}

	/// <summary>Moves backward by a caller-selected width without moving before column zero.</summary>
	/// <param name="width">The width to retreat, such as one column or a preceding unit's width.</param>
	public void Backspace( ulong width ) {
		this.Column = width >= this.Column
			? 0
			: this.Column - width;
	}

	/// <summary>Applies carriage-return behavior by resetting the display column to zero.</summary>
	public void CarriageReturn() {
		this.Column = 0;
	}

	/// <summary>Resets the state to a caller-selected display column.</summary>
	/// <param name="column">The new display column.</param>
	public void Reset( ulong column = 0 ) {
		this.Column = column;
	}

	/// <summary>Advances to the next configured tab stop when one exists.</summary>
	/// <param name="tabStops">The tab-stop model.</param>
	/// <returns><see langword="true"/> when a next stop exists; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="ArgumentNullException">The tab-stop model is <see langword="null"/>.</exception>
	/// <exception cref="OverflowException">The recurring next stop cannot be represented.</exception>
	public bool TryAdvanceToNextTabStop( TabStopSet tabStops ) {
		ArgumentNullException.ThrowIfNull( tabStops );
		var next = tabStops.GetNextStop( this.Column );
		if ( next is null ) {
			return false;
		}
		this.Column = next.Value;
		return true;
	}
}
