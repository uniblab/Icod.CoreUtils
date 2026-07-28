namespace Icod.CoreUtils.Shared.Tests;

using Icod.CoreUtils.Shared.Temporary;
using Xunit;

public sealed class TemporaryObjectTests {
	[Fact]
	public void TemplateUsesOnlyFinalConsecutiveRun() {
		Assert.True( TemporaryNameTemplate.TryParse(
			"prefix.XX.middle.XXXX.suffix",
			null,
			out var template,
			out var error
		) );
		Assert.Null( error );
		Assert.Equal( 4, template!.ReplacementLength );
		Assert.Equal(
			"prefix.XX.middle.abcd.suffix",
			template.Render( "abcd" )
		);
	}

	[Fact]
	public void ExplicitSuffixBeginningWithXIsNotPartOfReplacementRun() {
		Assert.True( TemporaryNameTemplate.TryParse(
			"name.XXXX",
			"XX.txt",
			out var template,
			out _
		) );
		Assert.Equal( 4, template!.ReplacementLength );
		Assert.Equal( "name.abcdXX.txt", template.Render( "abcd" ) );
	}

	[Fact]
	public void ExplicitSuffixRequiresTemplateToEndInX() {
		Assert.False( TemporaryNameTemplate.TryParse(
			"name.XXXX.txt",
			".bak",
			out _,
			out var error
		) );
		Assert.Contains( "must end in X", error ?? string.Empty );
	}

	[Fact]
	public void TemplateRequiresAtLeastThreeConsecutiveCharacters() {
		Assert.False( TemporaryNameTemplate.TryParse(
			"name.XX",
			null,
			out _,
			out var error
		) );
		Assert.Contains( "too few X's", error ?? string.Empty );
	}

	[Fact]
	public void CombinedTemplateRetainsReplacementCoordinates() {
		Assert.True( TemporaryNameTemplate.TryParse(
			Path.Combine( "nested", "name.XXXX.txt" ),
			null,
			out var template,
			out _
		) );
		var combined = template!.WithDirectory( "root" );
		Assert.Equal(
			Path.Combine( "root", "nested", "name.abcd.txt" ),
			combined.Render( "abcd" )
		);
	}

	[Fact]
	public void CreatorRetriesOnlyCollisions() {
		Assert.True( TemporaryNameTemplate.TryParse(
			"name.XXX",
			null,
			out var template,
			out _
		) );
		var fileSystem = new ScriptedFileSystem(
			TemporaryObjectAttemptResult.Collided(),
			TemporaryObjectAttemptResult.Succeeded()
		);
		var creator = new SecureTemporaryObjectCreator(
			fileSystem,
			new SequenceRandomSource( 0, 0, 0, 1, 1, 1 ),
			maximumAttempts: 3
		);
		var result = creator.Create( template!, TemporaryObjectKind.File );
		Assert.True( result.IsSuccess );
		Assert.Equal( 2, result.Attempts );
		Assert.Equal( new[] { "name.aaa", "name.bbb" }, fileSystem.Paths );
	}

	[Fact]
	public void CreatorStopsOnNonCollisionFailure() {
		Assert.True( TemporaryNameTemplate.TryParse(
			"name.XXX",
			null,
			out var template,
			out _
		) );
		var fileSystem = new ScriptedFileSystem(
			TemporaryObjectAttemptResult.Failed( "denied" ),
			TemporaryObjectAttemptResult.Succeeded()
		);
		var creator = new SecureTemporaryObjectCreator(
			fileSystem,
			new SequenceRandomSource( 0, 0, 0, 1, 1, 1 ),
			maximumAttempts: 3
		);
		var result = creator.Create( template!, TemporaryObjectKind.File );
		Assert.False( result.IsSuccess );
		Assert.Equal( "denied", result.ErrorMessage ?? string.Empty );
		Assert.Single( fileSystem.Paths );
	}

