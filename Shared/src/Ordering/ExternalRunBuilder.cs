namespace Icod.CoreUtils.Shared.Ordering;

using Icod.CommandFramework.Temporary;

/// <summary>Builds individually sorted stable runs under a caller-selected memory estimate.</summary>
/// <typeparam name="T">The ordered value type.</typeparam>
public sealed class ExternalRunBuilder<T> {
	private readonly StableComparer<T> comparer;
	private readonly IExternalRunCodec<T> codec;
	private readonly ExternalOrderingOptions<T> options;

	/// <summary>Initializes a sorted-run builder.</summary>
	/// <param name="valueComparer">The primary value comparer.</param>
	/// <param name="codec">The run codec.</param>
	/// <param name="options">The memory and file options.</param>
	public ExternalRunBuilder(
		IComparer<T> valueComparer,
		IExternalRunCodec<T> codec,
		ExternalOrderingOptions<T> options
	) {
		ArgumentNullException.ThrowIfNull( valueComparer );
		ArgumentNullException.ThrowIfNull( codec );
		ArgumentNullException.ThrowIfNull( options );
		this.comparer = new StableComparer<T>( valueComparer );
		this.codec = codec;
		this.options = options;
	}

	/// <summary>Consumes an input sequence and writes stable sorted runs into an owned workspace.</summary>
	/// <param name="source">The asynchronous input sequence.</param>
	/// <param name="workspace">The temporary workspace that owns generated run files.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The run descriptors in creation order.</returns>
	/// <remarks>
	/// A single item whose estimate exceeds the configured memory limit is emitted as a one-item run;
	/// no generic ordering engine can subdivide an opaque item without violating its codec contract.
	/// </remarks>
	public async Task<IReadOnlyList<ExternalRun>> BuildAsync(
		IAsyncEnumerable<T> source,
		TemporaryWorkspace workspace,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( source );
		ArgumentNullException.ThrowIfNull( workspace );
		var runs = new List<ExternalRun>();
		var items = new List<StableItem<T>>();
		long estimatedBytes = 0;
		long ordinal = 0;
		await foreach ( var value in source.WithCancellation( cancellationToken ).ConfigureAwait( false ) ) {
			cancellationToken.ThrowIfCancellationRequested();
			var payloadSize = this.options.SizeEstimator( value );
			if ( 0 > payloadSize ) {
				throw new InvalidOperationException(
					"The external-ordering size estimator returned a negative value."
				);
			}
			var itemSize = checked( payloadSize + this.options.PerItemOverheadBytes );
			if (
				( 0 < items.Count )
				&& ( itemSize > this.options.MemoryLimitBytes - estimatedBytes )
			) {
				runs.Add( await this.FlushAsync(
					items,
					workspace,
					cancellationToken
				).ConfigureAwait( false ) );
				items.Clear();
				estimatedBytes = 0;
			}
			items.Add( new StableItem<T>( value, ordinal ) );
			ordinal = checked( ordinal + 1 );
			estimatedBytes = checked( estimatedBytes + itemSize );
		}
		if ( 0 < items.Count ) {
			runs.Add( await this.FlushAsync(
				items,
				workspace,
				cancellationToken
			).ConfigureAwait( false ) );
		}
		return runs;
	}

	private async Task<ExternalRun> FlushAsync(
		List<StableItem<T>> items,
		TemporaryWorkspace workspace,
		CancellationToken cancellationToken
	) {
		items.Sort( this.comparer );
		var path = workspace.CreateFile(
			this.options.RunFileTemplate,
			cancellationToken
		);
		var streamOptions = new FileStreamOptions {
			Mode = FileMode.Open,
			Access = FileAccess.Write,
			Share = FileShare.None,
			BufferSize = this.options.FileBufferSize,
			Options = FileOptions.Asynchronous | FileOptions.SequentialScan
		};
		await using ( var stream = new FileStream( path, streamOptions ) ) {
			stream.SetLength( 0 );
			foreach ( var item in items ) {
				cancellationToken.ThrowIfCancellationRequested();
				await this.codec.WriteAsync(
					stream,
					item,
					cancellationToken
				).ConfigureAwait( false );
			}
			await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
		}
		return new ExternalRun( path, items.Count );
	}
}
