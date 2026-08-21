namespace Icod.CoreUtils.Shuf;

using System.Buffers;
using System.Buffers.Binary;
using Icod.CommandFramework.IO;
using Icod.CommandFramework.Records;

/// <summary>Stores arbitrarily large byte records and a fixed-width random-access location index in owned temporary files.</summary>
internal sealed class SpoolRecordStore : IAsyncDisposable {
	private const int LocationSize = sizeof( long ) * 2;
	private const string TemporarySpoolPrefix = "icod-coreutils-shuf-";
	private readonly TemporarySpool myData;
	private readonly TemporarySpool myIndex;
	private readonly byte[] myLocationBuffer = new byte[ LocationSize ];
	private bool mySealed;

	private SpoolRecordStore( TemporarySpool data, TemporarySpool index ) {
		this.myData = data;
		this.myIndex = index;
	}

	/// <summary>Gets the number of stored records.</summary>
	internal ulong Count { get; private set; }

	/// <summary>Creates an empty externally backed record store.</summary>
	/// <returns>The created record store.</returns>
	internal static SpoolRecordStore Create() {
		var data = TemporarySpool.Create( fileNamePrefix: TemporarySpoolPrefix );
		try {
			return new SpoolRecordStore(
				data,
				TemporarySpool.Create( fileNamePrefix: TemporarySpoolPrefix )
			);
		} catch {
			data.Dispose();
			throw;
		}
	}

