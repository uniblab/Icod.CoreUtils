namespace Icod.CoreUtils.Timeout.Tests;

using System.Globalization;
using System.Text;
using Icod.Processes;
using Icod.Timing;
using Xunit;

/// <summary>Exercises GNU Coreutils 9.11 <c>timeout</c> parsing and F4 orchestration.</summary>
public sealed class CommandTests {
	/// <summary>Verifies a normal child exit is propagated unchanged.</summary>
	[Fact]
	public async Task PropagatesNormalExitStatus() {
		var executor = FakeExecutor.Exited( 37 );
		var status = await Command.RunAsync( new[] { "1", "child" }, stdout: Stream.Null, stderr: Stream.Null, processExecutor: executor, signalProvider: new FakeSignals( executor ), clock: new NeverClock() );
		Assert.Equal( 37, status );
		Assert.True( executor.LastOptions!.CreateProcessGroup );
	}

	/// <summary>Verifies duration zero disables the primary timer.</summary>
	[Fact]
	public async Task ZeroDurationDisablesTimeout() {
		var executor = FakeExecutor.Exited( 0 );
		var clock = new RecordingClock();
		var signals = new FakeSignals( executor );
		var status = await Command.RunAsync( new[] { "0", "child" }, stdout: Stream.Null, stderr: Stream.Null, processExecutor: executor, signalProvider: signals, clock: clock );
		Assert.Equal( 0, status );
		Assert.Empty( clock.Delays );
		Assert.Empty( signals.Deliveries );
	}

	/// <summary>Verifies default timeout delivery reaches both the child and its F4 process group.</summary>
	[Fact]
	public async Task DefaultTimeoutSignalsProcessAndGroup() {
		var executor = FakeExecutor.Waiting();
		var signals = new FakeSignals( executor ) { CompleteOnSignalNumber = 15 };
		var status = await Command.RunAsync( new[] { "1", "child" }, stdout: Stream.Null, stderr: Stream.Null, processExecutor: executor, signalProvider: signals, clock: new ImmediateClock() );
		Assert.Equal( 124, status );
		Assert.Contains( signals.Deliveries, item => ProcessTargetKind.Process == item.Target.Kind && 15 == item.Signal.Number );
		Assert.Contains( signals.Deliveries, item => ProcessTargetKind.ProcessGroup == item.Target.Kind && 15 == item.Signal.Number );
		Assert.Contains( signals.Deliveries, item => ProcessTargetKind.Process == item.Target.Kind && "CONT" == item.Signal.Name );
		Assert.Contains( signals.Deliveries, item => ProcessTargetKind.ProcessGroup == item.Target.Kind && "CONT" == item.Signal.Name );
	}

	/// <summary>Verifies foreground mode omits process-group delivery and launch isolation.</summary>
	[Fact]
	public async Task ForegroundSignalsOnlyTheChild() {
		var executor = FakeExecutor.Waiting();
		var signals = new FakeSignals( executor ) { CompleteOnSignalNumber = 15 };
		var status = await Command.RunAsync( new[] { "--foreground", "1", "child" }, stdout: Stream.Null, stderr: Stream.Null, processExecutor: executor, signalProvider: signals, clock: new ImmediateClock() );
		Assert.Equal( 124, status );
		Assert.False( executor.LastOptions!.CreateProcessGroup );
		Assert.DoesNotContain( signals.Deliveries, item => ProcessTargetKind.ProcessGroup == item.Target.Kind );
		Assert.DoesNotContain( signals.Deliveries, item => "CONT" == item.Signal.Name );
	}

	/// <summary>Verifies preserve-status returns the child signal status after a timeout.</summary>
	[Fact]
	public async Task PreserveStatusReturnsChildTermination() {
		var executor = FakeExecutor.Waiting();
		var signals = new FakeSignals( executor ) { CompleteOnSignalNumber = 15 };
		var status = await Command.RunAsync( new[] { "--preserve-status", "1", "child" }, stdout: Stream.Null, stderr: Stream.Null, processExecutor: executor, signalProvider: signals, clock: new ImmediateClock() );
		Assert.Equal( 143, status );
	}

