namespace Icod.CoreUtils.Shared.Text;

/// <summary>Reads byte-preserving logical lines from a <see cref="TextUnitReader"/>.</summary>
/// <remarks>
/// A line feed terminates a logical line and is retained through <see cref="TextLine.HasLineFeed"/>.
/// The reader does not own or dispose the supplied text-unit reader or its source stream.
/// </remarks>
public sealed class TextLineReader {
	private readonly TextUnitReader myReader;

	/// <summary>Initializes a logical-line reader.</summary>
	/// <param name="reader">The text-unit reader.</param>
	/// <exception cref="ArgumentNullException">The reader is <see langword="null"/>.</exception>
	public TextLineReader( TextUnitReader reader ) {
		this.myReader = reader ?? throw new ArgumentNullException( nameof( reader ) );
	}

	/// <summary>Reads the next logical line synchronously.</summary>
	/// <returns>The next logical line, or <see langword="null"/> at end of input.</returns>
	public TextLine? Read() {
		var units = new List<TextUnit>();
		while ( true ) {
			var value = this.myReader.Read();
			if ( value is not TextUnit unit ) {
				return 0 == units.Count ? null : new TextLine( units, false );
			}
			if ( IsLineFeed( unit ) ) {
				return new TextLine( units, true );
			}
			units.Add( unit );
		}
	}

	/// <summary>Reads the next logical line asynchronously.</summary>
	/// <param name="cancellationToken">A token that can cancel asynchronous source reads.</param>
	/// <returns>The next logical line, or <see langword="null"/> at end of input.</returns>
	public async ValueTask<TextLine?> ReadAsync( CancellationToken cancellationToken = default ) {
		var units = new List<TextUnit>();
		while ( true ) {
			var value = await this.myReader.ReadAsync( cancellationToken ).ConfigureAwait( false );
			if ( value is not TextUnit unit ) {
				return 0 == units.Count ? null : new TextLine( units, false );
			}
			if ( IsLineFeed( unit ) ) {
				return new TextLine( units, true );
			}
			units.Add( unit );
		}
	}

	private static bool IsLineFeed( TextUnit unit ) {
		return 1 == unit.ByteCount && (byte)'\n' == unit.GetByte( 0 );
	}
}
