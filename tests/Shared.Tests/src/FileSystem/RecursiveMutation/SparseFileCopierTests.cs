using Icod.CoreUtils.Shared.FileSystem;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.Platform;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.RecursiveMutation;

/// <summary>Tests sparse-file detection, preservation, and controlled fallback.</summary>
public sealed class SparseFileCopierTests {
	/// <summary>Verifies that only reported allocated ranges are copied while logical length is retained.</summary>
	[Fact]
	public async Task CopiesOnlyAllocatedRangesWhenSparseSupportIsAvailable() {
		var directory = Directory.CreateTempSubdirectory( "e5-sparse-" );
		try {
			var sourcePath = System.IO.Path.Combine( directory.FullName, "source" );
			var destinationPath = System.IO.Path.Combine( directory.FullName, "destination" );
			await File.WriteAllBytesAsync( sourcePath, new byte[] { 1, 2, 0, 0, 0, 0, 0, 0, 8, 9 } );
			await File.WriteAllBytesAsync( destinationPath, Enumerable.Repeat( (byte)0xFF, 10 ).ToArray() );
			await using var source = new FileStream( sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read );
			await using var destination = new FileStream( destinationPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None );
			var map = new FileAllocationMap(
				10,
				new[] { new FileAllocationRange( 0, 2 ), new FileAllocationRange( 8, 2 ) }
			);
			var result = await new SparseFileCopier( new SyntheticOperations( map ) ).CopyAsync(
				source,
				destination,
				RecursiveSparseFilePolicy.Require
			);
			Assert.True( result.Succeeded );
			Assert.True( result.SparseSource );
			Assert.True( result.SparsePreserved );
			Assert.Equal( 4, result.BytesCopied );
			Assert.Equal( 10, destination.Length );
			destination.Position = 0;
			var bytes = new byte[10];
			_ = await destination.ReadAsync( bytes );
			Assert.Equal( new byte[] { 1, 2, 0, 0, 0, 0, 0, 0, 8, 9 }, bytes );
		} finally {
			directory.Delete( true );
		}
	}

	/// <summary>Verifies controlled failure when sparse preservation is mandatory but unavailable.</summary>
	[Fact]
	public async Task RequireFailsWhenAllocationQueriesAreUnavailable() {
		var directory = Directory.CreateTempSubdirectory( "e5-sparse-" );
		try {
			var sourcePath = System.IO.Path.Combine( directory.FullName, "source" );
			var destinationPath = System.IO.Path.Combine( directory.FullName, "destination" );
			await File.WriteAllBytesAsync( sourcePath, new byte[] { 1 } );
			await using var source = new FileStream( sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read );
			await using var destination = new FileStream( destinationPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None );
			var result = await new SparseFileCopier( new UnsupportedOperations() ).CopyAsync(
				source,
				destination,
				RecursiveSparseFilePolicy.Require
			);
			Assert.False( result.Succeeded );
			Assert.True( result.Message!.Contains( "unavailable", StringComparison.OrdinalIgnoreCase ) );
		} finally {
			directory.Delete( true );
		}
	}


	/// <summary>Verifies that a stale allocation map is rejected when sparse preservation is mandatory.</summary>
	[Fact]
	public async Task RequireRejectsAllocationMapWithDifferentLogicalLength() {
		var directory = Directory.CreateTempSubdirectory( "e5-sparse-" );
		try {
			var sourcePath = System.IO.Path.Combine( directory.FullName, "source" );
			var destinationPath = System.IO.Path.Combine( directory.FullName, "destination" );
			await File.WriteAllBytesAsync( sourcePath, new byte[] { 1, 2, 3, 4 } );
			await using var source = new FileStream( sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read );
			await using var destination = new FileStream( destinationPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None );
			var staleMap = new FileAllocationMap( 3, new[] { new FileAllocationRange( 0, 3 ) } );
			var result = await new SparseFileCopier( new SyntheticOperations( staleMap ) ).CopyAsync(
				source,
				destination,
				RecursiveSparseFilePolicy.Require
			);
			Assert.False( result.Succeeded );
			Assert.True( result.Message!.Contains( "changed", StringComparison.OrdinalIgnoreCase ) );
			Assert.Equal( 0, destination.Length );
		} finally {
			directory.Delete( true );
		}
	}

