namespace Icod.CoreUtils.Shared.FileSystem;

/// <summary>
/// Describes the host operating system APIs available for durable flush and sparse-file operations.
/// Individual filesystems may still reject an operation that the operating system exposes.
/// </summary>
public sealed record FileSystemCapabilities(
	bool SupportsDataOnlyFileFlush,
	bool SupportsDataAndMetadataFileFlush,
	bool SupportsFileSystemFlush,
	bool SupportsGlobalFlush,
	bool SupportsSparseExtension,
	bool SupportsAllocatedRangeQuery
);

/// <summary>Identifies the durability requested for a file-specific flush.</summary>
public enum FileFlushMode {
	/// <summary>Flush file data without requiring unrelated metadata to reach durable storage.</summary>
	DataOnly,
	/// <summary>Flush file data and the metadata required to describe the file.</summary>
	DataAndMetadata,
}

/// <summary>Describes a logical byte range for which storage may be allocated.</summary>
public readonly record struct FileAllocationRange {
	/// <summary>Initializes an allocated logical range.</summary>
	/// <param name="offset">The zero-based byte offset.</param>
	/// <param name="length">The range length in bytes.</param>
	public FileAllocationRange( long offset, long length ) {
		if ( 0 > offset ) {
			throw new ArgumentOutOfRangeException(
				nameof( offset )
			);
		}
		if ( 0 >= length ) {
			throw new ArgumentOutOfRangeException(
				nameof( length )
			);
		}
		_ = checked( offset + length );
		this.Offset = offset;
		this.Length = length;
	}

	/// <summary>Gets the zero-based byte offset.</summary>
	public long Offset { get; }
	/// <summary>Gets the range length in bytes.</summary>
	public long Length { get; }
	/// <summary>Gets the first byte offset after the range.</summary>
	public long End => checked( this.Offset + this.Length );
}

/// <summary>
/// Describes the logical ranges that the operating system reports as potentially allocated.
/// Reported ranges may contain zero bytes and need not correspond one-for-one with physical extents.
/// </summary>
public sealed class FileAllocationMap {
	/// <summary>Initializes an allocation map.</summary>
	/// <param name="logicalLength">The logical file length.</param>
	/// <param name="ranges">Ordered, non-overlapping allocated logical ranges.</param>
	public FileAllocationMap(
		long logicalLength,
		IEnumerable<FileAllocationRange> ranges
	) {
		if ( 0 > logicalLength ) {
			throw new ArgumentOutOfRangeException(
				nameof( logicalLength )
			);
		}
		ArgumentNullException.ThrowIfNull(
			ranges
		);
		var copy = ranges.ToArray();
		var previousEnd = 0L;
		var reportedAllocatedLength = 0L;
		for ( var index = 0; index < copy.Length; index++ ) {
			var range = copy[index];
			if (
				0 < index
				&& range.Offset < previousEnd
			) {
				throw new ArgumentException(
					"allocated ranges must be ordered and non-overlapping",
					nameof( ranges )
				);
			}
			if ( logicalLength < range.End ) {
				throw new ArgumentException(
					"an allocated range extends beyond the logical file length",
					nameof( ranges )
				);
			}
			previousEnd = range.End;
			reportedAllocatedLength = checked(
				reportedAllocatedLength + range.Length
			);
		}
		this.LogicalLength = logicalLength;
		this.Ranges = copy;
		this.ReportedAllocatedLength = reportedAllocatedLength;
	}

	/// <summary>Gets the logical file length.</summary>
	public long LogicalLength { get; }
	/// <summary>Gets the ordered allocated logical ranges.</summary>
	public IReadOnlyList<FileAllocationRange> Ranges { get; }
	/// <summary>Gets the sum of the reported allocated logical ranges.</summary>
	public long ReportedAllocatedLength { get; }
	/// <summary>Gets whether the reported ranges leave at least one logical hole.</summary>
	public bool IsSparse => this.ReportedAllocatedLength < this.LogicalLength;
}

/// <summary>Describes a completed sparse-extension request.</summary>
public sealed record SparseExtensionInfo(
	long OriginalLength,
	long NewLength,
	Icod.CoreUtils.Shared.Platform.PlatformOperationResult<FileAllocationMap> Allocation
) {
	/// <summary>Gets whether the allocation query confirmed at least one logical hole.</summary>
	public bool SparseConfirmed =>
		this.Allocation.Succeeded
		&& null != this.Allocation.Value
		&& this.Allocation.Value.IsSparse
	;
}
