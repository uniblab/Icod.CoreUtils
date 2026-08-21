namespace Icod.CoreUtils.Sync.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.FileSystem;
using Icod.CommandFramework.Platform;
using Xunit;

public sealed class SyncCommandTests {

	[Fact]
	public async Task HelpDescribesAllDocumentedOptions() {
		var result = await RunAsync(
			[ "--help" ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Contains( "--data", result.StandardOutput, StringComparison.Ordinal );
		Assert.Contains( "--file-system", result.StandardOutput, StringComparison.Ordinal );
		Assert.Contains( "FILE", result.StandardOutput, StringComparison.Ordinal );
		Assert.Equal( string.Empty, result.StandardError );
	}

	[Fact]
	public async Task VersionWritesVersionAndSucceeds() {
		var result = await RunAsync(
			[ "--version" ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Contains( "sync (Icod.CoreUtils)", result.StandardOutput, StringComparison.Ordinal );
		Assert.Equal( string.Empty, result.StandardError );
	}

	[Fact]
	public async Task HelpWriteFailuresReturnAControlledFailure() {
		using var input = new StringReader( string.Empty );
		using var output = new ThrowingTextWriter();
		using var error = new StringWriter();
		var context = new CommandContext(
			"sync",
			input,
			output,
			error
		);

		var exitCode = await Command.RunAsync(
			[ "--help" ],
			context,
			new FakeFileSystemOperations()
		);

		Assert.Equal( CommandExitCodes.Failure, exitCode );
		Assert.Contains( "simulated write failure", error.ToString(), StringComparison.Ordinal );
	}

	[Fact]
	public async Task UnknownOptionsFailWithoutInvokingTheProvider() {
		var provider = new FakeFileSystemOperations();
		var result = await RunAsync(
			[ "--unknown" ],
			provider
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "unknown", result.StandardError, StringComparison.OrdinalIgnoreCase );
		Assert.Empty( provider.FileFlushes );
		Assert.Empty( provider.FileSystemFlushes );
		Assert.Equal( 0, provider.GlobalFlushCount );
	}

	[Fact]
	public async Task NoOperandsRequestsOneGlobalFlush() {
		var provider = new FakeFileSystemOperations();
		var result = await RunAsync(
			[],
			provider
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 1, provider.GlobalFlushCount );
		Assert.Empty( provider.FileFlushes );
		Assert.Empty( provider.FileSystemFlushes );
	}

	[Fact]
	public async Task FileSystemOptionWithoutOperandsRequestsOneGlobalFlush() {
		var provider = new FakeFileSystemOperations();
		var result = await RunAsync(
			[ "--file-system" ],
			provider
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 1, provider.GlobalFlushCount );
		Assert.Empty( provider.FileSystemFlushes );
	}

	[Fact]
	public async Task DefaultOperandsRequestDataAndMetadataFlushes() {
		var provider = new FakeFileSystemOperations();
		var result = await RunAsync(
			[ "first", "second" ],
			provider
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal(
			new[] {
				new FileFlushRequest( "first", FileFlushMode.DataAndMetadata ),
				new FileFlushRequest( "second", FileFlushMode.DataAndMetadata ),
			},
			provider.FileFlushes
		);
		Assert.Equal( 0, provider.GlobalFlushCount );
	}

	[Fact]
	public async Task DataOptionRequestsDataOnlyFlushes() {
		var provider = new FakeFileSystemOperations();
		var result = await RunAsync(
			[ "target", "-d" ],
			provider
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		var request = Assert.Single( provider.FileFlushes );
		Assert.Equal( "target", request.Path );
		Assert.Equal( FileFlushMode.DataOnly, request.Mode );
	}

	[Fact]
	public async Task DataOptionRequiresAnOperand() {
		var provider = new FakeFileSystemOperations();
		var result = await RunAsync(
			[ "--data" ],
			provider
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "needs at least one argument", result.StandardError, StringComparison.Ordinal );
		Assert.Equal( 0, provider.GlobalFlushCount );
	}

	[Fact]
	public async Task DataAndFileSystemOptionsAreMutuallyExclusive() {
		var provider = new FakeFileSystemOperations();
		var result = await RunAsync(
			[ "--data", "--file-system", "target" ],
			provider
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "cannot specify both", result.StandardError, StringComparison.Ordinal );
		Assert.Empty( provider.FileFlushes );
		Assert.Empty( provider.FileSystemFlushes );
	}

	[Fact]
	public async Task FileSystemOptionFlushesEachContainingFileSystemWhenSupported() {
		var provider = new FakeFileSystemOperations {
			CapabilitiesValue = CreateCapabilities(
				supportsFileSystemFlush: true,
				supportsGlobalFlush: true
			),
		};
		var result = await RunAsync(
			[ "-f", "first", "second" ],
			provider
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( new[] { "first", "second" }, provider.FileSystemFlushes );
		Assert.Equal( 0, provider.GlobalFlushCount );
	}

	[Fact]
	public async Task FileSystemOptionFallsBackToOneGlobalFlushWhenSyncfsIsUnavailable() {
		var provider = new FakeFileSystemOperations {
			CapabilitiesValue = CreateCapabilities(
				supportsFileSystemFlush: false,
				supportsGlobalFlush: true
			),
		};
		var result = await RunAsync(
			[ "--file-system", "first", "second" ],
			provider
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 1, provider.GlobalFlushCount );
		Assert.Empty( provider.FileSystemFlushes );
		Assert.Empty( provider.FileFlushes );
	}

	[Fact]
	public async Task UnsupportedGlobalFlushProducesAControlledFailure() {
		var provider = new FakeFileSystemOperations {
			GlobalResult = PlatformOperationResult.Unsupported(
				"global flushing is unavailable"
			),
		};
		var result = await RunAsync(
			[],
			provider
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "global flushing is unavailable", result.StandardError, StringComparison.Ordinal );
	}


	[Fact]
	public async Task UnsupportedPerFileFlushNamesTheAffectedOperand() {
		var provider = new FakeFileSystemOperations();
		provider.PathResults[ "target" ] = PlatformOperationResult.Unsupported(
			"data-only flushing is unavailable"
		);
		var result = await RunAsync(
			[ "--data", "target" ],
			provider
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "target", result.StandardError, StringComparison.Ordinal );
		Assert.Contains( "unavailable", result.StandardError, StringComparison.Ordinal );
	}

	[Fact]
	public async Task PerFileFailuresDoNotPreventLaterOperands() {
		var provider = new FakeFileSystemOperations();
		provider.PathResults[ "first" ] = PlatformOperationResult.Failure(
			"cannot synchronize 'first': simulated failure"
		);
		var result = await RunAsync(
			[ "first", "second" ],
			provider
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Collection(
			provider.FileFlushes,
			_ => { },
			_ => { }
		);
		Assert.Contains( "simulated failure", result.StandardError, StringComparison.Ordinal );
	}

	[Fact]
	public async Task PathnameWildcardsUseTheSharedExpansionPolicy() {
		using var temporary = new TemporaryDirectory();
		var first = temporary.CreateFile( "a.bin" );
		var second = temporary.CreateFile( "b.bin" );
		_ = temporary.CreateFile( "ignored.txt" );
		var provider = new FakeFileSystemOperations();
		var result = await RunAsync(
			[ temporary.PathFor( "*.bin" ) ],
			provider
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal(
			new[] { first, second },
			provider.FileFlushes.Select( item => item.Path ).ToArray()
		);
	}

	[Fact]
	public async Task PreCanceledCommandsReturnTheCanceledExitCode() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var provider = new FakeFileSystemOperations();
		var result = await RunAsync(
			[ "target" ],
			provider,
			cancellation.Token
		);

		Assert.Equal( CommandExitCodes.Canceled, result.ExitCode );
		Assert.Empty( provider.FileFlushes );
	}

	[Fact]
	public async Task ProviderCancellationReturnsTheCanceledExitCode() {
		var provider = new FakeFileSystemOperations {
			CancelFileFlush = true,
		};
		var result = await RunAsync(
			[ "target" ],
			provider
		);

		Assert.Equal( CommandExitCodes.Canceled, result.ExitCode );
	}


	[Fact]
	public async Task SystemProviderReportsDataOnlyCapabilityDeterministically() {
		if (
			!OperatingSystem.IsWindows()
			&& !OperatingSystem.IsLinux()
			&& !OperatingSystem.IsMacOS()
			&& !OperatingSystem.IsFreeBSD()
		) {
			return;
		}
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "data-only.bin" );
		await File.WriteAllBytesAsync(
			path,
			[ 1, 2, 3, 4 ]
		);

		var result = await RunAsync(
			[ "--data", path ],
			SystemFileSystemOperations.Instance
		);

		if ( SystemFileSystemOperations.Instance.Capabilities.SupportsDataOnlyFileFlush ) {
			Assert.Equal( CommandExitCodes.Success, result.ExitCode );
			Assert.Equal( string.Empty, result.StandardError );
		} else {
			Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
			Assert.Contains( "unavailable", result.StandardError, StringComparison.OrdinalIgnoreCase );
		}
	}

	[Fact]
	public async Task SystemProviderFlushesARegularFileOnSupportedCiPlatforms() {
		if (
			!OperatingSystem.IsWindows()
			&& !OperatingSystem.IsLinux()
			&& !OperatingSystem.IsMacOS()
			&& !OperatingSystem.IsFreeBSD()
		) {
			return;
		}
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin" );
		await File.WriteAllBytesAsync(
			path,
			[ 1, 2, 3, 4 ]
		);

		var result = await RunAsync(
			[ path ],
			SystemFileSystemOperations.Instance
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( string.Empty, result.StandardError );
	}

	private static FileSystemCapabilities CreateCapabilities(
		bool supportsFileSystemFlush,
		bool supportsGlobalFlush
	) => new(
		SupportsDataOnlyFileFlush: true,
		SupportsDataAndMetadataFileFlush: true,
		SupportsFileSystemFlush: supportsFileSystemFlush,
		SupportsGlobalFlush: supportsGlobalFlush,
		SupportsSparseExtension: true,
		SupportsAllocatedRangeQuery: true
	);

	private static async Task<CommandRunResult> RunAsync(
		string[] arguments,
		IFileSystemOperations? provider = null,
		CancellationToken cancellationToken = default
	) {
		using var input = new StringReader( string.Empty );
		using var output = new StringWriter();
		using var error = new StringWriter();
		var context = new CommandContext(
			"sync",
			input,
			output,
			error,
			cancellationToken: cancellationToken
		);
		var exitCode = await Command.RunAsync(
			arguments,
			context,
			provider ?? new FakeFileSystemOperations()
		);
		return new CommandRunResult(
			exitCode,
			output.ToString(),
			error.ToString()
		);
	}

	private sealed class FakeFileSystemOperations : IFileSystemOperations {

		public FileSystemCapabilities CapabilitiesValue {
			get;
			init;
		} = CreateCapabilities(
			supportsFileSystemFlush: true,
			supportsGlobalFlush: true
		);
		public FileSystemCapabilities Capabilities => this.CapabilitiesValue;
		public List<FileFlushRequest> FileFlushes { get; } = [];
		public List<string> FileSystemFlushes { get; } = [];
		public Dictionary<string, PlatformOperationResult> PathResults { get; } = new(
			StringComparer.Ordinal
		);
		public int GlobalFlushCount { get; private set; }
		public PlatformOperationResult GlobalResult {
			get;
			init;
		} = PlatformOperationResult.Success();
		public bool CancelFileFlush {
			get;
			init;
		}

		public ValueTask<PlatformOperationResult> FlushFileAsync(
			FileStream file,
			FileFlushMode mode,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult(
				PlatformOperationResult.Success()
			);
		}

		public ValueTask<PlatformOperationResult> FlushFileAsync(
			string path,
			FileFlushMode mode,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( this.CancelFileFlush ) {
				throw new OperationCanceledException();
			}
			this.FileFlushes.Add(
				new FileFlushRequest(
					path,
					mode
				)
			);
			return ValueTask.FromResult(
				this.PathResults.TryGetValue(
					path,
					out var result
				)
					? result
					: PlatformOperationResult.Success()
			);
		}

		public ValueTask<PlatformOperationResult> FlushFileSystemAsync(
			string path,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.FileSystemFlushes.Add(
				path
			);
			return ValueTask.FromResult(
				this.PathResults.TryGetValue(
					path,
					out var result
				)
					? result
					: PlatformOperationResult.Success()
			);
		}

		public ValueTask<PlatformOperationResult> FlushAllFileSystemsAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.GlobalFlushCount++;
			return ValueTask.FromResult(
				this.GlobalResult
			);
		}

		public ValueTask<PlatformOperationResult<SparseExtensionInfo>> ExtendSparseAsync(
			FileStream file,
			long newLength,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult(
			PlatformOperationResult<SparseExtensionInfo>.Unsupported(
				"not used by sync tests"
			)
		);

		public ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync(
			FileStream file,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult(
			PlatformOperationResult<FileAllocationMap>.Unsupported(
				"not used by sync tests"
			)
		);

		public ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync(
			string path,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult(
			PlatformOperationResult<FileAllocationMap>.Unsupported(
				"not used by sync tests"
			)
		);
	}

	private sealed class ThrowingTextWriter : TextWriter {

		public override Encoding Encoding => Encoding.UTF8;

		public override Task WriteAsync(
			ReadOnlyMemory<char> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromException(
				new IOException( "simulated write failure" )
			);
		}
	}

	private sealed class TemporaryDirectory : IDisposable {

		public string PathValue { get; }

		public TemporaryDirectory() {
			this.PathValue = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				System.String.Concat(
					"Icod.CoreUtils.Sync.Tests-",
					Guid.NewGuid().ToString( "N" )
				)
			);
			Directory.CreateDirectory(
				this.PathValue
			);
		}

		public string CreateFile(
			string name
		) {
			var path = this.PathFor(
				name
			);
			using var file = File.Create(
				path
			);
			return path;
		}

		public string PathFor(
			string name
		) => System.IO.Path.Combine(
			this.PathValue,
			name
		);

		public void Dispose() {
			try {
				Directory.Delete(
					this.PathValue,
					recursive: true
				);
			} catch ( IOException ) {
			} catch ( UnauthorizedAccessException ) {
			}
		}
	}

	private sealed record CommandRunResult(
		int ExitCode,
		string StandardOutput,
		string StandardError
	);

	private sealed record FileFlushRequest(
		string Path,
		FileFlushMode Mode
	);
}
