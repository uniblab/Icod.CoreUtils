namespace Icod.CoreUtils.Tr;

using System.Buffers;

/// <summary>Applies a compiled <c>tr</c> plan to an unbounded byte stream.</summary>
internal static class TrEngine {
	private const int BufferSize = 64 * 1024;

	/// <summary>Transforms an input stream into an output stream.</summary>
	/// <param name="input">The byte input.</param>
	/// <param name="output">The byte output.</param>
	/// <param name="plan">The compiled transformation plan.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing completion.</returns>
	public static async Task TransformAsync(
		Stream input,
		Stream output,
		TrTransformPlan plan,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( input );
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( plan );
		var inputBuffer = ArrayPool<byte>.Shared.Rent( BufferSize );
		var outputBuffer = ArrayPool<byte>.Shared.Rent( BufferSize );
		var hasPrevious = false;
		byte previous = 0;
		try {
			while ( true ) {
				var count = await input.ReadAsync(
					inputBuffer.AsMemory( 0, BufferSize ),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == count ) {
					break;
				}
				var outputCount = 0;
				for ( var index = 0; index < count; index++ ) {
					var source = inputBuffer[index];
					if ( plan.Deletion[source] ) {
						continue;
					}
					var transformed = plan.Translation[source];
					if ( hasPrevious && transformed == previous && plan.Squeezing[transformed] ) {
						continue;
					}
					outputBuffer[outputCount++] = transformed;
					previous = transformed;
					hasPrevious = true;
				}
				if ( 0 < outputCount ) {
					await output.WriteAsync(
						outputBuffer.AsMemory( 0, outputCount ),
						cancellationToken
					).ConfigureAwait( false );
				}
			}
			await output.FlushAsync( cancellationToken ).ConfigureAwait( false );
		} finally {
			ArrayPool<byte>.Shared.Return( inputBuffer );
			ArrayPool<byte>.Shared.Return( outputBuffer );
		}
	}
}