	/// <summary>Appends one complete record, excluding its separator.</summary>
	/// <param name="data">The record bytes.</param>
	/// <param name="cancellationToken">A token that may cancel the operation.</param>
	/// <returns>A task representing the operation.</returns>
	internal async Task AppendRecordAsync(
		ReadOnlyMemory<byte> data,
		CancellationToken cancellationToken
	) {
		this.ThrowIfSealed();
		var offset = this.myData.Stream.Position;
		await this.myData.Stream.WriteAsync( data, cancellationToken ).ConfigureAwait( false );
		await this.AppendLocationAsync(
			new RecordLocation( offset, data.Length ),
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>Reads separator-delimited records from a stream without materializing an entire record or input.</summary>
	/// <param name="source">The input stream.</param>
	/// <param name="separator">The one-byte input separator.</param>
	/// <param name="cancellationToken">A token that may cancel the operation.</param>
	/// <returns>A task representing the operation.</returns>
	internal async Task AppendRecordsAsync(
		Stream source,
		byte separator,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( source );
		this.ThrowIfSealed();
		using var reader = new DelimitedByteRecordSegmentReader( source, separator );
		var recordOffset = this.myData.Stream.Position;
		long recordLength = 0;
		while ( await reader.ReadAsync( cancellationToken ).ConfigureAwait( false ) is ByteRecordSegment segment ) {
			await this.myData.Stream.WriteAsync( segment.Data, cancellationToken ).ConfigureAwait( false );
			recordLength = checked( recordLength + segment.Data.Length );
			if ( !segment.EndsRecord ) {
				continue;
			}
			await this.AppendLocationAsync(
				new RecordLocation( recordOffset, recordLength ),
				cancellationToken
			).ConfigureAwait( false );
			recordOffset = this.myData.Stream.Position;
			recordLength = 0;
		}
	}

	/// <summary>Flushes the stores and prevents further record appends.</summary>
	/// <param name="cancellationToken">A token that may cancel the operation.</param>
	/// <returns>A task representing the operation.</returns>
	internal async Task SealAsync( CancellationToken cancellationToken ) {
		if ( this.mySealed ) {
			return;
		}
		await this.myData.Stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
		await this.myIndex.Stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
		this.mySealed = true;
	}

	/// <summary>Randomizes the requested index prefix using an exact partial Fisher-Yates shuffle.</summary>
	/// <param name="selectionCount">The number of leading records that must be randomized.</param>
	/// <param name="randomSource">The random source, or <see langword="null"/> when the store has fewer than two records.</param>
	/// <param name="cancellationToken">A token that may cancel the operation.</param>
	/// <returns>A task representing the operation.</returns>
	internal async Task ShufflePrefixAsync(
		ulong selectionCount,
		IRandomByteSource? randomSource,
		CancellationToken cancellationToken
	) {
		this.ThrowIfNotSealed();
		if ( this.Count < selectionCount ) {
			throw new ArgumentOutOfRangeException( nameof( selectionCount ) );
		}
		for ( ulong index = 0; index < selectionCount; index++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			var remaining = this.Count - index;
			if ( 1UL >= remaining ) {
				break;
			}
			if ( null == randomSource ) {
				throw new InvalidOperationException( "A random source is required to shuffle multiple records." );
			}
			var selected = checked(
				index + await randomSource.NextInclusiveAsync(
					remaining - 1UL,
					cancellationToken
				).ConfigureAwait( false )
			);
			if ( selected != index ) {
				await this.SwapLocationsAsync( index, selected, cancellationToken ).ConfigureAwait( false );
			}
		}
	}

	/// <summary>Writes the requested leading records and synthesizes the configured separator after every record.</summary>
	/// <param name="destination">The destination stream.</param>
	/// <param name="count">The number of records to write.</param>
	/// <param name="separator">The output record separator.</param>
	/// <param name="cancellationToken">A token that may cancel the operation.</param>
	/// <returns>A task representing the operation.</returns>
	internal async Task WritePrefixAsync(
		Stream destination,
		ulong count,
		byte separator,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( destination );
		this.ThrowIfNotSealed();
		if ( this.Count < count ) {
			throw new ArgumentOutOfRangeException( nameof( count ) );
		}
		var buffer = ArrayPool<byte>.Shared.Rent( StreamOperations.DefaultBufferSize );
		try {
			for ( ulong index = 0; index < count; index++ ) {
				await this.WriteRecordAsync(
					destination,
					index,
					separator,
					buffer,
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			ArrayPool<byte>.Shared.Return( buffer );
		}
	}

	/// <summary>Writes records selected independently with replacement.</summary>
	/// <param name="destination">The destination stream.</param>
	/// <param name="count">The finite output count, or <see langword="null"/> for unbounded output.</param>
	/// <param name="randomSource">The random source, or <see langword="null"/> when only one record exists.</param>
	/// <param name="separator">The output record separator.</param>
	/// <param name="cancellationToken">A token that may cancel the operation.</param>
	/// <returns>A task representing the operation.</returns>
	internal async Task WriteRepeatedAsync(
		Stream destination,
		System.Numerics.BigInteger? count,
		IRandomByteSource? randomSource,
		byte separator,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( destination );
		this.ThrowIfNotSealed();
		if ( 0UL == this.Count ) {
			throw new InvalidOperationException( "no lines to repeat" );
		}
		var buffer = ArrayPool<byte>.Shared.Rent( StreamOperations.DefaultBufferSize );
		try {
			var written = System.Numerics.BigInteger.Zero;
			while ( !count.HasValue || written < count.Value ) {
				cancellationToken.ThrowIfCancellationRequested();
				var index = 0UL;
				if ( 1UL < this.Count ) {
					if ( null == randomSource ) {
						throw new InvalidOperationException( "A random source is required to repeat multiple records." );
					}
					index = await randomSource.NextInclusiveAsync(
						this.Count - 1UL,
						cancellationToken
					).ConfigureAwait( false );
				}
				await this.WriteRecordAsync(
					destination,
					index,
					separator,
					buffer,
					cancellationToken
				).ConfigureAwait( false );
				written += System.Numerics.BigInteger.One;
			}
		} finally {
			ArrayPool<byte>.Shared.Return( buffer );
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync() {
		try {
			await this.myIndex.DisposeAsync().ConfigureAwait( false );
		} finally {
			await this.myData.DisposeAsync().ConfigureAwait( false );
		}
	}

	private async Task AppendLocationAsync( RecordLocation location, CancellationToken cancellationToken ) {
		if ( this.Count >= (ulong)(long.MaxValue / LocationSize) ) {
			throw new IOException( "too many input records for the temporary index" );
		}
		this.myIndex.Stream.Seek( 0, SeekOrigin.End );
		BinaryPrimitives.WriteInt64LittleEndian( this.myLocationBuffer.AsSpan( 0, sizeof( long ) ), location.Offset );
		BinaryPrimitives.WriteInt64LittleEndian( this.myLocationBuffer.AsSpan( sizeof( long ), sizeof( long ) ), location.Length );
		await this.myIndex.Stream.WriteAsync( this.myLocationBuffer.AsMemory(), cancellationToken ).ConfigureAwait( false );
		this.Count++;
	}

	private async Task<RecordLocation> ReadLocationAsync( ulong index, CancellationToken cancellationToken ) {
		if ( this.Count <= index ) {
			throw new ArgumentOutOfRangeException( nameof( index ) );
		}
		this.myIndex.Stream.Seek( checked( (long)index * LocationSize ), SeekOrigin.Begin );
		await ReadExactlyAsync( this.myIndex.Stream, this.myLocationBuffer, cancellationToken ).ConfigureAwait( false );
		return new RecordLocation(
			BinaryPrimitives.ReadInt64LittleEndian( this.myLocationBuffer.AsSpan( 0, sizeof( long ) ) ),
			BinaryPrimitives.ReadInt64LittleEndian( this.myLocationBuffer.AsSpan( sizeof( long ), sizeof( long ) ) )
		);
	}

	private async Task SwapLocationsAsync( ulong left, ulong right, CancellationToken cancellationToken ) {
		var leftLocation = await this.ReadLocationAsync( left, cancellationToken ).ConfigureAwait( false );
		var rightLocation = await this.ReadLocationAsync( right, cancellationToken ).ConfigureAwait( false );
		await this.WriteLocationAsync( left, rightLocation, cancellationToken ).ConfigureAwait( false );
		await this.WriteLocationAsync( right, leftLocation, cancellationToken ).ConfigureAwait( false );
	}

	private async Task WriteLocationAsync( ulong index, RecordLocation location, CancellationToken cancellationToken ) {
		this.myIndex.Stream.Seek( checked( (long)index * LocationSize ), SeekOrigin.Begin );
		BinaryPrimitives.WriteInt64LittleEndian( this.myLocationBuffer.AsSpan( 0, sizeof( long ) ), location.Offset );
		BinaryPrimitives.WriteInt64LittleEndian( this.myLocationBuffer.AsSpan( sizeof( long ), sizeof( long ) ), location.Length );
		await this.myIndex.Stream.WriteAsync( this.myLocationBuffer.AsMemory(), cancellationToken ).ConfigureAwait( false );
	}

	private async Task WriteRecordAsync(
		Stream destination,
		ulong index,
		byte separator,
		byte[] buffer,
		CancellationToken cancellationToken
	) {
		var location = await this.ReadLocationAsync( index, cancellationToken ).ConfigureAwait( false );
		this.myData.Stream.Seek( location.Offset, SeekOrigin.Begin );
		var remaining = location.Length;
		while ( 0L < remaining ) {
			var requested = (int)Math.Min( buffer.Length, remaining );
			var count = await this.myData.Stream.ReadAsync(
				buffer.AsMemory( 0, requested ),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == count ) {
				throw new EndOfStreamException( "temporary record data ended unexpectedly" );
			}
			await destination.WriteAsync(
				buffer.AsMemory( 0, count ),
				cancellationToken
			).ConfigureAwait( false );
			remaining -= count;
		}
		buffer[0] = separator;
		await destination.WriteAsync( buffer.AsMemory( 0, 1 ), cancellationToken ).ConfigureAwait( false );
	}

	private static async Task ReadExactlyAsync(
		Stream source,
		Memory<byte> buffer,
		CancellationToken cancellationToken
	) {
		var offset = 0;
		while ( offset < buffer.Length ) {
			var count = await source.ReadAsync( buffer[offset..], cancellationToken ).ConfigureAwait( false );
			if ( 0 == count ) {
				throw new EndOfStreamException( "temporary record index ended unexpectedly" );
			}
			offset += count;
		}
	}

	private void ThrowIfNotSealed() {
		if ( !this.mySealed ) {
			throw new InvalidOperationException( "The record store must be sealed before reading or shuffling." );
		}
	}

	private void ThrowIfSealed() {
		if ( this.mySealed ) {
			throw new InvalidOperationException( "The record store has already been sealed." );
		}
	}

	private readonly record struct RecordLocation( long Offset, long Length );
}
