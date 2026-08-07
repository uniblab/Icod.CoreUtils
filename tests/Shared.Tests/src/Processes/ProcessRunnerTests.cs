namespace Icod.CoreUtils.Shared.Tests.Processes;

using Xunit;
using System.Text;
using Icod.CoreUtils.Shared.Processes;
using Icod.CoreUtils.Shared.Time;

/// <summary>
/// Exercises argument-safe launch, stream forwarding, environment construction, working directories, and timeout cleanup.
/// </summary>
public sealed class ProcessRunnerTests {
	/// <summary>Verifies that arguments are forwarded without shell interpolation.</summary>
	[Fact]
	public async Task PreservesExactArguments() {
		var options = CreateHostOptions(
			"args",
			"a value with spaces",
			"\"quoted\"",
			"semi;colon",
			string.Empty
		);
		options.CaptureStandardOutput = true;
		ProcessIdentity? startedIdentity = null;
		options.ProcessStarted = identity => startedIdentity = identity;

		var result = await ProcessRunner.RunAsync(
			options
		);

		Assert.Equal(
			0,
			result.ExitCode
		);
		Assert.True(
			result.Started
		);
		Assert.NotNull(
			result.Identity
		);
		Assert.Equal(
			result.Identity,
			startedIdentity
		);
		var expected = string.Concat(
			new[] {
				"a value with spaces",
				"\"quoted\"",
				"semi;colon",
				string.Empty
			}.Select(
				value => string.Concat(
					"B:",
					Convert.ToBase64String(
						Encoding.UTF8.GetBytes( value )
					),
					Environment.NewLine
				)
			)
		);
		Assert.Equal(
			expected,
			result.StandardOutput
		);
	}

	/// <summary>Verifies inherited environment modification and working-directory selection.</summary>
	[Fact]
	public async Task AppliesEnvironmentAndWorkingDirectory() {
		var environmentOptions = CreateHostOptions(
			"environment",
			"ICOD_F4_VALUE"
		);
		environmentOptions.Environment = ProcessEnvironment.CreateInheritedBuilder()
			.Set(
				"ICOD_F4_VALUE",
				"expected"
			)
			.Build();
		environmentOptions.CaptureStandardOutput = true;
		var environmentResult = await ProcessRunner.RunAsync(
			environmentOptions
		);

		var directory = Path.Combine(
			AppContext.BaseDirectory,
			$"icod-f4-cwd-{Guid.NewGuid():N}"
		);
		Directory.CreateDirectory(
			directory
		);
		try {
			var directoryOptions = CreateHostOptions(
				"cwd"
			);
			directoryOptions.WorkingDirectory = directory;
			directoryOptions.CaptureStandardOutput = true;
			var directoryResult = await ProcessRunner.RunAsync(
				directoryOptions
			);

			Assert.Equal(
				"expected",
				environmentResult.StandardOutput
			);
			Assert.Equal(
				Path.GetFullPath( directory ),
				Path.GetFullPath( directoryResult.StandardOutput! )
			);
		} finally {
			Directory.Delete(
				directory,
				true
			);
		}
	}

	/// <summary>Verifies controlled not-found launch classification and GNU-facing status translation.</summary>
	[Fact]
	public async Task ClassifiesMissingExecutable() {
		var options = new ProcessRunOptions(
			$"icod-f4-missing-{Guid.NewGuid():N}"
		) {
			ResolveExecutable = true,
			ReturnLaunchFailureResult = true
		};

		var result = await ProcessRunner.RunAsync(
			options
		);

		Assert.False(
			result.Started
		);
		Assert.Equal(
			ProcessTerminationKind.LaunchFailed,
			result.Termination.Kind
		);
		Assert.Equal(
			ProcessLaunchFailureKind.NotFound,
			result.Termination.LaunchFailureKind
		);
		Assert.Equal(
			127,
			result.Termination.ToPortableExitCode()
		);
	}

	/// <summary>Verifies monotonic timeout classification and child cleanup.</summary>
	[Fact]
	public async Task TimesOutAndTerminatesChild() {
		var options = CreateHostOptions(
			"sleep",
			"30000"
		);
		options.Timeout = TimeSpan.FromSeconds(
			30
		);
		var executor = new SystemProcessExecutor(
			SystemExecutableLocator.Instance,
			SystemProcessInspector.Instance,
			new ImmediateTimeoutClock()
		);

		var result = await executor.RunAsync(
			options
		);

		Assert.True(
			result.Started
		);
		Assert.True(
			result.TimedOut
		);
		Assert.False(
			result.WasCanceled
		);
		Assert.True(
			TimeSpan.Zero < result.Elapsed
		);
	}

	private sealed class ImmediateTimeoutClock : IMonotonicClock {
		private long _timestamp;

		/// <summary>Initializes an immediate timeout clock.</summary>
		public ImmediateTimeoutClock() {
		}

		/// <inheritdoc />
		public long GetTimestamp() => Interlocked.Read(
			ref this._timestamp
		);

		/// <inheritdoc />
		public TimeSpan GetElapsedTime(
			long startingTimestamp,
			long endingTimestamp
		) => TimeSpan.FromTicks(
			endingTimestamp - startingTimestamp
		);

		/// <inheritdoc />
		public ValueTask DelayAsync(
			TimeSpan delay,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			Interlocked.Add(
				ref this._timestamp,
				delay.Ticks
			);
			return ValueTask.CompletedTask;
		}
	}

	private static ProcessRunOptions CreateHostOptions(
		params string[] arguments
	) {
		var host = GetProcessTestHostPath();
		Assert.True(
			File.Exists( host ),
			$"Process test host was not built at '{host}'."
		);
		var dotnet = Environment.GetEnvironmentVariable(
			"DOTNET_HOST_PATH"
		) ?? "dotnet";
		var options = new ProcessRunOptions(
			dotnet
		) {
			ReturnLaunchFailureResult = true
		};
		options.Arguments.Add(
			host
		);
		foreach ( var argument in arguments ) {
			options.Arguments.Add(
				argument
			);
		}
		return options;
	}

	private static string GetProcessTestHostPath() {
		var targetFrameworkDirectory = new DirectoryInfo(
			AppContext.BaseDirectory
		);
		var configurationDirectory = targetFrameworkDirectory.Parent
			?? throw new InvalidOperationException( "Unable to locate the test configuration directory." );
		var testsDirectory = configurationDirectory.Parent?.Parent?.Parent
			?? throw new InvalidOperationException( "Unable to locate the tests directory." );
		return Path.Combine(
			testsDirectory.FullName,
			"ProcessTestHost",
			"bin",
			configurationDirectory.Name,
			targetFrameworkDirectory.Name,
			"Icod.CoreUtils.ProcessTestHost.dll"
		);
	}
}
