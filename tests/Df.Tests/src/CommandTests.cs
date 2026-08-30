namespace Icod.CoreUtils.Df.Tests;

using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Traversal;
using Icod.CoreUtils.Shared.FileSystem.Usage;
using Icod.CoreUtils.Shared.Presentation;
using Xunit;

/// <summary>Verifies the public <c>df</c> command boundary.</summary>
public sealed class CommandTests {
	/// <summary>Verifies type, block-size, and total output over an injected provider.</summary>
	[Fact]
	public async Task ReportsSelectedFileSystemsAndTotal() {
		var provider = new FakeUsageProvider( CreateSnapshot( "/one", "dev-one", "testfs", 4096, 1024, 1024 ) );
		var output = new StringWriter();
		var error = new StringWriter();

		var exitCode = await Icod.CoreUtils.Df.Command.RunAsync(
			new[] { "--block-size=1K", "--print-type", "--total", "/one" },
			stdout: output,
			stderr: error,
			usageProvider: provider,
			environmentProvider: new FakeEnvironmentVariableProvider()
		);

		Assert.Equal( 0, exitCode );
		Assert.Contains( "Filesystem", output.ToString() );
		Assert.Contains( "testfs", output.ToString() );
		Assert.Contains( "dev-one", output.ToString() );
		Assert.Contains( "total", output.ToString() );
		Assert.Equal( string.Empty, error.ToString() );
	}

	/// <summary>Verifies explicit output fields and unavailable inode values.</summary>
	[Fact]
	public async Task PrintsControlledUnavailableInodes() {
		var snapshot = CreateSnapshot( "/one", "dev-one", "testfs", 4096, 1024, 1024 );
		var output = new StringWriter();
		var exitCode = await Icod.CoreUtils.Df.Command.RunAsync(
			new[] { "--output=source,itotal,iavail,target", "/one" },
			stdout: output,
			usageProvider: new FakeUsageProvider( snapshot ),
			environmentProvider: new FakeEnvironmentVariableProvider()
		);

		Assert.Equal( 0, exitCode );
		Assert.Contains( "Inodes", output.ToString() );
		Assert.Contains( "IFree", output.ToString() );
		Assert.Contains( "-", output.ToString() );
	}


	/// <summary>Verifies portable output ignores block-size environment overrides.</summary>
	[Fact]
	public async Task PortabilityUsesPosixDefaultUnits() {
		var output = new StringWriter();
		var environment = new FakeEnvironmentVariableProvider( new Dictionary<string, string?> {
			[ "DF_BLOCK_SIZE" ] = "1"
		} );

		var exitCode = await Icod.CoreUtils.Df.Command.RunAsync(
			new[] { "--portability", "/one" },
			stdout: output,
			usageProvider: new FakeUsageProvider( CreateSnapshot( "/one", "dev-one", "testfs", 4096, 1024, 1024 ) ),
			environmentProvider: environment
		);

		Assert.Equal( 0, exitCode );
		Assert.Contains( "1024-blocks", output.ToString() );
		Assert.Contains( "Capacity", output.ToString() );
		Assert.DoesNotContain( "4096-blocks", output.ToString() );
	}

	/// <summary>Verifies inode percentages are based on total inode capacity.</summary>
	[Fact]
	public async Task InodePercentageUsesTotalInodes() {
		var snapshot = CreateSnapshot(
			"/one", "dev-one", "testfs", 4096, 1024, 1024,
			totalInodes: 100, freeInodes: 20, availableInodes: 10
		);
		var output = new StringWriter();

		var exitCode = await Icod.CoreUtils.Df.Command.RunAsync(
			new[] { "--output=ipcent", "/one" },
			stdout: output,
			usageProvider: new FakeUsageProvider( snapshot ),
			environmentProvider: new FakeEnvironmentVariableProvider()
		);

		Assert.Equal( 0, exitCode );
		Assert.Contains( "80%", output.ToString() );
		Assert.DoesNotContain( "89%", output.ToString() );
	}

