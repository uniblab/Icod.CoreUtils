extern alias IcodPath;

using Icod.CoreUtils.Shared.FileSystem.Traversal;
using IPathIndirectionInspector = IcodPath::Icod.Path.IPathIndirectionInspector;
using PathIndirectionInfo = IcodPath::Icod.Path.PathIndirectionInfo;
using WindowsReparseTags = IcodPath::Icod.Path.WindowsReparseTags;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Traversal;

/// <summary>Exercises platform-neutral provider policy over injected reparse-point characterizations.</summary>
public sealed class ReparsePointClassificationTests {
	/// <summary>Verifies a recognized non-name-surrogate reparse point retains its underlying file kind.</summary>
	[Fact]
	public async Task PreservesUnderlyingKindForCloudPlaceholder() {
		var path = CreateTemporaryFile();
		try {
			var provider = new SystemReadOnlyFileSystemProvider(
				new FixedIndirectionInspector(
					PathIndirectionInfo.WindowsReparsePoint(
						WindowsReparseTags.Cloud,
						false,
						attributes: FileAttributes.ReparsePoint
					)
				)
			);

			var observation = await provider.ObserveAsync( path, PathDereferenceMode.NoFollow );

			Assert.Equal( FileSystemEntryKind.File, observation.Kind );
			Assert.True( observation.IsCloudPlaceholder );
			Assert.True( observation.IsReparsePoint );
			Assert.False( observation.IsPathIndirection );
			Assert.False( observation.WasDereferenced );
		} finally {
			File.Delete( path );
		}
	}

	/// <summary>Verifies an uncharacterized reparse point is not treated as an ordinary directory.</summary>
	[Fact]
	public async Task QuarantinesUncharacterizedDirectoryReparsePoint() {
		var path = CreateTemporaryDirectory();
		try {
			var provider = new SystemReadOnlyFileSystemProvider(
				new FixedIndirectionInspector(
					PathIndirectionInfo.Unknown(
						attributes: FileAttributes.Directory | FileAttributes.ReparsePoint
					)
				)
			);

			var observation = await provider.ObserveAsync( path, PathDereferenceMode.NoFollow );

			Assert.Equal( FileSystemEntryKind.ReparsePoint, observation.Kind );
			Assert.True( observation.IsReparsePoint );
			Assert.False( observation.IsPathIndirection );
		} finally {
			Directory.Delete( path );
		}
	}

	private static string CreateTemporaryFile() {
		var path = Path.Combine(
			Path.GetTempPath(),
			string.Concat( "icod-reparse-file-", Guid.NewGuid().ToString( "N" ) )
		);
		using ( File.Create( path ) ) {
		}
		return path;
	}

	private static string CreateTemporaryDirectory() {
		var path = Path.Combine(
			Path.GetTempPath(),
			string.Concat( "icod-reparse-directory-", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( path );
		return path;
	}

	private sealed class FixedIndirectionInspector : IPathIndirectionInspector {
		private readonly PathIndirectionInfo information;

		/// <summary>Initializes an inspector that always returns one characterization.</summary>
		/// <param name="information">The characterization to return.</param>
		public FixedIndirectionInspector( PathIndirectionInfo information ) {
			this.information = information;
		}

		/// <inheritdoc/>
		public ValueTask<PathIndirectionInfo> InspectAsync(
			string path,
			CancellationToken cancellationToken = default
		) {
			ArgumentException.ThrowIfNullOrEmpty( path );
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( this.information );
		}
	}
}
