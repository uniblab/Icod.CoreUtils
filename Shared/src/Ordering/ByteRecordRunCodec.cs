namespace Icod.CoreUtils.Shared.Ordering;

using System.Buffers.Binary;
using Icod.CommandFramework.Records;

/// <summary>Serializes byte records and their original ordinals in a deterministic length-prefixed run format.</summary>
public sealed class ByteRecordRunCodec : IExternalRunCodec<ByteRecord> {
	private const int HeaderLength = 13;
	private readonly int maximumRecordLength;

	/// <summary>Initializes a byte-record run codec.</summary>
	/// <param name="maximumRecordLength">The maximum accepted serialized record length.</param>
	public ByteRecordRunCodec( int maximumRecordLength = int.MaxValue ) {
		ArgumentOutOfRangeException.ThrowIfNegative( maximumRecordLength );
		this.maximumRecordLength = maximumRecordLength;
	}

	/// <summary>Gets the maximum accepted serialized record length.</summary>
	public int MaximumRecordLength => this.maximumRecordLength;

	/// <inheritdoc/>
	public async ValueTask WriteAsync(
		Stream destination,
		StableItem<ByteRecord> item,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( item );
		if ( item.Value.Content.Length > this.maximumRecordLength ) {
			throw new InvalidDataException(
				"The byte record exceeds the configured run-codec limit."
			);
		}
		var header = new byte[ HeaderLength ];
		BinaryPrimitives.WriteInt64LittleEndian(
			header.AsSpan( 0, sizeof( long ) ),
			item.OriginalOrdinal
		);
		BinaryPrimitives.WriteInt32LittleEndian(
			header.AsSpan( sizeof( long ), sizeof( int ) ),
			item.Value.Content.Length
		);
		header[ ^1 ] = item.Value.IsTerminated ? (byte)1 : (byte)0;
		await destination.WriteAsync( header, cancellationToken ).ConfigureAwait( false );
		await destination.WriteAsync(
			item.Value.Content,
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <inheritdoc/>
	public async ValueTask<ExternalRunReadResult<ByteRecord>> ReadAsync(
		Stream source,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( source );
		var header = new byte[ HeaderLength ];
		var first = await source.ReadAsync(
			header.AsMemory( 0, 1 ),
			cancellationToken
		).ConfigureAwait( false );
		if ( 0 == first ) {
			return ExternalRunReadResult<ByteRecord>.EndOfStream();
		}
		await source.ReadExactlyAsync(
			header.AsMemory( 1 ),
			cancellationToken
		).ConfigureAwait( false );
		var ordinal = BinaryPrimitives.ReadInt64LittleEndian(
			header.AsSpan( 0, sizeof( long ) )
		);
		if ( 0 > ordinal ) {
			throw new InvalidDataException( "A run contains a negative original ordinal." );
		}
		var length = BinaryPrimitives.ReadInt32LittleEndian(
			header.AsSpan( sizeof( long ), sizeof( int ) )
		);
		if ( ( 0 > length ) || ( length > this.maximumRecordLength ) ) {
			throw new InvalidDataException(
				"A run contains an invalid byte-record length."
			);
		}
		if ( 1 < header[ ^1 ] ) {
			throw new InvalidDataException(
				"A run contains an invalid record-termination flag."
			);
		}
		var content = new byte[ length ];
		await source.ReadExactlyAsync( content, cancellationToken ).ConfigureAwait( false );
		return ExternalRunReadResult<ByteRecord>.FromItem(
			new StableItem<ByteRecord>(
				new ByteRecord( content, 1 == header[ ^1 ] ),
				ordinal
			)
		);
	}
}