	/// <summary>Verifies kill-after escalates once to KILL and returns 137.</summary>
	[Fact]
	public async Task KillAfterEscalatesToKill() {
		var executor = FakeExecutor.Waiting();
		var signals = new FakeSignals( executor ) { CompleteOnSignalNumber = 9 };
		using var error = new MemoryStream();
		var status = await Command.RunAsync(
			new[] { "--verbose", "--kill-after=2", "1", "child" },
			stdout: Stream.Null,
			stderr: error,
			processExecutor: executor,
			signalProvider: signals,
			clock: new ImmediateClock()
		);
		Assert.Equal( 137, status );
		Assert.Contains( signals.Deliveries, item => 15 == item.Signal.Number );
		Assert.Contains( signals.Deliveries, item => 9 == item.Signal.Number );
		var diagnostic = Encoding.UTF8.GetString( error.ToArray() );
		Assert.Contains( "signal TERM", diagnostic, StringComparison.Ordinal );
		Assert.Contains( "signal KILL", diagnostic, StringComparison.Ordinal );
	}

	/// <summary>Verifies signed numeric signal operands are rejected like GNU operand2sig.</summary>
	[Theory]
	[InlineData( "+15" )]
	[InlineData( "-15" )]
	public async Task RejectsSignedNumericSignals( string signal ) {
		ArgumentNullException.ThrowIfNull( signal );
		var executor = FakeExecutor.Exited( 0 );
		using var error = new MemoryStream();
		var status = await Command.RunAsync( new[] { "--signal", signal, "1", "child" }, stdout: Stream.Null, stderr: error, processExecutor: executor, signalProvider: new FakeSignals( executor ) );
		Assert.Equal( 125, status );
		Assert.Null( executor.LastOptions );
	}

	/// <summary>Verifies GNU operand2sig accepts shell-style signal exit statuses.</summary>
	[Theory]
	[InlineData( "143" )]
	[InlineData( "271" )]
	public async Task AcceptsShellExitStatusSignalNumbers(
		string signal
	) {
		ArgumentNullException.ThrowIfNull( signal );
		var executor = FakeExecutor.Waiting();
		var signals = new FakeSignals( executor ) { CompleteOnSignalNumber = 15 };
		var status = await Command.RunAsync(
			new[] { "--signal", signal, "1", "child" },
			stdout: Stream.Null,
			stderr: Stream.Null,
			processExecutor: executor,
			signalProvider: signals,
			clock: new ImmediateClock()
		);
		Assert.Equal( 124, status );
		Assert.Contains(
			signals.Deliveries,
			item => ProcessTargetKind.Process == item.Target.Kind && 15 == item.Signal.Number
		);
	}

	/// <summary>Verifies GNU operand2sig does not trim signal operands.</summary>
	[Theory]
	[InlineData( " 15" )]
	[InlineData( "15 " )]
	[InlineData( " TERM" )]
	[InlineData( "TERM " )]
	public async Task RejectsWhitespaceAroundSignalOperands(
		string signal
	) {
		ArgumentNullException.ThrowIfNull( signal );
		var executor = FakeExecutor.Exited( 0 );
		var status = await Command.RunAsync(
			new[] { "--signal", signal, "1", "child" },
			stdout: Stream.Null,
			stderr: Stream.Null,
			processExecutor: executor,
			signalProvider: new FakeSignals( executor )
		);
		Assert.Equal( 125, status );
		Assert.Null( executor.LastOptions );
	}

	/// <summary>Verifies GNU hexadecimal floating-point durations and suffix scaling.</summary>
	[Fact]
	public async Task ParsesHexadecimalFloatingDuration() {
		var executor = FakeExecutor.Waiting();
		var signals = new FakeSignals( executor ) { CompleteOnSignalNumber = 15 };
		var clock = new RecordingImmediateClock();
		var status = await Command.RunAsync( new[] { "0x1.8p1s", "child" }, stdout: Stream.Null, stderr: Stream.Null, processExecutor: executor, signalProvider: signals, clock: clock );
		Assert.Equal( 124, status );
		Assert.Equal( TimeSpan.FromSeconds( 3 ), Assert.Single( clock.Delays ) );
	}

