namespace Icod.CoreUtils.Rmdir.Tests;

using RmDirCommand = Icod.CoreUtils.Rmdir.Command;
using Xunit;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Mutation;

/// <summary>Exercises GNU-compatible <c>rmdir</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies ordered parent removal and the eventual failure at a nonempty absolute ancestor.</summary>
	[Fact]
	public async Task RemovesRequestedAbsoluteChainBeforeNonemptyAncestorFailure() {
		using var temporary = new TemporaryDirectory();
		var ancestor = System.IO.Path.Combine( temporary.Path, "ancestor" );
		var parent = System.IO.Path.Combine( ancestor, "parent" );
		var child = System.IO.Path.Combine( parent, "child" );
		Directory.CreateDirectory( child );
		File.WriteAllText( System.IO.Path.Combine( ancestor, "keep" ), "data" );
		Assert.True( System.IO.Path.IsPathFullyQualified( child ) );
		var output = new StringWriter();
		var error = new StringWriter();

		var status = await RmDirCommand.RunAsync(
			new[] { "--parents", "--verbose", child },
			CreateContext( output, error )
		);

		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.False( Directory.Exists( child ) );
		Assert.False( Directory.Exists( parent ) );
		Assert.True( Directory.Exists( ancestor ) );
		Assert.True( Directory.Exists( temporary.Path ) );
		var text = output.ToString();
		Assert.Contains( string.Concat( "rmdir: removing directory, '", child, "'" ), text );
		Assert.Contains( string.Concat( "rmdir: removing directory, '", parent, "'" ), text );
		Assert.Contains( string.Concat( "rmdir: removing directory, '", ancestor, "'" ), text );
		Assert.Contains( "Directory not empty", error.ToString() );
	}

	/// <summary>Verifies that only nonempty-directory failure is ignored.</summary>
	[Fact]
	public async Task IgnoreFailOnNonEmptyLeavesDirectoryAndReturnsSuccess() {
		using var temporary = new TemporaryDirectory();
		var directory = System.IO.Path.Combine( temporary.Path, "nonempty" );
		Directory.CreateDirectory( directory );
		File.WriteAllText( System.IO.Path.Combine( directory, "item" ), "data" );
		var error = new StringWriter();

		var status = await RmDirCommand.RunAsync(
			new[] { "--ignore-fail-on-non-empty", directory },
			CreateContext( new StringWriter(), error )
		);

		Assert.Equal( CommandExitCodes.Success, status );
		Assert.True( Directory.Exists( directory ) );
		Assert.Equal( string.Empty, error.ToString() );
	}

	/// <summary>Verifies that a directory symbolic link is not followed or removed by <c>rmdir</c>.</summary>
	[Fact]
	public async Task RefusesDirectorySymbolicLink() {
		using var temporary = new TemporaryDirectory();
		var target = System.IO.Path.Combine( temporary.Path, "target" );
		var link = System.IO.Path.Combine( temporary.Path, "link" );
		Directory.CreateDirectory( target );
		var creation = await SystemFileSystemMutationProvider.Instance.CreateSymbolicLinkAsync(
			link,
			target,
			targetIsDirectory: true
		);
		if ( !creation.Supported || !creation.Succeeded ) return;
		var error = new StringWriter();

		var status = await RmDirCommand.RunAsync(
			new[] { link },
			CreateContext( new StringWriter(), error )
		);

		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.True( Directory.Exists( target ) );
		Assert.True( Directory.Exists( link ) );
		Assert.Contains( "Not a directory", error.ToString() );
	}

	/// <summary>Verifies deterministic missing-operand behavior.</summary>
	[Fact]
	public async Task MissingOperandFails() {
		var error = new StringWriter();

		var status = await RmDirCommand.RunAsync(
			Array.Empty<string>(),
			CreateContext( new StringWriter(), error )
		);

		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.StartsWith( "rmdir: missing operand", error.ToString() );
	}

	private static CommandContext CreateContext( TextWriter output, TextWriter error ) {
		return new CommandContext( "rmdir", TextReader.Null, output, error );
	}

	private sealed class TemporaryDirectory : IDisposable {
		public TemporaryDirectory() {
			Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				string.Concat( "Icod-RmDir-", Guid.NewGuid().ToString( "N" ) )
			);
			Directory.CreateDirectory( Path );
		}

		public string Path { get; }

		public void Dispose() {
			try {
				Directory.Delete( Path, recursive: true );
			} catch ( IOException ) { }
			catch ( UnauthorizedAccessException ) { }
		}
	}
}
