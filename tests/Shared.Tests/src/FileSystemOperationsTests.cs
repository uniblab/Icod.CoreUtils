namespace Icod.CoreUtils.Shared.Tests;

using Icod.CoreUtils.Shared.FileSystem;
using Icod.CoreUtils.Shared.Platform;
using Xunit;

public sealed class FileSystemOperationsTests {
	private static readonly IFileSystemOperations Operations = SystemFileSystemOperations.Instance;

	[Fact]
	public void CapabilityReportMatchesSupportedOperatingSystems() {
		var capabilities = Operations.Capabilities;
		if ( OperatingSystem.IsWindows() ) {
			Assert.False( capabilities.SupportsDataOnlyFileFlush );
			Assert.True( capabilities.SupportsDataAndMetadataFileFlush );
			Assert.False( capabilities.SupportsFileSystemFlush );
			Assert.False( capabilities.SupportsGlobalFlush );
			Assert.True( capabilities.SupportsSparseExtension );
			Assert.True( capabilities.SupportsAllocatedRangeQuery );
			return;
		}
		if ( OperatingSystem.IsLinux() ) {
			Assert.True( capabilities.SupportsDataOnlyFileFlush );
			Assert.True( capabilities.SupportsDataAndMetadataFileFlush );
			Assert.True( capabilities.SupportsFileSystemFlush );
			Assert.True( capabilities.SupportsGlobalFlush );
			Assert.True( capabilities.SupportsSparseExtension );
			Assert.True( capabilities.SupportsAllocatedRangeQuery );
			return;
		}
		if ( OperatingSystem.IsMacOS() ) {
			Assert.False( capabilities.SupportsDataOnlyFileFlush );
			Assert.True( capabilities.SupportsDataAndMetadataFileFlush );
			Assert.False( capabilities.SupportsFileSystemFlush );
			Assert.True( capabilities.SupportsGlobalFlush );
			Assert.True( capabilities.SupportsSparseExtension );
			Assert.False( capabilities.SupportsAllocatedRangeQuery );
			return;
		}
		if ( OperatingSystem.IsFreeBSD() ) {
			Assert.True( capabilities.SupportsDataOnlyFileFlush );
			Assert.True( capabilities.SupportsDataAndMetadataFileFlush );
			Assert.False( capabilities.SupportsFileSystemFlush );
			Assert.True( capabilities.SupportsGlobalFlush );
			Assert.True( capabilities.SupportsSparseExtension );
			Assert.True( capabilities.SupportsAllocatedRangeQuery );
		}
	}

	[Fact]
	public void AllocationMapCopiesAndProtectsItsRanges() {
		var source = new[] {
			new FileAllocationRange(
				0,
				4
			),
		};
		var map = new FileAllocationMap(
			8,
			source
		);
		source[0] = new FileAllocationRange(
			4,
			4
		);

		Assert.Equal(
			0,
			map.Ranges[0].Offset
		);
		var ranges = Assert.IsAssignableFrom<IList<FileAllocationRange>>(
			map.Ranges
		);
		Assert.True( ranges.IsReadOnly );
		Assert.Throws<NotSupportedException>(
			() => ranges[0] = new FileAllocationRange(
				4,
				4
			)
		);
	}

	[Fact]
	public void AllocationMapRejectsInvalidRangeSequences() {
		Assert.Throws<ArgumentException>(
			() => new FileAllocationMap(
				16,
				new[] {
					new FileAllocationRange( 8, 4 ),
					new FileAllocationRange( 4, 4 ),
				}
			)
		);
		Assert.Throws<ArgumentException>(
			() => new FileAllocationMap(
				16,
				new[] {
					new FileAllocationRange( 12, 8 ),
				}
			)
		);
	}

