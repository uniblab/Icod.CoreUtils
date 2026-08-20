namespace Icod.CoreUtils.Shared.Tests.FileSystem.Usage;

using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.FileSystem.Usage;
using Xunit;

/// <summary>Verifies shared disk-usage accounting over the system providers.</summary>
public sealed class DiskUsageCalculatorTests {
	/// <summary>Verifies apparent-size accounting for a literal file is exact.</summary>
	[Fact]
	public async Task ReportsExactApparentSizeForFile() {
		var path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "icod-du-file-", Guid.NewGuid().ToString( "N" ) ) );
		try {
			await File.WriteAllBytesAsync( path, new byte[ 73 ] );
			var calculator = new DiskUsageCalculator(
				SystemFileSystemMetadataProvider.Instance,
				SystemReadOnlyFileSystemProvider.Instance
			);

			var calculation = await calculator.CalculateAsync( path, new DiskUsageCalculationOptions { ApparentSize = true } );

			var entry = Assert.Single( calculation.Entries );
			Assert.Equal( 73UL, entry.Value );
			Assert.Equal( 0, entry.Depth );
			Assert.False( entry.IsDirectory );
			Assert.Empty( calculation.Diagnostics );
		} finally {
			File.Delete( path );
		}
	}


	/// <summary>Verifies apparent-size directory totals contain only regular files and undereferenced links.</summary>
	[Fact]
	public async Task ApparentDirectorySizeExcludesDirectoryMetadata() {
		var root = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "icod-du-apparent-tree-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( root );
		try {
			await File.WriteAllBytesAsync( System.IO.Path.Combine( root, "payload.bin" ), new byte[ 41 ] );
			var calculator = new DiskUsageCalculator(
				SystemFileSystemMetadataProvider.Instance,
				SystemReadOnlyFileSystemProvider.Instance
			);

			var calculation = await calculator.CalculateAsync(
				root,
				new DiskUsageCalculationOptions { ApparentSize = true }
			);

			var rootEntry = Assert.Single( calculation.Entries, entry => entry.Depth == 0 && entry.IsDirectory );
			Assert.Equal( 41UL, rootEntry.Value );
			Assert.Empty( calculation.Diagnostics );
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies exclusions suppress matching descendants and preserve a root result.</summary>
	[Fact]
	public async Task AppliesExclusionPatterns() {
		var root = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "icod-du-tree-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( root );
		try {
			await File.WriteAllTextAsync( System.IO.Path.Combine( root, "keep.txt" ), "keep" );
			await File.WriteAllTextAsync( System.IO.Path.Combine( root, "skip.tmp" ), "skip" );
			var calculator = new DiskUsageCalculator(
				SystemFileSystemMetadataProvider.Instance,
				SystemReadOnlyFileSystemProvider.Instance
			);

			var calculation = await calculator.CalculateAsync(
				root,
				new DiskUsageCalculationOptions { ApparentSize = true, ExcludePatterns = new[] { "*.tmp" } }
			);

			Assert.DoesNotContain( calculation.Entries, entry => entry.Path.EndsWith( "skip.tmp", StringComparison.Ordinal ) );
			Assert.Contains( calculation.Entries, entry => entry.Path.EndsWith( "keep.txt", StringComparison.Ordinal ) );
			Assert.Contains( calculation.Entries, entry => entry.Depth == 0 && entry.IsDirectory );
		} finally {
			Directory.Delete( root, true );
		}
	}
}
