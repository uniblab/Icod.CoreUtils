/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace Icod.CoreUtils.Shared.Ordering;

using System.Runtime.ExceptionServices;
using Icod.CommandFramework.Temporary;

/// <summary>Coordinates bounded-memory stable run generation, bounded-fan-in merge passes, and deterministic workspace cleanup.</summary>
/// <typeparam name="T">The ordered value type.</typeparam>
public sealed class ExternalOrderingEngine<T> {
	private readonly IComparer<T> valueComparer;
	private readonly IExternalRunCodec<T> codec;
	private readonly ExternalOrderingOptions<T> options;
	private readonly Func<CancellationToken, TemporaryWorkspace> workspaceFactory;

	/// <summary>Initializes an external-ordering engine.</summary>
	/// <param name="valueComparer">The primary value comparer.</param>
	/// <param name="codec">The temporary-run codec.</param>
	/// <param name="options">The run and merge options.</param>
	/// <param name="workspaceFactory">An optional injectable secure-workspace factory.</param>
	public ExternalOrderingEngine(
		IComparer<T> valueComparer,
		IExternalRunCodec<T> codec,
		ExternalOrderingOptions<T> options,
		Func<CancellationToken, TemporaryWorkspace>? workspaceFactory = null
	) {
		ArgumentNullException.ThrowIfNull( valueComparer );
		ArgumentNullException.ThrowIfNull( codec );
		ArgumentNullException.ThrowIfNull( options );
		this.valueComparer = valueComparer;
		this.codec = codec;
		this.options = options;
		this.workspaceFactory = workspaceFactory
			?? ( cancellationToken => TemporaryWorkspace.Create(
				cancellationToken: cancellationToken
			) );
	}

	/// <summary>Orders an asynchronous sequence and writes values without retaining the complete result.</summary>
	/// <param name="source">The asynchronous input sequence.</param>
	/// <param name="writeAsync">The destination callback invoked in stable order.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task representing ordering, output, and cleanup.</returns>
	/// <remarks>
	/// Cleanup is attempted without the operation cancellation token. If both the operation and cleanup fail,
	/// an <see cref="AggregateException"/> preserves both failures in that order.
	/// </remarks>
	public async Task OrderAsync(
		IAsyncEnumerable<T> source,
		Func<T, CancellationToken, ValueTask> writeAsync,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( source );
		ArgumentNullException.ThrowIfNull( writeAsync );
		TemporaryWorkspace? workspace = null;
		Exception? operationFailure = null;
		try {
			workspace = this.workspaceFactory( cancellationToken );
			var builder = new ExternalRunBuilder<T>(
				this.valueComparer,
				this.codec,
				this.options
			);
			var runs = await builder.BuildAsync(
				source,
				workspace,
				cancellationToken
			).ConfigureAwait( false );
			var reduced = await this.ReduceRunsAsync(
				runs,
				workspace,
				cancellationToken
			).ConfigureAwait( false );
			var merger = new StableExternalMerger<T>(
				this.valueComparer,
				this.codec,
				this.options.FileBufferSize
			);
			await merger.MergeAsync(
				reduced,
				( item, token ) => writeAsync( item.Value, token ),
				cancellationToken
			).ConfigureAwait( false );
		} catch ( Exception exception ) {
			operationFailure = exception;
		}
		Exception? cleanupFailure = null;
		if ( null != workspace ) {
			try {
				workspace.Dispose();
			} catch ( Exception exception ) {
				cleanupFailure = exception;
			}
		}
		if ( ( null != operationFailure ) && ( null != cleanupFailure ) ) {
			throw new AggregateException( operationFailure, cleanupFailure );
		}
		if ( null != operationFailure ) {
			ExceptionDispatchInfo.Capture( operationFailure ).Throw();
		}
		if ( null != cleanupFailure ) {
			ExceptionDispatchInfo.Capture( cleanupFailure ).Throw();
		}
	}

	private async Task<IReadOnlyList<ExternalRun>> ReduceRunsAsync(
		IReadOnlyList<ExternalRun> initialRuns,
		TemporaryWorkspace workspace,
		CancellationToken cancellationToken
	) {
		var runs = initialRuns.ToList();
		var merger = new StableExternalMerger<T>(
			this.valueComparer,
			this.codec,
			this.options.FileBufferSize
		);
		while ( this.options.MergeFanIn < runs.Count ) {
			var nextPass = new List<ExternalRun>();
			for ( var offset = 0; offset < runs.Count; offset += this.options.MergeFanIn ) {
				cancellationToken.ThrowIfCancellationRequested();
				var count = Math.Min( this.options.MergeFanIn, runs.Count - offset );
				if ( 1 == count ) {
					nextPass.Add( runs[ offset ] );
					continue;
				}
				var group = runs.GetRange( offset, count );
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
					await merger.MergeAsync(
						group,
						( item, token ) => this.codec.WriteAsync( stream, item, token ),
						cancellationToken
					).ConfigureAwait( false );
					await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
				}
				long mergedCount = 0;
				foreach ( var run in group ) {
					mergedCount = checked( mergedCount + run.ItemCount );
				}
				nextPass.Add( new ExternalRun( path, mergedCount ) );
				foreach ( var run in group ) {
					workspace.DeleteFile( run.Path );
				}
			}
			runs = nextPass;
		}
		return runs;
	}
}