	[Fact]
	public async Task FileFlushDistinguishesDataOnlyFromDataAndMetadata() {
		var path = Path.GetTempFileName();
		try {
			await using var file = OpenTemporaryFile(
				path
			);
			await file.WriteAsync(
				new byte[] { 1, 2, 3, 4 }
			);
			var metadata = await Operations.FlushFileAsync(
				file,
				FileFlushMode.DataAndMetadata
			);
			if ( Operations.Capabilities.SupportsDataAndMetadataFileFlush ) {
				Assert.True( metadata.Supported );
				Assert.True(
					metadata.Succeeded,
					metadata.Message
				);
			} else {
				Assert.False( metadata.Supported );
				Assert.False( metadata.Succeeded );
				Assert.NotNull( metadata.Message );
			}

			var dataOnly = await Operations.FlushFileAsync(
				file,
				FileFlushMode.DataOnly
			);
			if ( Operations.Capabilities.SupportsDataOnlyFileFlush ) {
				Assert.True( dataOnly.Supported );
				Assert.True(
					dataOnly.Succeeded,
					dataOnly.Message
				);
			} else {
				Assert.False( dataOnly.Supported );
				Assert.False( dataOnly.Succeeded );
				Assert.NotNull( dataOnly.Message );
			}
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task InvalidFileFlushModeReturnsControlledFailure() {
		var path = Path.GetTempFileName();
		try {
			await using var file = OpenTemporaryFile(
				path
			);
			var result = await Operations.FlushFileAsync(
				file,
				(FileFlushMode)Int32.MaxValue
			);
			Assert.True( result.Supported );
			Assert.False( result.Succeeded );
			Assert.NotNull( result.Message );
		} finally {
			File.Delete(
				path
			);
		}
	}


	[Fact]
	public async Task PathnameFileFlushDistinguishesDataOnlyFromDataAndMetadata() {
		var path = Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync(
				path,
				[ 1, 2, 3, 4 ]
			);
			var metadata = await Operations.FlushFileAsync(
				path,
				FileFlushMode.DataAndMetadata
			);
			if ( Operations.Capabilities.SupportsDataAndMetadataFileFlush ) {
				Assert.True( metadata.Supported );
				Assert.True(
					metadata.Succeeded,
					metadata.Message
				);
			} else {
				Assert.False( metadata.Supported );
				Assert.False( metadata.Succeeded );
				Assert.NotNull( metadata.Message );
			}

			var dataOnly = await Operations.FlushFileAsync(
				path,
				FileFlushMode.DataOnly
			);
			if ( Operations.Capabilities.SupportsDataOnlyFileFlush ) {
				Assert.True( dataOnly.Supported );
				Assert.True(
					dataOnly.Succeeded,
					dataOnly.Message
				);
			} else {
				Assert.False( dataOnly.Supported );
				Assert.False( dataOnly.Succeeded );
				Assert.NotNull( dataOnly.Message );
			}
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task InvalidPathnameFileFlushModeReturnsControlledFailure() {
		var path = Path.GetTempFileName();
		try {
			var result = await Operations.FlushFileAsync(
				path,
				(FileFlushMode)Int32.MaxValue
			);
			Assert.True( result.Supported );
			Assert.False( result.Succeeded );
			Assert.NotNull( result.Message );
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task MissingPathnameFileFlushReturnsAControlledFailure() {
		var path = Path.Combine(
			Path.GetTempPath(),
			System.String.Concat(
				"Icod.CoreUtils-missing-",
				Guid.NewGuid().ToString( "N" )
			)
		);
		var result = await Operations.FlushFileAsync(
			path,
			FileFlushMode.DataAndMetadata
		);

		if ( Operations.Capabilities.SupportsDataAndMetadataFileFlush ) {
			Assert.True( result.Supported );
			Assert.False( result.Succeeded );
			Assert.NotNull( result.Message );
		} else {
			Assert.False( result.Supported );
			Assert.False( result.Succeeded );
			Assert.NotNull( result.Message );
		}
	}

	[Fact]
	public async Task DisposedFileOperationsReturnControlledResults() {
		var path = Path.GetTempFileName();
		try {
			var file = OpenTemporaryFile(
				path
			);
			await file.DisposeAsync();

			var flush = await Operations.FlushFileAsync(
				file,
				FileFlushMode.DataAndMetadata
			);
			Assert.False( flush.Succeeded );
			Assert.NotNull( flush.Message );

			var extension = await Operations.ExtendSparseAsync(
				file,
				4096
			);
			Assert.False( extension.Succeeded );
			Assert.NotNull( extension.Message );

			var allocation = await Operations.GetAllocatedRangesAsync(
				file
			);
			Assert.False( allocation.Succeeded );
			Assert.NotNull( allocation.Message );
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task FileSystemSpecificFlushUsesSyncFsOnlyWhereAvailable() {
		var path = Path.GetTempFileName();
		try {
			var result = await Operations.FlushFileSystemAsync(
				path
			);
			if ( Operations.Capabilities.SupportsFileSystemFlush ) {
				Assert.True( result.Supported );
				Assert.True(
					result.Succeeded,
					result.Message
				);
			} else {
				Assert.False( result.Supported );
				Assert.False( result.Succeeded );
				Assert.NotNull( result.Message );
			}
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task GlobalFlushReturnsAControlledPlatformResult() {
		var result = await Operations.FlushAllFileSystemsAsync();
		if ( Operations.Capabilities.SupportsGlobalFlush ) {
			Assert.True( result.Supported );
			Assert.True(
				result.Succeeded,
				result.Message
			);
		} else {
			Assert.False( result.Supported );
			Assert.False( result.Succeeded );
			Assert.NotNull( result.Message );
		}
	}

	[Fact]
	public async Task SparseExtensionPreservesDataPositionAndLength() {
		const long requestedLength = 4L * 1024L * 1024L;
		var path = Path.GetTempFileName();
		try {
			await using var file = OpenTemporaryFile(
				path
			);
			var prefix = Enumerable.Range(
				0,
				4096
			).Select(
				value => unchecked( (byte)value )
			).ToArray();
			await file.WriteAsync(
				prefix
			);
			file.Position = 37;

			var result = await Operations.ExtendSparseAsync(
				file,
				requestedLength
			);
			if ( !result.Supported ) {
				Assert.NotNull( result.Message );
				return;
			}
			Assert.True(
				result.Succeeded,
				result.Message
			);
			Assert.NotNull( result.Value );
			Assert.Equal(
				4096,
				result.Value.OriginalLength
			);
			Assert.Equal(
				requestedLength,
				result.Value.NewLength
			);
			Assert.Equal(
				requestedLength,
				file.Length
			);
			Assert.Equal(
				37,
				file.Position
			);

			file.Position = 0;
			var actual = new byte[prefix.Length];
			await file.ReadExactlyAsync(
				actual
			);
			Assert.Equal(
				prefix,
				actual
			);

			AssertControlledAllocationResult(
				result.Value.Allocation,
				requestedLength
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task SparseExtensionRejectsShrinking() {
		var path = Path.GetTempFileName();
		try {
			await using var file = OpenTemporaryFile(
				path
			);
			file.SetLength(
				4096
			);
			var result = await Operations.ExtendSparseAsync(
				file,
				2048
			);
			if ( !Operations.Capabilities.SupportsSparseExtension ) {
				Assert.False( result.Supported );
				Assert.NotNull( result.Message );
				return;
			}
			Assert.True( result.Supported );
			Assert.False( result.Succeeded );
			Assert.Equal(
				4096,
				file.Length
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task SparseExtensionRequiresAWritableSeekableStream() {
		var path = Path.GetTempFileName();
		try {
			await using var file = new FileStream(
				path,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete,
				bufferSize: 4096,
				FileOptions.Asynchronous | FileOptions.RandomAccess
			);
			var result = await Operations.ExtendSparseAsync(
				file,
				4096
			);
			if ( !Operations.Capabilities.SupportsSparseExtension ) {
				Assert.False( result.Supported );
				Assert.NotNull( result.Message );
				return;
			}
			Assert.True( result.Supported );
			Assert.False( result.Succeeded );
			Assert.NotNull( result.Message );
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task SameLengthSparseExtensionPreservesPosition() {
		var path = Path.GetTempFileName();
		try {
			await using var file = OpenTemporaryFile(
				path
			);
			file.SetLength(
				4096
			);
			file.Position = 17;
			var result = await Operations.ExtendSparseAsync(
				file,
				4096
			);
			if ( !Operations.Capabilities.SupportsSparseExtension ) {
				Assert.False( result.Supported );
				Assert.NotNull( result.Message );
				return;
			}
			Assert.True( result.Supported );
			Assert.True(
				result.Succeeded,
				result.Message
			);
			Assert.Equal(
				17,
				file.Position
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task AllocatedRangeQueryDoesNotChangeTheFilePosition() {
		var path = Path.GetTempFileName();
		try {
			await using var file = OpenTemporaryFile(
				path
			);
			await file.WriteAsync(
				new byte[8192]
			);
			file.Position = 53;
			var result = await Operations.GetAllocatedRangesAsync(
				file
			);
			Assert.Equal(
				53,
				file.Position
			);
			AssertControlledAllocationResult(
				result,
				8192
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task AllocatedRangeQueryPreservesSynchronousHandlePositionForSubsequentIo() {
		var path = Path.GetTempFileName();
		try {
			using var file = OpenTemporaryFile(
				path,
				asynchronous: false
			);
			file.Write(
				new byte[8192]
			);
			file.Position = 53;
			var result = await Operations.GetAllocatedRangesAsync(
				file
			);
			if ( !result.Supported ) {
				Assert.NotNull( result.Message );
				return;
			}
			Assert.True(
				result.Succeeded,
				result.Message
			);
			Assert.Equal(
				53,
				file.Position
			);

			file.WriteByte(
				0x5a
			);
			file.Flush();
			Assert.Equal(
				54,
				file.Position
			);

			file.Position = 53;
			Assert.Equal(
				0x5a,
				file.ReadByte()
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task AllocatedRangePathOverloadReturnsAControlledResult() {
		var path = Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync(
				path,
				new byte[4096]
			);
			var result = await Operations.GetAllocatedRangesAsync(
				path
			);
			AssertControlledAllocationResult(
				result,
				4096
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task WindowsFileSystemControlsSupportSynchronousHandles() {
		if ( !OperatingSystem.IsWindows() ) {
			return;
		}

		var path = Path.GetTempFileName();
		try {
			using var file = OpenTemporaryFile(
				path,
				asynchronous: false
			);
			Assert.False( file.IsAsync );
			file.Write(
				new byte[4096]
			);
			file.Position = 19;

			var extension = await Operations.ExtendSparseAsync(
				file,
				64L * 1024L
			);
			if ( !extension.Supported ) {
				Assert.NotNull( extension.Message );
				return;
			}
			Assert.True(
				extension.Succeeded,
				extension.Message
			);
			Assert.Equal(
				19,
				file.Position
			);

			var allocation = await Operations.GetAllocatedRangesAsync(
				file
			);
			AssertControlledAllocationResult(
				allocation,
				64L * 1024L
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task RepeatedWindowsAsyncFileSystemControlsDoNotLeakIocpCompletions() {
		if ( !OperatingSystem.IsWindows() ) {
			return;
		}

		var path = Path.GetTempFileName();
		try {
			await using var file = OpenTemporaryFile(
				path
			);
			await file.WriteAsync(
				new byte[4096]
			);
			for ( var iteration = 0; 32 > iteration; iteration++ ) {
				var requestedLength = ( iteration + 2L ) * 64L * 1024L;
				var extension = await Operations.ExtendSparseAsync(
					file,
					requestedLength
				);
				if ( !extension.Supported ) {
					Assert.NotNull( extension.Message );
					return;
				}
				Assert.True(
					extension.Succeeded,
					extension.Message
				);

				var allocation = await Operations.GetAllocatedRangesAsync(
					file
				);
				AssertControlledAllocationResult(
					allocation,
					requestedLength
				);
				await Task.Yield();
			}
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task OperationsHonorCancellationBeforeNativeCalls() {
		var path = Path.GetTempFileName();
		try {
			await using var file = OpenTemporaryFile(
				path
			);
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();

			await Assert.ThrowsAsync<OperationCanceledException>(
				async () => {
					_ = await Operations.FlushFileAsync(
						file,
						FileFlushMode.DataAndMetadata,
						cancellation.Token
					);
				}
			);
			await Assert.ThrowsAsync<OperationCanceledException>(
				async () => {
					_ = await Operations.FlushFileAsync(
						path,
						FileFlushMode.DataAndMetadata,
						cancellation.Token
					);
				}
			);
			await Assert.ThrowsAsync<OperationCanceledException>(
				async () => {
					_ = await Operations.ExtendSparseAsync(
						file,
						4096,
						cancellation.Token
					);
				}
			);
			await Assert.ThrowsAsync<OperationCanceledException>(
				async () => {
					_ = await Operations.GetAllocatedRangesAsync(
						file,
						cancellation.Token
					);
				}
			);
			await Assert.ThrowsAsync<OperationCanceledException>(
				async () => {
					_ = await Operations.GetAllocatedRangesAsync(
						path,
						cancellation.Token
					);
				}
			);
		} finally {
			File.Delete(
				path
			);
		}
	}

	[Fact]
	public async Task FileSystemOperationsAreInjectable() {
		var expected = PlatformOperationResult.Unsupported(
			"injected"
		);
		IFileSystemOperations operations = new StubFileSystemOperations(
			expected
		);
		var result = await operations.FlushAllFileSystemsAsync();
		Assert.Same(
			expected,
			result
		);
		var pathnameResult = await operations.FlushFileAsync(
			"target",
			FileFlushMode.DataAndMetadata
		);
		Assert.Same(
			expected,
			pathnameResult
		);
	}

	private static FileStream OpenTemporaryFile(
		string path,
		bool asynchronous = true
	) => new(
		path,
		new FileStreamOptions {
			Access = FileAccess.ReadWrite,
			Mode = FileMode.Open,
			Share = FileShare.ReadWrite | FileShare.Delete,
			Options = FileOptions.RandomAccess
				| ( asynchronous ? FileOptions.Asynchronous : FileOptions.None ),
		}
	);

	private static void AssertControlledAllocationResult(
		PlatformOperationResult<FileAllocationMap> result,
		long expectedLogicalLength
	) {
		if ( Operations.Capabilities.SupportsAllocatedRangeQuery ) {
			if ( result.Supported ) {
				Assert.True(
					result.Succeeded,
					result.Message
				);
				Assert.NotNull( result.Value );
				Assert.Equal(
					expectedLogicalLength,
					result.Value.LogicalLength
				);
				AssertRangesAreOrdered(
					result.Value
				);
			} else {
				Assert.NotNull( result.Message );
			}
		} else {
			Assert.False( result.Supported );
			Assert.False( result.Succeeded );
			Assert.NotNull( result.Message );
		}
	}

	private static void AssertRangesAreOrdered(
		FileAllocationMap map
	) {
		var previousEnd = 0L;
		foreach ( var range in map.Ranges ) {
			Assert.True(
				0 <= range.Offset
			);
			Assert.True(
				0 < range.Length
			);
			Assert.True(
				previousEnd <= range.Offset
			);
			Assert.True(
				range.End <= map.LogicalLength
			);
			previousEnd = range.End;
		}
	}

	private sealed class StubFileSystemOperations(
		PlatformOperationResult result
	) : IFileSystemOperations {
		public FileSystemCapabilities Capabilities { get; } = new(
			false,
			false,
			false,
			false,
			false,
			false
		);

		public ValueTask<PlatformOperationResult> FlushFileAsync(
			FileStream file,
			FileFlushMode mode,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult(
			result
		);


		public ValueTask<PlatformOperationResult> FlushFileAsync(
			string path,
			FileFlushMode mode,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult(
			result
		);

		public ValueTask<PlatformOperationResult> FlushFileSystemAsync(
			string path,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult(
			result
		);

		public ValueTask<PlatformOperationResult> FlushAllFileSystemsAsync(
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult(
			result
		);

		public ValueTask<PlatformOperationResult<SparseExtensionInfo>> ExtendSparseAsync(
			FileStream file,
			long newLength,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult(
			PlatformOperationResult<SparseExtensionInfo>.Unsupported(
				result.Message ?? "injected"
			)
		);

		public ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync(
			FileStream file,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult(
			PlatformOperationResult<FileAllocationMap>.Unsupported(
				result.Message ?? "injected"
			)
		);

		public ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync(
			string path,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult(
			PlatformOperationResult<FileAllocationMap>.Unsupported(
				result.Message ?? "injected"
			)
		);
	}
}
