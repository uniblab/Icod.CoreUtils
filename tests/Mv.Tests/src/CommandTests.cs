namespace Icod.CoreUtils.Mv.Tests;

using MvCommand = Icod.CoreUtils.Mv.Command;
using Xunit;

/// <summary>Exercises Batch 44 <c>mv</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies ordinary files are renamed.</summary>
	[Fact]
	public async Task MovesOrdinaryFile() {
		var root = CreateTemporaryDirectory();
		try {
			var source = Path.Combine( root, "source" );
			var destination = Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "content" );
			var status = await RunAsync( new[] { source, destination } );
			Assert.Equal( 0, status );
			Assert.False( File.Exists( source ) );
			Assert.Equal( "content", await File.ReadAllTextAsync( destination ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies no-clobber retains both source and destination.</summary>
	[Fact]
	public async Task NoClobberRetainsBothFiles() {
		var root = CreateTemporaryDirectory();
		try {
			var source = Path.Combine( root, "source" );
			var destination = Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "new" );
			await File.WriteAllTextAsync( destination, "old" );
			var status = await RunAsync( new[] { "--no-clobber", source, destination } );
			Assert.Equal( 0, status );
			Assert.True( File.Exists( source ) );
			Assert.Equal( "old", await File.ReadAllTextAsync( destination ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies backup mode retains the former destination before source removal.</summary>
	[Fact]
	public async Task BackupRetainsFormerDestination() {
		var root = CreateTemporaryDirectory();
		try {
			var source = Path.Combine( root, "source" );
			var destination = Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "new" );
			await File.WriteAllTextAsync( destination, "old" );
			var status = await RunAsync( new[] { "--backup=simple", source, destination } );
			Assert.Equal( 0, status );
			Assert.False( File.Exists( source ) );
			Assert.Equal( "new", await File.ReadAllTextAsync( destination ) );
			Assert.Equal( "old", await File.ReadAllTextAsync( string.Concat( destination, "~" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies directories can be moved as one rename unit.</summary>
	[Fact]
	public async Task MovesDirectory() {
		var root = CreateTemporaryDirectory();
		try {
			var source = Directory.CreateDirectory( Path.Combine( root, "source" ) ).FullName;
			await File.WriteAllTextAsync( Path.Combine( source, "file" ), "content" );
			var destination = Path.Combine( root, "destination" );
			var status = await RunAsync( new[] { source, destination } );
			Assert.Equal( 0, status );
			Assert.False( Directory.Exists( source ) );
			Assert.Equal( "content", await File.ReadAllTextAsync( Path.Combine( destination, "file" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies target-directory mode moves multiple sources.</summary>
	[Fact]
	public async Task TargetDirectoryMovesMultipleSources() {
		var root = CreateTemporaryDirectory();
		try {
			var first = Path.Combine( root, "first" );
			var second = Path.Combine( root, "second" );
			var destination = Directory.CreateDirectory( Path.Combine( root, "destination" ) ).FullName;
			await File.WriteAllTextAsync( first, "one" );
			await File.WriteAllTextAsync( second, "two" );
			var status = await RunAsync( new[] { "--target-directory", destination, first, second } );
			Assert.Equal( 0, status );
			Assert.Equal( "one", await File.ReadAllTextAsync( Path.Combine( destination, "first" ) ) );
			Assert.Equal( "two", await File.ReadAllTextAsync( Path.Combine( destination, "second" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies a source cannot be moved onto the same filesystem entry.</summary>
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

	/// <summary>Verifies one failed source does not prevent a later independent source from being moved.</summary>
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
			Assert.False( File.Exists( source ) );
			Assert.Equal( "content", await File.ReadAllTextAsync( Path.Combine( destination, "source" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies help and invalid-option exit statuses.</summary>
	[Fact]
	public async Task ReportsHelpAndUsageErrors() {
		var output = new StringWriter();
		Assert.Equal( 0, await MvCommand.RunAsync( new[] { "--help" }, TextReader.Null, output, new StringWriter() ) );
		Assert.Contains( "Usage: mv", output.ToString() );
		Assert.Equal( 2, await RunAsync( new[] { "--not-an-option" } ) );
	}

	private static ValueTask<int> RunAsync( string[] args ) => MvCommand.RunAsync(
		args,
		TextReader.Null,
		new StringWriter(),
		new StringWriter()
	);

	private static string CreateTemporaryDirectory() {
		var path = Path.Combine( Path.GetTempPath(), string.Concat( "Icod-Mv-", Guid.NewGuid().ToString( "N" ) ) );
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