	/// <summary>Verifies durations accept both the current locale and the C locale.</summary>
	[Theory]
	[InlineData( "0,25", 0.25 )]
	[InlineData( "0.25", 0.25 )]
	[InlineData( "0x1,8p1s", 3.0 )]
	[InlineData( "0x1.8p1s", 3.0 )]
	public async Task AcceptsCurrentAndCLocaleDurations(
		string duration,
		double expectedSeconds
	) {
		ArgumentNullException.ThrowIfNull( duration );
		var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
		culture.NumberFormat.NumberDecimalSeparator = ",";
		culture.NumberFormat.NumberGroupSeparator = "_";
		var previousCulture = CultureInfo.CurrentCulture;
		try {
			CultureInfo.CurrentCulture = culture;
			var executor = FakeExecutor.Waiting();
			var signals = new FakeSignals( executor ) { CompleteOnSignalNumber = 15 };
			var clock = new RecordingImmediateClock();
			var status = await Command.RunAsync(
				new[] { duration, "child" },
				stdout: Stream.Null,
				stderr: Stream.Null,
				processExecutor: executor,
				signalProvider: signals,
				clock: clock
			);
			Assert.Equal( 124, status );
			Assert.Equal(
				TimeSpan.FromSeconds( expectedSeconds ),
				Assert.Single( clock.Delays )
			);
		} finally {
			CultureInfo.CurrentCulture = previousCulture;
		}
	}

	/// <summary>Verifies a localized hexadecimal spelling of zero still disables the timer.</summary>
	[Fact]
	public async Task LocalizedHexadecimalZeroDisablesTimeout() {
		var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
		culture.NumberFormat.NumberDecimalSeparator = ",";
		culture.NumberFormat.NumberGroupSeparator = "_";
		var previousCulture = CultureInfo.CurrentCulture;
		try {
			CultureInfo.CurrentCulture = culture;
			var executor = FakeExecutor.Exited( 0 );
			var clock = new RecordingClock();
			var signals = new FakeSignals( executor );
			var status = await Command.RunAsync(
				new[] { "0x0,0p0", "child" },
				stdout: Stream.Null,
				stderr: Stream.Null,
				processExecutor: executor,
				signalProvider: signals,
				clock: clock
			);
			Assert.Equal( 0, status );
			Assert.Empty( clock.Delays );
			Assert.Empty( signals.Deliveries );
		} finally {
			CultureInfo.CurrentCulture = previousCulture;
		}
	}

	/// <summary>Verifies an exponent-free trailing hexadecimal <c>d</c> remains a digit rather than a day suffix.</summary>
	[Fact]
	public async Task HexadecimalDWithoutExponentRemainsDigit() {
		var executor = FakeExecutor.Waiting();
		var signals = new FakeSignals( executor ) { CompleteOnSignalNumber = 15 };
		var clock = new RecordingImmediateClock();
		var status = await Command.RunAsync( new[] { "0x1d", "child" }, stdout: Stream.Null, stderr: Stream.Null, processExecutor: executor, signalProvider: signals, clock: clock );
		Assert.Equal( 124, status );
		Assert.Equal( TimeSpan.FromSeconds( 29 ), Assert.Single( clock.Delays ) );
	}

	/// <summary>Verifies F4 command lookup preserves GNU's command-not-found status.</summary>
	[Fact]
	public async Task MissingCommandReturns127() {
		using var error = new MemoryStream();
		var status = await Command.RunAsync( new[] { "0", $"icod-timeout-missing-{Guid.NewGuid():N}" }, stdout: Stream.Null, stderr: error );
		Assert.Equal( 127, status );
	}

