namespace Icod.CoreUtils.Cp.Tests;

using CpCommand = Icod.CoreUtils.Cp.Command;
using Xunit;

/// <summary>Exercises Batch 44 <c>cp</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies ordinary files are copied through the shared engine.</summary>
	[Fact]
	public async Task CopiesOrdinaryFile() {
		var root = CreateTemporaryDirectory();
		try {
			var source = Path.Combine( root, "source" );
			var destination = Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "content" );
			var status = await RunAsync( new[] { source, destination } );
			Assert.Equal( 0, status );
			Assert.Equal( "content", await File.ReadAllTextAsync( destination ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies no-clobber retains an existing destination.</summary>
	[Fact]
	public async Task NoClobberRetainsDestination() {
		var root = CreateTemporaryDirectory();
		try {
			var source = Path.Combine( root, "source" );
			var destination = Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "new" );
			await File.WriteAllTextAsync( destination, "old" );
			var status = await RunAsync( new[] { "--no-clobber", source, destination } );
			Assert.Equal( 0, status );
			Assert.Equal( "old", await File.ReadAllTextAsync( destination ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies simple backups retain the former destination.</summary>
	[Fact]
	public async Task SimpleBackupRetainsFormerDestination() {
		var root = CreateTemporaryDirectory();
		try {
			var source = Path.Combine( root, "source" );
			var destination = Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "new" );
			await File.WriteAllTextAsync( destination, "old" );
			var status = await RunAsync( new[] { "--backup=simple", source, destination } );
			Assert.Equal( 0, status );
			Assert.Equal( "new", await File.ReadAllTextAsync( destination ) );
			Assert.Equal( "old", await File.ReadAllTextAsync( string.Concat( destination, "~" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies recursive copies retain nested content.</summary>
	[Fact]
	public async Task RecursivelyCopiesDirectory() {
		var root = CreateTemporaryDirectory();
		try {
			var source = Directory.CreateDirectory( Path.Combine( root, "source" ) ).FullName;
			var nested = Directory.CreateDirectory( Path.Combine( source, "nested" ) ).FullName;
			await File.WriteAllTextAsync( Path.Combine( nested, "file" ), "content" );
			var destination = Path.Combine( root, "destination" );
			var status = await RunAsync( new[] { "--recursive", source, destination } );
			Assert.Equal( 0, status );
			Assert.Equal( "content", await File.ReadAllTextAsync( Path.Combine( destination, "nested", "file" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies E5 rejects a destination inside the source tree.</summary>
	[Fact]
	public async Task RejectsDestinationInsideSource() {
		var root = CreateTemporaryDirectory();
		try {
			var source = Directory.CreateDirectory( Path.Combine( root, "source" ) ).FullName;
			await File.WriteAllTextAsync( Path.Combine( source, "file" ), "content" );
			var destination = Path.Combine( source, "copy" );
			var error = new StringWriter();
			var status = await CpCommand.RunAsync( new[] { "--recursive", source, destination }, TextReader.Null, new StringWriter(), error );
			Assert.Equal( 1, status );
			Assert.Contains( "inside", error.ToString(), StringComparison.OrdinalIgnoreCase );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies multiple sources require an existing destination directory.</summary>
	[Fact]
	public async Task MultipleSourcesRequireDirectory() {
		var root = CreateTemporaryDirectory();
		try {
			var first = Path.Combine( root, "first" );
			var second = Path.Combine( root, "second" );
			var destination = Path.Combine( root, "missing" );
			await File.WriteAllTextAsync( first, "one" );
			await File.WriteAllTextAsync( second, "two" );
			var status = await RunAsync( new[] { first, second, destination } );
			Assert.Equal( 1, status );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies interactive replacement prompts exactly once.</summary>
	[Fact]
	public async Task InteractiveReplacementPromptsOnce() {
		var root = CreateTemporaryDirectory();
		try {
			var source = Path.Combine( root, "source" );
			var destination = Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "new" );
			await File.WriteAllTextAsync( destination, "old" );
			var error = new StringWriter();
			var status = await CpCommand.RunAsync(
				new[] { "--interactive", source, destination },
				new StringReader( "y" + Environment.NewLine ),
				new StringWriter(),
				error
			);
			Assert.Equal( 0, status );
			Assert.Equal( 1, error.ToString().Split( "overwrite", StringSplitOptions.None ).Length - 1 );
			Assert.Equal( "new", await File.ReadAllTextAsync( destination ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies update mode retains a newer destination.</summary>
	[Fact]
	public async Task UpdateRetainsNewerDestination() {
		var root = CreateTemporaryDirectory();
		try {
			var source = Path.Combine( root, "source" );
			var destination = Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "old-source" );
			await File.WriteAllTextAsync( destination, "new-destination" );
			File.SetLastWriteTimeUtc( source, new DateTime( 2020, 1, 1, 0, 0, 0, DateTimeKind.Utc ) );
			File.SetLastWriteTimeUtc( destination, new DateTime( 2021, 1, 1, 0, 0, 0, DateTimeKind.Utc ) );
			var status = await RunAsync( new[] { "--update", source, destination } );
			Assert.Equal( 0, status );
			Assert.Equal( "new-destination", await File.ReadAllTextAsync( destination ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies a source cannot be copied onto the same filesystem entry.</summary>
	[Fact]
	public async Task RejectsSameSourceAndDestination() {
		var root = CreateTemporaryDirectory();
		try {
			var path = Path.Combine( root, "file" );
			await File.WriteAllTextAsync( path, "content" );
			Assert.Equal( 1, await RunAsync( new[] { path, path } ) );
			Assert.Equal( "content", await File.ReadAllTextAsync( path ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies one failed source does not prevent a later independent source from being copied.</summary>
	[Fact]
	public async Task ContinuesAfterIndependentSourceFailure() {
		var root = CreateTemporaryDirectory();
		try {
			var missing = Path.Combine( root, "missing" );
			var source = Path.Combine( root, "source" );
			var destination = Directory.CreateDirectory( Path.Combine( root, "destination" ) ).FullName;
			await File.WriteAllTextAsync( source, "content" );
			var status = await RunAsync( new[] { missing, source, destination } );
			Assert.Equal( 1, status );
			Assert.Equal( "content", await File.ReadAllTextAsync( Path.Combine( destination, "source" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies help and invalid-option exit statuses.</summary>
	[Fact]
	public async Task ReportsHelpAndUsageErrors() {
		var output = new StringWriter();
		Assert.Equal( 0, await CpCommand.RunAsync( new[] { "--help" }, TextReader.Null, output, new StringWriter() ) );
		Assert.Contains( "Usage: cp", output.ToString() );
		Assert.Equal( 2, await RunAsync( new[] { "--not-an-option" } ) );
	}

	private static ValueTask<int> RunAsync( string[] args ) => CpCommand.RunAsync(
		args,
		TextReader.Null,
		new StringWriter(),
		new StringWriter()
	);

	private static string CreateTemporaryDirectory() {
		var path = Path.Combine( Path.GetTempPath(), string.Concat( "Icod-Cp-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( path );
		return path;
	}

	private static void DeleteTree( string path ) {
		try {
			if ( Directory.Exists( path ) ) Directory.Delete( path, recursive: true );
		} catch ( IOException ) {
		} catch ( UnauthorizedAccessException ) {
		}
	}
}
