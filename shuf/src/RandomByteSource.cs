namespace Icod.CoreUtils.Shuf;

using System.Security.Cryptography;

/// <summary>Supplies uniformly distributed random bytes to the <c>shuf</c> execution engine.</summary>
internal interface IRandomByteSource : IAsyncDisposable {
	/// <summary>Reads an unsigned value from the requested number of random bytes.</summary>
	/// <param name="byteCount">The number of bytes to consume, from one through eight.</param>
	/// <param name="cancellationToken">A token that may cancel the operation.</param>
	/// <returns>The little-endian unsigned value formed from the consumed bytes.</returns>
	ValueTask<ulong> ReadValueAsync( int byteCount, CancellationToken cancellationToken );
}

/// <summary>Creates random-byte sources and performs unbiased bounded selection.</summary>
internal static class RandomByteSource {
	/// <summary>Creates either a cryptographic source or a source backed by a named file.</summary>
	/// <param name="path">The random-source path, or <see langword="null"/> for cryptographic randomness.</param>
	/// <returns>The created random source.</returns>
	internal static IRandomByteSource Create( string? path ) {
		return null == path
			? new CryptographicRandomByteSource()
			: new FileRandomByteSource( path );
	}

	/// <summary>Returns an unbiased value in the inclusive interval from zero through <paramref name="maximumInclusive"/>.</summary>
	/// <param name="source">The source of random bytes.</param>
	/// <param name="maximumInclusive">The inclusive upper bound.</param>
	/// <param name="cancellationToken">A token that may cancel the operation.</param>
	/// <returns>An unbiased bounded value.</returns>
	internal static async ValueTask<ulong> NextInclusiveAsync(
		this IRandomByteSource source,
		ulong maximumInclusive,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( source );
		if ( 0UL == maximumInclusive ) {
			return 0UL;
		}
		var byteCount = GetRequiredByteCount( maximumInclusive );
		var bound = maximumInclusive + 1UL;
		if ( 8 == byteCount && 0UL == bound ) {
			return await source.ReadValueAsync( byteCount, cancellationToken ).ConfigureAwait( false );
		}
		var rejectionCount = 8 == byteCount
			? unchecked( 0UL - bound ) % bound
			: ( 1UL << ( byteCount * 8 ) ) % bound;
		var maximumAccepted = 8 == byteCount
			? ulong.MaxValue - rejectionCount
			: ( 1UL << ( byteCount * 8 ) ) - rejectionCount - 1UL;
		while ( true ) {
			var value = await source.ReadValueAsync( byteCount, cancellationToken ).ConfigureAwait( false );
			if ( value <= maximumAccepted ) {
				return value % bound;
			}
		}
	}

	private static int GetRequiredByteCount( ulong maximumInclusive ) {
		var byteCount = 1;
		while ( byteCount < sizeof( ulong ) && maximumInclusive >= ( 1UL << ( byteCount * 8 ) ) ) {
			byteCount++;
		}
		return byteCount;
	}
}

/// <summary>Supplies random values from the platform cryptographic random-number generator.</summary>
internal sealed class CryptographicRandomByteSource : IRandomByteSource {
	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc/>
	public ValueTask<ulong> ReadValueAsync( int byteCount, CancellationToken cancellationToken ) {
		ValidateByteCount( byteCount );
		cancellationToken.ThrowIfCancellationRequested();
		Span<byte> bytes = stackalloc byte[ sizeof( ulong ) ];
		RandomNumberGenerator.Fill( bytes[..byteCount] );
		return ValueTask.FromResult( CreateValue( bytes, byteCount ) );
	}

	private static ulong CreateValue( ReadOnlySpan<byte> bytes, int byteCount ) {
		ulong result = 0;
		for ( var index = 0; index < byteCount; index++ ) {
			result |= (ulong)bytes[index] << ( index * 8 );
		}
		return result;
	}

	private static void ValidateByteCount( int byteCount ) {
		if ( byteCount is < 1 or > sizeof( ulong ) ) {
			throw new ArgumentOutOfRangeException( nameof( byteCount ) );
		}
	}
}

/// <summary>Supplies deterministic random values from a caller-selected byte stream.</summary>
internal sealed class FileRandomByteSource : IRandomByteSource {
	private readonly byte[] myBuffer = new byte[ sizeof( ulong ) ];
	private readonly FileStream myStream;

	/// <summary>Initializes a random source backed by a named file.</summary>
	/// <param name="path">The file whose bytes provide randomness.</param>
	internal FileRandomByteSource( string path ) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		this.myStream = new FileStream(
			path,
			FileMode.Open,
			FileAccess.Read,
			FileShare.ReadWrite | FileShare.Delete,
			bufferSize: 4096,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => this.myStream.DisposeAsync();

	/// <inheritdoc/>
	public async ValueTask<ulong> ReadValueAsync( int byteCount, CancellationToken cancellationToken ) {
		if ( byteCount is < 1 or > sizeof( ulong ) ) {
			throw new ArgumentOutOfRangeException( nameof( byteCount ) );
		}
		var offset = 0;
		while ( offset < byteCount ) {
			var count = await this.myStream.ReadAsync(
				this.myBuffer.AsMemory( offset, byteCount - offset ),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == count ) {
				throw new EndOfStreamException( "random source ended before enough bytes were available" );
			}
			offset += count;
		}
		ulong result = 0;
		for ( var index = 0; index < byteCount; index++ ) {
			result |= (ulong)this.myBuffer[index] << ( index * 8 );
		}
		return result;
	}
}
