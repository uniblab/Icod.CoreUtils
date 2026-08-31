namespace Icod.CoreUtils.StdBuf.Tests;

using System.Runtime.InteropServices;
using Icod.Processes;
using Xunit;

public sealed class CommandTests {
	[Fact]
	public async Task HelpDoesNotRequireNativeSupport() {
		var output = new StringWriter();
		var executor = new FakeProcessExecutor();
		var status = await Command.RunAsync(
			new[] { "--help" },
			stdout: output,
			processExecutor: executor,
			platform: FakeStdBufPlatform.Unsupported()
		);
		Assert.Equal( 0, status );
		Assert.Contains( "Usage: stdbuf", output.ToString() );
		Assert.Null( executor.Options );
	}

	[Fact]
	public async Task MissingOperandUsesInternalFailureStatus() {
		var error = new StringWriter();
		var status = await Command.RunAsync(
			new[] { "-oL" },
			stderr: error,
			processExecutor: new FakeProcessExecutor(),
			platform: FakeStdBufPlatform.Supported()
		);
		Assert.Equal( 125, status );
		Assert.Contains( "missing operand", error.ToString() );
	}

	[Fact]
	public async Task RequiresAtLeastOneBufferingMode() {
		var error = new StringWriter();
		var executor = new FakeProcessExecutor();
		var status = await Command.RunAsync(
			new[] { "program" },
			stderr: error,
			processExecutor: executor,
			platform: FakeStdBufPlatform.Supported()
		);
		Assert.Equal( 125, status );
		Assert.Contains( "buffering mode option", error.ToString() );
		Assert.Null( executor.Options );
	}

	[Fact]
	public async Task LineBufferedInputIsRejected() {
		var error = new StringWriter();
		var status = await Command.RunAsync(
			new[] { "-iL", "program" },
			stderr: error,
			processExecutor: new FakeProcessExecutor(),
			platform: FakeStdBufPlatform.Supported()
		);
		Assert.Equal( 125, status );
		Assert.Contains( "line buffering standard input is meaningless", error.ToString() );
	}

	[Fact]
	public async Task BufferSizesNormalizeGnuSuffixes() {
		var executor = new FakeProcessExecutor();
		var status = await Command.RunAsync(
			new[] { "--output=1KB", "--error", "2MiB", "program" },
			processExecutor: executor,
			platform: FakeStdBufPlatform.Supported(),
			environmentVariableProvider: static _ => null
		);
		Assert.Equal( 0, status );
		var options = Assert.IsType<ProcessRunOptions>( executor.Options );
		Assert.Equal( "1000", options.EnvironmentVariables[ "_STDBUF_O" ] );
		Assert.Equal( "2097152", options.EnvironmentVariables[ "_STDBUF_E" ] );
	}

	[Fact]
	public async Task ChildArgumentsRemainLiteralAndStopOptionParsing() {
		var executor = new FakeProcessExecutor {
			Result = ProcessResult.FromTermination(
				ProcessTermination.Exited( 17 )
			)
		};
		var status = await Command.RunAsync(
			new[] { "-oL", "program", "arg with spaces", "--output=0", "; rm -rf /" },
			processExecutor: executor,
			platform: FakeStdBufPlatform.Supported(),
			environmentVariableProvider: static name => "LD_PRELOAD" == name
				? "/old/preload.so"
				: null
		);
		Assert.Equal( 17, status );
		var options = Assert.IsType<ProcessRunOptions>( executor.Options );
		Assert.Equal( "program", options.FileName );
		Assert.Equal(
			new[] { "arg with spaces", "--output=0", "; rm -rf /" },
			options.Arguments
		);
		Assert.Equal( "L", options.EnvironmentVariables[ "_STDBUF_O" ] );
		Assert.Equal(
			"/old/preload.so:/opt/icod/libicodstdbuf.so",
			options.EnvironmentVariables[ "LD_PRELOAD" ]
		);
		Assert.True( options.ResolveExecutable );
		Assert.True( options.ReturnLaunchFailureResult );
	}

