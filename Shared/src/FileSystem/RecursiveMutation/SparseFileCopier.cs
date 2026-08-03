using Icod.CoreUtils.Shared.FileSystem;

namespace Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;

/// <summary>Describes the result of copying one ordinary file with an E5 sparse-file policy.</summary>
/// <param name="Succeeded">Whether the complete copy succeeded.</param>
/// <param name="SparseSource">Whether the source allocation map identified holes.</param>
/// <param name="SparsePreserved">Whether the destination retained the source holes.</param>
/// <param name="LogicalLength">The logical source length represented by the result.</param>
/// <param name="BytesCopied">The number of source bytes physically transferred.</param>
/// <param name="Message">An optional controlled failure message.</param>
/// <param name="Exception">An optional underlying exception.</param>
public sealed record SparseFileCopyResult(
	bool Succeeded,
	bool SparseSource,
	bool SparsePreserved,
	long LogicalLength,
	long BytesCopied,
	string? Message = null,
	Exception? Exception = null
);

/// <summary>Copies ordinary-file contents while preserving reported holes when supported.</summary>
public sealed class SparseFileCopier {
	private readonly IFileSystemOperations _operations;

	/// <summary>Initializes a sparse-aware file copier.</summary>
	/// <param name="operations">The capability-aware allocation and sparse-extension provider.</param>
	public SparseFileCopier( IFileSystemOperations operations ) {
		ArgumentNullException.ThrowIfNull( operations );
		_operations = operations;
	}

