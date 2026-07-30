namespace Icod.CoreUtils.Shared.Ranges;

/// <summary>Represents an inclusive unsigned range whose upper endpoint may be open.</summary>
public readonly struct InclusiveRange : IEquatable<InclusiveRange> {

	/// <summary>Initializes an inclusive range.</summary>
	/// <param name="start">The inclusive lower endpoint.</param>
	/// <param name="end">The inclusive upper endpoint, or <see langword="null"/> for no upper bound.</param>
	public InclusiveRange(
		ulong start,
		ulong? end
	) {
		if ( end.HasValue && end.Value < start ) {
			throw new ArgumentOutOfRangeException(
				nameof( end ),
				"The upper endpoint cannot precede the lower endpoint."
			);
		}
		this.Start = start;
		this.End = end;
	}

	/// <summary>Gets the inclusive lower endpoint.</summary>
	public ulong Start { get; }

	/// <summary>Gets the inclusive upper endpoint, or <see langword="null"/> when the range is open-ended.</summary>
	public ulong? End { get; }

	/// <summary>Gets whether the range has no upper bound.</summary>
	public bool IsOpenEnded => !this.End.HasValue;

	/// <summary>Determines whether the supplied value belongs to this range.</summary>
	/// <param name="value">The value to test.</param>
	/// <returns><see langword="true"/> when the value is within the inclusive endpoints.</returns>
	public bool Contains( ulong value ) => this.Start <= value && ( !this.End.HasValue || value <= this.End.Value );

	/// <inheritdoc/>
	public bool Equals( InclusiveRange other ) => this.Start == other.Start && this.End == other.End;

	/// <inheritdoc/>
	public override bool Equals( object? obj ) => obj is InclusiveRange other && this.Equals( other );

	/// <inheritdoc/>
	public override int GetHashCode() => HashCode.Combine( this.Start, this.End );

	/// <summary>Determines whether two ranges contain equal endpoints.</summary>
	public static bool operator ==( InclusiveRange left, InclusiveRange right ) => left.Equals( right );

	/// <summary>Determines whether two ranges contain different endpoints.</summary>
	public static bool operator !=( InclusiveRange left, InclusiveRange right ) => !left.Equals( right );

}