	[Fact]
	public void CreatorReportsCollisionExhaustion() {
		Assert.True( TemporaryNameTemplate.TryParse(
			"name.XXX",
			null,
			out var template,
			out _
		) );
		var fileSystem = new AlwaysCollidingFileSystem();
		var creator = new SecureTemporaryObjectCreator(
			fileSystem,
			new ConstantRandomSource(),
			maximumAttempts: 2
		);
		var result = creator.Create( template!, TemporaryObjectKind.Directory );
		Assert.False( result.IsSuccess );
		Assert.Equal( 2, result.Attempts );
		Assert.Contains( "2 attempts", result.ErrorMessage ?? string.Empty );
	}

	[Fact]
	public void CreatorHonorsCancellationBeforeFilesystemAttempt() {
		Assert.True( TemporaryNameTemplate.TryParse(
			"name.XXX",
			null,
			out var template,
			out _
		) );
		using var source = new CancellationTokenSource();
		source.Cancel();
		var fileSystem = new AlwaysCollidingFileSystem();
		var creator = new SecureTemporaryObjectCreator(
			fileSystem,
			new ConstantRandomSource()
		);
		Assert.Throws<OperationCanceledException>(
			() => creator.Create( template!, TemporaryObjectKind.File, source.Token )
		);
		Assert.Equal( 0, fileSystem.Attempts );
	}

	[Fact]
	public void CreatorHonorsCancellationAfterNameGenerationBeforeFilesystemAttempt() {
		Assert.True( TemporaryNameTemplate.TryParse(
			"name.XXX",
			null,
			out var template,
			out _
		) );
		using var source = new CancellationTokenSource();
		var fileSystem = new AlwaysCollidingFileSystem();
		var creator = new SecureTemporaryObjectCreator(
			fileSystem,
			new CancelingRandomSource( source )
		);
		Assert.Throws<OperationCanceledException>(
			() => creator.Create( template!, TemporaryObjectKind.File, source.Token )
		);
		Assert.Equal( 0, fileSystem.Attempts );
	}

	[Fact]
	public void GeneratedCharactersUseOnlyTheGnuBase62Alphabet() {
		Assert.True( TemporaryNameTemplate.TryParse(
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			null,
			out var template,
			out _
		) );
		var fileSystem = new ScriptedFileSystem(
			TemporaryObjectAttemptResult.Succeeded()
		);
		var random = new SequenceRandomSource( Enumerable.Range( 0, 62 ).ToArray() );
		var creator = new SecureTemporaryObjectCreator( fileSystem, random );
		var result = creator.Create( template!, TemporaryObjectKind.NameOnly );
		Assert.Equal(
			"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789",
			result.Path ?? string.Empty
		);
	}

	[Fact]
	public void SystemProviderCreatesFileExclusively() {
		using var workspace = new Workspace();
		var path = Path.Combine( workspace.Root, "file" );
		var first = SystemTemporaryObjectFileSystem.Instance.TryCreate(
			path,
			TemporaryObjectKind.File
		);
		var second = SystemTemporaryObjectFileSystem.Instance.TryCreate(
			path,
			TemporaryObjectKind.File
		);
		Assert.Equal( TemporaryObjectAttemptStatus.Success, first.Status );
		Assert.Equal( TemporaryObjectAttemptStatus.Collision, second.Status );
	}

	[Fact]
	public void NameOnlyTreatsAnExclusivelyOpenedFileAsACollision() {
		using var workspace = new Workspace();
		var path = Path.Combine( workspace.Root, "locked" );
		using var stream = new FileStream(
			path,
			FileMode.CreateNew,
			FileAccess.ReadWrite,
			FileShare.None
		);
		var result = SystemTemporaryObjectFileSystem.Instance.TryCreate(
			path,
			TemporaryObjectKind.NameOnly
		);
		Assert.Equal( TemporaryObjectAttemptStatus.Collision, result.Status );
	}