	/// <summary>Copies the complete source stream according to the requested sparse policy.</summary>
	/// <param name="source">The readable and seekable source stream.</param>
	/// <param name="destination">The writable and seekable destination stream.</param>
	/// <param name="policy">The requested sparse-file policy.</param>
	/// <param name="bufferSize">The content-copy buffer size.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A controlled copy result. The caller retains ownership of both streams.</returns>
	public async ValueTask<SparseFileCopyResult> CopyAsync(
		FileStream source,
		FileStream destination,
		RecursiveSparseFilePolicy policy = RecursiveSparseFilePolicy.WhenSupported,
		int bufferSize = 81_920,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( source );
		ArgumentNullException.ThrowIfNull( destination );
		if ( ReferenceEquals( source, destination ) ) {
			throw new ArgumentException( "The source and destination streams must be distinct.", nameof( destination ) );
		}
		if ( !source.CanRead || !source.CanSeek ) {
			throw new ArgumentException( "The source stream must be readable and seekable.", nameof( source ) );
		}
		if ( !destination.CanWrite || !destination.CanSeek ) {
			throw new ArgumentException( "The destination stream must be writable and seekable.", nameof( destination ) );
		}
		if ( !Enum.IsDefined( typeof( RecursiveSparseFilePolicy ), policy ) ) {
			throw new ArgumentOutOfRangeException( nameof( policy ) );
		}
		if ( 1 > bufferSize ) {
			throw new ArgumentOutOfRangeException( nameof( bufferSize ) );
		}
		var sourcePosition = source.Position;
		var destinationPosition = destination.Position;
		var sourceLength = source.Length;
		var sparseSource = false;
		try {
			if ( policy == RecursiveSparseFilePolicy.Never ) {
				return await CopyDenseAsync( source, destination, bufferSize, cancellationToken ).ConfigureAwait( false );
			}
			if ( !_operations.Capabilities.SupportsAllocatedRangeQuery ) {
				return policy == RecursiveSparseFilePolicy.Require
					? new SparseFileCopyResult( false, false, false, sourceLength, 0, "Allocated-range queries are unavailable." )
					: await CopyDenseAsync( source, destination, bufferSize, cancellationToken ).ConfigureAwait( false );
			}
			var allocationResult = await _operations.GetAllocatedRangesAsync( source, cancellationToken ).ConfigureAwait( false );
			if ( !allocationResult.Succeeded || allocationResult.Value is null ) {
				if ( policy == RecursiveSparseFilePolicy.Require ) {
					return new SparseFileCopyResult(
						false,
						false,
						false,
						sourceLength,
						0,
						allocationResult.Message ?? "Allocated ranges could not be obtained.",
						allocationResult.Exception
					);
				}
				return await CopyDenseAsync( source, destination, bufferSize, cancellationToken ).ConfigureAwait( false );
			}
			var allocation = allocationResult.Value;
			if ( allocation.LogicalLength != sourceLength ) {
				const string message = "The source length changed while its allocation map was being prepared.";
				if ( policy == RecursiveSparseFilePolicy.Require ) {
					return new SparseFileCopyResult( false, allocation.IsSparse, false, sourceLength, 0, message );
				}
				return (await CopyDenseAsync( source, destination, bufferSize, cancellationToken ).ConfigureAwait( false )) with {
					SparseSource = allocation.IsSparse,
					Message = message
				};
			}
			sparseSource = allocation.IsSparse;
			if ( !sparseSource ) {
				return await CopyDenseAsync( source, destination, bufferSize, cancellationToken ).ConfigureAwait( false );
			}
			if ( !_operations.Capabilities.SupportsSparseExtension ) {
				if ( policy == RecursiveSparseFilePolicy.Require ) {
					return new SparseFileCopyResult( false, true, false, allocation.LogicalLength, 0, "Sparse destination creation is unavailable." );
				}
				return (await CopyDenseAsync( source, destination, bufferSize, cancellationToken ).ConfigureAwait( false )) with {
					SparseSource = true
				};
			}
			destination.Position = 0;
			destination.SetLength( 0 );
			var extension = await _operations.ExtendSparseAsync(
				destination,
				allocation.LogicalLength,
				cancellationToken
			).ConfigureAwait( false );
			if ( !extension.Succeeded ) {
				if ( policy == RecursiveSparseFilePolicy.Require ) {
					return new SparseFileCopyResult(
						false,
						true,
						false,
						allocation.LogicalLength,
						0,
						extension.Message ?? "The sparse destination could not be prepared.",
						extension.Exception
					);
				}
				return (await CopyDenseAsync( source, destination, bufferSize, cancellationToken ).ConfigureAwait( false )) with {
					SparseSource = true
				};
			}
			var buffer = new byte[bufferSize];
			var copied = 0L;
			foreach ( var range in allocation.Ranges ) {
				cancellationToken.ThrowIfCancellationRequested();
				source.Position = range.Offset;
				destination.Position = range.Offset;
				var remaining = range.Length;
				while ( remaining > 0 ) {
					var requested = (int)Math.Min( buffer.Length, remaining );
					var read = await source.ReadAsync( buffer.AsMemory( 0, requested ), cancellationToken ).ConfigureAwait( false );
					if ( read == 0 ) {
						throw new EndOfStreamException( "The source ended within a reported allocated range." );
					}
					await destination.WriteAsync( buffer.AsMemory( 0, read ), cancellationToken ).ConfigureAwait( false );
					remaining -= read;
					copied = checked( copied + read );
				}
			}
			if ( destination.Length != allocation.LogicalLength ) {
				destination.SetLength( allocation.LogicalLength );
			}
			return new SparseFileCopyResult( true, true, true, allocation.LogicalLength, copied );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			throw;
		} catch ( Exception exception ) {
			return new SparseFileCopyResult( false, sparseSource, false, sourceLength, 0, exception.Message, exception );
		} finally {
			source.Position = Math.Min( sourcePosition, source.Length );
			destination.Position = Math.Min( destinationPosition, destination.Length );
		}
	}

	private static async ValueTask<SparseFileCopyResult> CopyDenseAsync(
		FileStream source,
		FileStream destination,
		int bufferSize,
		CancellationToken cancellationToken
	) {
		source.Position = 0;
		destination.Position = 0;
		destination.SetLength( 0 );
		await source.CopyToAsync( destination, bufferSize, cancellationToken ).ConfigureAwait( false );
		return new SparseFileCopyResult( true, false, false, source.Length, source.Length );
	}
}
