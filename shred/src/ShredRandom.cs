namespace Icod.CoreUtils.Shred;

using System.Security.Cryptography;

/// <summary>Supplies exact quantities of random bytes to the overwrite engine.</summary>
internal interface IShredRandomSource : IAsyncDisposable {
	/// <summary>Fills the complete destination or throws if the source is exhausted.</summary>
	/// <param name="destination">The destination buffer.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	ValueTask FillAsync( Memory<byte> destination, CancellationToken cancellationToken );
}

/// <summary>Uses the operating system cryptographic random-number provider.</summary>
internal sealed class CryptoShredRandomSource : IShredRandomSource {
	/// <inheritdoc />
	public ValueTask FillAsync( Memory<byte> destination, CancellationToken cancellationToken ) {
		cancellationToken.ThrowIfCancellationRequested();
		RandomNumberGenerator.Fill( destination.Span );
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Consumes an external stream as a finite source of random bytes.</summary>
internal sealed class StreamShredRandomSource : IShredRandomSource {
	private readonly Stream stream;

	/// <summary>Initializes a source that owns the supplied stream.</summary>
	/// <param name="stream">The readable source stream.</param>
	public StreamShredRandomSource( Stream stream ) {
		this.stream = stream ?? throw new ArgumentNullException( nameof( stream ) );
		if ( !stream.CanRead ) {
			throw new ArgumentException( "The random-source stream must be readable.", nameof( stream ) );
		}
	}

	/// <inheritdoc />
	public async ValueTask FillAsync( Memory<byte> destination, CancellationToken cancellationToken ) {
		var filled = 0;
		while ( filled < destination.Length ) {
			var read = await stream.ReadAsync( destination[ filled.. ], cancellationToken ).ConfigureAwait( false );
			if ( read == 0 ) {
				throw new EndOfStreamException( "random source exhausted before enough data was read" );
			}
			filled += read;
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync() => await stream.DisposeAsync().ConfigureAwait( false );
}

/// <summary>Identifies one planned overwrite pass.</summary>
internal sealed class ShredPass {
	/// <summary>Initializes a planned pass.</summary>
	/// <param name="pattern">The repeating pattern, or <see langword="null"/> for random bytes.</param>
	/// <param name="isFinalZero">Whether the pass is the requested final zero pass.</param>
	public ShredPass( byte[]? pattern, bool isFinalZero ) {
		Pattern = pattern;
		IsFinalZero = isFinalZero;
	}

	/// <summary>Gets the repeating pattern, or <see langword="null"/> for random bytes.</summary>
	public byte[]? Pattern { get; }

	/// <summary>Gets whether this is the requested final zero pass.</summary>
	public bool IsFinalZero { get; }

	/// <summary>Gets whether this pass consumes random bytes.</summary>
	public bool IsRandom => Pattern is null;

	/// <summary>Gets a human-readable pass description.</summary>
	public string Description => IsRandom
		? "random"
		: IsFinalZero
			? "000000"
			: Convert.ToHexString( Pattern! ).ToLowerInvariant();
}

/// <summary>Builds a balanced sequence of random and fixed overwrite patterns.</summary>
internal static class ShredPassPlanner {
	private static readonly byte[][] Patterns = [
		[ 0x00 ], [ 0xFF ], [ 0x55 ], [ 0xAA ],
		[ 0x24, 0x92, 0x49 ], [ 0x49, 0x24, 0x92 ], [ 0x92, 0x49, 0x24 ],
		[ 0x6D, 0xB6, 0xDB ], [ 0xB6, 0xDB, 0x6D ], [ 0xDB, 0x6D, 0xB6 ],
		[ 0x11 ], [ 0x22 ], [ 0x33 ], [ 0x44 ], [ 0x66 ], [ 0x77 ],
		[ 0x88 ], [ 0x99 ], [ 0xBB ], [ 0xCC ], [ 0xDD ], [ 0xEE ]
	];

	/// <summary>Creates the pass sequence, randomizing fixed-pattern order through the selected random source.</summary>
	/// <param name="iterations">The requested overwrite-pass count.</param>
	/// <param name="appendZero">Whether to append a final zero pass.</param>
	/// <param name="randomSource">The random source used for ordering.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The pass sequence.</returns>
	public static async ValueTask<IReadOnlyList<ShredPass>> CreateAsync(
		int iterations,
		bool appendZero,
		IShredRandomSource randomSource,
		CancellationToken cancellationToken
	) {
		var result = new List<ShredPass>( iterations + ( appendZero ? 1 : 0 ) );
		if ( iterations <= 3 ) {
			for ( var index = 0; index < iterations; index++ ) {
				result.Add( new ShredPass( null, false ) );
			}
		} else {
			var randomizedPatterns = Patterns.Select( static pattern => pattern.ToArray() ).ToList();
			await ShuffleAsync( randomizedPatterns, randomSource, cancellationToken ).ConfigureAwait( false );

			result.Add( new ShredPass( null, false ) );
			var fixedCount = Math.Min( iterations - 2, randomizedPatterns.Count );
			for ( var index = 0; index < fixedCount; index++ ) {
				result.Add( new ShredPass( randomizedPatterns[ index ], false ) );
			}
			while ( result.Count < iterations - 1 ) {
				result.Add( new ShredPass( null, false ) );
			}
			result.Add( new ShredPass( null, false ) );
		}

		if ( appendZero ) {
			result.Add( new ShredPass( [ 0x00 ], true ) );
		}
		return result;
	}

	private static async ValueTask ShuffleAsync(
		IList<byte[]> values,
		IShredRandomSource randomSource,
		CancellationToken cancellationToken
	) {
		var random = new byte[ 4 ];
		for ( var index = values.Count - 1; index > 0; index-- ) {
			await randomSource.FillAsync( random, cancellationToken ).ConfigureAwait( false );
			var value = BitConverter.ToUInt32( random, 0 );
			var selected = (int)( value % (uint)( index + 1 ) );
			( values[ index ], values[ selected ] ) = ( values[ selected ], values[ index ] );
		}
	}
}
