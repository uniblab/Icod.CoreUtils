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
			Assert.False( capabilities.SupportsAllocatedRangeQuery );
		}
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
			Assert.True( metadata.Supported );
			Assert.True(
				metadata.Succeeded,
				metadata.Message
			);

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
	public async Task FileSystemSpecificFlushUsesSyncFsOnlyWhereAvailable() {
		var path = Path.GetTempFileName();
		try {
			var result = await Operations.FlushFileSystemAsync(
				path
			);
			if ( OperatingSystem.IsLinux() ) {
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
		if (
			OperatingSystem.IsLinux()
			|| OperatingSystem.IsMacOS()
			|| OperatingSystem.IsFreeBSD()
		) {
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
			Assert.True( result.Supported );
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

			var allocation = result.Value.Allocation;
			if ( Operations.Capabilities.SupportsAllocatedRangeQuery ) {
				if ( allocation.Supported ) {
					Assert.True(
						allocation.Succeeded,
						allocation.Message
					);
					Assert.NotNull( allocation.Value );
					Assert.Equal(
						requestedLength,
						allocation.Value.LogicalLength
					);
					AssertRangesAreOrdered(
						allocation.Value
					);
				} else {
					Assert.NotNull( allocation.Message );
				}
			} else {
				Assert.False( allocation.Supported );
				Assert.NotNull( allocation.Message );
			}
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
			if ( Operations.Capabilities.SupportsAllocatedRangeQuery ) {
				if ( result.Supported ) {
					Assert.True(
						result.Succeeded,
						result.Message
					);
					Assert.NotNull( result.Value );
					AssertRangesAreOrdered(
						result.Value
					);
				} else {
					Assert.NotNull( result.Message );
				}
			} else {
				Assert.False( result.Supported );
				Assert.NotNull( result.Message );
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
	}

	private static FileStream OpenTemporaryFile(
		string path
	) => new(
		path,
		new FileStreamOptions {
			Access = FileAccess.ReadWrite,
			Mode = FileMode.Open,
			Share = FileShare.ReadWrite | FileShare.Delete,
			Options = FileOptions.Asynchronous | FileOptions.RandomAccess,
		}
	);

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
