namespace Icod.CoreUtils.Shared.Ordering;

/// <summary>Performs a stable k-way merge of already sorted run files.</summary>
/// <typeparam name="T">The ordered value type.</typeparam>
public sealed class StableExternalMerger<T> {
	private readonly StableComparer<T> comparer;
	private readonly IExternalRunCodec<T> codec;
	private readonly int fileBufferSize;

	/// <summary>Initializes a stable external merger.</summary>
	/// <param name="valueComparer">The primary value comparer.</param>
	/// <param name="codec">The run codec.</param>
	/// <param name="fileBufferSize">The buffer size used for each run stream.</param>
	public StableExternalMerger(
		IComparer<T> valueComparer,
		IExternalRunCodec<T> codec,
		int fileBufferSize = ExternalOrderingOptions<T>.DefaultFileBufferSize
	) {
		ArgumentNullException.ThrowIfNull( valueComparer );
		ArgumentNullException.ThrowIfNull( codec );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( fileBufferSize );
		this.comparer = new StableComparer<T>( valueComparer );
		this.codec = codec;
		this.fileBufferSize = fileBufferSize;
	}

	/// <summary>Merges sorted runs and sends each stable item to an asynchronous destination.</summary>
	/// <param name="runs">The nonoverlapping sorted runs.</param>
	/// <param name="writeAsync">The destination callback.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task representing the complete merge.</returns>
	/// <exception cref="InvalidDataException">A run contains fewer or more records than its descriptor reports.</exception>
	public async Task MergeAsync(
		IReadOnlyList<ExternalRun> runs,
		Func<StableItem<T>, CancellationToken, ValueTask> writeAsync,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( runs );
		ArgumentNullException.ThrowIfNull( writeAsync );
		if ( 0 == runs.Count ) {
			return;
		}
		var cursors = new RunCursor[ runs.Count ];
		var queue = new PriorityQueue<MergeHead, MergeHead>(
			new MergeHeadComparer( this.comparer )
		);
		try {
			for ( var index = 0; index < runs.Count; index++ ) {
				cancellationToken.ThrowIfCancellationRequested();
				var run = runs[ index ];
				var streamOptions = new FileStreamOptions {
					Mode = FileMode.Open,
					Access = FileAccess.Read,
					Share = FileShare.Read,
					BufferSize = this.fileBufferSize,
					Options = FileOptions.Asynchronous | FileOptions.SequentialScan
				};
				var cursor = new RunCursor(
					new FileStream( run.Path, streamOptions ),
					run.ItemCount
				);
				cursors[ index ] = cursor;
				var first = await ReadNextAsync(
					cursor,
					this.codec,
					cancellationToken
				).ConfigureAwait( false );
				if ( null != first ) {
					queue.Enqueue(
						new MergeHead( first, index ),
						new MergeHead( first, index )
					);
				}
			}
			while ( queue.TryDequeue( out var head, out _ ) ) {
				cancellationToken.ThrowIfCancellationRequested();
				await writeAsync( head.Item, cancellationToken ).ConfigureAwait( false );
				var next = await ReadNextAsync(
					cursors[ head.RunIndex ],
					this.codec,
					cancellationToken
				).ConfigureAwait( false );
				if ( null != next ) {
					var nextHead = new MergeHead( next, head.RunIndex );
					queue.Enqueue( nextHead, nextHead );
				}
			}
		} finally {
			foreach ( var cursor in cursors ) {
				if ( null != cursor ) {
					await cursor.Stream.DisposeAsync().ConfigureAwait( false );
				}
			}
		}
	}

	private static async ValueTask<StableItem<T>?> ReadNextAsync(
		RunCursor cursor,
		IExternalRunCodec<T> codec,
		CancellationToken cancellationToken
	) {
		if ( 0 == cursor.Remaining ) {
			var extra = await codec.ReadAsync(
				cursor.Stream,
				cancellationToken
			).ConfigureAwait( false );
			if ( extra.HasItem ) {
				throw new InvalidDataException(
					"A sorted run contains more items than its descriptor reports."
				);
			}
			return null;
		}
		var result = await codec.ReadAsync(
			cursor.Stream,
			cancellationToken
		).ConfigureAwait( false );
		if ( !result.HasItem || ( null == result.Item ) ) {
			throw new InvalidDataException(
				"A sorted run ended before its reported item count."
			);
		}
		cursor.Remaining--;
		return result.Item;
	}

	private sealed class RunCursor {
		/// <summary>Initializes one private run cursor.</summary>
		/// <param name="stream">The open run stream.</param>
		/// <param name="remaining">The reported number of unread items.</param>
		public RunCursor( FileStream stream, long remaining ) {
			this.Stream = stream;
			this.Remaining = remaining;
		}

		/// <summary>Gets the open run stream.</summary>
		public FileStream Stream { get; }

		/// <summary>Gets or sets the reported number of unread items.</summary>
		public long Remaining { get; set; }
	}

	private sealed record MergeHead(
		StableItem<T> Item,
		int RunIndex
	);

	private sealed class MergeHeadComparer : IComparer<MergeHead> {
		private readonly StableComparer<T> comparer;

		/// <summary>Initializes one private merge-head comparer.</summary>
		/// <param name="comparer">The stable item comparer.</param>
		public MergeHeadComparer( StableComparer<T> comparer ) {
			this.comparer = comparer;
		}

		int IComparer<MergeHead>.Compare( MergeHead? x, MergeHead? y ) {
			if ( ReferenceEquals( x, y ) ) {
				return 0;
			}
			if ( null == x ) {
				return -1;
			}
			if ( null == y ) {
				return 1;
			}
			var result = this.comparer.Compare( x.Item, y.Item );
			return 0 != result ? result : x.RunIndex.CompareTo( y.RunIndex );
		}
	}
}
