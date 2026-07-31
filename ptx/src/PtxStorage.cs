namespace Icod.CoreUtils.Ptx;

using System.Buffers.Binary;
using Icod.CoreUtils.Shared.Ordering;

/// <summary>Writes each selected context once and supports bounded random reads during output.</summary>
internal sealed class PtxContextStore : IAsyncDisposable {
	private readonly string path;
	private FileStream? writer;
	private bool sealedForReading;

	/// <summary>Initializes a context store over an existing empty workspace file.</summary>
	/// <param name="path">The owned spool pathname.</param>
	internal PtxContextStore( string path ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		this.path = path;
		this.writer = new FileStream(
			path,
			FileMode.Open,
			FileAccess.Write,
			FileShare.Read | FileShare.Delete,
			65_536,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
		this.writer.SetLength( 0 );
	}

	/// <summary>Appends a context and returns its starting byte offset.</summary>
	/// <param name="content">The context bytes.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The starting offset.</returns>
	internal async ValueTask<long> AppendAsync(
		ReadOnlyMemory<byte> content,
		CancellationToken cancellationToken
	) {
		if ( this.sealedForReading || null == this.writer ) {
			throw new InvalidOperationException( "The context store has been sealed." );
		}
		var offset = this.writer.Position;
		await this.writer.WriteAsync( content, cancellationToken ).ConfigureAwait( false );
		return offset;
	}

	/// <summary>Flushes and closes the writer before ordered output begins.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the seal operation.</returns>
	internal async ValueTask SealAsync( CancellationToken cancellationToken ) {
		if ( this.sealedForReading ) {
			return;
		}
		this.sealedForReading = true;
		if ( null != this.writer ) {
			await this.writer.FlushAsync( cancellationToken ).ConfigureAwait( false );
			await this.writer.DisposeAsync().ConfigureAwait( false );
			this.writer = null;
		}
	}

	/// <summary>Reads one previously appended context.</summary>
	/// <param name="offset">The stored offset.</param>
	/// <param name="length">The stored length.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The independently owned context bytes.</returns>
	internal async ValueTask<byte[]> ReadAsync(
		long offset,
		int length,
		CancellationToken cancellationToken
	) {
		if ( !this.sealedForReading ) {
			throw new InvalidOperationException( "The context store has not been sealed." );
		}
		ArgumentOutOfRangeException.ThrowIfNegative( offset );
		ArgumentOutOfRangeException.ThrowIfNegative( length );
		var buffer = new byte[ length ];
		await using var source = new FileStream(
			this.path,
			FileMode.Open,
			FileAccess.Read,
			FileShare.ReadWrite | FileShare.Delete,
			65_536,
			FileOptions.Asynchronous | FileOptions.RandomAccess
		);
		source.Position = offset;
		await source.ReadExactlyAsync( buffer, cancellationToken ).ConfigureAwait( false );
		return buffer;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync() {
		if ( null != this.writer ) {
			await this.writer.DisposeAsync().ConfigureAwait( false );
			this.writer = null;
		}
	}
}

/// <summary>Serializes occurrence metadata and stable ordinals into Shared external-ordering runs.</summary>
internal sealed class PtxOccurrenceRunCodec : IExternalRunCodec<PtxOccurrence> {
	private const int HeaderLength = 36;

	/// <inheritdoc/>
	public async ValueTask WriteAsync(
		Stream destination,
		StableItem<PtxOccurrence> item,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( item );
		var value = item.Value;
		var header = new byte[ HeaderLength ];
		BinaryPrimitives.WriteInt64LittleEndian( header.AsSpan( 0, 8 ), item.OriginalOrdinal );
		BinaryPrimitives.WriteInt64LittleEndian( header.AsSpan( 8, 8 ), value.ContextOffset );
		BinaryPrimitives.WriteInt32LittleEndian( header.AsSpan( 16, 4 ), value.ContextLength );
		BinaryPrimitives.WriteInt32LittleEndian( header.AsSpan( 20, 4 ), value.KeywordStart );
		BinaryPrimitives.WriteInt32LittleEndian( header.AsSpan( 24, 4 ), value.KeywordLength );
		BinaryPrimitives.WriteInt32LittleEndian( header.AsSpan( 28, 4 ), value.Keyword.Length );
		BinaryPrimitives.WriteInt32LittleEndian( header.AsSpan( 32, 4 ), value.Reference.Length );
		await destination.WriteAsync( header, cancellationToken ).ConfigureAwait( false );
		await destination.WriteAsync( value.Keyword, cancellationToken ).ConfigureAwait( false );
		await destination.WriteAsync( value.Reference, cancellationToken ).ConfigureAwait( false );
	}

	/// <inheritdoc/>
	public async ValueTask<ExternalRunReadResult<PtxOccurrence>> ReadAsync(
		Stream source,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( source );
		var header = new byte[ HeaderLength ];
		var first = await source.ReadAsync( header.AsMemory( 0, 1 ), cancellationToken ).ConfigureAwait( false );
		if ( 0 == first ) {
			return ExternalRunReadResult<PtxOccurrence>.EndOfStream();
		}
		await source.ReadExactlyAsync( header.AsMemory( 1 ), cancellationToken ).ConfigureAwait( false );
		var ordinal = BinaryPrimitives.ReadInt64LittleEndian( header.AsSpan( 0, 8 ) );
		var contextOffset = BinaryPrimitives.ReadInt64LittleEndian( header.AsSpan( 8, 8 ) );
		var contextLength = BinaryPrimitives.ReadInt32LittleEndian( header.AsSpan( 16, 4 ) );
		var keywordStart = BinaryPrimitives.ReadInt32LittleEndian( header.AsSpan( 20, 4 ) );
		var keywordLength = BinaryPrimitives.ReadInt32LittleEndian( header.AsSpan( 24, 4 ) );
		var keywordStorageLength = BinaryPrimitives.ReadInt32LittleEndian( header.AsSpan( 28, 4 ) );
		var referenceLength = BinaryPrimitives.ReadInt32LittleEndian( header.AsSpan( 32, 4 ) );
		if (
			0 > ordinal
			|| 0 > contextOffset
			|| 0 > contextLength
			|| 0 > keywordStart
			|| 0 >= keywordLength
			|| keywordLength != keywordStorageLength
			|| keywordStart > contextLength - keywordLength
			|| 0 > referenceLength
		) {
			throw new InvalidDataException( "A ptx run contains invalid occurrence metadata." );
		}
		var keyword = new byte[ keywordStorageLength ];
		var reference = new byte[ referenceLength ];
		await source.ReadExactlyAsync( keyword, cancellationToken ).ConfigureAwait( false );
		await source.ReadExactlyAsync( reference, cancellationToken ).ConfigureAwait( false );
		return ExternalRunReadResult<PtxOccurrence>.FromItem(
			new StableItem<PtxOccurrence>(
				new PtxOccurrence(
					keyword,
					contextOffset,
					contextLength,
					keywordStart,
					keywordLength,
					reference
				),
				ordinal
			)
		);
	}
}
