namespace Icod.Patch.Tests;

using System.IO;
using System.Text;
using Xunit;

/// <summary>Exercises the provisional Phase P9 transaction and failure-injection boundary.</summary>
public sealed class WaveCTransactionTests {
	/// <summary>Verifies a post-replacement metadata failure restores the original file.</summary>
	[Fact]
	public async Task MetadataFailureRollsBackCommittedReplacement() {
		var directory = CreateTemporaryDirectory();
		var target = Path.Combine( directory, "target.txt" );
		await File.WriteAllTextAsync( target, "old\n" );
		try {
			var fileSystem = new SystemPatchFileSystem();
			var plan = await CreateWritePlanAsync( fileSystem, target, "new\n" );
			await using var transaction = await fileSystem.CreateTransactionAsync(
				plan,
				new ThrowingFailureInjector( PatchTransactionStage.ApplyMetadata )
			);
			await transaction.StageAsync();
			var result = await transaction.CommitAsync();
			Assert.False( result.Succeeded );
			Assert.Equal( "old\n", await File.ReadAllTextAsync( target ) );
			AssertNoTemporaryFiles( directory );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies a later artifact failure rolls back earlier committed artifacts.</summary>
	[Fact]
	public async Task LaterCommitFailureRollsBackEarlierArtifact() {
		var directory = CreateTemporaryDirectory();
		var first = Path.Combine( directory, "first.txt" );
		var second = Path.Combine( directory, "second.txt" );
		await File.WriteAllTextAsync( first, "first-old\n" );
		await File.WriteAllTextAsync( second, "second-old\n" );
		try {
			var fileSystem = new SystemPatchFileSystem();
			var firstObservation = await fileSystem.ObserveAsync( first, followPathIndirection: false );
			var secondObservation = await fileSystem.ObserveAsync( second, followPathIndirection: false );
			var artifacts = new[] {
				CreateWriteArtifact( first, firstObservation, "first-new\n", "operation" ),
				CreateWriteArtifact( second, secondObservation, "second-new\n", "operation" )
			};
			var plan = new PatchArtifactPlan( artifacts, PatchExitStatus.Success, Array.Empty<string>() );
			await using var transaction = await fileSystem.CreateTransactionAsync(
				plan,
				new ThrowingFailureInjector( PatchTransactionStage.Commit, occurrence: 2 )
			);
			var result = await transaction.CommitAsync();
			Assert.False( result.Succeeded );
			Assert.Equal( "first-old\n", await File.ReadAllTextAsync( first ) );
			Assert.Equal( "second-old\n", await File.ReadAllTextAsync( second ) );
			AssertNoTemporaryFiles( directory );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies destination changes after staging are detected before replacement.</summary>
	[Fact]
	public async Task RevalidationRejectsDestinationChangedAfterStaging() {
		var directory = CreateTemporaryDirectory();
		var target = Path.Combine( directory, "target.txt" );
		await File.WriteAllTextAsync( target, "old\n" );
		try {
			var fileSystem = new SystemPatchFileSystem();
			var plan = await CreateWritePlanAsync( fileSystem, target, "new\n" );
			await using var transaction = await fileSystem.CreateTransactionAsync( plan );
			await transaction.StageAsync();
			await File.WriteAllTextAsync( target, "external-change-with-different-size\n" );
			var result = await transaction.CommitAsync();
			Assert.False( result.Succeeded );
			Assert.Contains( "changed after planning", string.Join( "\n", result.Diagnostics ) );
			Assert.Equal( "external-change-with-different-size\n", await File.ReadAllTextAsync( target ) );
			AssertNoTemporaryFiles( directory );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies a validation-only input guard prevents committing stale output-mode results.</summary>
	[Fact]
	public async Task ValidationOnlyGuardDetectsInputChange() {
		var directory = CreateTemporaryDirectory();
		var input = Path.Combine( directory, "input.txt" );
		var output = Path.Combine( directory, "output.txt" );
		await File.WriteAllTextAsync( input, "old\n" );
		try {
			var fileSystem = new SystemPatchFileSystem();
			var inputObservation = await fileSystem.ObserveAsync( input, followPathIndirection: false );
			var outputObservation = await fileSystem.ObserveAsync( output, followPathIndirection: false );
			var plan = new PatchArtifactPlan(
				new[] {
					new PatchArtifact(
						PatchArtifactKind.Target,
						PatchArtifactAction.ValidateOnly,
						input,
						null,
						inputObservation,
						new PatchArtifactMetadata(),
						input
					),
					CreateWriteArtifact( output, outputObservation, "new\n" )
				},
				PatchExitStatus.Success,
				Array.Empty<string>()
			);
			await using var transaction = await fileSystem.CreateTransactionAsync( plan );
			await transaction.StageAsync();
			await File.WriteAllTextAsync( input, "external-change-with-different-size\n" );
			var result = await transaction.CommitAsync();
			Assert.False( result.Succeeded );
			Assert.False( File.Exists( output ) );
			AssertNoTemporaryFiles( directory );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies cancellation after staging cleans temporary files and preserves the target.</summary>
	[Fact]
	public async Task CancellationCleansStagedFiles() {
		var directory = CreateTemporaryDirectory();
		var target = Path.Combine( directory, "target.txt" );
		await File.WriteAllTextAsync( target, "old\n" );
		using var cancellation = new CancellationTokenSource();
		try {
			var fileSystem = new SystemPatchFileSystem();
			var plan = await CreateWritePlanAsync( fileSystem, target, "new\n" );
			await using var transaction = await fileSystem.CreateTransactionAsync(
				plan,
				new CancelingFailureInjector( PatchTransactionStage.Commit, cancellation )
			);
			await transaction.StageAsync( cancellation.Token );
			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => transaction.CommitAsync( cancellation.Token )
			);
			Assert.Equal( "old\n", await File.ReadAllTextAsync( target ) );
			AssertNoTemporaryFiles( directory );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	private static async Task<PatchArtifactPlan> CreateWritePlanAsync(
		SystemPatchFileSystem fileSystem,
		string target,
		string value
	) {
		var observation = await fileSystem.ObserveAsync( target, followPathIndirection: false );
		return new PatchArtifactPlan(
			new[] { CreateWriteArtifact( target, observation, value ) },
			PatchExitStatus.Success,
			Array.Empty<string>()
		);
	}

	private static PatchArtifact CreateWriteArtifact(
		string target,
		PatchFileObservation observation,
		string value,
		string? transactionUnitId = null
	) {
		return new PatchArtifact(
			PatchArtifactKind.Target,
			PatchArtifactAction.Write,
			target,
			PatchArtifactContent.FromBytes( Encoding.UTF8.GetBytes( value ) ),
			observation,
			new PatchArtifactMetadata(),
			target,
			transactionUnitId
		);
	}

	private static string CreateTemporaryDirectory() {
		var path = Path.Combine(
			Path.GetTempPath(),
			string.Concat( "icod-patch-p9-", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( path );
		return path;
	}

	private static void AssertNoTemporaryFiles( string directory ) {
		Assert.DoesNotContain(
			Directory.EnumerateFiles( directory ),
			path => Path.GetFileName( path ).Contains( ".patch-", StringComparison.Ordinal )
		);
	}

	private sealed class ThrowingFailureInjector : IPatchTransactionFailureInjector {
		private readonly PatchTransactionStage selectedStage;
		private readonly int occurrence;
		private int count;

		/// <summary>Initializes a deterministic throwing injector.</summary>
		public ThrowingFailureInjector( PatchTransactionStage selectedStage, int occurrence = 1 ) {
			this.selectedStage = selectedStage;
			this.occurrence = occurrence;
		}

		/// <inheritdoc/>
		public ValueTask OnStageAsync(
			PatchTransactionStage stage,
			PatchArtifact artifact,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( this.selectedStage != stage ) {
				return ValueTask.CompletedTask;
			}
			this.count++;
			if ( this.occurrence == this.count ) {
				throw new IOException( string.Concat( "injected ", stage.ToString() ) );
			}
			return ValueTask.CompletedTask;
		}
	}

	private sealed class CancelingFailureInjector : IPatchTransactionFailureInjector {
		private readonly PatchTransactionStage selectedStage;
		private readonly CancellationTokenSource cancellation;

		/// <summary>Initializes a deterministic cancellation injector.</summary>
		public CancelingFailureInjector(
			PatchTransactionStage selectedStage,
			CancellationTokenSource cancellation
		) {
			this.selectedStage = selectedStage;
			this.cancellation = cancellation;
		}

		/// <inheritdoc/>
		public ValueTask OnStageAsync(
			PatchTransactionStage stage,
			PatchArtifact artifact,
			CancellationToken cancellationToken = default
		) {
			if ( this.selectedStage == stage ) {
				this.cancellation.Cancel();
			}
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}
}