	/// <summary>Verifies the system F4 executor actually bounds a real child process.</summary>
	[Fact]
	public async Task TimesOutRealChildThroughSystemExecutor() {
		var host = GetProcessTestHostPath();
		Assert.True( File.Exists( host ), $"Process test host was not built at '{host}'." );
		var dotnet = Environment.GetEnvironmentVariable( "DOTNET_HOST_PATH" ) ?? "dotnet";
		// The POSIX process-group launcher requires inherited standard streams. This real-child
		// integration test is the deliberate inter-process communication exception to test stream isolation.
		var status = await Command.RunAsync( new[] { "0.05", dotnet, host, "sleep", "30000" } );
		Assert.Equal( 124, status );
	}

	/// <summary>Verifies the standalone POSIX monitor leads the same process group inherited by its command.</summary>
	[Fact]
	public async Task StandalonePosixMonitorLeadsTimedProcessGroup() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var observationPath = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-timeout-pgid-{Guid.NewGuid():N}"
		);
		try {
			var timeoutAssembly = GetTimeoutAssemblyPath();
			Assert.True( File.Exists( timeoutAssembly ), $"timeout was not built at '{timeoutAssembly}'." );
			var dotnet = Environment.GetEnvironmentVariable(
				"DOTNET_HOST_PATH"
			) ?? "dotnet";
			var startInfo = new System.Diagnostics.ProcessStartInfo(
				dotnet
			) {
				UseShellExecute = false
			};
			startInfo.ArgumentList.Add(
				timeoutAssembly
			);
			startInfo.ArgumentList.Add(
				"2"
			);
			startInfo.ArgumentList.Add(
				"/bin/sh"
			);
			startInfo.ArgumentList.Add(
				"-c"
			);
			startInfo.ArgumentList.Add(
				"printf '%s:%s' \"$$\" \"$(ps -o pgid= -p $$)\" > \"$1\"; sleep 30"
			);
			startInfo.ArgumentList.Add(
				"icod-timeout-test"
			);
			startInfo.ArgumentList.Add(
				observationPath
			);

			using var process = System.Diagnostics.Process.Start(
				startInfo
			) ?? throw new InvalidOperationException( "Unable to start standalone timeout." );
			var timeoutProcessId = process.Id;
			await process.WaitForExitAsync();

			Assert.Equal( 124, process.ExitCode );
			Assert.True(
				File.Exists( observationPath ),
				"Timed child did not report its process-group identity."
			);
			var fields = (
				await File.ReadAllTextAsync(
					observationPath
				)
			).Split(
				':'
			);
			Assert.Equal( 2, fields.Length );
			Assert.True(
				int.TryParse(
					fields[ 0 ],
					out var childProcessId
				)
			);
			Assert.True(
				int.TryParse(
					fields[ 1 ],
					out var childProcessGroupId
				)
			);
			Assert.NotEqual(
				timeoutProcessId,
				childProcessId
			);
			Assert.Equal(
				timeoutProcessId,
				childProcessGroupId
			);
		} finally {
			if ( File.Exists( observationPath ) ) {
				File.Delete(
					observationPath
				);
			}
		}
	}

	/// <summary>Verifies standalone Linux timeout preserves an inherited ignored interrupt disposition.</summary>
	[Fact]
	public async Task StandaloneLinuxMonitorHonorsInheritedIgnoredInterrupt() {
		if ( !OperatingSystem.IsLinux() ) {
			return;
		}

		var status = await RunStandaloneWithIgnoredInterruptAsync(
			"0.2",
			"/bin/sh",
			"-c",
			"kill -INT \"$PPID\"; sleep 30"
		);
		Assert.Equal( 124, status );
	}

	/// <summary>Verifies an explicitly selected timeout signal is handled even when inherited as ignored.</summary>
	[Fact]
	public async Task StandaloneLinuxMonitorHandlesSelectedIgnoredTimeoutSignal() {
		if ( !OperatingSystem.IsLinux() ) {
			return;
		}

		var status = await RunStandaloneWithIgnoredInterruptAsync(
			"--signal=INT",
			"--kill-after=0.2",
			"0.2",
			"/bin/sh",
			"-c",
			"sleep 30"
		);
		Assert.Equal( 124, status );
	}

	/// <summary>Verifies POSIX timeout monitors are not stopped by terminal background-I/O signals.</summary>
	[Theory]
	[InlineData( "TTIN" )]
	[InlineData( "TTOU" )]
	public async Task StandalonePosixMonitorSuppressesTerminalStopSignals(
		string signalName
	) {
		ArgumentNullException.ThrowIfNull( signalName );
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var timeoutAssembly = GetTimeoutAssemblyPath();
		Assert.True( File.Exists( timeoutAssembly ), $"timeout was not built at '{timeoutAssembly}'." );
		var dotnet = Environment.GetEnvironmentVariable(
			"DOTNET_HOST_PATH"
		) ?? "dotnet";
		var startInfo = new System.Diagnostics.ProcessStartInfo(
			dotnet
		) {
			UseShellExecute = false
		};
		startInfo.ArgumentList.Add(
			timeoutAssembly
		);
		startInfo.ArgumentList.Add(
			"0.2"
		);
		startInfo.ArgumentList.Add(
			"/bin/sh"
		);
		startInfo.ArgumentList.Add(
			"-c"
		);
		startInfo.ArgumentList.Add(
			$"kill -{signalName} \"$PPID\"; sleep 30"
		);

		System.Diagnostics.Process? process = null;
		try {
			process = System.Diagnostics.Process.Start(
				startInfo
			) ?? throw new InvalidOperationException( "Unable to start standalone timeout." );
			using var waitCancellation = new CancellationTokenSource(
				TimeSpan.FromSeconds( 10 )
			);
			await process.WaitForExitAsync(
				waitCancellation.Token
			);

			Assert.Equal( 124, process.ExitCode );
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

	/// <summary>Verifies a hexadecimal exponent marker requires decimal exponent digits.</summary>
	[Theory]
	[InlineData( "0x1p" )]
	[InlineData( "0x1p+" )]
	[InlineData( "0x1p-no" )]
	public async Task RejectsIncompleteHexadecimalExponent( string duration ) {
		ArgumentNullException.ThrowIfNull( duration );
		var executor = FakeExecutor.Exited( 0 );
		var status = await Command.RunAsync( new[] { duration, "child" }, stdout: Stream.Null, stderr: Stream.Null, processExecutor: executor, signalProvider: new FakeSignals( executor ) );
		Assert.Equal( 125, status );
		Assert.Null( executor.LastOptions );
	}

	/// <summary>Verifies whitespace cannot separate an explicit numeric sign from the duration.</summary>
	[Fact]
	public async Task RejectsWhitespaceAfterDurationSign() {
		var executor = FakeExecutor.Exited( 0 );
		var status = await Command.RunAsync( new[] { "+ 1", "child" }, stdout: Stream.Null, stderr: Stream.Null, processExecutor: executor, signalProvider: new FakeSignals( executor ) );
		Assert.Equal( 125, status );
		Assert.Null( executor.LastOptions );
	}

	/// <summary>Verifies no-argument common options reject an attached value.</summary>
	[Theory]
	[InlineData( "--help=value" )]
	[InlineData( "--version=value" )]
	public async Task CommonOptionsRejectAttachedValues( string option ) {
		ArgumentNullException.ThrowIfNull( option );
		var executor = FakeExecutor.Exited( 0 );
		var status = await Command.RunAsync( new[] { option }, stdout: Stream.Null, stderr: Stream.Null, processExecutor: executor, signalProvider: new FakeSignals( executor ) );
		Assert.Equal( 125, status );
		Assert.Null( executor.LastOptions );
	}

	/// <summary>Verifies GNU long-option abbreviations remain accepted.</summary>
	[Fact]
	public async Task AcceptsUnambiguousLongOptionAbbreviations() {
		var executor = FakeExecutor.Exited( 12 );
		var status = await Command.RunAsync( new[] { "--pres", "0", "child" }, stdout: Stream.Null, stderr: Stream.Null, processExecutor: executor, signalProvider: new FakeSignals( executor ), clock: new NeverClock() );
		Assert.Equal( 12, status );
	}

	private static async Task<int> RunStandaloneWithIgnoredInterruptAsync(
		params string[] timeoutArguments
	) {
		var timeoutAssembly = GetTimeoutAssemblyPath();
		if ( !File.Exists( timeoutAssembly ) ) {
			throw new FileNotFoundException(
				"Standalone timeout was not built.",
				timeoutAssembly
			);
		}
		var dotnet = Environment.GetEnvironmentVariable(
			"DOTNET_HOST_PATH"
		) ?? "dotnet";
		var startInfo = new System.Diagnostics.ProcessStartInfo(
			"/bin/sh"
		) {
			UseShellExecute = false
		};
		startInfo.ArgumentList.Add(
			"-c"
		);
		startInfo.ArgumentList.Add(
			"trap '' INT; exec \"$@\""
		);
		startInfo.ArgumentList.Add(
			"icod-timeout-ignore-test"
		);
		startInfo.ArgumentList.Add(
			dotnet
		);
		startInfo.ArgumentList.Add(
			timeoutAssembly
		);
		foreach ( var argument in timeoutArguments ) {
			startInfo.ArgumentList.Add(
				argument
			);
		}

		System.Diagnostics.Process? process = null;
		try {
			process = System.Diagnostics.Process.Start(
				startInfo
			) ?? throw new InvalidOperationException( "Unable to start standalone timeout." );
			using var waitCancellation = new CancellationTokenSource(
				TimeSpan.FromSeconds( 10 )
			);
			await process.WaitForExitAsync(
				waitCancellation.Token
			);
			return process.ExitCode;
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

	private static string GetTimeoutAssemblyPath() {
		var targetFrameworkDirectory = new DirectoryInfo(
			AppContext.BaseDirectory
		);
		var configurationDirectory = targetFrameworkDirectory.Parent
			?? throw new InvalidOperationException();
		var testsDirectory = configurationDirectory.Parent?.Parent?.Parent
			?? throw new InvalidOperationException();
		var repositoryDirectory = testsDirectory.Parent
			?? throw new InvalidOperationException();
		return System.IO.Path.Combine(
			repositoryDirectory.FullName,
			"bin",
			configurationDirectory.Name,
			targetFrameworkDirectory.Name,
			"timeout.dll"
		);
	}

	private static string GetProcessTestHostPath() {
		var targetFrameworkDirectory = new DirectoryInfo( AppContext.BaseDirectory );
		var configurationDirectory = targetFrameworkDirectory.Parent ?? throw new InvalidOperationException();
		var testsDirectory = configurationDirectory.Parent?.Parent?.Parent ?? throw new InvalidOperationException();
		return System.IO.Path.Combine(
			testsDirectory.FullName,
			"ProcessTestHost", "bin", configurationDirectory.Name, targetFrameworkDirectory.Name,
			"Icod.CoreUtils.ProcessTestHost.dll"
		);
	}

	private sealed class FakeExecutor : IProcessExecutor {
		private readonly TaskCompletionSource<ProcessResult> _completion = new( TaskCreationOptions.RunContinuationsAsynchronously );
		internal ProcessRunOptions? LastOptions { get; private set; }
		internal static FakeExecutor Exited( int status ) {
			var value = new FakeExecutor();
			value._completion.TrySetResult( ProcessResult.FromTermination( ProcessTermination.Exited( status ), identity: new ProcessIdentity( 4242 ) ) );
			return value;
		}
		internal static FakeExecutor Waiting() => new();
		internal void CompleteWithSignal( ProcessSignal signal ) {
			ArgumentNullException.ThrowIfNull( signal );
			this._completion.TrySetResult( ProcessResult.FromTermination( ProcessTermination.Signaled( signal ), identity: new ProcessIdentity( 4242 ) ) );
		}
		public Task<ProcessResult> RunAsync( ProcessRunOptions options, CancellationToken cancellationToken = default ) {
			ArgumentNullException.ThrowIfNull( options );
			this.LastOptions = options;
			options.ProcessStarted?.Invoke( new ProcessIdentity( 4242 ) );
			return this._completion.Task;
		}
	}

	private sealed class FakeSignals : IProcessSignalProvider {
		private readonly FakeExecutor _executor;
		internal int? CompleteOnSignalNumber { get; init; }
		internal List<(ProcessTarget Target, ProcessSignal Signal)> Deliveries { get; } = new();
		internal FakeSignals( FakeExecutor executor ) => this._executor = executor;
		public ProcessControlCapabilities Capabilities => ProcessControlCapabilities.SignalDelivery | ProcessControlCapabilities.ProcessGroupTargets;
		public IReadOnlyList<ProcessSignal> ListSignals() => ProcessSignalCatalog.PortableSignals;
		public ProcessOperationResult<ProcessSignal> ParseSignal( string text ) {
			ArgumentNullException.ThrowIfNull( text );
			return ProcessSignalCatalog.Parse( text );
		}
		public ProcessOperationResult<ProcessSignal> TranslateSignal( int number ) => ProcessSignalCatalog.Translate( number );
		public ProcessOperationResult<ProcessSignalDisposition> ObserveDisposition( ProcessIdentity identity, ProcessSignal signal ) {
			ArgumentNullException.ThrowIfNull( identity );
			ArgumentNullException.ThrowIfNull( signal );
			return ProcessOperationResult<ProcessSignalDisposition>.Failure( ProcessOperationStatus.Unsupported );
		}
		public Task<ProcessOperationResult> DeliverAsync( ProcessTarget target, ProcessSignal signal, int? queuedValue = null, CancellationToken cancellationToken = default ) {
			ArgumentNullException.ThrowIfNull( target );
			ArgumentNullException.ThrowIfNull( signal );
			this.Deliveries.Add( ( target, signal ) );
			if ( ProcessTargetKind.Process == target.Kind && this.CompleteOnSignalNumber == signal.Number ) {
				this._executor.CompleteWithSignal( signal );
			}
			return Task.FromResult( ProcessOperationResult.Success() );
		}
	}

	private sealed class ImmediateClock : IMonotonicClock {
		public long GetTimestamp() => 0;
		public TimeSpan GetElapsedTime( long startTimestamp, long endTimestamp ) => TimeSpan.Zero;
		public ValueTask DelayAsync( TimeSpan delay, CancellationToken cancellationToken = default ) => ValueTask.CompletedTask;
	}

	private sealed class RecordingClock : IMonotonicClock {
		internal List<TimeSpan> Delays { get; } = new();
		public long GetTimestamp() => 0;
		public TimeSpan GetElapsedTime( long startTimestamp, long endTimestamp ) => TimeSpan.Zero;
		public ValueTask DelayAsync( TimeSpan delay, CancellationToken cancellationToken = default ) { this.Delays.Add( delay ); return ValueTask.CompletedTask; }
	}

	private sealed class RecordingImmediateClock : IMonotonicClock {
		internal List<TimeSpan> Delays { get; } = new();
		public long GetTimestamp() => 0;
		public TimeSpan GetElapsedTime( long startTimestamp, long endTimestamp ) => TimeSpan.Zero;
		public ValueTask DelayAsync( TimeSpan delay, CancellationToken cancellationToken = default ) { this.Delays.Add( delay ); return ValueTask.CompletedTask; }
	}

	private sealed class NeverClock : IMonotonicClock {
		public long GetTimestamp() => 0;
		public TimeSpan GetElapsedTime( long startTimestamp, long endTimestamp ) => TimeSpan.Zero;
		public async ValueTask DelayAsync( TimeSpan delay, CancellationToken cancellationToken = default ) => await Task.Delay( System.Threading.Timeout.InfiniteTimeSpan, cancellationToken );
	}
}