	private sealed class SyntheticOperations : IFileSystemOperations {
		private readonly FileAllocationMap _map;

		/// <summary>Initializes operations with one deterministic allocation map.</summary>
		public SyntheticOperations( FileAllocationMap map ) {
			_map = map;
		}

		/// <summary>Gets the synthetic capability report.</summary>
		public FileSystemCapabilities Capabilities { get; } = new( true, true, true, true, true, true );

		/// <summary>Returns a successful synthetic file flush.</summary>
		public ValueTask<PlatformOperationResult> FlushFileAsync( FileStream file, FileFlushMode mode, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult( PlatformOperationResult.Success() );

		/// <summary>Returns a successful synthetic filesystem flush.</summary>
		public ValueTask<PlatformOperationResult> FlushFileSystemAsync( string path, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult( PlatformOperationResult.Success() );

		/// <summary>Returns a successful synthetic global flush.</summary>
		public ValueTask<PlatformOperationResult> FlushAllFileSystemsAsync( CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult( PlatformOperationResult.Success() );

		/// <summary>Extends the stream or returns the configured unsupported result.</summary>
		public ValueTask<PlatformOperationResult<SparseExtensionInfo>> ExtendSparseAsync(
			FileStream file,
			long newLength,
			CancellationToken cancellationToken = default
		) {
			file.SetLength( newLength );
			return ValueTask.FromResult( PlatformOperationResult<SparseExtensionInfo>.Success(
				new SparseExtensionInfo(
					0,
					newLength,
					PlatformOperationResult<FileAllocationMap>.Success( _map )
				)
			) );
		}

		/// <summary>Returns the configured open-stream allocation result.</summary>
		public ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync(
			FileStream file,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult( PlatformOperationResult<FileAllocationMap>.Success( _map ) );

		/// <summary>Returns the configured pathname allocation result.</summary>
		public ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync(
			string path,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult( PlatformOperationResult<FileAllocationMap>.Success( _map ) );
	}

	private sealed class UnsupportedOperations : IFileSystemOperations {
		/// <summary>Initializes operations that expose no sparse-file capabilities.</summary>
		public UnsupportedOperations() {
		}

		/// <summary>Gets the synthetic capability report.</summary>
		public FileSystemCapabilities Capabilities { get; } = new( true, true, true, true, false, false );

		/// <summary>Returns a successful synthetic file flush.</summary>
		public ValueTask<PlatformOperationResult> FlushFileAsync( FileStream file, FileFlushMode mode, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult( PlatformOperationResult.Success() );

		/// <summary>Returns a successful synthetic filesystem flush.</summary>
		public ValueTask<PlatformOperationResult> FlushFileSystemAsync( string path, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult( PlatformOperationResult.Success() );

		/// <summary>Returns a successful synthetic global flush.</summary>
		public ValueTask<PlatformOperationResult> FlushAllFileSystemsAsync( CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult( PlatformOperationResult.Success() );

		/// <summary>Extends the stream or returns the configured unsupported result.</summary>
		public ValueTask<PlatformOperationResult<SparseExtensionInfo>> ExtendSparseAsync( FileStream file, long newLength, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult( PlatformOperationResult<SparseExtensionInfo>.Unsupported( "unsupported" ) );

		/// <summary>Returns an unsupported open-stream allocation result.</summary>
		public ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync( FileStream file, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult( PlatformOperationResult<FileAllocationMap>.Unsupported( "unsupported" ) );

		/// <summary>Returns an unsupported pathname allocation result.</summary>
		public ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync( string path, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult( PlatformOperationResult<FileAllocationMap>.Unsupported( "unsupported" ) );
	}
}