	[Fact]
	public async Task PosixHostCanRequestCurrentProcessReplacement() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}
		var executor = new FakeProcessExecutor();
		var status = await Command.RunAsync(
			new[] { "-oL", "program" },
			processExecutor: executor,
			platform: FakeStdBufPlatform.Supported(),
			environmentVariableProvider: static _ => null,
			replaceCurrentProcess: true
		);
		Assert.Equal( 0, status );
		var options = Assert.IsType<ProcessRunOptions>( executor.Options );
		Assert.True( options.ReplaceCurrentProcess );
		Assert.Equal( "program", options.ArgumentZero );
	}

	/// <summary>Verifies the standalone host really replaces itself instead of supervising an extra child process.</summary>
	[Fact]
	public async Task StandaloneLinuxStdBufPreservesProcessIdentity() {
		if ( !OperatingSystem.IsLinux() ) {
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
			"stdbuf.dll"
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
			"-o0"
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

	[Fact]
	public async Task LaterModeOptionReplacesEarlierMode() {
		var executor = new FakeProcessExecutor();
		var status = await Command.RunAsync(
			new[] { "-oL", "--output=0", "program" },
			processExecutor: executor,
			platform: FakeStdBufPlatform.Supported(),
			environmentVariableProvider: static _ => null
		);
		Assert.Equal( 0, status );
		var options = Assert.IsType<ProcessRunOptions>( executor.Options );
		Assert.Equal( "0", options.EnvironmentVariables[ "_STDBUF_O" ] );
	}

	[Fact]
	public async Task UnsupportedPlatformNeverLaunchesChild() {
		var error = new StringWriter();
		var executor = new FakeProcessExecutor();
		var status = await Command.RunAsync(
			new[] { "-o0", "program" },
			stderr: error,
			processExecutor: executor,
			platform: FakeStdBufPlatform.Unsupported()
		);
		Assert.Equal( 125, status );
		Assert.Contains( "unsupported", error.ToString() );
		Assert.Null( executor.Options );
	}

	[Fact]
	public async Task NotFoundAndCannotInvokeStatusesArePreserved() {
		var notFoundError = new StringWriter();
		var cannotInvokeError = new StringWriter();
		var notFound = new FakeProcessExecutor {
			Result = ProcessResult.FromTermination(
				ProcessTermination.LaunchFailed(
					"not found",
					ProcessLaunchFailureKind.NotFound
				),
				started: false
			)
		};
		var cannotInvoke = new FakeProcessExecutor {
			Result = ProcessResult.FromTermination(
				ProcessTermination.LaunchFailed(
					"permission denied",
					ProcessLaunchFailureKind.CannotInvoke
				),
				started: false
			)
		};
		Assert.Equal(
			127,
			await Command.RunAsync(
				new[] { "-o0", "missing" },
				stderr: notFoundError,
				processExecutor: notFound,
				platform: FakeStdBufPlatform.Supported(),
				environmentVariableProvider: static _ => null
			)
		);
		Assert.Contains(
			"failed to run command 'missing': not found",
			notFoundError.ToString()
		);
		Assert.Equal(
			126,
			await Command.RunAsync(
				new[] { "-o0", "blocked" },
				stderr: cannotInvokeError,
				processExecutor: cannotInvoke,
				platform: FakeStdBufPlatform.Supported(),
				environmentVariableProvider: static _ => null
			)
		);
		Assert.Contains(
			"failed to run command 'blocked': permission denied",
			cannotInvokeError.ToString()
		);
	}

	[Fact]
	public void LinuxPlatformPrefersArchitectureQualifiedNativeShim() {
		if ( !OperatingSystem.IsLinux() ) {
			return;
		}

		var architectureName = RuntimeInformation.OSArchitecture switch {
			Architecture.X64 => "x64",
			Architecture.Arm64 => "arm64",
			_ => null
		};
		if ( null == architectureName ) {
			return;
		}

		var fallbackPath = System.IO.Path.Combine(
			AppContext.BaseDirectory,
			"libicodstdbuf.so"
		);
		Assert.True(
			File.Exists( fallbackPath ),
			$"Missing native buffering shim: {fallbackPath}"
		);
		var qualifiedPath = System.IO.Path.Combine(
			AppContext.BaseDirectory,
			$"libicodstdbuf-linux-{architectureName}.so"
		);
		var createdQualifiedShim = false;
		if ( !File.Exists( qualifiedPath ) ) {
			File.Copy(
				fallbackPath,
				qualifiedPath
			);
			createdQualifiedShim = true;
		}

		try {
			Assert.True(
				SystemStdBufPlatform.Instance.TryGetPreloadConfiguration(
					out var configuration,
					out var unsupportedReason
				),
				unsupportedReason
			);
			Assert.Equal(
				System.IO.Path.GetFullPath( qualifiedPath ),
				configuration.LibraryPath
			);
		} finally {
			if ( createdQualifiedShim ) {
				File.Delete( qualifiedPath );
			}
		}
	}

	[Fact]
	public async Task LinuxNativeShimAppliesLineBuffering() {
		if ( !OperatingSystem.IsLinux() ) {
			return;
		}

		var probe = System.IO.Path.Combine(
			AppContext.BaseDirectory,
			"stdbuf-buffering-probe"
		);
		Assert.True( File.Exists( probe ), $"Missing native buffering probe: {probe}" );
		var executor = new CapturingProcessExecutor();
		var status = await Command.RunAsync(
			new[] { "-oL", probe },
			processExecutor: executor,
			platform: SystemStdBufPlatform.Instance,
			environmentVariableProvider: static _ => null
		);
		Assert.Equal( 0, status );
		var result = Assert.IsType<ProcessResult>( executor.Result );
		Assert.Contains( "first=0 newline=2 flush=0", result.StandardOutput! );
	}

	private sealed class FakeProcessExecutor : IProcessExecutor {
		public ProcessRunOptions? Options { get; private set; }
		public ProcessResult Result { get; set; } = ProcessResult.FromTermination(
			ProcessTermination.Exited( 0 )
		);

		public Task<ProcessResult> RunAsync(
			ProcessRunOptions options,
			CancellationToken cancellationToken = default
		) {
			_ = cancellationToken;
			this.Options = options;
			return Task.FromResult( this.Result );
		}
	}

	private sealed class CapturingProcessExecutor : IProcessExecutor {
		public ProcessResult? Result { get; private set; }

		public async Task<ProcessResult> RunAsync(
			ProcessRunOptions options,
			CancellationToken cancellationToken = default
		) {
			options.CaptureStandardOutput = true;
			options.CaptureStandardError = true;
			var result = await SystemProcessExecutor.Instance.RunAsync(
				options,
				cancellationToken
			).ConfigureAwait( false );
			this.Result = result;
			return result;
		}
	}

	private sealed class FakeStdBufPlatform : IStdBufPlatform {
		private readonly bool _supported;

		private FakeStdBufPlatform(
			bool supported
		) {
			this._supported = supported;
		}

		public static FakeStdBufPlatform Supported() => new( true );
		public static FakeStdBufPlatform Unsupported() => new( false );

		public bool TryGetPreloadConfiguration(
			out StdBufPreloadConfiguration configuration,
			out string unsupportedReason
		) {
			if ( !this._supported ) {
				configuration = default;
				unsupportedReason = "test platform has no preload support";
				return false;
			}
			configuration = new StdBufPreloadConfiguration(
				"LD_PRELOAD",
				"/opt/icod/libicodstdbuf.so",
				":"
			);
			unsupportedReason = string.Empty;
			return true;
		}
	}
}
