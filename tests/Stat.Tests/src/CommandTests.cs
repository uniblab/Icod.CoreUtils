namespace Icod.CoreUtils.Stat.Tests;

using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Traversal;
using Icod.CommandFramework.Platform;
using Xunit;

/// <summary>Exercises the Batch 36 <c>stat</c> command.</summary>
public sealed class CommandTests {
	/// <summary>Verifies that an operand is required.</summary>
	[Fact]
	public async Task MissingOperandFails() {
		var standardOutput = new StringWriter();
		var standardError = new StringWriter();
		var exitCode = await Command.RunAsync(
			Array.Empty<string>(), TextReader.Null, standardOutput, standardError
		);
		Assert.Equal( 1, exitCode );
		Assert.Contains( "missing operand", standardError.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies file-size, file-kind, and operand format directives.</summary>
	[Fact]
	public async Task CustomFormatReportsAuthoritativeFileFields() {
		using var workspace = new TemporaryWorkspace();
		var file = System.IO.Path.Combine( workspace.Path, "five.txt" );
		await File.WriteAllTextAsync( file, "12345" );
		var standardOutput = new StringWriter();
		var standardError = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { "--format=%s|%F|%n", file },
			TextReader.Null,
			standardOutput,
			standardError
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal(
			string.Concat( "5|regular file|", file, Environment.NewLine ),
			standardOutput.ToString()
		);
		Assert.Equal( string.Empty, standardError.ToString() );
	}

	/// <summary>Verifies that printf escapes are interpreted without an added newline.</summary>
	[Fact]
	public async Task PrintfInterpretsEscapesWithoutGeneratedNewline() {
		using var workspace = new TemporaryWorkspace();
		var file = System.IO.Path.Combine( workspace.Path, "item" );
		await File.WriteAllTextAsync( file, "x" );
		var standardOutput = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { "--printf=%s\\t%n", file }, TextReader.Null, standardOutput, new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( string.Concat( "1\t", file ), standardOutput.ToString() );
	}

	/// <summary>Verifies unrecognized printf escapes emit their character without terminating the format.</summary>
	[Fact]
	public async Task PrintfRetainsUnknownEscapeCharacter() {
		using var workspace = new TemporaryWorkspace();
		var file = System.IO.Path.Combine( workspace.Path, "item-escape" );
		await File.WriteAllTextAsync( file, "x" );
		var standardOutput = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { "--printf=before\\cafter", file },
			TextReader.Null,
			standardOutput,
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( "beforecafter", standardOutput.ToString() );
	}

	/// <summary>Verifies filesystem-format output.</summary>
	[Fact]
	public async Task FileSystemModeReportsOperandAndType() {
		using var workspace = new TemporaryWorkspace();
		var standardOutput = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { "--file-system", "--format=%n|%T", workspace.Path },
			TextReader.Null,
			standardOutput,
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		Assert.StartsWith( string.Concat( workspace.Path, "|" ), standardOutput.ToString(), StringComparison.Ordinal );
		Assert.EndsWith( Environment.NewLine, standardOutput.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies the GNU terse file layout contains the expected sixteen fields.</summary>
	[Fact]
	public async Task TerseFileReportContainsSixteenFields() {
		var metadata = new FileSystemMetadata(
			"synthetic",
			FileSystemEntryKind.File,
			false,
			false,
			new FileSystemEntryIdentity( "test", "entry" ),
			new FileSystemIdentity( "test", "filesystem" )
		);
		var standardOutput = new StringWriter();
		var context = new CommandContext(
			"stat", TextReader.Null, standardOutput, new StringWriter()
		);
		var exitCode = await Command.RunAsync(
			new[] { "--terse", "synthetic" },
			context,
			new SyntheticMetadataProvider( metadata )
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal(
			16,
			standardOutput.ToString().TrimEnd().Split(
				' ', StringSplitOptions.RemoveEmptyEntries
			).Length
		);
	}

	/// <summary>Verifies that directories are reported through the metadata adapter.</summary>
	[Fact]
	public async Task DefaultReportRecognizesDirectory() {
		using var workspace = new TemporaryWorkspace();
		var standardOutput = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { workspace.Path }, TextReader.Null, standardOutput, new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		Assert.Contains( "directory", standardOutput.ToString(), StringComparison.Ordinal );
		Assert.Contains( "Change:", standardOutput.ToString(), StringComparison.Ordinal );
		Assert.Contains( "Birth:", standardOutput.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies link-object and dereferenced-target reporting where links are available.</summary>
	[Fact]
	public async Task DereferenceOptionChangesSymbolicLinkKindWhenSupported() {
		using var workspace = new TemporaryWorkspace();
		var target = System.IO.Path.Combine( workspace.Path, "target" );
		var link = System.IO.Path.Combine( workspace.Path, "link" );
		await File.WriteAllTextAsync( target, "abc" );
		try {
			File.CreateSymbolicLink( link, target );
		} catch ( Exception exception ) when (
			exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException
		) {
			return;
		}
		var physicalOutput = new StringWriter();
		var targetOutput = new StringWriter();
		Assert.Equal( 0, await Command.RunAsync(
			new[] { "--format=%F|%N", link }, TextReader.Null, physicalOutput, new StringWriter()
		) );
		Assert.Equal( 0, await Command.RunAsync(
			new[] { "--dereference", "--format=%F|%N", link }, TextReader.Null, targetOutput, new StringWriter()
		) );
		Assert.Contains( "symbolic link", physicalOutput.ToString(), StringComparison.Ordinal );
		Assert.Contains( "regular file", targetOutput.ToString(), StringComparison.Ordinal );
		Assert.Contains( " -> ", physicalOutput.ToString(), StringComparison.Ordinal );
		Assert.DoesNotContain( " -> ", targetOutput.ToString(), StringComparison.Ordinal );
	}


	/// <summary>Verifies major/minor modifiers and timestamp precision syntax are accepted.</summary>
	[Fact]
	public async Task SupportsExtendedDeviceAndTimestampDirectives() {
		using var workspace = new TemporaryWorkspace();
		var file = System.IO.Path.Combine( workspace.Path, "extended" );
		await File.WriteAllTextAsync( file, "x" );
		var standardOutput = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { "--format=%Hd|%Ld|%Hr|%Lr|%.3Y", file },
			TextReader.Null,
			standardOutput,
			new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		var fields = standardOutput.ToString().TrimEnd().Split( '|' );
		Assert.Equal( 5, fields.Length );
		Assert.All( fields[..4], value => Assert.True( ulong.TryParse( value, out _ ) ) );
		Assert.Matches( @"^-?\d+\.\d{3}$", fields[4] );
	}


	/// <summary>Verifies inode-change and birth timestamps remain separate format fields.</summary>
	[Fact]
	public async Task ChangeAndBirthTimesUseDistinctMetadataFields() {
		var change = DateTimeOffset.FromUnixTimeSeconds( 100 );
		var birth = DateTimeOffset.FromUnixTimeSeconds( 200 );
		var metadata = new FileSystemMetadata(
			"synthetic",
			FileSystemEntryKind.File,
			false,
			false,
			new FileSystemEntryIdentity( "test", "entry" ),
			new FileSystemIdentity( "test", "filesystem" )
		) {
			ChangeTime = FileSystemMetadataValue<DateTimeOffset>.Available( change ),
			BirthTime = FileSystemMetadataValue<DateTimeOffset>.Available( birth ),
		};
		var standardOutput = new StringWriter();
		var context = new CommandContext(
			"stat", TextReader.Null, standardOutput, new StringWriter()
		);
		var exitCode = await Command.RunAsync(
			new[] { "--format=%Z|%W", "synthetic" },
			context,
			new SyntheticMetadataProvider( metadata )
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( string.Concat( "100|200", Environment.NewLine ), standardOutput.ToString() );
	}

	/// <summary>Verifies alternate hexadecimal formatting does not prefix a zero value.</summary>
	[Fact]
	public async Task AlternateHexadecimalZeroHasNoPrefix() {
		var metadata = new FileSystemMetadata(
			"synthetic",
			FileSystemEntryKind.File,
			false,
			false,
			new FileSystemEntryIdentity( "test", "entry" ),
			new FileSystemIdentity( "test", "filesystem" )
		) {
			Mode = FileSystemMetadataValue<uint>.Available( 0 ),
		};
		var standardOutput = new StringWriter();
		var context = new CommandContext(
			"stat", TextReader.Null, standardOutput, new StringWriter()
		);
		var exitCode = await Command.RunAsync(
			new[] { "--format=%#R|%#t|%#T|%#.0R|%.0r|%#.0a", "synthetic" },
			context,
			new SyntheticMetadataProvider( metadata )
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( string.Concat( "0|0|0|||0", Environment.NewLine ), standardOutput.ToString() );
	}

	/// <summary>Verifies sign flags apply to signed epochs but not unsigned metadata values.</summary>
	[Fact]
	public async Task SignFlagsApplyOnlyToSignedTimestampFields() {
		var metadata = new FileSystemMetadata(
			"synthetic",
			FileSystemEntryKind.File,
			false,
			false,
			new FileSystemEntryIdentity( "test", "entry" ),
			new FileSystemIdentity( "test", "filesystem" )
		) {
			Size = FileSystemMetadataValue<ulong>.Available( 0 ),
			ModificationTime = FileSystemMetadataValue<DateTimeOffset>.Available(
				DateTimeOffset.FromUnixTimeSeconds( 5 )
			),
		};
		var standardOutput = new StringWriter();
		var context = new CommandContext(
			"stat", TextReader.Null, standardOutput, new StringWriter()
		);
		var exitCode = await Command.RunAsync(
			new[] { "--format=%+s|% s|%+Y|% Y", "synthetic" },
			context,
			new SyntheticMetadataProvider( metadata )
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( string.Concat( "0|0|+5| 5", Environment.NewLine ), standardOutput.ToString() );
	}

	/// <summary>Verifies Linux device identifiers use the native <c>dev_t</c> encoding.</summary>
	[Fact]
	public async Task LinuxDeviceNumbersUseNativeEncoding() {
		var metadata = new FileSystemMetadata(
			"synthetic",
			FileSystemEntryKind.File,
			false,
			false,
			new FileSystemEntryIdentity( "test", "entry" ),
			new FileSystemIdentity( "linux-mount-id", "42" )
		) {
			DeviceIdentifier = FileSystemMetadataValue<string>.Available( "8:1" ),
		};
		var standardOutput = new StringWriter();
		var context = new CommandContext(
			"stat", TextReader.Null, standardOutput, new StringWriter()
		);
		var exitCode = await Command.RunAsync(
			new[] { "--format=%d|%D|%Hd|%Ld", "synthetic" },
			context,
			new SyntheticMetadataProvider( metadata )
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( string.Concat( "2049|801|8|1", Environment.NewLine ), standardOutput.ToString() );
	}

	/// <summary>Verifies fractional timestamps before the Unix epoch retain their sign.</summary>
	[Fact]
	public async Task NegativeFractionalEpochTimestampRetainsSign() {
		var metadata = new FileSystemMetadata(
			"synthetic",
			FileSystemEntryKind.File,
			false,
			false,
			new FileSystemEntryIdentity( "test", "entry" ),
			new FileSystemIdentity( "test", "filesystem" )
		) {
			ModificationTime = FileSystemMetadataValue<DateTimeOffset>.Available(
				DateTimeOffset.UnixEpoch.AddTicks( -5_000_000 )
			),
		};
		var standardOutput = new StringWriter();
		var context = new CommandContext(
			"stat", TextReader.Null, standardOutput, new StringWriter()
		);
		var exitCode = await Command.RunAsync(
			new[] { "--format=%.3Y", "synthetic" },
			context,
			new SyntheticMetadataProvider( metadata )
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( string.Concat( "-0.500", Environment.NewLine ), standardOutput.ToString() );
	}

	/// <summary>Verifies unsupported nondefault attribute-cache policies fail explicitly.</summary>
	[Theory]
	[InlineData( "always" )]
	[InlineData( "never" )]
	public async Task UnsupportedCachePolicyFailsExplicitly( string policy ) {
		var standardError = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { string.Concat( "--cached=", policy ), "." },
			TextReader.Null,
			new StringWriter(),
			standardError
		);
		Assert.Equal( 1, exitCode );
		Assert.Contains( "unsupported", standardError.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies a failed operand does not suppress a later successful report.</summary>
	[Fact]
	public async Task ContinuesAfterMissingOperand() {
		using var workspace = new TemporaryWorkspace();
		var present = System.IO.Path.Combine( workspace.Path, "present" );
		await File.WriteAllTextAsync( present, "ok" );
		var missing = System.IO.Path.Combine( workspace.Path, "missing" );
		var standardOutput = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { "--format=%n", missing, present },
			TextReader.Null,
			standardOutput,
			new StringWriter()
		);
		Assert.Equal( 1, exitCode );
		Assert.Equal( string.Concat( present, Environment.NewLine ), standardOutput.ToString() );
	}

	/// <summary>Verifies an invalid directive produces a controlled command error.</summary>
	[Fact]
	public async Task InvalidFormatDirectiveFails() {
		using var workspace = new TemporaryWorkspace();
		var file = System.IO.Path.Combine( workspace.Path, "invalid-format" );
		await File.WriteAllTextAsync( file, "x" );
		var standardError = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { "--format=%q", file },
			TextReader.Null,
			new StringWriter(),
			standardError
		);
		Assert.Equal( 1, exitCode );
		Assert.Contains( "invalid format directive", standardError.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies help and version endpoints.</summary>
	[Theory]
	[InlineData( "--help", "Usage: stat" )]
	[InlineData( "--version", "stat (Icod.CoreUtils)" )]
	public async Task ReportsHelpAndVersion( string option, string expected ) {
		var standardOutput = new StringWriter();
		var exitCode = await Command.RunAsync(
			new[] { option }, TextReader.Null, standardOutput, new StringWriter()
		);
		Assert.Equal( 0, exitCode );
		Assert.StartsWith( expected, standardOutput.ToString(), StringComparison.Ordinal );
	}


	private sealed class SyntheticMetadataProvider : IFileSystemMetadataProvider {
		private readonly FileSystemMetadata _metadata;

		/// <summary>Initializes the provider with one deterministic observation.</summary>
		/// <param name="metadata">The metadata returned for every path.</param>
		public SyntheticMetadataProvider( FileSystemMetadata metadata ) {
			_metadata = metadata;
		}

		/// <inheritdoc/>
		public ValueTask<FileSystemMetadata> GetMetadataAsync(
			string path,
			bool followSymbolicLink,
			CancellationToken cancellationToken = default
		) => new( _metadata );

		/// <inheritdoc/>
		public ValueTask<FileSystemInformation> GetFileSystemInformationAsync(
			string path,
			CancellationToken cancellationToken = default
		) => new( new FileSystemInformation(
			path, new FileSystemIdentity( "test", "filesystem" )
		) );

		/// <inheritdoc/>
		public ValueTask<PlatformOperationResult> SetTimestampsAsync(
			string path,
			FileTimestampMutationRequest request,
			bool followSymbolicLink,
			CancellationToken cancellationToken = default
		) => throw new NotSupportedException();
	}

	private sealed class TemporaryWorkspace : IDisposable {
		/// <summary>Creates an isolated temporary directory.</summary>
		public TemporaryWorkspace() {
			Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				string.Concat( "icod-stat-tests-", Guid.NewGuid().ToString( "N" ) )
			);
			Directory.CreateDirectory( Path );
		}

		/// <summary>Gets the temporary directory path.</summary>
		public string Path { get; }

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
