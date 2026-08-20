namespace Icod.CoreUtils.Cp.Tests;

using Icod.CoreUtils.Shared.FileSystem;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using CpCommand = Icod.CoreUtils.Cp.Command;
using Xunit;

/// <summary>Exercises reparse-point fidelity and strict sparse-file behavior.</summary>
public sealed class ReparseAndSparseTests {
	/// <summary>Verifies recursive copy preserves a directory symbolic link without traversing its target.</summary>
	[Fact]
	public async Task RecursiveCopyPreservesDirectorySymbolicLink() {
		var root = CreateTemporaryDirectory();
		var source = Directory.CreateDirectory( System.IO.Path.Combine( root, "source" ) ).FullName;
		var target = Directory.CreateDirectory( System.IO.Path.Combine( root, "target" ) ).FullName;
		var sourceLink = System.IO.Path.Combine( source, "link" );
		var destination = System.IO.Path.Combine( root, "destination" );
		await File.WriteAllTextAsync( System.IO.Path.Combine( target, "sentinel" ), "target" );
		try {
			if ( !await TryCreateDirectorySymbolicLinkAsync( sourceLink, target ) ) return;
			var status = await RunAsync( new[] { "--recursive", source, destination } );
			Assert.Equal( 0, status );
			var copiedLink = System.IO.Path.Combine( destination, "link" );
			var observation = await SystemReadOnlyFileSystemProvider.Instance.ObserveAsync(
				copiedLink,
				PathDereferenceMode.NoFollow
			);
			Assert.True( observation.IsSymbolicLink );
			Assert.True( File.Exists( System.IO.Path.Combine( target, "sentinel" ) ) );
		} finally {
			RemovePhysicalReparsePoint( sourceLink );
			RemovePhysicalReparsePoint( System.IO.Path.Combine( destination, "link" ) );
			DeleteTree( root );
		}
	}

	/// <summary>Verifies recursive copy preserves a Windows junction rather than converting or traversing it.</summary>
	[Fact]
	public async Task RecursiveCopyPreservesWindowsJunction() {
		if ( !OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		var source = Directory.CreateDirectory( System.IO.Path.Combine( root, "source" ) ).FullName;
		var target = Directory.CreateDirectory( System.IO.Path.Combine( root, "target" ) ).FullName;
		var sourceJunction = System.IO.Path.Combine( source, "junction" );
		var destination = System.IO.Path.Combine( root, "destination" );
		await File.WriteAllTextAsync( System.IO.Path.Combine( target, "sentinel" ), "target" );
		try {
			if ( !await TryCreateJunctionAsync( sourceJunction, target ) ) return;
			var status = await RunAsync( new[] { "--recursive", source, destination } );
			Assert.Equal( 0, status );
			var copiedJunction = System.IO.Path.Combine( destination, "junction" );
			var observation = await SystemReadOnlyFileSystemProvider.Instance.ObserveAsync(
				copiedJunction,
				PathDereferenceMode.NoFollow
			);
			Assert.True( observation.IsJunction );
			Assert.True( File.Exists( System.IO.Path.Combine( target, "sentinel" ) ) );
		} finally {
			RemovePhysicalReparsePoint( sourceJunction );
			RemovePhysicalReparsePoint( System.IO.Path.Combine( destination, "junction" ) );
			DeleteTree( root );
		}
	}

	/// <summary>Verifies <c>--sparse=always</c> uses the E5 allocation-map path and retains holes.</summary>
	[Fact]
	public async Task SparseAlwaysPreservesReportedHoles() {
		var operations = SystemFileSystemOperations.Instance;
		if ( !operations.Capabilities.SupportsAllocatedRangeQuery || !operations.Capabilities.SupportsSparseExtension ) return;
		var root = CreateTemporaryDirectory();
		try {
			const long length = 16L * 1024L * 1024L;
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "destination" );
			await using ( var stream = new FileStream(
				source,
				FileMode.CreateNew,
				FileAccess.ReadWrite,
				FileShare.None,
				4096,
				FileOptions.Asynchronous
			) ) {
				var extension = await operations.ExtendSparseAsync( stream, length );
				if ( !extension.Succeeded ) return;
				stream.Position = 0;
				await stream.WriteAsync( new byte[] { 0x41 } );
				stream.Position = length - 1;
				await stream.WriteAsync( new byte[] { 0x5a } );
				await stream.FlushAsync();
				var allocation = await operations.GetAllocatedRangesAsync( stream );
				if ( !allocation.Succeeded || allocation.Value?.IsSparse != true ) return;
			}

			var status = await RunAsync( new[] { "--sparse=always", "--reflink=never", source, destination } );
			Assert.Equal( 0, status );
			Assert.Equal( length, new FileInfo( destination ).Length );
			await using var copied = new FileStream( destination, FileMode.Open, FileAccess.Read, FileShare.Read );
			var destinationAllocation = await operations.GetAllocatedRangesAsync( copied );
			Assert.True( destinationAllocation.Succeeded, destinationAllocation.Message );
			Assert.NotNull( destinationAllocation.Value );
			Assert.True( destinationAllocation.Value!.IsSparse );
			copied.Position = 0;
			Assert.Equal( 0x41, copied.ReadByte() );
			copied.Position = length - 1;
			Assert.Equal( 0x5a, copied.ReadByte() );
		} finally {
			DeleteTree( root );
		}
	}

	private static ValueTask<int> RunAsync( string[] args ) => CpCommand.RunAsync(
		args,
		TextReader.Null,
		new StringWriter(),
		new StringWriter()
	);

	private static async ValueTask<bool> TryCreateDirectorySymbolicLinkAsync( string path, string target ) {
		var result = await SystemFileSystemMutationProvider.Instance.CreateSymbolicLinkAsync(
			path,
			target,
			targetIsDirectory: true,
			precondition: FileSystemMutationPrecondition.DestinationMustNotExist()
		);
		return result.Succeeded;
	}

	private static async ValueTask<bool> TryCreateJunctionAsync( string path, string target ) {
		var result = await SystemFileSystemMutationProvider.Instance.CreateJunctionAsync(
			path,
			target,
			FileSystemMutationPrecondition.DestinationMustNotExist()
		);
		return result.Succeeded;
	}

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-Cp-Reparse-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( path );
		return path;
	}

	private static void RemovePhysicalReparsePoint( string path ) {
		try {
			var attributes = File.GetAttributes( path );
			if ( (attributes & FileAttributes.ReparsePoint) == 0 ) return;
			if ( (attributes & FileAttributes.Directory) != 0 ) Directory.Delete( path, recursive: false );
			else File.Delete( path );
		} catch ( FileNotFoundException ) {
		} catch ( DirectoryNotFoundException ) {
		} catch ( IOException ) {
		} catch ( UnauthorizedAccessException ) {
		}
	}

	private static void DeleteTree( string path ) {
		try {
			if ( Directory.Exists( path ) ) Directory.Delete( path, recursive: true );
		} catch ( IOException ) {
		} catch ( UnauthorizedAccessException ) {
		}
	}
}