	[Fact]
	public void SystemProviderCreatesDirectoryExclusively() {
		using var workspace = new Workspace();
		var path = Path.Combine( workspace.Root, "directory" );
		var first = SystemTemporaryObjectFileSystem.Instance.TryCreate(
			path,
			TemporaryObjectKind.Directory
		);
		var second = SystemTemporaryObjectFileSystem.Instance.TryCreate(
			path,
			TemporaryObjectKind.Directory
		);
		Assert.Equal( TemporaryObjectAttemptStatus.Success, first.Status );
		Assert.Equal( TemporaryObjectAttemptStatus.Collision, second.Status );
	}

	[Fact]
	public void NameOnlyTreatsDanglingSymbolicLinkAsCollision() {
		using var workspace = new Workspace();
		var path = Path.Combine( workspace.Root, "link" );
		try {
			File.CreateSymbolicLink( path, Path.Combine( workspace.Root, "missing" ) );
		} catch ( Exception exception ) when (
			exception is UnauthorizedAccessException
				or IOException
				or NotSupportedException
		) {
			return;
		}
		var result = SystemTemporaryObjectFileSystem.Instance.TryCreate(
			path,
			TemporaryObjectKind.NameOnly
		);
		Assert.Equal( TemporaryObjectAttemptStatus.Collision, result.Status );
	}

	private sealed class SequenceRandomSource : ISecureRandomSource {
		private readonly Queue<int> values;

		public SequenceRandomSource( params int[] values ) {
			this.values = new Queue<int>( values );
		}

		public int GetInt32( int exclusiveUpperBound ) {
			Assert.NotEmpty( values );
			var value = values.Dequeue();
			Assert.InRange( value, 0, exclusiveUpperBound - 1 );
			return value;
		}
	}

	private sealed class CancelingRandomSource : ISecureRandomSource {
		private readonly CancellationTokenSource source;

		public CancelingRandomSource( CancellationTokenSource source ) {
			this.source = source;
		}

		public int GetInt32( int exclusiveUpperBound ) {
			source.Cancel();
			return 0;
		}
	}

	private sealed class ConstantRandomSource : ISecureRandomSource {
		public int GetInt32( int exclusiveUpperBound ) {
			return 0;
		}
	}

	private sealed class ScriptedFileSystem : ITemporaryObjectFileSystem {
		private readonly Queue<TemporaryObjectAttemptResult> results;

		public ScriptedFileSystem( params TemporaryObjectAttemptResult[] results ) {
			this.results = new Queue<TemporaryObjectAttemptResult>( results );
		}

		public List<string> Paths { get; } = new();

		public TemporaryObjectAttemptResult TryCreate(
			string path,
			TemporaryObjectKind kind
		) {
			Paths.Add( path );
			return results.Dequeue();
		}

		public bool TryDelete(
			string path,
			TemporaryObjectKind kind,
			out string? errorMessage
		) {
			errorMessage = null;
			return true;
		}
	}

	private sealed class AlwaysCollidingFileSystem : ITemporaryObjectFileSystem {
		public int Attempts { get; private set; }

		public TemporaryObjectAttemptResult TryCreate(
			string path,
			TemporaryObjectKind kind
		) {
			Attempts++;
			return TemporaryObjectAttemptResult.Collided();
		}

		public bool TryDelete(
			string path,
			TemporaryObjectKind kind,
			out string? errorMessage
		) {
			errorMessage = null;
			return true;
		}
	}

	private sealed class Workspace : IDisposable {
		public Workspace() {
			Root = Path.Combine(
				Path.GetTempPath(),
				string.Concat( "Icod.CoreUtils.Shared.Temporary.Tests-", Guid.NewGuid().ToString( "N" ) )
			);
			Directory.CreateDirectory( Root );
		}

		public string Root { get; }

		public void Dispose() {
			try {
				Directory.Delete( Root, recursive: true );
			} catch ( IOException ) {
			} catch ( UnauthorizedAccessException ) {
			}
		}
	}
}
