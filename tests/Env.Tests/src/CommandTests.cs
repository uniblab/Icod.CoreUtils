namespace Icod.CoreUtils.Env.Tests;

using System.Text;
using Icod.Processes;
using Xunit;

/// <summary>Tests GNU <c>env</c> command behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies inherited variables, mutation, and ordinary output.</summary>
	[Fact]
	public async Task PrintsResultingEnvironment() {
		var source = ProcessEnvironment.CreateEmptyBuilder().Set( "A", "1" ).Build();
		var output = new MemoryStream();
		var exitCode = await Command.RunAsync( [ "B=two words" ], stdout: output, stderr: new MemoryStream(), sourceEnvironment: source );
		Assert.Equal( 0, exitCode );
		Assert.Contains( string.Concat( "A=1", Environment.NewLine ), Text( output ), StringComparison.Ordinal );
		Assert.Contains( string.Concat( "B=two words", Environment.NewLine ), Text( output ), StringComparison.Ordinal );
	}

	/// <summary>Verifies NUL-terminated environment reporting.</summary>
	[Fact]
	public async Task NullTerminatesEnvironmentOutput() {
		var source = ProcessEnvironment.CreateEmptyBuilder().Set( "A", "1" ).Build();
		var output = new MemoryStream();
		var exitCode = await Command.RunAsync( [ "-0" ], stdout: output, stderr: new MemoryStream(), sourceEnvironment: source );
		Assert.Equal( 0, exitCode );
		Assert.Equal( "A=1\0", Text( output ) );
	}

	/// <summary>Verifies clearing, removal, assignments, lookup, and exact arguments reach F4.</summary>
	[Fact]
	public async Task BuildsExactChildEnvironmentAndArguments() {
		var executor = new FakeExecutor( ProcessTermination.Exited( 7 ) );
		var source = ProcessEnvironment.CreateEmptyBuilder().Set( "OLD", "x" ).Set( "DROP", "y" ).Build();
		var exitCode = await Command.RunAsync(
			[ "-i", "-u", "DROP", "A=1", "tool", "two words", "" ],
			stdout: new MemoryStream(),
			stderr: new MemoryStream(),
			processExecutor: executor,
			sourceEnvironment: source
		);
		Assert.Equal( 7, exitCode );
		Assert.NotNull( executor.Options );
		Assert.Equal( "tool", executor.Options!.FileName );
		Assert.True( executor.Options.ResolveExecutable );
		Assert.Equal( [ "two words", "" ], executor.Options.Arguments );
		Assert.Single( executor.Options.Environment!.Variables );
		Assert.Equal( "1", executor.Options.Environment.Variables[ "A" ] );
	}

	/// <summary>Verifies GNU split-string quoting, escapes, and original-environment expansion.</summary>
	[Fact]
	public async Task SplitStringUsesOriginalEnvironment() {
		var executor = new FakeExecutor( ProcessTermination.Exited( 0 ) );
		var source = ProcessEnvironment.CreateEmptyBuilder().Set( "WORDS", "two words" ).Build();
		var exitCode = await Command.RunAsync(
			[ "-S", "-i A='x y' tool \"${WORDS}\" left\\_right" ],
			stdout: new MemoryStream(), stderr: new MemoryStream(), processExecutor: executor, sourceEnvironment: source
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( [ "two words", "left", "right" ], executor.Options!.Arguments );
		Assert.Equal( "x y", executor.Options.Environment!.Variables[ "A" ] );
		Assert.False( executor.Options.Environment.Variables.ContainsKey( "WORDS" ) );
	}

	/// <summary>Verifies unset split-string variables disappear while explicitly empty variables retain an empty argument.</summary>
	[Fact]
	public async Task SplitStringDistinguishesUnsetAndEmptyVariables() {
		var executor = new FakeExecutor( ProcessTermination.Exited( 0 ) );
		var source = ProcessEnvironment.CreateEmptyBuilder().Set( "EMPTY", string.Empty ).Build();
		var exitCode = await Command.RunAsync(
			[ "-S", "tool ${MISSING} ${EMPTY}" ],
			stdout: new MemoryStream(), stderr: new MemoryStream(), processExecutor: executor, sourceEnvironment: source
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( [ string.Empty ], executor.Options!.Arguments );
	}

	/// <summary>Verifies working directory and native argument zero are carried through the shared launcher contract.</summary>
	[Fact]
	public async Task CarriesWorkingDirectoryAndArgumentZero() {
		var executor = new FakeExecutor( ProcessTermination.Exited( 0 ) );
		var exitCode = await Command.RunAsync(
			[ "-C", "work", "-a", "custom-zero", "tool" ],
			stdout: new MemoryStream(), stderr: new MemoryStream(), processExecutor: executor,
			sourceEnvironment: ProcessEnvironment.CreateEmptyBuilder().Build()
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( "work", executor.Options!.WorkingDirectory );
		Assert.Equal( "custom-zero", executor.Options.ArgumentZero );
	}

	/// <summary>Verifies the POSIX command-line path preserves the original command spelling as native argument zero.</summary>
	[Fact]
	public async Task PosixCommandLinePreservesNativeArgumentZero() {
		if ( OperatingSystem.IsWindows() ) return;
		var executor = new FakeExecutor( ProcessTermination.Exited( 0 ) );
		var exitCode = await Command.RunAsync(
			[ "tool", "arg" ],
			processExecutor: executor,
			sourceEnvironment: ProcessEnvironment.CreateEmptyBuilder().Build(),
			replaceCurrentProcess: true
		);
		Assert.Equal( 0, exitCode );
		Assert.Equal( "tool", executor.Options!.ArgumentZero );
		Assert.True( executor.Options.ReplaceCurrentProcess );
	}

	/// <summary>Verifies the standalone host really replaces itself instead of supervising an extra child process.</summary>
	[Fact]
	public async Task StandalonePosixEnvPreservesProcessIdentity() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var targetFrameworkDirectory = new DirectoryInfo(
			AppContext.BaseDirectory
		);
		var configurationDirectory = targetFrameworkDirectory.Parent
			?? throw new InvalidOperationException();
		var testsDirectory = configurationDirectory.Parent?.Parent?.Parent
			?? throw new InvalidOperationException();
		var repositoryDirectory = testsDirectory.Parent
			?? throw new InvalidOperationException();
		var commandAssembly = System.IO.Path.Combine(
			repositoryDirectory.FullName,
			"bin",
			configurationDirectory.Name,
			targetFrameworkDirectory.Name,
			"env.dll"
		);
		Assert.True(
			File.Exists( commandAssembly ),
			$"Standalone command was not built at '{commandAssembly}'."
		);

		var dotnet = Environment.GetEnvironmentVariable(
			"DOTNET_HOST_PATH"
		) ?? "dotnet";
		var startInfo = new System.Diagnostics.ProcessStartInfo(
			dotnet
		) {
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		startInfo.ArgumentList.Add(
			commandAssembly
		);
		startInfo.ArgumentList.Add(
			"/bin/sh"
		);
		startInfo.ArgumentList.Add(
			"-c"
		);
		startInfo.ArgumentList.Add(
			"printf '%s' \"$$\""
		);

		System.Diagnostics.Process? process = null;
		try {
			process = System.Diagnostics.Process.Start(
				startInfo
			) ?? throw new InvalidOperationException( "Unable to start standalone command." );
			var expectedProcessId = process.Id;
			var outputTask = process.StandardOutput.ReadToEndAsync();
			var errorTask = process.StandardError.ReadToEndAsync();
			using var waitCancellation = new CancellationTokenSource(
				TimeSpan.FromSeconds( 10 )
			);
			await process.WaitForExitAsync(
				waitCancellation.Token
			);

			var output = await outputTask;
			var error = await errorTask;
			Assert.True(
				0 == process.ExitCode,
				$"Standalone command exited with status {process.ExitCode}: {error}"
			);
			Assert.True(
				int.TryParse( output, out var actualProcessId ),
				$"Replacement command reported an invalid process ID: '{output}'."
			);
			Assert.Equal( expectedProcessId, actualProcessId );
		} finally {
			if ( null != process ) {
				if ( !process.HasExited ) {
					process.Kill(
						entireProcessTree: true
					);
					await process.WaitForExitAsync();
				}
				process.Dispose();
			}
		}
	}

	/// <summary>Verifies launch signal options create one consolidated child policy.</summary>
	[Fact]
	public async Task CarriesSignalPolicy() {
		var executor = new FakeExecutor( ProcessTermination.Exited( 0 ) );
		var exitCode = await Command.RunAsync(
			[ "--ignore-signal=HUP", "--block-signal=TERM", "tool" ],
			stdout: new MemoryStream(), stderr: new MemoryStream(), processExecutor: executor,
			sourceEnvironment: ProcessEnvironment.CreateEmptyBuilder().Build()
		);
		Assert.Equal( 0, exitCode );
		var policy = executor.Options!.SignalPolicy!;
		Assert.Equal( ProcessSignalLaunchDisposition.Ignored, policy.Directives[ 1 ].Disposition );
		Assert.True( policy.Directives[ 15 ].Blocked );
	}

	/// <summary>Verifies signal listing reports an inherited blocked mask when the provider exposes it.</summary>
	[Fact]
	public async Task ListsInheritedBlockedSignalHandling() {
		var executor = new FakeExecutor( ProcessTermination.Exited( 0 ) );
		var signals = new FakeSignalProvider( blockedSignalNumber: 15 );
		var diagnostics = new MemoryStream();
		var exitCode = await Command.RunAsync(
			[ "--list-signal-handling", "tool" ],
			stdout: new MemoryStream(),
			stderr: diagnostics,
			processExecutor: executor,
			signalProvider: signals
		);
		Assert.Equal( 0, exitCode );
		Assert.Contains( "TERM", Text( diagnostics ), StringComparison.Ordinal );
		Assert.Contains( "BLOCK", Text( diagnostics ), StringComparison.Ordinal );
	}

	/// <summary>Verifies GNU launch-status translation.</summary>
	[Theory]
	[InlineData( ProcessLaunchFailureKind.NotFound, 127 )]
	[InlineData( ProcessLaunchFailureKind.CannotInvoke, 126 )]
	[InlineData( ProcessLaunchFailureKind.SetupFailed, 125 )]
	public async Task TranslatesLaunchFailures( ProcessLaunchFailureKind kind, int expected ) {
		var executor = new FakeExecutor( ProcessTermination.LaunchFailed( "failure", kind ) );
		var exitCode = await Command.RunAsync( [ "tool" ], stdout: new MemoryStream(), stderr: new MemoryStream(), processExecutor: executor );
		Assert.Equal( expected, exitCode );
	}

	/// <summary>Verifies an empty explicit environment still gets the POSIX default executable search path.</summary>
	[Fact]
	public void EmptyEnvironmentUsesDefaultPosixSearchPath() {
		if ( OperatingSystem.IsWindows() ) return;
		var result = SystemExecutableLocator.Instance.Locate(
			"sh",
			ProcessEnvironment.CreateEmptyBuilder().Build()
		);
		Assert.True( result.Succeeded, result.Message );
	}

	/// <summary>Verifies a missing -C directory is an env setup failure rather than a command lookup failure.</summary>
	[Fact]
	public async Task MissingWorkingDirectoryReturnsInternalFailure() {
		var missing = System.IO.Path.Combine( System.IO.Path.GetTempPath(), $"icod-env-missing-{Guid.NewGuid():N}" );
		var error = new MemoryStream();
		var exitCode = await Command.RunAsync(
			[ "-C", missing, "definitely-not-a-command" ],
			stdout: new MemoryStream(),
			stderr: error,
			sourceEnvironment: ProcessEnvironment.CreateEmptyBuilder().Build()
		);
		Assert.Equal( 125, exitCode );
		Assert.Contains( "Working directory", Text( error ), StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Verifies an empty command name remains a GNU not-found result instead of throwing during launch setup.</summary>
	[Fact]
	public async Task EmptyCommandNameReturnsNotFound() {
		var error = new MemoryStream();
		var exitCode = await Command.RunAsync(
			[ string.Empty ],
			stdout: new MemoryStream(),
			stderr: error,
			sourceEnvironment: ProcessEnvironment.CreateEmptyBuilder().Build()
		);
		Assert.Equal( 127, exitCode );
	}

	/// <summary>Verifies an existing directory is classified as found but not invokable.</summary>
	[Fact]
	public async Task DirectoryCommandReturnsCannotInvoke() {
		if ( OperatingSystem.IsWindows() ) return;
		var error = new MemoryStream();
		var exitCode = await Command.RunAsync(
			[ System.IO.Path.GetTempPath() ],
			stdout: new MemoryStream(),
			stderr: error
		);
		Assert.Equal( 126, exitCode );
	}

	/// <summary>Verifies an explicitly empty working directory is a setup failure rather than being ignored.</summary>
	[Fact]
	public async Task EmptyWorkingDirectoryReturnsInternalFailure() {
		var exitCode = await Command.RunAsync(
			[ "-C", string.Empty, "tool" ],
			stdout: new MemoryStream(),
			stderr: new MemoryStream(),
			sourceEnvironment: ProcessEnvironment.CreateEmptyBuilder().Build()
		);
		Assert.Equal( 125, exitCode );
	}

	/// <summary>Verifies GNU's split-string hint is emitted when a not-found command contains shell whitespace.</summary>
	[Fact]
	public async Task NotFoundCommandWithWhitespaceSuggestsSplitString() {
		var executor = new FakeExecutor( ProcessTermination.LaunchFailed( "not found", ProcessLaunchFailureKind.NotFound ) );
		var error = new MemoryStream();
		var exitCode = await Command.RunAsync(
			[ "tool --flag" ],
			stdout: new MemoryStream(),
			stderr: error,
			processExecutor: executor
		);
		Assert.Equal( 127, exitCode );
		Assert.Contains( "-[v]S", Text( error ), StringComparison.Ordinal );
	}

	/// <summary>Verifies GNU rejects an empty name supplied to <c>--unset</c>.</summary>
	[Fact]
	public async Task RejectsEmptyUnsetName() {
		var error = new MemoryStream();
		var exitCode = await Command.RunAsync( [ "-u", string.Empty ], stdout: new MemoryStream(), stderr: error );
		Assert.Equal( 125, exitCode );
		Assert.Contains( "cannot unset", Text( error ), StringComparison.Ordinal );
	}

	/// <summary>Verifies command-incompatible NUL output is rejected.</summary>
	[Fact]
	public async Task RejectsNullOutputWithCommand() {
		var error = new MemoryStream();
		var exitCode = await Command.RunAsync( [ "-0", "tool" ], stdout: new MemoryStream(), stderr: error );
		Assert.Equal( 125, exitCode );
		Assert.Contains( "--null", Text( error ), StringComparison.Ordinal );
	}

	private static string Text( MemoryStream stream ) => Encoding.UTF8.GetString( stream.ToArray() );

	private sealed class FakeSignalProvider : IProcessSignalProvider, IProcessSignalMaskProvider {
		private readonly int _blockedSignalNumber;
		public FakeSignalProvider( int blockedSignalNumber ) { this._blockedSignalNumber = blockedSignalNumber; }
		public ProcessControlCapabilities Capabilities => ProcessControlCapabilities.SignalDisposition | ProcessControlCapabilities.SignalMaskObservation;
		public IReadOnlyList<ProcessSignal> ListSignals() => [ new ProcessSignal( 15, "TERM" ) ];
		public ProcessOperationResult<ProcessSignal> ParseSignal( string text ) => ProcessSignalCatalog.Parse( text );
		public ProcessOperationResult<ProcessSignal> TranslateSignal( int number ) => ProcessSignalCatalog.Translate( number );
		public ProcessOperationResult<ProcessSignalDisposition> ObserveDisposition( ProcessIdentity identity, ProcessSignal signal ) => ProcessOperationResult<ProcessSignalDisposition>.Success( ProcessSignalDisposition.Default );
		public ProcessOperationResult<bool> ObserveBlocked( ProcessIdentity identity, ProcessSignal signal ) => ProcessOperationResult<bool>.Success( this._blockedSignalNumber == signal.Number );
		public Task<ProcessOperationResult> DeliverAsync( ProcessTarget target, ProcessSignal signal, int? queuedValue = null, CancellationToken cancellationToken = default ) => Task.FromResult( ProcessOperationResult.Success() );
	}

	private sealed class FakeExecutor : IProcessExecutor {
		private readonly ProcessTermination _termination;
		public FakeExecutor( ProcessTermination termination ) { this._termination = termination; }
		public ProcessRunOptions? Options { get; private set; }
		public Task<ProcessResult> RunAsync( ProcessRunOptions options, CancellationToken cancellationToken = default ) {
			this.Options = options;
			return Task.FromResult( ProcessResult.FromTermination( this._termination, ProcessTerminationKind.LaunchFailed != this._termination.Kind ) );
		}
	}
}
