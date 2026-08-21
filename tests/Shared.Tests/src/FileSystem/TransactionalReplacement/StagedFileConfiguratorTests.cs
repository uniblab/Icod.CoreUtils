namespace Icod.CoreUtils.Shared.Tests.FileSystem.TransactionalReplacement;

using System.Text;
using Icod.CommandFramework.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;
using Xunit;

/// <summary>Exercises the Batch 45 pre-publication staged-file configuration contract.</summary>
public sealed class StagedFileConfiguratorTests {
	/// <summary>Verifies configuration runs against the private stage before it is published.</summary>
	[Fact]
	public async Task ConfiguresPrivateStageBeforePublication() {
		var root = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-E6-Configure-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( root );
		try {
			var destination = System.IO.Path.Combine( root, "destination" );
			var artifact = new TransactionalReplacementArtifact(
				"unit",
				destination,
				TransactionalReplacementAction.Replace,
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				async (stream, token) => {
					await stream.WriteAsync( Encoding.UTF8.GetBytes( "content" ), token );
				},
				stagedFileConfigurator: async (path, token) => {
					await File.AppendAllTextAsync( path, "-configured", token );
				}
			);
			await using var transaction = new TransactionalFileReplacementTransaction(
				new[] { artifact },
				SystemTransactionalReplacementFileSystem.Instance,
				new TransactionalReplacementOptions { RequireStagedDurability = false }
			);
			await transaction.StageAsync();
			Assert.False( File.Exists( destination ) );
			var result = await transaction.CommitAsync();
			Assert.True( result.Succeeded );
			Assert.Equal( "content-configured", await File.ReadAllTextAsync( destination ) );
		} finally {
			try { Directory.Delete( root, recursive: true ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
		}
	}

	/// <summary>Verifies a configurator failure removes the private stage and publishes nothing.</summary>
	[Fact]
	public async Task ConfiguratorFailureCleansStageBeforePublication() {
		var root = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-E6-Configure-Fail-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( root );
		try {
			var destination = System.IO.Path.Combine( root, "destination" );
			var artifact = new TransactionalReplacementArtifact(
				"unit",
				destination,
				TransactionalReplacementAction.Replace,
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				async (stream, token) => {
					await stream.WriteAsync( Encoding.UTF8.GetBytes( "content" ), token );
				},
				stagedFileConfigurator: static (_, _) => throw new IOException( "configuration failed" )
			);
			await using var transaction = new TransactionalFileReplacementTransaction(
				new[] { artifact },
				SystemTransactionalReplacementFileSystem.Instance,
				new TransactionalReplacementOptions { RequireStagedDurability = false }
			);
			await Assert.ThrowsAsync<IOException>( async () => await transaction.StageAsync() );
			Assert.False( File.Exists( destination ) );
			Assert.Empty( Directory.EnumerateFileSystemEntries( root ) );
		} finally {
			try { Directory.Delete( root, recursive: true ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
		}
	}

	/// <summary>Verifies non-replacement artifacts cannot supply a staged-file configurator.</summary>
	[Fact]
	public void RejectsConfiguratorForNonReplacementArtifact() {
		Assert.Throws<ArgumentException>( () => new TransactionalReplacementArtifact(
			"unit",
			"destination",
			TransactionalReplacementAction.Delete,
			FileSystemMutationPrecondition.DestinationMustNotExist(),
			stagedFileConfigurator: static (_, _) => ValueTask.CompletedTask
		) );
	}
}
