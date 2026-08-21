namespace Icod.CoreUtils.Unlink.Tests;

using UnlinkCommand = Icod.CoreUtils.Unlink.Command;
using Xunit;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.FileSystem.Mutation;

/// <summary>Exercises GNU-compatible <c>unlink</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies ordinary-file removal.</summary>
	[Fact]
	public async Task RemovesOrdinaryFile() {
		using var temporary = new TemporaryDirectory();
		var file = System.IO.Path.Combine( temporary.Path, "file" );
		File.WriteAllText( file, "data" );

		var status = await UnlinkCommand.RunAsync(
			new[] { file },
			CreateContext( new StringWriter(), new StringWriter() )
		);

		Assert.Equal( CommandExitCodes.Success, status );
		Assert.False( File.Exists( file ) );
	}

	/// <summary>Verifies physical symbolic-link removal without touching the target.</summary>
	[Fact]
	public async Task RemovesSymbolicLinkWithoutRemovingTarget() {
		var target = System.IO.Path.Combine( temporary.Path, "target" );
		var link = System.IO.Path.Combine( temporary.Path, "link" );
		File.WriteAllText( target, "data" );
		var creation = await SystemFileSystemMutationProvider.Instance.CreateSymbolicLinkAsync(
			link,
			target,
			targetIsDirectory: false
		);
		if ( !creation.Supported || !creation.Succeeded ) return;

		var status = await UnlinkCommand.RunAsync(
			new[] { link },
			CreateContext( new StringWriter(), new StringWriter() )
		);

		Assert.Equal( CommandExitCodes.Success, status );
		Assert.True( File.Exists( target ) );
		Assert.False( File.Exists( link ) );
		Assert.False( Directory.Exists( link ) );
	}

	/// <summary>Verifies Windows directory-symlink reparse removal without traversing the target.</summary>
	[Fact]
	public async Task RemovesWindowsDirectorySymbolicLinkWithoutRemovingTarget() {
		if ( !OperatingSystem.IsWindows() ) return;
		var target = System.IO.Path.Combine( temporary.Path, "directory-target" );
		var link = System.IO.Path.Combine( temporary.Path, "directory-link" );
		Directory.CreateDirectory( target );
		File.WriteAllText( System.IO.Path.Combine( target, "item" ), "data" );
		var creation = await SystemFileSystemMutationProvider.Instance.CreateSymbolicLinkAsync(
			link,
			target,
			targetIsDirectory: true
		);
		if ( !creation.Supported || !creation.Succeeded ) return;

		var status = await UnlinkCommand.RunAsync(
			new[] { link },
			CreateContext( new StringWriter(), new StringWriter() )
		);

		Assert.Equal( CommandExitCodes.Success, status );
		Assert.True( Directory.Exists( target ) );
		Assert.True( File.Exists( System.IO.Path.Combine( target, "item" ) ) );
		Assert.False( Directory.Exists( link ) );
	}

	/// <summary>Verifies Windows junction removal without traversing or deleting its target.</summary>
	[Fact]
	public async Task RemovesWindowsJunctionWithoutRemovingTarget() {
		if ( !OperatingSystem.IsWindows() ) return;
		var target = System.IO.Path.Combine( temporary.Path, "target" );
		var junction = System.IO.Path.Combine( temporary.Path, "junction" );
		Directory.CreateDirectory( target );
		File.WriteAllText( System.IO.Path.Combine( target, "item" ), "data" );
		var creation = await SystemFileSystemMutationProvider.Instance.CreateJunctionAsync(
			junction,
			target
		);
		Assert.True( creation.Supported );
		Assert.True( creation.Succeeded, creation.Message );

		var status = await UnlinkCommand.RunAsync(
			new[] { junction },
			CreateContext( new StringWriter(), new StringWriter() )
		);

		Assert.Equal( CommandExitCodes.Success, status );
		Assert.True( Directory.Exists( target ) );
		Assert.True( File.Exists( System.IO.Path.Combine( target, "item" ) ) );
		Assert.False( Directory.Exists( junction ) );
	}

	/// <summary>Verifies that an ordinary directory is rejected.</summary>
	[Fact]
	public async Task RefusesOrdinaryDirectory() {
		var directory = System.IO.Path.Combine( temporary.Path, "directory" );
		Directory.CreateDirectory( directory );
		var error = new StringWriter();

		var status = await UnlinkCommand.RunAsync(
			new[] { directory },
			CreateContext( new StringWriter(), error )
		);

		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.True( Directory.Exists( directory ) );
		Assert.Contains( "Is a directory", error.ToString() );
	}

	/// <summary>Verifies the exact one-operand command contract.</summary>
	[Fact]
	public async Task RejectsMissingAndExtraOperands() {
		var missingError = new StringWriter();
		var missing = await UnlinkCommand.RunAsync(
			Array.Empty<string>(),
			CreateContext( new StringWriter(), missingError )
		);
		var extraError = new StringWriter();
		var extra = await UnlinkCommand.RunAsync(
			new[] { "one", "two" },
			CreateContext( new StringWriter(), extraError )
		);

		Assert.Equal( CommandExitCodes.Failure, missing );
		Assert.Equal( CommandExitCodes.Failure, extra );
		Assert.StartsWith( "unlink: missing operand", missingError.ToString() );
		Assert.Contains( "extra operand 'two'", extraError.ToString() );
	}

	private static CommandContext CreateContext( TextWriter output, TextWriter error ) {
		return new CommandContext( "unlink", TextReader.Null, output, error );
	}

	private sealed class TemporaryDirectory : IDisposable {
		public TemporaryDirectory() {
			Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				string.Concat( "Icod-Unlink-", Guid.NewGuid().ToString( "N" ) )
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
