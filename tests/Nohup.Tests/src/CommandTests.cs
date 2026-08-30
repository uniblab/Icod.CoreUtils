namespace Icod.CoreUtils.Nohup.Tests;

using System.Text;
using Icod.Processes;
using Icod.Terminal;
using Xunit;

/// <summary>Tests GNU <c>nohup</c> command behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies ordinary redirected streams pass through unchanged.</summary>
	[Fact]
	public async Task PreservesAlreadyRedirectedStreams() {
		var input = new MemoryStream();
		var output = new MemoryStream();
		var error = new MemoryStream();
		var executor = new FakeExecutor();
		var exitCode = await Command.RunAsync( [ "tool", "a b" ], input, output, error, new FakeTerminalProvider(), executor );
		Assert.Equal( 0, exitCode );
		Assert.Same( input, executor.Options!.StandardInput );
		Assert.Same( output, executor.Options.StandardOutput );
		Assert.Same( error, executor.Options.StandardError );
		Assert.Equal( [ "a b" ], executor.Options.Arguments );
		Assert.Equal( ProcessCancellationPolicy.LeaveRunning, executor.Options.CancellationPolicy );
	}

	/// <summary>Verifies terminal output uses one append file for stdout and terminal stderr.</summary>
	[Fact]
	public async Task RedirectsTerminalOutputAndErrorToNohupOut() {
		var terminal = new FakeTerminalProvider { Input = true, Output = true, Error = true };
		var executor = new FakeExecutor();
		var files = new FakeOutputFiles();
		var diagnostics = new MemoryStream();
		var exitCode = await Command.RunAsync( [ "tool" ], stderr: diagnostics, terminalProvider: terminal, processExecutor: executor, outputFileProvider: files );
		Assert.Equal( 0, exitCode );
		if ( OperatingSystem.IsWindows() ) {
			Assert.NotNull( executor.Options!.StandardInput );
			Assert.False( executor.Options.UseUnreadableStandardInput );
		} else {
			Assert.Null( executor.Options!.StandardInput );
			Assert.True( executor.Options.UseUnreadableStandardInput );
		}
		Assert.Same( executor.Options.StandardOutput, executor.Options.StandardError );
		Assert.Equal( "nohup.out", files.Paths.Single() );
		Assert.Contains( "appending output", Encoding.UTF8.GetString( diagnostics.ToArray() ), StringComparison.Ordinal );
	}

	/// <summary>Verifies a failed current-directory output falls back to HOME.</summary>
	[Fact]
	public async Task FallsBackToHomeNohupOut() {
		var terminal = new FakeTerminalProvider { Output = true };
		var executor = new FakeExecutor();
		var files = new FakeOutputFiles { FailFirst = true };
		var environment = ProcessEnvironment.CreateEmptyBuilder().Set( "HOME", "/home/test" ).Build();
		var exitCode = await Command.RunAsync( [ "tool" ], stderr: new MemoryStream(), terminalProvider: terminal, processExecutor: executor, outputFileProvider: files, sourceEnvironment: environment );
		Assert.Equal( 0, exitCode );
		Assert.Equal( [ "nohup.out", System.IO.Path.Combine( "/home/test", "nohup.out" ) ], files.Paths );
	}

	/// <summary>Verifies terminal stderr follows an already redirected stdout.</summary>
	[Fact]
	public async Task RedirectsTerminalErrorToExistingStandardOutput() {
		var terminal = new FakeTerminalProvider { Error = true };
		var executor = new FakeExecutor();
		var output = new MemoryStream();
		var exitCode = await Command.RunAsync( [ "tool" ], stdout: output, stderr: new MemoryStream(), terminalProvider: terminal, processExecutor: executor );
		Assert.Equal( 0, exitCode );
		Assert.Same( output, executor.Options!.StandardError );
	}

	/// <summary>Verifies a closed stdout with terminal stderr appends stderr to nohup.out while preserving closed child stdout.</summary>
	[Fact]
	public async Task RedirectsTerminalErrorWhenStandardOutputIsClosed() {
		var terminal = new FakeTerminalProvider { Error = true };
		var executor = new FakeExecutor();
		var files = new FakeOutputFiles();
		var standardStreams = new FakeStandardStreamStateProvider { OutputClosed = true };
		var exitCode = await Command.RunAsync(
			[ "tool" ],
			stderr: new MemoryStream(),
			terminalProvider: terminal,
			processExecutor: executor,
			outputFileProvider: files,
			standardStreamStateProvider: standardStreams
		);
		Assert.Equal( 0, exitCode );
		Assert.Null( executor.Options!.StandardOutput );
		Assert.NotNull( executor.Options.StandardError );
		Assert.Equal( "nohup.out", files.Paths.Single() );
		Assert.Equal( 1, standardStreams.Reservations );
	}

	/// <summary>Verifies the system output provider appends rather than truncating an existing destination.</summary>
	[Fact]
	public async Task SystemOutputProviderAppends() {
		var directory = System.IO.Path.Combine( System.IO.Path.GetTempPath(), $"icod-nohup-{Guid.NewGuid():N}" );
		Directory.CreateDirectory( directory );
		var path = System.IO.Path.Combine( directory, "nohup.out" );
		try {
			await File.WriteAllTextAsync( path, "first" );
			await using ( var destination = SystemNohupOutputFileProvider.Instance.OpenAppend( path ) ) {
				await destination.Stream.WriteAsync( Encoding.UTF8.GetBytes( "second" ) );
			}
			Assert.Equal( "firstsecond", await File.ReadAllTextAsync( path ) );
		} finally {
			Directory.Delete( directory, true );
		}
	}

	/// <summary>Verifies newly created POSIX output is exactly user-readable and user-writable.</summary>
	[Fact]
	public async Task SystemOutputProviderCreatesPrivatePosixFile() {
		if ( OperatingSystem.IsWindows() ) return;
		var directory = System.IO.Path.Combine( System.IO.Path.GetTempPath(), $"icod-nohup-mode-{Guid.NewGuid():N}" );
		Directory.CreateDirectory( directory );
		var path = System.IO.Path.Combine( directory, "nohup.out" );
		try {
			await using ( var destination = SystemNohupOutputFileProvider.Instance.OpenAppend( path ) ) {
				await destination.Stream.WriteAsync( Encoding.UTF8.GetBytes( "x" ) );
			}
			const UnixFileMode permissionBits = UnixFileMode.UserRead | UnixFileMode.UserWrite
				| UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupWrite
				| UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite
				| UnixFileMode.OtherExecute;
			Assert.Equal( UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode( path ) & permissionBits );
		} finally {
			Directory.Delete( directory, true );
		}
	}

	/// <summary>Verifies POSIXLY_CORRECT selects the POSIX internal-failure status.</summary>
	[Fact]
	public async Task PosixlyCorrectUsesStatus127ForInternalFailure() {
		var environment = ProcessEnvironment.CreateEmptyBuilder().Set( "POSIXLY_CORRECT", string.Empty ).Build();
		var exitCode = await Command.RunAsync( [], stderr: new MemoryStream(), terminalProvider: new FakeTerminalProvider(), sourceEnvironment: environment );
		Assert.Equal( 127, exitCode );
	}

	/// <summary>Verifies a missing command uses the GNU internal-failure status.</summary>
	[Fact]
	public async Task MissingOperandReturnsInternalFailure() {
		var exitCode = await Command.RunAsync( [], stderr: new MemoryStream(), terminalProvider: new FakeTerminalProvider() );
		Assert.Equal( 125, exitCode );
	}

	/// <summary>Verifies an invalid option uses the GNU internal-failure status.</summary>
	[Fact]
	public async Task InvalidOptionReturnsInternalFailure() {
		var exitCode = await Command.RunAsync( [ "--definitely-invalid" ], stderr: new MemoryStream(), terminalProvider: new FakeTerminalProvider() );
		Assert.Equal( 125, exitCode );
	}

	/// <summary>Verifies POSIX launches inherit an ignored SIGHUP disposition.</summary>
	[Fact]
	public async Task IgnoresHangupForPosixChild() {
		var executor = new FakeExecutor();
		var exitCode = await Command.RunAsync( [ "tool" ], stderr: new MemoryStream(), terminalProvider: new FakeTerminalProvider(), processExecutor: executor );
		Assert.Equal( 0, exitCode );
		if ( OperatingSystem.IsWindows() ) {
			Assert.Null( executor.Options!.SignalPolicy );
		} else {
			Assert.NotNull( executor.Options!.SignalPolicy );
			Assert.Equal( ProcessSignalLaunchDisposition.Ignored, executor.Options.SignalPolicy!.Directives[ 1 ].Disposition );
		}
	}

	/// <summary>Verifies executable lookup failures retain GNU 126/127 meanings.</summary>
	[Theory]
	[InlineData( ProcessLaunchFailureKind.NotFound, 127 )]
	[InlineData( ProcessLaunchFailureKind.CannotInvoke, 126 )]
	public async Task TranslatesLaunchFailures( ProcessLaunchFailureKind kind, int expected ) {
		var executor = new FakeExecutor( ProcessTermination.LaunchFailed( "failure", kind ) );
		var exitCode = await Command.RunAsync( [ "tool" ], stderr: new MemoryStream(), terminalProvider: new FakeTerminalProvider(), processExecutor: executor );
		Assert.Equal( expected, exitCode );
	}

	private sealed class FakeExecutor : IProcessExecutor {
		private readonly ProcessTermination _termination;
		public FakeExecutor() : this( ProcessTermination.Exited( 0 ) ) { }
		public FakeExecutor( ProcessTermination termination ) { this._termination = termination; }
		public ProcessRunOptions? Options { get; private set; }
		public Task<ProcessResult> RunAsync( ProcessRunOptions options, CancellationToken cancellationToken = default ) {
			this.Options = options;
			return Task.FromResult( ProcessResult.FromTermination( this._termination, ProcessTerminationKind.LaunchFailed != this._termination.Kind ) );
		}
	}

	private sealed class FakeTerminalProvider : ITerminalControlProvider {
		public bool Input { get; init; }
		public bool Output { get; init; }
		public bool Error { get; init; }

		/// <inheritdoc />
		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			var isTerminal = endpoint.FileDescriptor switch {
				0 => this.Input,
				1 => this.Output,
				2 => this.Error,
				_ => false
			};
			if ( !isTerminal ) {
				return TerminalControlResult<TerminalEndpointObservation>.Available(
					new TerminalEndpointObservation(
						false,
						null,
						null,
						TerminalControlCapabilities.None
					)
				);
			}
			var platform = OperatingSystem.IsWindows()
				? TerminalPlatformKind.WindowsConsole
				: TerminalPlatformKind.PosixTermios
			;
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					platform,
					TerminalControlCapabilities.Attachment
				)
			);
		}

		/// <inheritdoc />
		public TerminalControlResult<Icod.TermInfo.TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<Icod.TermInfo.TerminalSize>.Unsupported( "not used" );
		}

		/// <inheritdoc />
		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Unsupported( "not used" );
		}

		/// <inheritdoc />
		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			ArgumentNullException.ThrowIfNull( mode );
			return TerminalControlMutationResult.Unsupported( "not used" );
		}
	}

	private sealed class FakeStandardStreamStateProvider : INohupStandardStreamStateProvider {
		public bool OutputClosed { get; init; }
		public int Reservations { get; private set; }
		public bool IsStandardOutputClosed() => this.OutputClosed;
		public IDisposable ReserveClosedStandardOutput() {
			this.Reservations++;
			return NoopDisposable.Instance;
		}
	}

	private sealed class NoopDisposable : IDisposable {
		public static NoopDisposable Instance { get; } = new();
		public void Dispose() { }
	}

	private sealed class FakeOutputFiles : INohupOutputFileProvider {
		public bool FailFirst { get; init; }
		public List<string> Paths { get; } = [];
		public NohupOutputDestination OpenAppend( string path ) {
			this.Paths.Add( path );
			if ( this.FailFirst && 1 == this.Paths.Count ) throw new IOException( "denied" );
			return new NohupOutputDestination( path, new MemoryStream() );
		}
	}
}
