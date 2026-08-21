namespace Icod.CoreUtils.Rm.Tests;

using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CommandFramework.Platform;
using RmCommand = Icod.CoreUtils.Rm.Command;
using Xunit;

/// <summary>Exercises GNU-compatible <c>rm</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies a missing operand is rejected unless force is active.</summary>
	[Fact]
	public async Task MissingOperandHonorsForce() {
		var error = new StringWriter();
		var failure = await RunAsync( Array.Empty<string>(), TextReader.Null, new StringWriter(), error );
		var success = await RunAsync( new[] { "--force" }, TextReader.Null, new StringWriter(), new StringWriter() );
		Assert.Equal( CommandExitCodes.Failure, failure );
		Assert.Contains( "missing operand", error.ToString() );
		Assert.Equal( CommandExitCodes.Success, success );
	}

	/// <summary>Verifies ordinary files are removed with stable no-follow preconditions.</summary>
	[Fact]
	public async Task RemovesFileWithIdentityPrecondition() {
		var root = CreateTemporaryDirectory();
		var path = System.IO.Path.Combine( root, "file" );
		await File.WriteAllTextAsync( path, "content" );
		var mutation = new RecordingForwardingMutationProvider();
		try {
			var status = await RunInjectedAsync( new[] { path }, TextReader.Null, new StringWriter(), new StringWriter(), mutation );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.False( File.Exists( path ) );
			var request = Assert.Single( mutation.Removals );
			Assert.False( request.IsDirectory );
			Assert.Equal( PathDereferenceMode.NoFollow, request.Precondition?.DereferenceMode );
			Assert.NotNull( request.Precondition?.ExpectedIdentity );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies directories require recursive mode or explicit empty-directory removal.</summary>
	[Fact]
	public async Task DirectoryRequiresRecursiveOrDir() {
		var first = CreateTemporaryDirectory();
		var second = CreateTemporaryDirectory();
		try {
			var error = new StringWriter();
			var failure = await RunAsync( new[] { first }, TextReader.Null, new StringWriter(), error );
			var success = await RunAsync( new[] { "--dir", second }, TextReader.Null, new StringWriter(), new StringWriter() );
			Assert.Equal( CommandExitCodes.Failure, failure );
			Assert.Contains( "Is a directory", error.ToString() );
			Assert.True( Directory.Exists( first ) );
			Assert.Equal( CommandExitCodes.Success, success );
			Assert.False( Directory.Exists( second ) );
		} finally {
			DeleteTree( first );
			DeleteTree( second );
		}
	}

	/// <summary>Verifies recursive removal mutates descendants before their parent directories.</summary>
	[Fact]
	public async Task RecursivelyRemovesInPostorder() {
		var root = CreateTemporaryDirectory();
		var nested = Directory.CreateDirectory( System.IO.Path.Combine( root, "nested" ) ).FullName;
		var child = System.IO.Path.Combine( nested, "child" );
		await File.WriteAllTextAsync( child, "content" );
		var mutation = new RecordingForwardingMutationProvider();
		try {
			var status = await RunInjectedAsync( new[] { "--recursive", root }, TextReader.Null, new StringWriter(), new StringWriter(), mutation );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.False( Directory.Exists( root ) );
			Assert.Equal( new[] { child, nested, root }, mutation.Removals.Select( request => request.Path ).ToArray() );
			Assert.All( mutation.Removals, request => Assert.NotNull( request.Precondition?.ExpectedIdentity ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies force suppresses missing recursive operands.</summary>
	[Fact]
	public async Task ForceSuppressesMissingRecursiveOperand() {
		var missing = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-Rm-Missing-", Guid.NewGuid().ToString( "N" ) ) );
		var error = new StringWriter();
		var status = await RunAsync( new[] { "--force", "--recursive", missing }, TextReader.Null, new StringWriter(), error );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( string.Empty, error.ToString() );
	}

	/// <summary>Verifies an always-interactive refusal leaves an entry unchanged.</summary>
	[Fact]
	public async Task InteractiveRefusalRetainsFile() {
		var root = CreateTemporaryDirectory();
		var path = System.IO.Path.Combine( root, "file" );
		await File.WriteAllTextAsync( path, "content" );
		try {
			var status = await RunAsync( new[] { "--interactive=always", path }, new StringReader( "n" + Environment.NewLine ), new StringWriter(), new StringWriter() );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.True( File.Exists( path ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies GNU interactive aliases and encounter-order precedence.</summary>
	[Fact]
	public async Task InteractiveAliasesAndPrecedenceAreHonored() {
		var root = CreateTemporaryDirectory();
		var first = System.IO.Path.Combine( root, "first" );
		var second = System.IO.Path.Combine( root, "second" );
		await File.WriteAllTextAsync( first, "one" );
		await File.WriteAllTextAsync( second, "two" );
		try {
			var firstStatus = await RunAsync( new[] { "--interactive=a", first }, new StringReader( "y" + Environment.NewLine ), new StringWriter(), new StringWriter() );
			var secondStatus = await RunAsync( new[] { "-i", "-f", second }, TextReader.Null, new StringWriter(), new StringWriter() );
			Assert.Equal( CommandExitCodes.Success, firstStatus );
			Assert.False( File.Exists( first ) );
			Assert.Equal( CommandExitCodes.Success, secondStatus );
			Assert.False( File.Exists( second ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies interactive-never does not undo an earlier force missing-file policy.</summary>
	[Fact]
	public async Task InteractiveNeverRetainsEarlierForceMissingPolicy() {
		var missing = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-Rm-Missing-", Guid.NewGuid().ToString( "N" ) ) );
		var status = await RunAsync(
			new[] { "--force", "--interactive=never", missing },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);
		Assert.Equal( CommandExitCodes.Success, status );
	}

	/// <summary>Verifies the once-interactive mode can reject an entire recursive operation.</summary>
	[Fact]
	public async Task InteractiveOnceCanRejectRecursiveOperation() {
		var root = CreateTemporaryDirectory();
		await File.WriteAllTextAsync( System.IO.Path.Combine( root, "child" ), "content" );
		try {
			var status = await RunAsync( new[] { "-I", "--recursive", root }, new StringReader( "n" + Environment.NewLine ), new StringWriter(), new StringWriter() );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.True( Directory.Exists( root ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies once-interactive mode still protects unwritable entries after its operation-wide check.</summary>
	[Fact]
	public async Task InteractiveOnceStillPromptsForWriteProtectedFile() {
		var root = CreateTemporaryDirectory();
		var path = System.IO.Path.Combine( root, "file" );
		await File.WriteAllTextAsync( path, "content" );
		SetWriteProtected( path, true );
		try {
			var error = new StringWriter();
			var status = await RunInjectedAsync(
				new[] { "-I", path },
				new StringReader( "n" + Environment.NewLine ),
				new StringWriter(),
				error,
				SystemFileSystemMutationProvider.Instance,
				true
			);
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.True( File.Exists( path ) );
			Assert.Contains( "write-protected", error.ToString() );
		} finally {
			SetWriteProtected( path, false );
			DeleteTree( root );
		}
	}

	/// <summary>Verifies declining one recursive child retains its parent without reporting a removal failure.</summary>
	[Fact]
	public async Task DeclinedRecursiveChildRetainsParentDirectory() {
		var root = CreateTemporaryDirectory();
		var child = System.IO.Path.Combine( root, "child" );
		await File.WriteAllTextAsync( child, "content" );
		var mutation = new RecordingForwardingMutationProvider();
		try {
			var status = await RunInjectedAsync(
				new[] { "--interactive=always", "--recursive", root },
				new StringReader( string.Concat( "y", Environment.NewLine, "n", Environment.NewLine ) ),
				new StringWriter(),
				new StringWriter(),
				mutation
			);
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.True( File.Exists( child ) );
			Assert.True( Directory.Exists( root ) );
			Assert.Empty( mutation.Removals );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies centralized pathname expansion removes only matching roots.</summary>
	[Fact]
	public async Task ExpandsPathnamePatterns() {
		var root = CreateTemporaryDirectory();
		var first = System.IO.Path.Combine( root, "one.tmp" );
		var second = System.IO.Path.Combine( root, "two.tmp" );
		var retained = System.IO.Path.Combine( root, "three.txt" );
		await File.WriteAllTextAsync( first, "one" );
		await File.WriteAllTextAsync( second, "two" );
		await File.WriteAllTextAsync( retained, "three" );
		try {
			var status = await RunAsync( new[] { System.IO.Path.Combine( root, "*.tmp" ) }, TextReader.Null, new StringWriter(), new StringWriter() );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.False( File.Exists( first ) );
			Assert.False( File.Exists( second ) );
			Assert.True( File.Exists( retained ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies explicitly named intermediate symbolic links may be followed while expanding a terminal pattern.</summary>
	[Fact]
	public async Task ExpandsThroughExplicitIntermediateSymbolicLink() {
		var root = CreateTemporaryDirectory();
		var target = Directory.CreateDirectory( System.IO.Path.Combine( root, "target" ) ).FullName;
		var link = System.IO.Path.Combine( root, "link" );
		var matched = System.IO.Path.Combine( target, "matched.tmp" );
		var retained = System.IO.Path.Combine( target, "retained.txt" );
		await File.WriteAllTextAsync( matched, "matched" );
		await File.WriteAllTextAsync( retained, "retained" );
		try {
			try {
				Directory.CreateSymbolicLink( link, target );
			} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException ) {
				return;
			}
			var status = await RunAsync(
				new[] { System.IO.Path.Combine( link, "*.tmp" ) },
				TextReader.Null,
				new StringWriter(),
				new StringWriter()
			);
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.False( File.Exists( matched ) );
			Assert.True( File.Exists( retained ) );
			Assert.True( Directory.Exists( link ) );
		} finally {
			try { Directory.Delete( link, recursive: false ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
			DeleteTree( root );
		}
	}

	/// <summary>Verifies a failed operand does not prevent a later operand from being removed.</summary>
	[Fact]
	public async Task ContinuesAfterOperandFailure() {
		var root = CreateTemporaryDirectory();
		var existing = System.IO.Path.Combine( root, "existing" );
		var missing = System.IO.Path.Combine( root, "missing" );
		await File.WriteAllTextAsync( existing, "content" );
		try {
			var status = await RunAsync( new[] { missing, existing }, TextReader.Null, new StringWriter(), new StringWriter() );
			Assert.Equal( CommandExitCodes.Failure, status );
			Assert.False( File.Exists( existing ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies preserve-root blocks a filesystem root before any mutation request.</summary>
	[Fact]
	public async Task PreservesFileSystemRootByDefault() {
		var root = System.IO.Path.GetPathRoot( System.IO.Path.GetFullPath( System.IO.Path.GetTempPath() ) )!;
		var mutation = new RecordingOnlyMutationProvider();
		var error = new StringWriter();
		var status = await RunInjectedAsync( new[] { "--recursive", root }, TextReader.Null, new StringWriter(), error, mutation );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Empty( mutation.Removals );
		Assert.Contains( "dangerous", error.ToString() );
	}

	/// <summary>Verifies a trailing separator never turns a terminal symbolic link into recursive target deletion.</summary>
	[Fact]
	public async Task TrailingSeparatorDoesNotFollowSymbolicLink() {
		var root = CreateTemporaryDirectory();
		var target = System.IO.Path.Combine( root, "target" );
		var link = System.IO.Path.Combine( root, "link" );
		await File.WriteAllTextAsync( target, "content" );
		try {
			try {
				File.CreateSymbolicLink( link, target );
			} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException ) {
				return;
			}
			var mutation = new RecordingOnlyMutationProvider();
			var status = await RunInjectedAsync(
				new[] { "--recursive", string.Concat( link, System.IO.Path.DirectorySeparatorChar ) },
				TextReader.Null,
				new StringWriter(),
				new StringWriter(),
				mutation
			);
			Assert.Equal( CommandExitCodes.Failure, status );
			Assert.Empty( mutation.Removals );
			Assert.True( File.Exists( target ) );
		} finally {
			try { File.Delete( link ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
			DeleteTree( root );
		}
	}

	/// <summary>Verifies a terminal write-protected prompt can retain a file.</summary>
	[Fact]
	public async Task PromptsForWriteProtectedFileOnTerminal() {
		var root = CreateTemporaryDirectory();
		var path = System.IO.Path.Combine( root, "file" );
		await File.WriteAllTextAsync( path, "content" );
		SetWriteProtected( path, true );
		try {
			var error = new StringWriter();
			var status = await RunInjectedAsync(
				new[] { path },
				new StringReader( "n" + Environment.NewLine ),
				new StringWriter(),
				error,
				SystemFileSystemMutationProvider.Instance,
				true
			);
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.True( File.Exists( path ) );
			Assert.Contains( "write-protected", error.ToString() );
		} finally {
			SetWriteProtected( path, false );
			DeleteTree( root );
		}
	}

	/// <summary>Verifies verbose mode reports successful removals.</summary>
	[Fact]
	public async Task VerboseReportsRemoval() {
		var root = CreateTemporaryDirectory();
		var path = System.IO.Path.Combine( root, "file" );
		await File.WriteAllTextAsync( path, "content" );
		try {
			var output = new StringWriter();
			var status = await RunAsync( new[] { "--verbose", path }, TextReader.Null, output, new StringWriter() );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.Contains( "removed", output.ToString() );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies GNU forbids abbreviation of the destructive no-preserve-root option.</summary>
	[Fact]
	public async Task NoPreserveRootMayNotBeAbbreviated() {
		var error = new StringWriter();
		var status = await RunAsync(
			new[] { "--no-preserve", "missing" },
			TextReader.Null,
			new StringWriter(),
			error
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "may not abbreviate", error.ToString() );
	}

	/// <summary>Verifies final dot and dot-dot operands are refused.</summary>
	[Theory]
	[InlineData( "." )]
	[InlineData( ".." )]
	public async Task RefusesDotOperands( string operand ) {
		var error = new StringWriter();
		var status = await RunAsync( new[] { "--force", "--recursive", operand }, TextReader.Null, new StringWriter(), error );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "may not be removed", error.ToString() );
	}

	private static ValueTask<int> RunAsync(
		string[] args,
		TextReader input,
		TextWriter output,
		TextWriter error
	) => RunInjectedAsync( args, input, output, error, SystemFileSystemMutationProvider.Instance );

	private static ValueTask<int> RunInjectedAsync(
		string[] args,
		TextReader input,
		TextWriter output,
		TextWriter error,
		IFileSystemMutationProvider mutationProvider,
		bool standardInputIsTerminal = false
	) => RmCommand.RunAsync(
		args,
		new CommandContext( "rm", input, output, error ),
		SystemReadOnlyFileSystemProvider.Instance,
		SystemFileSystemMetadataProvider.Instance,
		mutationProvider,
		SystemIdentityProvider.Instance,
		standardInputIsTerminal
	);

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-Rm-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( path );
		return path;
	}

	private static void SetWriteProtected( string path, bool value ) {
		if ( !File.Exists( path ) ) return;
		if ( OperatingSystem.IsWindows() ) {
			var attributes = File.GetAttributes( path );
			File.SetAttributes( path, value ? attributes | FileAttributes.ReadOnly : attributes & ~FileAttributes.ReadOnly );
			return;
		}
		var mode = File.GetUnixFileMode( path );
		var write = UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
		File.SetUnixFileMode( path, value ? mode & ~write : mode | UnixFileMode.UserWrite );
	}

	private static void DeleteTree( string path ) {
		if ( string.IsNullOrEmpty( path ) || !Directory.Exists( path ) ) return;
		try {
			foreach ( var file in Directory.EnumerateFiles( path, "*", SearchOption.AllDirectories ) ) {
				try { File.SetAttributes( file, FileAttributes.Normal ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
			}
			Directory.Delete( path, recursive: true );
		} catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
	}

	private readonly record struct RemovalRequest(
		string Path,
		bool IsDirectory,
		FileSystemMutationPrecondition? Precondition
	);

	private sealed class RecordingForwardingMutationProvider : IFileSystemMutationProvider {
		private readonly IFileSystemMutationProvider _inner = SystemFileSystemMutationProvider.Instance;
		/// <inheritdoc/>
		public FileSystemMutationCapabilities Capabilities => _inner.Capabilities;
		/// <summary>Gets recorded removal requests.</summary>
		public List<RemovalRequest> Removals { get; } = new();
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> RemoveFileAsync( string path, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) {
			Removals.Add( new RemovalRequest( path, false, precondition ) );
			return _inner.RemoveFileAsync( path, precondition, cancellationToken );
		}
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> RemoveDirectoryAsync( string path, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) {
			Removals.Add( new RemovalRequest( path, true, precondition ) );
			return _inner.RemoveDirectoryAsync( path, precondition, cancellationToken );
		}
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateDirectoryAsync( string path, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => _inner.CreateDirectoryAsync( path, mode, creationMask, precondition, cancellationToken );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateFileAsync( string path, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => _inner.CreateFileAsync( path, mode, creationMask, precondition, cancellationToken );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateHardLinkAsync( string path, string existingPath, PathDereferenceMode existingPathDereferenceMode, FileSystemMutationPrecondition? destinationPrecondition = null, FileSystemMutationPrecondition? existingPathPrecondition = null, CancellationToken cancellationToken = default ) => _inner.CreateHardLinkAsync( path, existingPath, existingPathDereferenceMode, destinationPrecondition, existingPathPrecondition, cancellationToken );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateSymbolicLinkAsync( string path, string target, bool targetIsDirectory, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => _inner.CreateSymbolicLinkAsync( path, target, targetIsDirectory, precondition, cancellationToken );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateJunctionAsync( string path, string target, FileSystemMutationPrecondition? destinationPrecondition = null, FileSystemMutationPrecondition? targetPrecondition = null, CancellationToken cancellationToken = default ) => _inner.CreateJunctionAsync( path, target, destinationPrecondition, targetPrecondition, cancellationToken );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateFifoAsync( string path, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => _inner.CreateFifoAsync( path, mode, creationMask, precondition, cancellationToken );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateDeviceNodeAsync( string path, FileSystemEntryKind kind, DeviceNumber deviceNumber, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => _inner.CreateDeviceNodeAsync( path, kind, deviceNumber, mode, creationMask, precondition, cancellationToken );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> SetModeAsync( string path, PosixFileMode mode, PathDereferenceMode dereferenceMode, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => _inner.SetModeAsync( path, mode, dereferenceMode, precondition, cancellationToken );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> SetOwnershipAsync( string path, uint? userId, uint? groupId, PathDereferenceMode dereferenceMode, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => _inner.SetOwnershipAsync( path, userId, groupId, dereferenceMode, precondition, cancellationToken );
	}

	private sealed class RecordingOnlyMutationProvider : IFileSystemMutationProvider {
		private static readonly FileSystemMutationCapabilities AllCapabilities = new(
			true, true, true, true, true, true, true, true, true, true, true, true, true
		);
		/// <inheritdoc/>
		public FileSystemMutationCapabilities Capabilities => AllCapabilities;
		/// <summary>Gets recorded removal requests.</summary>
		public List<RemovalRequest> Removals { get; } = new();
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> RemoveFileAsync( string path, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) {
			Removals.Add( new RemovalRequest( path, false, precondition ) );
			return ValueTask.FromResult( Success( path, false, precondition ) );
		}
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> RemoveDirectoryAsync( string path, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) {
			Removals.Add( new RemovalRequest( path, true, precondition ) );
			return ValueTask.FromResult( Success( path, true, precondition ) );
		}
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateDirectoryAsync( string path, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateFileAsync( string path, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateHardLinkAsync( string path, string existingPath, PathDereferenceMode existingPathDereferenceMode, FileSystemMutationPrecondition? destinationPrecondition = null, FileSystemMutationPrecondition? existingPathPrecondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateSymbolicLinkAsync( string path, string target, bool targetIsDirectory, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateJunctionAsync( string path, string target, FileSystemMutationPrecondition? destinationPrecondition = null, FileSystemMutationPrecondition? targetPrecondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateFifoAsync( string path, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateDeviceNodeAsync( string path, FileSystemEntryKind kind, DeviceNumber deviceNumber, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> SetModeAsync( string path, PosixFileMode mode, PathDereferenceMode dereferenceMode, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> SetOwnershipAsync( string path, uint? userId, uint? groupId, PathDereferenceMode dereferenceMode, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );

		private static ValueTask<FileSystemMutationResult> Unused( string path ) =>
			ValueTask.FromResult( FileSystemMutationResult.Unsupported( path, "not used" ) );

		private static FileSystemMutationResult Success( string path, bool isDirectory, FileSystemMutationPrecondition? precondition ) =>
			FileSystemMutationResult.Success( new FileSystemMutationOutcome(
				path,
				isDirectory ? FileSystemMutationOperation.RemoveDirectory : FileSystemMutationOperation.RemoveFile,
				isDirectory ? FileSystemEntryKind.Directory : FileSystemEntryKind.File,
				precondition?.ExpectedIdentity ?? FileSystemEntryIdentity.Unavailable,
				null,
				false
			) );
	}
}
