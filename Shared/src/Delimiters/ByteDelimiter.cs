namespace Icod.CoreUtils.Shared.Delimiters;

/// <summary>Represents a required, nonempty immutable byte delimiter.</summary>
public sealed class ByteDelimiter : IEquatable<ByteDelimiter> {

	private readonly byte[] myBytes;

	/// <summary>Initializes a delimiter by copying its bytes.</summary>
	/// <param name="bytes">The nonempty delimiter bytes.</param>
	public ByteDelimiter( ReadOnlySpan<byte> bytes ) {
		if ( bytes.IsEmpty ) {
			throw new ArgumentException(
				"A delimiter cannot be empty.",
				nameof( bytes )
			);
		}
		this.myBytes = bytes.ToArray();
	}

	/// <summary>Gets the immutable delimiter bytes.</summary>
	public ReadOnlyMemory<byte> Bytes => this.myBytes;

	/// <summary>Gets the delimiter length in bytes.</summary>
	public int Length => this.myBytes.Length;

	/// <inheritdoc/>
	public bool Equals( ByteDelimiter? other ) => null != other && this.myBytes.AsSpan().SequenceEqual( other.myBytes );

	/// <inheritdoc/>
	public override bool Equals( object? obj ) => this.Equals( obj as ByteDelimiter );

	/// <inheritdoc/>
	public override int GetHashCode() {
		var hash = new HashCode();
		foreach ( var value in this.myBytes ) {
			hash.Add( value );
		}
		return hash.ToHashCode();
	}

}
