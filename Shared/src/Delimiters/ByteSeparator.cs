namespace Icod.CoreUtils.Shared.Delimiters;

/// <summary>Represents an immutable byte separator that may be empty.</summary>
public sealed class ByteSeparator : IEquatable<ByteSeparator> {

	private readonly byte[] myBytes;

	/// <summary>Initializes a separator by copying its bytes.</summary>
	/// <param name="bytes">The separator bytes, which may be empty.</param>
	public ByteSeparator( ReadOnlySpan<byte> bytes ) {
		this.myBytes = bytes.ToArray();
	}

	/// <summary>Gets an empty separator.</summary>
	public static ByteSeparator Empty { get; } = new( Array.Empty<byte>() );

	/// <summary>Gets the immutable separator bytes.</summary>
	public ReadOnlyMemory<byte> Bytes => this.myBytes;

	/// <summary>Gets whether the separator contains no bytes.</summary>
	public bool IsEmpty => 0 == this.myBytes.Length;

	/// <inheritdoc/>
	public bool Equals( ByteSeparator? other ) => null != other && this.myBytes.AsSpan().SequenceEqual( other.myBytes );

	/// <inheritdoc/>
	public override bool Equals( object? obj ) => this.Equals( obj as ByteSeparator );

	/// <inheritdoc/>
	public override int GetHashCode() {
		var hash = new HashCode();
		foreach ( var value in this.myBytes ) {
			hash.Add( value );
		}
		return hash.ToHashCode();
	}

}
