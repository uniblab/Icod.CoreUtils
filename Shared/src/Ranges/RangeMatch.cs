namespace Icod.CoreUtils.Shared.Ranges;

/// <summary>Describes membership and range-boundary state at one position.</summary>
public readonly struct RangeMatch {

	/// <summary>Initializes a range match.</summary>
	/// <param name="isSelected">Whether the position is selected.</param>
	/// <param name="isRangeStart">Whether the position begins a normalized range.</param>
	public RangeMatch(
		bool isSelected,
		bool isRangeStart
	) {
		if ( isRangeStart && !isSelected ) {
			throw new ArgumentException(
				"A range start must also be selected.",
				nameof( isRangeStart )
			);
		}
		this.IsSelected = isSelected;
		this.IsRangeStart = isRangeStart;
	}

	/// <summary>Gets whether the position is selected.</summary>
	public bool IsSelected { get; }

	/// <summary>Gets whether the position begins a normalized range.</summary>
	public bool IsRangeStart { get; }

}
