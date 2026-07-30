namespace Icod.CoreUtils.Shared.Text;

using System.Collections.ObjectModel;
using System.Text;

/// <summary>Represents one logical input line while retaining every source byte.</summary>
/// <remarks>
/// The terminating line-feed byte is represented by <see cref="HasLineFeed"/> rather than in
/// <see cref="Units"/>. Carriage returns and every other input byte remain ordinary text units.
/// </remarks>
public sealed class TextLine {
	private static readonly byte[] ourLineFeed = [ (byte)'\n' ];
	private readonly TextUnit[] myUnits;
	private readonly ReadOnlyCollection<TextUnit> myUnitView;

	/// <summary>Initializes a byte-preserving logical line.</summary>
	/// <param name="units">The text units excluding a terminating line feed.</param>
	/// <param name="hasLineFeed">Whether the source line ended with a line-feed byte.</param>
	/// <exception cref="ArgumentNullException">The unit sequence is <see langword="null"/>.</exception>
	public TextLine( IEnumerable<TextUnit> units, bool hasLineFeed ) {
		ArgumentNullException.ThrowIfNull( units );
		this.myUnits = units.ToArray();
		this.myUnitView = Array.AsReadOnly( this.myUnits );
		this.HasLineFeed = hasLineFeed;
		var byteCount = 0;
		foreach ( var unit in this.myUnits ) {
			byteCount = checked(byteCount + unit.ByteCount);
		}
		this.ByteCount = byteCount;
	}

	/// <summary>Gets the number of retained content bytes, excluding a terminating line feed.</summary>
	public int ByteCount { get; }

	/// <summary>Gets whether the source line ended with a line-feed byte.</summary>
	public bool HasLineFeed { get; }

	/// <summary>Gets whether the logical line contains no content bytes.</summary>
	public bool IsEmpty => 0 == this.myUnits.Length;

	/// <summary>Gets the retained text units, excluding a terminating line feed.</summary>
	public IReadOnlyList<TextUnit> Units => this.myUnitView;

	/// <summary>Returns the exact source bytes represented by this line.</summary>
	/// <param name="includeLineFeed">Whether to append the retained terminating line feed.</param>
	/// <returns>A new byte array containing the requested source bytes.</returns>
	public byte[] ToByteArray( bool includeLineFeed = true ) {
		var length = checked(this.ByteCount + ((includeLineFeed && this.HasLineFeed) ? 1 : 0));
		var result = new byte[length];
		var offset = 0;
		foreach ( var unit in this.myUnits ) {
			offset += unit.CopyBytesTo( result.AsSpan( offset ) );
		}
		if ( includeLineFeed && this.HasLineFeed ) {
			result[offset] = ourLineFeed[0];
		}
		return result;
	}

	/// <summary>Creates a managed string suitable for classification and regular-expression matching.</summary>
	/// <returns>A string containing decoded scalars and one Latin-1 code point for each opaque byte.</returns>
	/// <remarks>
	/// The returned value is a decision surface, not a replacement serialization. Use
	/// <see cref="ToByteArray(bool)"/> or <see cref="WriteAsync(Stream, bool, CancellationToken)"/>
	/// when exact source-byte reproduction is required.
	/// </remarks>
	public string ToDecodedString() {
		var builder = new StringBuilder( this.myUnits.Length );
		foreach ( var unit in this.myUnits ) {
			if ( unit.Scalar is { } scalar ) {
				builder.Append( scalar.ToString() );
			} else {
				builder.Append( (char)unit.GetByte( 0 ) );
			}
		}
		return builder.ToString();
	}

	/// <summary>Writes the exact source bytes represented by this line.</summary>
	/// <param name="output">The destination stream.</param>
	/// <param name="includeLineFeed">Whether to append the retained terminating line feed.</param>
	/// <param name="cancellationToken">A token that can cancel asynchronous writes.</param>
	/// <returns>A value task that represents the asynchronous write.</returns>
	public async ValueTask WriteAsync(
		Stream output,
		bool includeLineFeed = true,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		var unitBuffer = new byte[4];
		foreach ( var unit in this.myUnits ) {
			var count = unit.CopyBytesTo( unitBuffer );
			await output.WriteAsync( unitBuffer.AsMemory( 0, count ), cancellationToken ).ConfigureAwait( false );
		}
		if ( includeLineFeed && this.HasLineFeed ) {
			await output.WriteAsync( ourLineFeed, cancellationToken ).ConfigureAwait( false );
		}
	}
}
