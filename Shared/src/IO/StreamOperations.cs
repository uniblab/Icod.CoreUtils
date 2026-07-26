namespace Icod.CoreUtils.Shared.IO;

using System.Buffers;

/// <summary>
/// Provides bounded, cancellation-aware asynchronous stream operations.
/// </summary>
public static class StreamOperations {

	/// <summary>Default reusable byte-buffer size.</summary>
	public const int DefaultBufferSize = 65536;

	/// <summary>
	/// Copies the remaining source stream to the destination.
	/// </summary>
	/// <returns>The number of bytes copied.</returns>
	public static async Task<long> CopyAsync(
		Stream source,
		Stream destination,
		int bufferSize = DefaultBufferSize,
		CancellationToken cancellationToken = default
	) {
		return await CopyCountAsync(
			source,
			destination,
			long.MaxValue,
			bufferSize,
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>
	/// Copies at most <paramref name="count"/> bytes.
	/// </summary>
	/// <returns>The number of bytes copied.</returns>
	public static async Task<long> CopyCountAsync(
		Stream source,
		Stream destination,
		long count,
		int bufferSize = DefaultBufferSize,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			source
		);
		ArgumentNullException.ThrowIfNull(
			destination
		);
		if ( count < 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( count )
			);
		}
		if ( bufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( bufferSize )
			);
		}
		cancellationToken.ThrowIfCancellationRequested();

		var buffer = ArrayPool<byte>.Shared.Rent(
			bufferSize
		);
		long copied = 0;
		try {
			while ( copied < count ) {
				var requested = (int)Math.Min(
					buffer.Length,
					count - copied
				);
				var read = await source.ReadAsync(
					buffer.AsMemory(
						0,
						requested
					),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}
				await destination.WriteAsync(
					buffer.AsMemory(
						0,
						read
					),
					cancellationToken
				).ConfigureAwait( false );
				copied += read;
			}
			return copied;
		} finally {
			ArrayPool<byte>.Shared.Return(
				buffer
			);
		}
	}

	/// <summary>
	/// Skips at most <paramref name="count"/> bytes, seeking when possible.
	/// </summary>
	/// <returns>The number of bytes skipped.</returns>
	public static async Task<long> SkipAsync(
		Stream source,
		long count,
		int bufferSize = DefaultBufferSize,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			source
		);
		if ( count < 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( count )
			);
		}
		if ( bufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( bufferSize )
			);
		}
		cancellationToken.ThrowIfCancellationRequested();
		if ( source.CanSeek ) {
			var start = source.Position;
			if ( source.Length <= start ) {
				return 0;
			}
			var available = source.Length - start;
			var target = count < available
				? start + count
				: source.Length
			;
			source.Seek(
				target,
				SeekOrigin.Begin
			);
			return target - start;
		}

		var buffer = ArrayPool<byte>.Shared.Rent(
			bufferSize
		);
		long skipped = 0;
		try {
			while ( skipped < count ) {
				var read = await source.ReadAsync(
					buffer.AsMemory(
						0,
						(int)Math.Min(
							buffer.Length,
							count - skipped
						)
					),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}
				skipped += read;
			}
			return skipped;
		} finally {
			ArrayPool<byte>.Shared.Return(
				buffer
			);
		}
	}

	/// <summary>
	/// Copies a byte range beginning at the supplied offset.
	/// </summary>
	public static async Task<long> CopyRangeAsync(
		Stream source,
		Stream destination,
		long offset,
		long count,
		int bufferSize = DefaultBufferSize,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( source );
		ArgumentNullException.ThrowIfNull( destination );
		if ( offset < 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( offset )
			);
		}
		if ( count < 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( count )
			);
		}
		if ( bufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( bufferSize )
			);
		}
		cancellationToken.ThrowIfCancellationRequested();
		if ( source.CanSeek ) {
			source.Seek(
				offset,
				SeekOrigin.Begin
			);
		} else {
			await SkipAsync(
				source,
				offset,
				bufferSize,
				cancellationToken
			).ConfigureAwait( false );
		}
		return await CopyCountAsync(
			source,
			destination,
			count,
			bufferSize,
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>
	/// Reads at most the requested bounded number of bytes.
	/// </summary>
	public static async Task<byte[]> ReadAtMostAsync(
		Stream source,
		int count,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			source
		);
		if ( count < 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( count )
			);
		}
		cancellationToken.ThrowIfCancellationRequested();
		var output = new byte[ count ];
		var offset = 0;
		while ( offset < output.Length ) {
			var read = await source.ReadAsync(
				output.AsMemory(
					offset
				),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				break;
			}
			offset += read;
		}
		if ( offset == output.Length ) {
			return output;
		}
		Array.Resize(
			ref output,
			offset
		);
		return output;
	}

}
