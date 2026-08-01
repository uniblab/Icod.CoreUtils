namespace Icod.DiffUtils.Shared.Layout;

/// <summary>Describes GNU side-by-side output columns for a requested total width.</summary>
public readonly struct SideBySideGeometry {
	/// <summary>Initializes side-by-side column geometry.</summary>
	/// <param name="halfWidth">The maximum display width of either input field.</param>
	/// <param name="column2Offset">The zero-based display column at which the right field begins.</param>
	/// <param name="separatorColumn">The zero-based display column occupied by a nonblank separator.</param>
	public SideBySideGeometry( int halfWidth, int column2Offset, int separatorColumn ) {
		ArgumentOutOfRangeException.ThrowIfNegative( halfWidth );
		ArgumentOutOfRangeException.ThrowIfNegative( column2Offset );
		ArgumentOutOfRangeException.ThrowIfNegative( separatorColumn );
		this.HalfWidth = halfWidth;
		this.Column2Offset = column2Offset;
		this.SeparatorColumn = separatorColumn;
	}

	/// <summary>Gets the maximum display width of either input field.</summary>
	public int HalfWidth { get; }

	/// <summary>Gets the zero-based display column at which the right field begins.</summary>
	public int Column2Offset { get; }

	/// <summary>Gets the zero-based display column occupied by a nonblank separator.</summary>
	public int SeparatorColumn { get; }
}
