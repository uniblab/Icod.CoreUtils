namespace Icod.CoreUtils.Nice.Tests;

using Icod.CommandFramework.Diagnostics;
using Icod.Processes;
using Xunit;

/// <summary>Exercises GNU 9.11 <c>nice</c> parsing, priority ordering, launch status, streams, and failures.</summary>
public sealed class CommandTests {
	/// <summary>Verifies no-command mode reports the current F4 niceness.</summary>
	[Fact]
	public async Task PrintsCurrentNiceness() {
		var priorities = new FakePriorityProvider { Current = 7 };
		using var output = new StringWriter();
		var status = await Command.RunAsync( Array.Empty<string>(), CreateContext( stdout: output ), priorityProvider: priorities );
		Assert.Equal( 0, status );
		Assert.Equal( string.Concat( "7", Environment.NewLine ), output.ToString() );
	}

	/// <summary>Verifies the historical GNU signed adjustment spellings.</summary>
	[Theory]
	[InlineData( "-10", 10 )]
	[InlineData( "--10", -10 )]
	[InlineData( "-+10", 10 )]
	public async Task ParsesHistoricalAdjustmentForms( string option, int expectedAdjustment ) {
		var priorities = new FakePriorityProvider();
		var executor = new ThrowingExecutor();
		var status = await Command.RunAsync( new[] { option, "child" }, CreateContext(), processExecutor: executor, priorityProvider: priorities );
		Assert.Equal( 125, status );
		Assert.Equal( Math.Clamp( expectedAdjustment, -20, 19 ), priorities.LastSetValue );
		Assert.True( executor.Called );
	}

	/// <summary>Verifies GNU numeric parsing accepts leading but rejects trailing whitespace.</summary>
	[Theory]
	[InlineData( " 5", true )]
	[InlineData( "5 ", false )]
	public async Task AdjustmentWhitespaceMatchesGnuParsing( string adjustment, bool expectedLaunch ) {
		var executor = new ThrowingExecutor();
		using var error = new StringWriter();
		var status = await Command.RunAsync(
			new[] { "-n", adjustment, "child" },
			CreateContext( stderr: error ),
			processExecutor: executor,
			priorityProvider: new FakePriorityProvider()
		);
		Assert.Equal( 125, status );
		Assert.Equal( expectedLaunch, executor.Called );
	}

