namespace Icod.CoreUtils.Touch.Tests;

using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Traversal;
using Xunit;

/// <summary>Exercises the Batch 36 <c>touch</c> command.</summary>
public sealed class CommandTests {
	private static readonly IFileSystemMetadataProvider MetadataProvider =
		SystemFileSystemMetadataProvider.Instance;

	/// <summary>Verifies that at least one file operand is required.</summary>
	[Fact]
	public async Task MissingOperandFails() {
		var standardError = new StringWriter();
		var exitCode = await Command.RunAsync(
			Array.Empty<string>(), TextReader.Null, new StringWriter(), standardError
		);
		Assert.Equal( 1, exitCode );
		Assert.Contains( "missing file operand", standardError.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies that a missing regular file is created by default.</summary>
	[Fact]
	public async Task CreatesMissingFileByDefault() {
		using var workspace = new TemporaryWorkspace();
		var file = System.IO.Path.Combine( workspace.Path, "created" );
		var exitCode = await Command.RunAsync(
			new[] { file }, TextReader.Null, new StringWriter(), new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		Assert.True( File.Exists( file ) );
	}

	/// <summary>Verifies that <c>--no-create</c> leaves a missing operand absent.</summary>
	[Fact]
	public async Task NoCreateLeavesMissingFileAbsent() {
		using var workspace = new TemporaryWorkspace();
		var file = System.IO.Path.Combine( workspace.Path, "absent" );
		var exitCode = await Command.RunAsync(
			new[] { "--no-create", file }, TextReader.Null, new StringWriter(), new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		Assert.False( File.Exists( file ) );
	}

	/// <summary>Verifies GNU epoch-date parsing for both selected timestamps.</summary>
	[Fact]
	public async Task DateOptionSetsAccessAndModificationTimes() {
		using var workspace = new TemporaryWorkspace();
		var file = await workspace.CreateFileAsync( "dated", "content" );
		var expected = DateTimeOffset.FromUnixTimeSeconds( 946684800 );
		var exitCode = await Command.RunAsync(
			new[] { "--date=@946684800", file },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		var metadata = await MetadataProvider.GetMetadataAsync( file, true );
		AssertAvailableTimestamp( expected, metadata.AccessTime );
		AssertAvailableTimestamp( expected, metadata.ModificationTime );
	}

	/// <summary>Verifies that <c>-a</c> preserves the modification timestamp.</summary>
	[Fact]
	public async Task AccessOnlyPreservesModificationTime() {
		using var workspace = new TemporaryWorkspace();
		var file = await workspace.CreateFileAsync( "access-only", "content" );
		var originalAccess = DateTimeOffset.FromUnixTimeSeconds( 978307200 );
		var originalModification = DateTimeOffset.FromUnixTimeSeconds( 1009843200 );
		await SetTimesAsync( file, originalAccess, originalModification, PathDereferenceMode.FollowEligiblePathIndirection );

		var expectedAccess = DateTimeOffset.FromUnixTimeSeconds( 1041379200 );
		var exitCode = await Command.RunAsync(
			new[] { "-a", "--date=@1041379200", file },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		var metadata = await MetadataProvider.GetMetadataAsync( file, true );
		AssertAvailableTimestamp( expectedAccess, metadata.AccessTime );
		AssertAvailableTimestamp( originalModification, metadata.ModificationTime );
	}


	/// <summary>Verifies that <c>-m</c> preserves the access timestamp.</summary>
	[Fact]
	public async Task ModificationOnlyPreservesAccessTime() {
		using var workspace = new TemporaryWorkspace();
		var file = await workspace.CreateFileAsync( "modification-only", "content" );
		var originalAccess = DateTimeOffset.FromUnixTimeSeconds( 1009843200 );
		var originalModification = DateTimeOffset.FromUnixTimeSeconds( 1041379200 );
		await SetTimesAsync(
			file,
			originalAccess,
			originalModification,
			PathDereferenceMode.FollowEligiblePathIndirection
		);

		var expectedModification = DateTimeOffset.FromUnixTimeSeconds( 1072915200 );
		var exitCode = await Command.RunAsync(
			new[] { "-m", "--date=@1072915200", file },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		var metadata = await MetadataProvider.GetMetadataAsync( file, true );
		AssertAvailableTimestamp( originalAccess, metadata.AccessTime );
		AssertAvailableTimestamp( expectedModification, metadata.ModificationTime );
	}

	/// <summary>Verifies <c>--time=mtime</c> selects only the modification timestamp.</summary>
	[Fact]
	public async Task TimeWordSelectsModificationTimestamp() {
		using var workspace = new TemporaryWorkspace();
		var file = await workspace.CreateFileAsync( "time-word", "content" );
		var originalAccess = DateTimeOffset.FromUnixTimeSeconds( 1072915200 );
		await SetTimesAsync(
			file,
			originalAccess,
			DateTimeOffset.FromUnixTimeSeconds( 1104537600 ),
			PathDereferenceMode.FollowEligiblePathIndirection
		);
		var expectedModification = DateTimeOffset.FromUnixTimeSeconds( 1136073600 );
		var exitCode = await Command.RunAsync(
			new[] { "--time=mtime", "--date=@1136073600", file },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		var metadata = await MetadataProvider.GetMetadataAsync( file, true );
		AssertAvailableTimestamp( originalAccess, metadata.AccessTime );
		AssertAvailableTimestamp( expectedModification, metadata.ModificationTime );
	}

	/// <summary>Verifies that reference timestamps are copied independently.</summary>
	[Fact]
	public async Task ReferenceCopiesSelectedTimestamps() {
		using var workspace = new TemporaryWorkspace();
		var reference = await workspace.CreateFileAsync( "reference", "r" );
		var target = await workspace.CreateFileAsync( "target", "t" );
		var referenceAccess = DateTimeOffset.FromUnixTimeSeconds( 1072915200 );
		var referenceModification = DateTimeOffset.FromUnixTimeSeconds( 1104537600 );
		await SetTimesAsync(
			reference,
			referenceAccess,
			referenceModification,
			PathDereferenceMode.FollowEligiblePathIndirection
		);

		var exitCode = await Command.RunAsync(
			new[] { "--reference", reference, target },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		var metadata = await MetadataProvider.GetMetadataAsync( target, true );
		AssertAvailableTimestamp( referenceAccess, metadata.AccessTime );
		AssertAvailableTimestamp( referenceModification, metadata.ModificationTime );
	}

	/// <summary>Verifies GNU relative dates use the corresponding reference timestamp as their origin.</summary>
	[Fact]
	public async Task RelativeDateUsesReferenceModificationTime() {
		using var workspace = new TemporaryWorkspace();
		var reference = await workspace.CreateFileAsync( "reference-relative", "r" );
		var target = await workspace.CreateFileAsync( "target-relative", "t" );
		var referenceModification = DateTimeOffset.FromUnixTimeSeconds( 1136073600 );
		await SetTimesAsync(
			reference,
			DateTimeOffset.FromUnixTimeSeconds( 1136070000 ),
			referenceModification,
			PathDereferenceMode.FollowEligiblePathIndirection
		);

		var exitCode = await Command.RunAsync(
			new[] { "-m", "--reference", reference, "--date=5 seconds ago", target },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		var metadata = await MetadataProvider.GetMetadataAsync( target, true );
		AssertAvailableTimestamp( referenceModification.AddSeconds( -5 ), metadata.ModificationTime );
	}

	/// <summary>Verifies POSIX <c>[[CC]YY]MMDDhhmm[.ss]</c> parsing.</summary>
	[Fact]
	public async Task TimestampOptionUsesLocalCalendarFields() {
		using var workspace = new TemporaryWorkspace();
		var file = await workspace.CreateFileAsync( "timestamp", "content" );
		var exitCode = await Command.RunAsync(
			new[] { "-t", "200001020304.05", file },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		var metadata = await MetadataProvider.GetMetadataAsync( file, true );
		Assert.True( metadata.ModificationTime.IsAvailable );
		var local = TimeZoneInfo.ConvertTime(
			metadata.ModificationTime.GetRequiredValue(), TimeZoneInfo.Local
		);
		Assert.Equal( 2000, local.Year );
		Assert.Equal( 1, local.Month );
		Assert.Equal( 2, local.Day );
		Assert.Equal( 3, local.Hour );
		Assert.Equal( 4, local.Minute );
		Assert.Equal( 5, local.Second );
	}

	/// <summary>Verifies a POSIX leap-second value normalizes into the following minute.</summary>
	[Fact]
	public async Task TimestampOptionNormalizesLeapSecond() {
		using var workspace = new TemporaryWorkspace();
		var file = await workspace.CreateFileAsync( "leap-second", "content" );
		var exitCode = await Command.RunAsync(
			new[] { "-t", "200001020304.60", file },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		var metadata = await MetadataProvider.GetMetadataAsync( file, true );
		Assert.True( metadata.ModificationTime.IsAvailable );
		var local = TimeZoneInfo.ConvertTime(
			metadata.ModificationTime.GetRequiredValue(), TimeZoneInfo.Local
		);
		Assert.Equal( 2000, local.Year );
		Assert.Equal( 1, local.Month );
		Assert.Equal( 2, local.Day );
		Assert.Equal( 3, local.Hour );
		Assert.Equal( 5, local.Minute );
		Assert.Equal( 0, local.Second );
	}

	/// <summary>Verifies that directory timestamps can be changed.</summary>
	[Fact]
	public async Task UpdatesDirectoryModificationTime() {
		using var workspace = new TemporaryWorkspace();
		var directory = System.IO.Path.Combine( workspace.Path, "directory" );
		Directory.CreateDirectory( directory );
		var expected = DateTimeOffset.FromUnixTimeSeconds( 1167609600 );
		var exitCode = await Command.RunAsync(
			new[] { "-m", "--date=@1167609600", directory },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		var metadata = await MetadataProvider.GetMetadataAsync( directory, true );
		AssertAvailableTimestamp( expected, metadata.ModificationTime );
	}

	/// <summary>Verifies no-follow mutation changes the link object without changing its target when supported.</summary>
	[Fact]
	public async Task NoDereferenceUpdatesSymbolicLinkObjectWhenSupported() {
		using var workspace = new TemporaryWorkspace();
		var target = await workspace.CreateFileAsync( "link-target", "content" );
		var link = System.IO.Path.Combine( workspace.Path, "link" );
		try {
			File.CreateSymbolicLink( link, target );
		} catch ( Exception exception ) when (
			exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException
		) {
			return;
		}
		var physical = await MetadataProvider.GetMetadataAsync( link, PathDereferenceMode.NoFollow );
		if ( !physical.TimestampMutationCapabilities.IsAvailable
			|| 0 == (physical.TimestampMutationCapabilities.GetRequiredValue()
				& FileTimestampMutationCapabilities.NoFollowSymbolicLink) ) {
			return;
		}
		var targetModification = DateTimeOffset.FromUnixTimeSeconds( 1199145600 );
		await SetTimesAsync(
			target,
			DateTimeOffset.FromUnixTimeSeconds( 1199142000 ),
			targetModification,
			PathDereferenceMode.FollowEligiblePathIndirection
		);
		var linkModification = DateTimeOffset.FromUnixTimeSeconds( 1230768000 );
		var exitCode = await Command.RunAsync(
			new[] { "--no-dereference", "-m", "--date=@1230768000", link },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		var linkAfter = await MetadataProvider.GetMetadataAsync( link, PathDereferenceMode.NoFollow );
		var targetAfter = await MetadataProvider.GetMetadataAsync( target, true );
		AssertAvailableTimestamp( linkModification, linkAfter.ModificationTime );
		AssertAvailableTimestamp( targetModification, targetAfter.ModificationTime );
	}


	/// <summary>Verifies no-create suppresses a missing no-follow operand without an error.</summary>
	[Fact]
	public async Task NoCreateAndNoDereferenceSkipMissingOperand() {
		using var workspace = new TemporaryWorkspace();
		var missing = System.IO.Path.Combine( workspace.Path, "missing-link" );
		var standardError = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { "--no-create", "--no-dereference", missing },
			TextReader.Null,
			new StringWriter(),
			standardError
		);
		Assert.Equal( 0, exitCode );
		Assert.False( File.Exists( missing ) );
		Assert.Equal( string.Empty, standardError.ToString() );
	}

	/// <summary>Verifies incompatible date-source options fail before mutating an operand.</summary>
	[Fact]
	public async Task TimestampAndDateOptionsAreMutuallyExclusive() {
		using var workspace = new TemporaryWorkspace();
		var file = await workspace.CreateFileAsync( "exclusive", "content" );
		var standardError = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { "-t", "200001020304.05", "--date=@0", file },
			TextReader.Null,
			new StringWriter(),
			standardError
		);
		Assert.Equal( 1, exitCode );
		Assert.Contains( "mutually exclusive", standardError.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies an unknown <c>--time</c> selector is rejected.</summary>
	[Fact]
	public async Task InvalidTimeWordFails() {
		using var workspace = new TemporaryWorkspace();
		var file = await workspace.CreateFileAsync( "invalid-time-word", "content" );
		var standardError = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { "--time=creation", file },
			TextReader.Null,
			new StringWriter(),
			standardError
		);
		Assert.Equal( 1, exitCode );
		Assert.Contains( "invalid argument", standardError.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies help and version endpoints.</summary>
	[Theory]
	[InlineData( "--help", "Usage: touch" )]
	[InlineData( "--version", "touch (Icod.CoreUtils)" )]
	public async Task ReportsHelpAndVersion( string option, string expected ) {
		var standardOutput = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { option }, TextReader.Null, standardOutput, new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		Assert.StartsWith( expected, standardOutput.ToString(), StringComparison.Ordinal );
	}

	private static async Task SetTimesAsync(
		string path,
		DateTimeOffset access,
		DateTimeOffset modification,
		PathDereferenceMode dereferenceMode
	) {
		var operation = await MetadataProvider.SetTimestampsAsync(
			path,
			new FileTimestampMutationRequest {
				AccessTime = FileTimestampChange.At( access ),
				ModificationTime = FileTimestampChange.At( modification ),
			},
			dereferenceMode
		);
		Assert.True( operation.Succeeded, operation.Message );
	}

	private static void AssertAvailableTimestamp(
		DateTimeOffset expected,
		FileSystemMetadataValue<DateTimeOffset> actual
	) {
		Assert.True( actual.IsAvailable, actual.Message );
		var difference = (actual.GetRequiredValue() - expected).Duration();
		Assert.True(
			difference <= TimeSpan.FromSeconds( 1 ),
			$"Expected {expected:O}; actual {actual.GetRequiredValue():O}."
		);
	}

	private sealed class TemporaryWorkspace : IDisposable {
		/// <summary>Creates an isolated temporary directory.</summary>
		public TemporaryWorkspace() {
			Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				string.Concat( "icod-touch-tests-", Guid.NewGuid().ToString( "N" ) )
			);
			Directory.CreateDirectory( Path );
		}

		/// <summary>Gets the temporary directory path.</summary>
		public string Path { get; }

		/// <summary>Creates one file in the temporary directory.</summary>
		/// <param name="name">The file name.</param>
		/// <param name="contents">The file contents.</param>
		/// <returns>The created file path.</returns>
		public async Task<string> CreateFileAsync( string name, string contents ) {
			var path = System.IO.Path.Combine( Path, name );
			await File.WriteAllTextAsync( path, contents );
			return path;
		}

		/// <inheritdoc/>
		public void Dispose() {
			try {
				Directory.Delete( Path, true );
			} catch ( IOException ) {
			} catch ( UnauthorizedAccessException ) {
			}
		}
	}
}
