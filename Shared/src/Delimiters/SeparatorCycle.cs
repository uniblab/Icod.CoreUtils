namespace Icod.CoreUtils.Shared.Delimiters;

/// <summary>Represents a nonempty immutable cycle of possibly empty byte separators.</summary>
public sealed class SeparatorCycle {

	private readonly ByteSeparator[] mySeparators;
	private readonly IReadOnlyList<ByteSeparator> myReadOnlySeparators;

	/// <summary>Initializes a separator cycle.</summary>
	/// <param name="separators">The nonempty sequence of immutable separator values.</param>
	public SeparatorCycle( IEnumerable<ByteSeparator> separators ) {
		ArgumentNullException.ThrowIfNull( separators );
		this.mySeparators = separators.ToArray();
		if ( 0 == this.mySeparators.Length ) {
			throw new ArgumentException(
				"A separator cycle must contain at least one element.",
				nameof( separators )
			);
		}
		if ( this.mySeparators.Any( value => null == value ) ) {
			throw new ArgumentException(
				"A separator cycle cannot contain null elements.",
				nameof( separators )
			);
		}
		this.myReadOnlySeparators = Array.AsReadOnly( this.mySeparators );
	}

	/// <summary>Gets the immutable cycle elements.</summary>
	public IReadOnlyList<ByteSeparator> Separators => this.myReadOnlySeparators;

	/// <summary>Gets the number of elements in the cycle.</summary>
	public int Count => this.mySeparators.Length;

	/// <summary>Gets one separator by zero-based index.</summary>
	/// <param name="index">The separator index.</param>
	/// <returns>The immutable separator at the index.</returns>
	public ByteSeparator this[int index] => this.mySeparators[index];

	/// <summary>Creates a cursor positioned before the first separator.</summary>
	/// <returns>A mutable cursor over this cycle.</returns>
	public SeparatorCycleCursor CreateCursor() => new( this );

}
