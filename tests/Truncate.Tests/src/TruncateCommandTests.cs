namespace Icod.CoreUtils.Truncate.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.Platform;
using Xunit;

public sealed class TruncateCommandTests {

	[Fact]
	public async Task HelpDescribesTheCompleteSizeGrammar() {
		var result = await RunAsync(
			[ "--help" ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Contains( "--no-create", result.StandardOutput, StringComparison.Ordinal );
		Assert.Contains( "--io-blocks", result.StandardOutput, StringComparison.Ordinal );
		Assert.Contains( "--reference", result.StandardOutput, StringComparison.Ordinal );
		Assert.Contains( "round down or up", result.StandardOutput, StringComparison.Ordinal );
		Assert.Equal( string.Empty, result.StandardError );
	}

	[Fact]
	public async Task VersionWritesVersionAndSucceeds() {
		var result = await RunAsync(
			[ "--version" ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Contains( "truncate (Icod.CoreUtils)", result.StandardOutput, StringComparison.Ordinal );
		Assert.Equal( string.Empty, result.StandardError );
	}

	[Fact]
	public async Task HelpWriteFailuresReturnAControlledFailure() {
		using var input = new StringReader( string.Empty );
		using var output = new ThrowingTextWriter();
		using var error = new StringWriter();
		var context = new CommandContext(
			"truncate",
			input,
			output,
			error
		);

		var exitCode = await Command.RunAsync(
			[ "--help" ],
			context,
			new FakeTruncatePlatform()
		);

		Assert.Equal( CommandExitCodes.Failure, exitCode );
		Assert.Contains( "simulated write failure", error.ToString(), StringComparison.Ordinal );
	}

	[Fact]
	public async Task MissingSizeAndReferenceFails() {
		using var temporary = new TemporaryDirectory();
		var result = await RunAsync(
			[ temporary.PathFor( "target" ) ]
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "--size", result.StandardError, StringComparison.Ordinal );
	}

	[Fact]
	public async Task MissingFileOperandFails() {
		var result = await RunAsync(
			[ "--size=10" ]
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "missing file operand", result.StandardError, StringComparison.Ordinal );
	}

	[Fact]
	public async Task AbsoluteSizeCreatesAFile() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "created.bin" );
		var result = await RunAsync(
			[ "--size=4096", path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 4096, new FileInfo( path ).Length );
		Assert.Equal( string.Empty, result.StandardOutput );
		Assert.Equal( string.Empty, result.StandardError );
	}

	[Fact]
	public async Task NoCreateSilentlyIgnoresAMissingFile() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "missing.bin" );
		var result = await RunAsync(
			[ "--no-create", "--size=10", path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.False( File.Exists( path ) );
		Assert.Equal( string.Empty, result.StandardError );
	}

	[Fact]
	public async Task NoCreateSilentlyIgnoresAMissingParentDirectory() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( System.IO.Path.Combine( "missing", "target.bin" ) );
		var result = await RunAsync(
			[ "-c", "--size=10", path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.False( File.Exists( path ) );
		Assert.Equal( string.Empty, result.StandardError );
	}

	[Fact]
	public async Task AbsoluteSizeShrinksExistingData() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "target.bin" );
		await File.WriteAllBytesAsync( path, Enumerable.Range( 0, 32 ).Select( value => ( byte )value ).ToArray() );

		var result = await RunAsync(
			[ "-s", "8", path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( Enumerable.Range( 0, 8 ).Select( value => ( byte )value ).ToArray(), await File.ReadAllBytesAsync( path ) );
	}

	[Fact]
	public async Task ExtensionPreservesExistingDataAndReadsAsZero() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "target.bin" );
		await File.WriteAllBytesAsync(
			path,
			[ 1, 2, 3 ]
		);

		var result = await RunAsync(
			[ "--size=8", path ]
		);
		var contents = await File.ReadAllBytesAsync(
			path
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( new byte[] { 1, 2, 3, 0, 0, 0, 0, 0 }, contents );
	}

	[Fact]
	public async Task PositiveRelativeSizeExtendsFromTheCurrentLength() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", 10 );
		var result = await RunAsync(
			[ "-s", "+7", path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 17, new FileInfo( path ).Length );
	}

	[Fact]
	public async Task NegativeRelativeSizeClampsAtZero() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", 10 );
		var result = await RunAsync(
			[ "-s-20", path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 0, new FileInfo( path ).Length );
	}

	[Theory]
	[InlineData( "<8", 12, 8 )]
	[InlineData( "<20", 12, 12 )]
	[InlineData( ">8", 12, 12 )]
	[InlineData( ">20", 12, 20 )]
	public async Task AtMostAndAtLeastModifiersAreConditional(
		string size,
		int initialLength,
		int expectedLength
	) {
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", initialLength );
		var result = await RunAsync(
			[ "--size", size, path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( expectedLength, new FileInfo( path ).Length );
	}

	[Theory]
	[InlineData( "/8", 19, 16 )]
	[InlineData( "%8", 19, 24 )]
	[InlineData( "/8", 16, 16 )]
	[InlineData( "%8", 16, 16 )]
	public async Task RoundingModifiersUseSizeMultiples(
		string size,
		int initialLength,
		int expectedLength
	) {
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", initialLength );
		var result = await RunAsync(
			[ "-s", size, path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( expectedLength, new FileInfo( path ).Length );
	}

	[Fact]
	public async Task ReferenceCopiesTheReferenceLength() {
		using var temporary = new TemporaryDirectory();
		var reference = temporary.CreateFile( "reference.bin", 37 );
		var target = temporary.CreateFile( "target.bin", 4 );
		var result = await RunAsync(
			[ "--reference", reference, target ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 37, new FileInfo( target ).Length );
	}

	[Fact]
	public async Task MissingReferenceFailsBeforeTargetsAreOpened() {
		using var temporary = new TemporaryDirectory();
		var reference = temporary.PathFor( "missing-reference.bin" );
		var target = temporary.PathFor( "target.bin" );
		var result = await RunAsync(
			[ "--reference", reference, target ]
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "cannot stat", result.StandardError, StringComparison.Ordinal );
		Assert.False( File.Exists( target ) );
	}

	[Fact]
	public async Task RelativeReferenceSizeIsBasedOnTheReferenceNotTheTarget() {
		using var temporary = new TemporaryDirectory();
		var reference = temporary.CreateFile( "reference.bin", 37 );
		var target = temporary.CreateFile( "target.bin", 100 );
		var result = await RunAsync(
			[ "--reference", reference, "--size=+5", target ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 42, new FileInfo( target ).Length );
	}

	[Fact]
	public async Task AbsoluteSizeCannotBeCombinedWithReference() {
		using var temporary = new TemporaryDirectory();
		var reference = temporary.CreateFile( "reference.bin", 37 );
		var target = temporary.CreateFile( "target.bin", 10 );
		var result = await RunAsync(
			[ "--reference", reference, "--size=5", target ]
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "absolute", result.StandardError, StringComparison.Ordinal );
		Assert.Equal( 10, new FileInfo( target ).Length );
	}

	[Theory]
	[InlineData( "1K", 1024 )]
	[InlineData( "1k", 1024 )]
	[InlineData( "1KB", 1000 )]
	[InlineData( "1KiB", 1024 )]
	[InlineData( "2M", 2097152 )]
	[InlineData( "2MB", 2000000 )]
	public async Task DocumentedSuffixesUseTheCorrectRadix(
		string size,
		int expectedLength
	) {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "target.bin" );
		var result = await RunAsync(
			[ "--size", size, path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( expectedLength, new FileInfo( path ).Length );
	}

	[Theory]
	[InlineData( "K", 1024 )]
	[InlineData( "kB", 1000 )]
	[InlineData( "kiB", 1024 )]
	public async Task GnuCompatibleBareAndLowercaseSuffixFormsAreAccepted(
		string size,
		int expectedLength
	) {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "target.bin" );
		var result = await RunAsync(
			[ "--size", size, path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( expectedLength, new FileInfo( path ).Length );
	}

	[Fact]
	public async Task ZeroWithAnOtherwiseHugeSuffixIsValid() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", 4 );
		var result = await RunAsync(
			[ "--size=0Q", path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 0, new FileInfo( path ).Length );
	}

	[Fact]
	public async Task IoBlocksMultipliesSizeByThePerFileBlockSize() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "target.bin" );
		var platform = new FakeTruncatePlatform {
			IoBlockSize = 4096,
		};
		var result = await RunAsync(
			[ "--io-blocks", "--size=3", path ],
			platform
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 12288, new FileInfo( path ).Length );
		Assert.Equal( 1, platform.IoBlockSizeQueryCount );
	}

	[Fact]
	public async Task IoBlocksScalesARelativeReferenceAdjustmentOnly() {
		using var temporary = new TemporaryDirectory();
		var reference = temporary.CreateFile( "reference.bin", 100 );
		var target = temporary.CreateFile( "target.bin", 1 );
		var platform = new FakeTruncatePlatform {
			IoBlockSize = 8,
		};
		var result = await RunAsync(
			[ "--io-blocks", "--reference", reference, "--size=+2", target ],
			platform
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 116, new FileInfo( target ).Length );
		Assert.Equal( 1, platform.IoBlockSizeQueryCount );
	}

	[Fact]
	public async Task IoBlocksRequiresSize() {
		using var temporary = new TemporaryDirectory();
		var reference = temporary.CreateFile( "reference.bin", 8 );
		var target = temporary.PathFor( "target.bin" );
		var result = await RunAsync(
			[ "--io-blocks", "--reference", reference, target ]
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "--size was not", result.StandardError, StringComparison.Ordinal );
	}

	[Fact]
	public async Task IoBlockDiscoveryFailuresBecomePerFileDiagnostics() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "target.bin" );
		var platform = new FakeTruncatePlatform {
			IoBlockSizeFailure = "simulated block-size failure",
		};
		var result = await RunAsync(
			[ "--io-blocks", "--size=3", path ],
			platform
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "simulated block-size failure", result.StandardError, StringComparison.Ordinal );
	}

	[Fact]
	public async Task IoBlockMultiplicationOverflowIsReportedPerFile() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "target.bin" );
		var platform = new FakeTruncatePlatform {
			IoBlockSize = 4096,
		};
		var result = await RunAsync(
			[ "--io-blocks", "--size=9223372036854775807", path ],
			platform
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "overflow", result.StandardError, StringComparison.Ordinal );
	}

	[Theory]
	[InlineData( "/0", "division by zero" )]
	[InlineData( "%0", "division by zero" )]
	[InlineData( "<+1", "multiple relative modifiers" )]
	[InlineData( "+-1", "invalid number" )]
	[InlineData( "+K", "invalid number" )]
	[InlineData( "1XB", "invalid number" )]
	[InlineData( "1Q", "size is too large" )]
	public async Task InvalidSizesFailBeforeOpeningTargets(
		string size,
		string expectedDiagnostic
	) {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "target.bin" );
		var result = await RunAsync(
			[ "--size", size, path ]
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( expectedDiagnostic, result.StandardError, StringComparison.Ordinal );
		Assert.False( File.Exists( path ) );
	}

	[Fact]
	public async Task RepeatedSizeWithoutAModifierInheritsTheEarlierMode() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", 20 );
		var result = await RunAsync(
			[ "--size=<5", "--size=12", path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 12, new FileInfo( path ).Length );
	}

	[Fact]
	public async Task RepeatedSizeWithoutAModifierInheritsRelativeMode() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", 20 );
		var result = await RunAsync(
			[ "--size=+5", "--size=7", path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 27, new FileInfo( path ).Length );
	}

	[Fact]
	public async Task RepeatedSignedRelativeModifiersAreRejected() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", 20 );
		var result = await RunAsync(
			[ "--size=+5", "--size=-7", path ]
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "multiple relative modifiers", result.StandardError, StringComparison.Ordinal );
		Assert.Equal( 20, new FileInfo( path ).Length );
	}

	[Fact]
	public async Task RepeatedExplicitSizeModifierReplacesTheEarlierMode() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", 20 );
		var result = await RunAsync(
			[ "--size=<5", "--size=>24", path ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 24, new FileInfo( path ).Length );
	}

	[Fact]
	public async Task OptionsArePermutedAroundOperands() {
		using var temporary = new TemporaryDirectory();
		var first = temporary.PathFor( "first.bin" );
		var second = temporary.PathFor( "second.bin" );
		var result = await RunAsync(
			[ first, "--size=11", second ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 11, new FileInfo( first ).Length );
		Assert.Equal( 11, new FileInfo( second ).Length );
	}

	[Fact]
	public async Task WildcardOperandsExpandDeterministically() {
		using var temporary = new TemporaryDirectory();
		var first = temporary.CreateFile( "first.dat", 1 );
		var second = temporary.CreateFile( "second.dat", 2 );
		temporary.CreateFile( "other.bin", 3 );
		var pattern = temporary.PathFor( "*.dat" );
		var result = await RunAsync(
			[ "--size=9", pattern ]
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 9, new FileInfo( first ).Length );
		Assert.Equal( 9, new FileInfo( second ).Length );
	}

	[Fact]
	public async Task AFailureDoesNotPreventLaterFilesFromBeingProcessed() {
		using var temporary = new TemporaryDirectory();
		var directory = temporary.PathFor( "directory" );
		Directory.CreateDirectory( directory );
		var target = temporary.PathFor( "target.bin" );
		var result = await RunAsync(
			[ "--size=13", directory, target ]
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Equal( 13, new FileInfo( target ).Length );
		Assert.Contains( directory, result.StandardError, StringComparison.Ordinal );
	}

	[Fact]
	public async Task GrowthUsesTheSparseAwarePlatformBoundary() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "target.bin" );
		var platform = new FakeTruncatePlatform();
		var result = await RunAsync(
			[ "--size=1048576", path ],
			platform
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 1, platform.SetLengthCount );
		Assert.Equal( 1048576, platform.LastLength );
		Assert.Equal( 1048576, new FileInfo( path ).Length );
	}

	[Fact]
	public async Task AnUnchangedLengthDoesNotInvokeThePlatformMutation() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", 16 );
		var platform = new FakeTruncatePlatform();
		var result = await RunAsync(
			[ "--size=16", path ],
			platform
		);

		Assert.Equal( CommandExitCodes.Success, result.ExitCode );
		Assert.Equal( 0, platform.SetLengthCount );
	}

	[Fact]
	public async Task PlatformFailuresBecomeConventionalDiagnostics() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", 4 );
		var platform = new FakeTruncatePlatform {
			SetLengthFailure = "simulated failure",
		};
		var result = await RunAsync(
			[ "--size=8", path ],
			platform
		);

		Assert.Equal( CommandExitCodes.Failure, result.ExitCode );
		Assert.Contains( "simulated failure", result.StandardError, StringComparison.Ordinal );
	}

	[Fact]
	public async Task PreCanceledCommandsReturnTheCanceledExitCode() {
		using var temporary = new TemporaryDirectory();
		var path = temporary.PathFor( "target.bin" );
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		var result = await RunAsync(
			[ "--size=10", path ],
			cancellationToken: cancellation.Token
		);

		Assert.Equal( CommandExitCodes.Canceled, result.ExitCode );
		Assert.False( File.Exists( path ) );
	}

	[Fact]
	public async Task SystemProviderReportsAPositiveIoBlockSizeOnSupportedCiPlatforms() {
		if (
			!OperatingSystem.IsWindows()
			&& !OperatingSystem.IsLinux()
			&& !OperatingSystem.IsMacOS()
			&& !OperatingSystem.IsFreeBSD()
		) {
			return;
		}
		using var temporary = new TemporaryDirectory();
		var path = temporary.CreateFile( "target.bin", 1 );
		await using var file = new FileStream(
			path,
			FileMode.Open,
			FileAccess.Write,
			FileShare.ReadWrite | FileShare.Delete,
			4096,
			FileOptions.Asynchronous | FileOptions.RandomAccess
		);

		var result = await SystemTruncatePlatform.Instance.GetIoBlockSizeAsync(
			file,
			path
		);

		Assert.True( result.Succeeded, result.Message );
		Assert.True( 0 < result.Value );
	}

	private static async Task<CommandRunResult> RunAsync(
		string[] arguments,
		ITruncatePlatform? platform = null,
		CancellationToken cancellationToken = default
	) {
		using var input = new StringReader( string.Empty );
		using var output = new StringWriter();
		using var error = new StringWriter();
		var context = new CommandContext(
			"truncate",
			input,
			output,
			error,
			cancellationToken: cancellationToken
		);
		var exitCode = await Command.RunAsync(
			arguments,
			context,
			platform ?? SystemTruncatePlatform.Instance
		);
		return new CommandRunResult(
			exitCode,
			output.ToString(),
			error.ToString()
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

	private sealed class FakeTruncatePlatform : ITruncatePlatform {

		public long IoBlockSize {
			get;
			init;
		} = 4096;
		public string? IoBlockSizeFailure {
			get;
			init;
		}
		public int IoBlockSizeQueryCount {
			get;
			private set;
		}
		public long LastLength {
			get;
			private set;
		}
		public int SetLengthCount {
			get;
			private set;
		}
		public string? SetLengthFailure {
			get;
			init;
		}

		public ValueTask<PlatformOperationResult<long>> GetIoBlockSizeAsync(
			FileStream file,
			string path,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.IoBlockSizeQueryCount++;
			if ( null != this.IoBlockSizeFailure ) {
				return ValueTask.FromResult(
					PlatformOperationResult<long>.Failure(
						this.IoBlockSizeFailure
					)
				);
			}
			return ValueTask.FromResult(
				PlatformOperationResult<long>.Success(
					this.IoBlockSize
				)
			);
		}

		public ValueTask<PlatformOperationResult> SetLengthAsync(
			FileStream file,
			long length,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.SetLengthCount++;
			this.LastLength = length;
			if ( null != this.SetLengthFailure ) {
				return ValueTask.FromResult(
					PlatformOperationResult.Failure(
						this.SetLengthFailure
					)
				);
			}
			file.SetLength(
				length
			);
			return ValueTask.FromResult(
				PlatformOperationResult.Success()
			);
		}
	}

	private sealed class TemporaryDirectory : IDisposable {

		public string PathValue {
			get;
		}

		public TemporaryDirectory() {
			this.PathValue = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				String.Concat( "Icod.CoreUtils.Truncate.Tests-", Guid.NewGuid().ToString( "N" ) )
			);
			Directory.CreateDirectory(
				this.PathValue
			);
		}

		public string CreateFile(
			string name,
			int length
		) {
			var path = this.PathFor(
				name
			);
			using var file = new FileStream(
				path,
				FileMode.CreateNew,
				FileAccess.Write,
				FileShare.ReadWrite | FileShare.Delete
			);
			file.SetLength(
				length
			);
			return path;
		}

		public string PathFor(
			string name
		) {
			return System.IO.Path.Combine(
				this.PathValue,
				name
			);
		}

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
}
