namespace Icod.DiffUtils.Shared.Lines;

using System.Buffers;
using System.Text;

/// <summary>Reads a comparison input as line-preserving UTF-8 text while retaining incomplete-line state.</summary>
public static class ComparisonDocumentReader {
	/// <summary>Reads the remaining stream contents into a comparison document.</summary>
	public static async Task<ComparisonDocument> ReadAsync(
		Stream source,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( source );
		cancellationToken.ThrowIfCancellationRequested();
		var writer = new ArrayBufferWriter<byte>();
		var buffer = ArrayPool<byte>.Shared.Rent( 65536 );
		try {
			while ( true ) {
				var read = await source.ReadAsync( buffer.AsMemory(), cancellationToken ).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}
				writer.Write( buffer.AsSpan( 0, read ) );
			}
		} finally {
			ArrayPool<byte>.Shared.Return( buffer );
		}
		return Decode( writer.WrittenSpan );
	}

	/// <summary>Decodes authoritative source bytes into logical lines.</summary>
	public static ComparisonDocument Decode( ReadOnlySpan<byte> bytes ) {
		var ownedBytes = bytes.ToArray();
		var sourceBytes = ownedBytes.AsSpan();
		var lines = new List<ComparisonLine>();
		var containsNull = sourceBytes.IndexOf( (byte)0 ) >= 0;
		var start = 0;
		for ( var index = 0; index < sourceBytes.Length; index++ ) {
			if ( (byte)'\n' != sourceBytes[index] ) {
				continue;
			}
			lines.Add( new ComparisonLine( DecodeText( sourceBytes[start..index] ), true ) );
			start = index + 1;
		}
		if ( start < sourceBytes.Length ) {
			lines.Add( new ComparisonLine( DecodeText( sourceBytes[start..] ), false ) );
		}
		return new ComparisonDocument( ownedBytes, lines.AsReadOnly(), containsNull );
	}

	private static string DecodeText( ReadOnlySpan<byte> bytes ) {
		return Encoding.UTF8.GetString( bytes );
	}
}