	/// <summary>Verifies synchronization options obey last-option precedence.</summary>
	[Fact]
	public void ParsesSynchronizationPrecedence() {
		Assert.False( DfOptionParser.Parse( new[] { "--sync", "--no-sync" } ).Synchronize );
		Assert.True( DfOptionParser.Parse( new[] { "--no-sync", "--sync" } ).Synchronize );
	}

	/// <summary>Verifies output-field validation and mutually exclusive legacy layouts.</summary>
	[Theory]
	[InlineData( "--output=" )]
	[InlineData( "--output --inodes" )]
	[InlineData( "--output=source --portability" )]
	[InlineData( "--output=source --print-type" )]
	public void RejectsInvalidOutputForms( string commandLine ) {
		Assert.Throws<DfUsageException>( () => DfOptionParser.Parse( commandLine.Split( ' ' ) ) );
	}

	/// <summary>Verifies the asynchronous command boundary exposes help.</summary>
	[Fact]
	public async Task ReportsHelp() {
		var output = new StringWriter();
		var exitCode = await Icod.CoreUtils.Df.Command.RunAsync( new[] { "--help" }, stdout: output );

		Assert.Equal( 0, exitCode );
		Assert.StartsWith( "Usage: df ", output.ToString() );
	}

	private static FileSystemUsageSnapshot CreateSnapshot(
		string mount,
		string device,
		string type,
		ulong total,
		ulong free,
		ulong available,
		ulong? totalInodes = null,
		ulong? freeInodes = null,
		ulong? availableInodes = null
	) {
		var information = new FileSystemInformation( mount, default ) {
			MountPoint = FileSystemMetadataValue<string>.Available( mount ),
			FileSystemType = FileSystemMetadataValue<string>.Available( type ),
			TotalBytes = FileSystemMetadataValue<ulong>.Available( total ),
			FreeBytes = FileSystemMetadataValue<ulong>.Available( free ),
			AvailableBytes = FileSystemMetadataValue<ulong>.Available( available )
		};
		return new FileSystemUsageSnapshot( mount, device, information, true ) {
			TotalInodes = totalInodes.HasValue
				? FileSystemMetadataValue<ulong>.Available( totalInodes.Value )
				: FileSystemMetadataValue<ulong>.Unavailable(),
			FreeInodes = freeInodes.HasValue
				? FileSystemMetadataValue<ulong>.Available( freeInodes.Value )
				: FileSystemMetadataValue<ulong>.Unavailable(),
			AvailableInodes = availableInodes.HasValue
				? FileSystemMetadataValue<ulong>.Available( availableInodes.Value )
				: FileSystemMetadataValue<ulong>.Unavailable()
		};
	}

	private sealed class FakeUsageProvider : IFileSystemUsageProvider {
		private readonly IReadOnlyList<FileSystemUsageSnapshot> snapshots;

		/// <summary>Initializes the fake provider.</summary>
		public FakeUsageProvider( params FileSystemUsageSnapshot[] snapshots ) {
			this.snapshots = snapshots;
		}

		/// <inheritdoc/>
		public Task<IReadOnlyList<FileSystemUsageSnapshot>> GetFileSystemsAsync(
			IReadOnlyList<string> paths,
			bool includeUnavailable,
			CancellationToken cancellationToken = default
		) {
			_ = paths;
			_ = includeUnavailable;
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult( snapshots );
		}
	}

	private sealed class FakeEnvironmentVariableProvider : IEnvironmentVariableProvider {
		private readonly IReadOnlyDictionary<string, string?> values;

		/// <summary>Initializes an empty environment.</summary>
		public FakeEnvironmentVariableProvider() : this( new Dictionary<string, string?>() ) { }

		/// <summary>Initializes the environment with selected values.</summary>
		public FakeEnvironmentVariableProvider( IReadOnlyDictionary<string, string?> values ) {
			this.values = values;
		}

		/// <inheritdoc/>
		public string? GetValue( string name ) => values.TryGetValue( name, out var value ) ? value : null;
	}
}
