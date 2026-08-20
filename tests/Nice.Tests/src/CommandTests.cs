namespace Icod.CoreUtils.Nice.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Processes;
using Xunit;

/// <summary>Exercises GNU 9.11 <c>nice</c> parsing, priority ordering, launch status, and failures.</summary>
public sealed class CommandTests {
	/// <summary>Verifies no-command mode reports the current F4 niceness.</summary>
	[Fact]
	public async Task PrintsCurrentNiceness() {
		var priorities = new FakePriorityProvider { Current = 7 };
		using var output = new MemoryStream();
		var status = await Command.RunAsync( Array.Empty<string>(), stdout: output, priorityProvider: priorities );
		Assert.Equal( 0, status );
		Assert.Equal( string.Concat( "7", Environment.NewLine ), Encoding.UTF8.GetString( output.ToArray() ) );
	}

	/// <summary>Verifies the historical GNU signed adjustment spellings.</summary>
	[Theory]
	[InlineData( "-10", 10 )]
	[InlineData( "--10", -10 )]
	[InlineData( "-+10", 10 )]
	public async Task ParsesHistoricalAdjustmentForms( string option, int expectedAdjustment ) {
		var priorities = new FakePriorityProvider();
		var executor = new ThrowingExecutor();
		var status = await Command.RunAsync( new[] { option, "child" }, processExecutor: executor, priorityProvider: priorities );
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
		using var error = new MemoryStream();
		var status = await Command.RunAsync(
			new[] { "-n", adjustment, "child" },
			stderr: error,
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
		using var error = new MemoryStream();
		var status = await Command.RunAsync( new[] { "child" }, stderr: error, processExecutor: executor, priorityProvider: priorities );
		Assert.Equal( 125, status );
		Assert.True( executor.Called );
		Assert.Contains( "cannot set niceness", Encoding.UTF8.GetString( error.ToArray() ), StringComparison.Ordinal );
	}

	/// <summary>Verifies failure to observe the current niceness prevents command launch.</summary>
	[Fact]
	public async Task GetPriorityFailurePreventsCommandLaunch() {
		var priorities = new FakePriorityProvider { GetFailure = ProcessOperationStatus.Failed };
		var executor = new ThrowingExecutor();
		using var error = new MemoryStream();
		var status = await Command.RunAsync( new[] { "child" }, stderr: error, processExecutor: executor, priorityProvider: priorities );
		Assert.Equal( 125, status );
		Assert.False( executor.Called );
		Assert.Contains( "cannot get niceness", Encoding.UTF8.GetString( error.ToArray() ), StringComparison.Ordinal );
	}

	/// <summary>Verifies explicit adjustment without a command is a GNU internal failure.</summary>
	[Fact]
	public async Task AdjustmentRequiresCommand() {
		using var error = new MemoryStream();
		var status = await Command.RunAsync( new[] { "-n", "4" }, stderr: error, priorityProvider: new FakePriorityProvider() );
		Assert.Equal( 125, status );
		Assert.Contains( "a command must be given", Encoding.UTF8.GetString( error.ToArray() ), StringComparison.Ordinal );
	}

	/// <summary>Verifies child exit status is propagated unchanged.</summary>
	[Fact]
	public async Task PropagatesChildExitStatus() {
		var host = GetProcessTestHostPath();
		Assert.True( File.Exists( host ), $"Process test host was not built at '{host}'." );
		var dotnet = Environment.GetEnvironmentVariable( "DOTNET_HOST_PATH" ) ?? "dotnet";
		var status = await Command.RunAsync(
			new[] { dotnet, host, "exit", "37" },
			priorityProvider: new FakePriorityProvider()
		);
		Assert.Equal( 37, status );
	}

	/// <summary>Verifies command-not-found maps to 127.</summary>
	[Fact]
	public async Task MissingCommandReturns127() {
		using var error = new MemoryStream();
		var status = await Command.RunAsync(
			new[] { $"icod-nice-missing-{Guid.NewGuid():N}" },
			stderr: error,
			priorityProvider: new FakePriorityProvider()
		);
		Assert.Equal( 127, status );
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

	private sealed class ThrowingExecutor : IProcessExecutor {
		internal bool Called { get; private set; }
		public Task<ProcessResult> RunAsync( ProcessRunOptions options, CancellationToken cancellationToken = default ) {
			this.Called = true;
			throw new InvalidOperationException( "test launch boundary" );
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
