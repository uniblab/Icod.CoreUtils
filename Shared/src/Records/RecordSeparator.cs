namespace Icod.CoreUtils.Shared.Records;

/// <summary>Represents the single byte that terminates a byte record.</summary>
public readonly struct RecordSeparator : IEquatable<RecordSeparator> {

	/// <summary>Initializes a record separator.</summary>
	/// <param name="value">The separator byte.</param>
	public RecordSeparator( byte value ) {
		this.Value = value;
	}

	/// <summary>Gets the separator byte.</summary>
	public byte Value { get; }

	/// <summary>Gets the conventional line-feed record separator.</summary>
	public static RecordSeparator LineFeed { get; } = new( (byte)'\n' );

	/// <summary>Gets the NUL record separator used by GNU <c>-z</c> modes.</summary>
	public static RecordSeparator Null { get; } = new( 0 );

	/// <inheritdoc/>
	public bool Equals( RecordSeparator other ) => this.Value == other.Value;

	/// <inheritdoc/>
	public override bool Equals( object? obj ) => obj is RecordSeparator other && this.Equals( other );

	/// <inheritdoc/>
	public override int GetHashCode() => this.Value.GetHashCode();

	/// <summary>Determines whether two separators contain the same byte.</summary>
	public static bool operator ==( RecordSeparator left, RecordSeparator right ) => left.Equals( right );

	/// <summary>Determines whether two separators contain different bytes.</summary>
	public static bool operator !=( RecordSeparator left, RecordSeparator right ) => !left.Equals( right );

}