	/// <summary>Verifies a permission-only niceness failure does not prevent command launch.</summary>
	[Fact]
	public async Task PermissionFailureStillAttemptsCommand() {
		var priorities = new FakePriorityProvider { SetFailure = ProcessOperationStatus.AccessDenied };
		var executor = new ThrowingExecutor();
		using var error = new StringWriter();
		var status = await Command.RunAsync( new[] { "child" }, CreateContext( stderr: error ), processExecutor: executor, priorityProvider: priorities );
		Assert.Equal( 125, status );
		Assert.True( executor.Called );
		Assert.Contains( "cannot set niceness", error.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies failure to observe the current niceness prevents command launch.</summary>
	[Fact]
	public async Task GetPriorityFailurePreventsCommandLaunch() {
		var priorities = new FakePriorityProvider { GetFailure = ProcessOperationStatus.Failed };
		var executor = new ThrowingExecutor();
		using var error = new StringWriter();
		var status = await Command.RunAsync( new[] { "child" }, CreateContext( stderr: error ), processExecutor: executor, priorityProvider: priorities );
		Assert.Equal( 125, status );
		Assert.False( executor.Called );
		Assert.Contains( "cannot get niceness", error.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies explicit adjustment without a command is a GNU internal failure.</summary>
	[Fact]
	public async Task AdjustmentRequiresCommand() {
		using var error = new StringWriter();
		var status = await Command.RunAsync( new[] { "-n", "4" }, CreateContext( stderr: error ), priorityProvider: new FakePriorityProvider() );
		Assert.Equal( 125, status );
		Assert.Contains( "a command must be given", error.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies child exit status is propagated unchanged.</summary>
	[Fact]
	public async Task PropagatesChildExitStatus() {
		var host = GetProcessTestHostPath();
		Assert.True( File.Exists( host ), $"Process test host was not built at '{host}'." );
		var dotnet = Environment.GetEnvironmentVariable( "DOTNET_HOST_PATH" ) ?? "dotnet";
		var status = await Command.RunAsync(
			new[] { dotnet, host, "exit", "37" },
			CreateContext(),
			priorityProvider: new FakePriorityProvider()
		);
		Assert.Equal( 37, status );
	}

	/// <summary>Verifies command-not-found maps to 127.</summary>
	[Fact]
	public async Task MissingCommandReturns127() {
		using var error = new StringWriter();
		var status = await Command.RunAsync(
			new[] { $"icod-nice-missing-{Guid.NewGuid():N}" },
			CreateContext( stderr: error ),
			priorityProvider: new FakePriorityProvider()
		);
		Assert.Equal( 127, status );
	}

	/// <summary>Verifies default child execution inherits all three native standard handles.</summary>
	[Fact]
	public async Task ChildStandardHandlesAreInheritedByDefault() {
		var executor = new RecordingExecutor();
		var status = await Command.RunAsync(
			new[] { "child" },
			CreateContext(),
			processExecutor: executor,
			priorityProvider: new FakePriorityProvider()
		);
		Assert.Equal( 0, status );
		Assert.NotNull( executor.Options );
		Assert.Null( executor.Options.StandardInput );
		Assert.Null( executor.Options.StandardOutput );
		Assert.Null( executor.Options.StandardError );
	}

	/// <summary>Verifies the standalone POSIX host can request current-process replacement.</summary>
	[Fact]
	public async Task PosixHostCanRequestCurrentProcessReplacement() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}
		var executor = new RecordingExecutor();
		var status = await Command.RunAsync(
			new[] { "child" },
			CreateContext(),
			processExecutor: executor,
			priorityProvider: new FakePriorityProvider(),
			replaceCurrentProcess: true
		);
		Assert.Equal( 0, status );
		Assert.NotNull( executor.Options );
		Assert.True( executor.Options.ReplaceCurrentProcess );
		Assert.Equal( "child", executor.Options.ArgumentZero );
	}

	/// <summary>Verifies explicitly supplied binary standard streams reach the child unchanged.</summary>
	[Fact]
	public async Task ChildStandardStreamOverridesArePreserved() {
		using var input = new MemoryStream();
		using var output = new MemoryStream();
		using var error = new MemoryStream();
		var executor = new RecordingExecutor();
		var status = await Command.RunAsync(
			new[] { "child" },
			CreateContext( stdinStream: input, stdoutStream: output, stderrStream: error ),
			processExecutor: executor,
			priorityProvider: new FakePriorityProvider()
		);
		Assert.Equal( 0, status );
		Assert.NotNull( executor.Options );
		Assert.Same( input, executor.Options.StandardInput );
		Assert.Same( output, executor.Options.StandardOutput );
		Assert.Same( error, executor.Options.StandardError );
	}

	/// <summary>Verifies command-context cancellation follows the standard command exit-code pattern.</summary>
	[Fact]
	public async Task CancellationReturns130() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var executor = new RecordingExecutor( ProcessTermination.Canceled() );
		var status = await Command.RunAsync(
			new[] { "child" },
			CreateContext( cancellationToken: cancellation.Token ),
			processExecutor: executor,
			priorityProvider: new FakePriorityProvider()
		);
		Assert.Equal( CommandExitCodes.Canceled, status );
	}

	private static CommandContext CreateContext(
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		Stream? stdinStream = null,
		Stream? stdoutStream = null,
		Stream? stderrStream = null,
		CancellationToken cancellationToken = default
	) => new(
		"nice",
		stdin ?? TextReader.Null,
		stdout ?? TextWriter.Null,
		stderr ?? TextWriter.Null,
		stdinStream,
		stdoutStream,
		stderrStream,
		cancellationToken
	);

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

	private sealed class ThrowingExecutor : IProcessExecutor {
		internal bool Called { get; private set; }
		public Task<ProcessResult> RunAsync( ProcessRunOptions options, CancellationToken cancellationToken = default ) {
			this.Called = true;
			throw new InvalidOperationException( "test launch boundary" );
		}
	}

	private sealed class RecordingExecutor : IProcessExecutor {
		private readonly ProcessTermination _termination;
		internal ProcessRunOptions? Options { get; private set; }
		internal RecordingExecutor( ProcessTermination? termination = null ) {
			this._termination = termination ?? ProcessTermination.Exited( 0 );
		}
		public Task<ProcessResult> RunAsync( ProcessRunOptions options, CancellationToken cancellationToken = default ) {
			this.Options = options;
			return Task.FromResult( ProcessResult.FromTermination( this._termination ) );
		}
	}

	private sealed class FakePriorityProvider : IProcessPriorityProvider {
		internal int Current { get; set; }
		internal int? LastSetValue { get; private set; }
		internal ProcessOperationStatus? GetFailure { get; set; }
		internal ProcessOperationStatus? SetFailure { get; set; }
		public ProcessControlCapabilities Capabilities => ProcessControlCapabilities.PriorityRead | ProcessControlCapabilities.PriorityWrite;
		public ProcessOperationResult<ProcessPriorityValue> GetPriority( ProcessTarget target ) {
			if ( null != this.GetFailure ) return ProcessOperationResult<ProcessPriorityValue>.Failure( this.GetFailure.Value, "get failed" );
			return ProcessOperationResult<ProcessPriorityValue>.Success( new ProcessPriorityValue( this.Current, false ) );
		}
		public ProcessOperationResult SetPriority( ProcessTarget target, int niceValue ) {
			this.LastSetValue = niceValue;
			if ( null != this.SetFailure ) return ProcessOperationResult.Failure( this.SetFailure.Value, "denied" );
			this.Current = niceValue;
			return ProcessOperationResult.Success();
		}
		public ProcessOperationResult<ProcessPriorityValue> AdjustPriority( ProcessTarget target, int increment ) {
			var current = this.GetPriority( target );
			if ( !current.Succeeded ) return current;
			var value = Math.Clamp( current.Value!.NiceValue + increment, -20, 19 );
			var changed = this.SetPriority( target, value );
			return changed.Succeeded
				? this.GetPriority( target )
				: ProcessOperationResult<ProcessPriorityValue>.Failure( changed.Status, changed.Message, changed.NativeErrorCode );
		}
	}
}
