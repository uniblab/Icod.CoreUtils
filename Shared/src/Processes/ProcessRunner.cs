namespace Icod.CoreUtils.Shared.Processes;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Icod.CoreUtils.Shared.Time;

/// <summary>
/// Provides the compatibility facade for shared child-process execution.
/// </summary>
public static class ProcessRunner {
	/// <summary>Runs a child process asynchronously through the system executor.</summary>
	public static Task<ProcessResult> RunAsync(
		ProcessRunOptions options,
		CancellationToken cancellationToken = default
	) => SystemProcessExecutor.Instance.RunAsync(
		options,
		cancellationToken
	);
}

/// <summary>
/// Executes child processes without shell quoting, blocking waits, or redirected-stream deadlocks.
/// </summary>
public sealed class SystemProcessExecutor : IProcessExecutor {
	private readonly IExecutableLocator _executableLocator;
	private readonly IProcessInspector _processInspector;
	private readonly IMonotonicClock _clock;

	/// <summary>Gets the shared system process executor.</summary>
	public static SystemProcessExecutor Instance {
		get;
	} = new(
		SystemExecutableLocator.Instance,
		SystemProcessInspector.Instance,
		SystemMonotonicClock.Instance
	);

	/// <summary>Initializes a system process executor with injectable providers.</summary>
	public SystemProcessExecutor(
		IExecutableLocator executableLocator,
		IProcessInspector processInspector,
		IMonotonicClock clock
	) {
		ArgumentNullException.ThrowIfNull(
			executableLocator
		);
		ArgumentNullException.ThrowIfNull(
			processInspector
		);
		ArgumentNullException.ThrowIfNull(
			clock
		);
		this._executableLocator = executableLocator;
		this._processInspector = processInspector;
		this._clock = clock;
	}

	/// <inheritdoc />
	public async Task<ProcessResult> RunAsync(
		ProcessRunOptions options,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			options
		);
		ArgumentNullException.ThrowIfNull(
			options.OutputEncoding
		);
		var timeout = options.Timeout;
		if ( null != timeout && TimeSpan.Zero >= timeout.Value ) {
			throw new ArgumentOutOfRangeException(
				nameof( options ),
				"The process timeout must be positive."
			);
		}
		var startedTimestamp = this._clock.GetTimestamp();
		if ( cancellationToken.IsCancellationRequested ) {
			return new ProcessResult(
				false,
				null,
				ProcessTermination.Canceled(),
				TimeSpan.Zero,
				null,
				null
			);
		}

		var environment = BuildEffectiveEnvironment(
			options
		);
		var executable = options.FileName;
		if ( options.ResolveExecutable ) {
			var located = this._executableLocator.Locate(
				executable,
				environment,
				options.WorkingDirectory
			);
			if ( !located.Succeeded ) {
				var message = located.Message ?? $"Unable to locate executable '{executable}'.";
				var failureKind = ProcessOperationStatus.Vanished == located.Status
					? ProcessLaunchFailureKind.NotFound
					: ProcessLaunchFailureKind.CannotInvoke
				;
				if ( !options.ReturnLaunchFailureResult ) {
					if ( ProcessLaunchFailureKind.NotFound == failureKind ) {
						throw new FileNotFoundException(
							message,
							executable
						);
					}
					throw new InvalidOperationException(
						message
					);
				}
				return this.CreateLaunchFailure(
					startedTimestamp,
					message,
					failureKind
				);
			}
			executable = located.Value!;
		}
		var startInfo = BuildStartInfo(
			options,
			executable,
			environment
		);
		using var process = new Process {
			StartInfo = startInfo,
			EnableRaisingEvents = true
		};
		try {
			if ( !process.Start() ) {
				const string message = "The operating system declined to start the process.";
				if ( !options.ReturnLaunchFailureResult ) {
					throw new InvalidOperationException(
						message
					);
				}
				return this.CreateLaunchFailure(
					startedTimestamp,
					message,
					ProcessLaunchFailureKind.CannotInvoke
				);
			}
		} catch ( Exception exception ) when (
			exception is Win32Exception
			or FileNotFoundException
			or DirectoryNotFoundException
			or InvalidOperationException
			or UnauthorizedAccessException
		) {
			if ( !options.ReturnLaunchFailureResult ) {
				throw;
			}
			return this.CreateLaunchFailure(
				startedTimestamp,
				exception.Message,
				ClassifyLaunchFailure(
					exception,
					options.WorkingDirectory
				)
			);
		}

		var identityResult = this._processInspector.ObserveIdentity(
			process.Id
		);
		var identity = identityResult.Succeeded
			? identityResult.Value!
			: new ProcessIdentity(
				process.Id
			)
		;
		using var capturedOutput = options.CaptureStandardOutput
			? new MemoryStream()
			: null
		;
		using var capturedError = options.CaptureStandardError
			? new MemoryStream()
			: null
		;
		using var timeoutCancellation = null == timeout
			? null
			: new CancellationTokenSource()
		;
		using var timeoutDelayCancellation = null == timeout
			? null
			: new CancellationTokenSource()
		;
		using var executionCancellation = null == timeoutCancellation
			? CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken
			)
			: CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken,
				timeoutCancellation.Token
			)
		;
		using var inputCancellation = CancellationTokenSource.CreateLinkedTokenSource(
			executionCancellation.Token
		);
		using var forwardingCancellation = CancellationTokenSource.CreateLinkedTokenSource(
			executionCancellation.Token
		);
		var timeoutTask = null == timeoutCancellation || null == timeoutDelayCancellation || null == timeout
			? Task.CompletedTask
			: this.TriggerTimeoutAsync(
				timeout.Value,
				timeoutCancellation,
				timeoutDelayCancellation.Token
			)
		;
		var inputTask = startInfo.RedirectStandardInput
			? ForwardInputAsync(
				options.StandardInput!,
				process.StandardInput.BaseStream,
				inputCancellation.Token
			)
			: Task.CompletedTask
		;
		var outputTask = startInfo.RedirectStandardOutput
			? ForwardOutputAsync(
				process.StandardOutput.BaseStream,
				options.StandardOutput,
				capturedOutput,
				forwardingCancellation.Token
			)
			: Task.CompletedTask
		;
		var errorTask = startInfo.RedirectStandardError
			? ForwardOutputAsync(
				process.StandardError.BaseStream,
				options.StandardError,
				capturedError,
				forwardingCancellation.Token
			)
			: Task.CompletedTask
		;
		try {
			options.ProcessStarted?.Invoke(
				identity
			);
			ProcessTermination termination;
			try {
				await process.WaitForExitAsync(
					executionCancellation.Token
				).ConfigureAwait( false );
				inputCancellation.Cancel();
				await Task.WhenAll(
					outputTask,
					errorTask
				).ConfigureAwait( false );
				termination = ProcessTermination.Exited(
					process.ExitCode
				);
			} catch ( OperationCanceledException ) when ( executionCancellation.IsCancellationRequested ) {
				var timedOut = null != timeoutCancellation
					&& timeoutCancellation.IsCancellationRequested
					&& !cancellationToken.IsCancellationRequested
				;
				inputCancellation.Cancel();
				forwardingCancellation.Cancel();
				if ( ProcessCancellationPolicy.LeaveRunning == options.CancellationPolicy ) {
					TryDisconnectRedirectedStreams(
						process,
						startInfo
					);
					termination = timedOut
						? ProcessTermination.TimedOut()
						: ProcessTermination.Canceled()
					;
				} else {
					var terminationRequested = TryKill(
						process,
						ProcessCancellationPolicy.KillProcessTree == options.CancellationPolicy
					);
					if ( terminationRequested ) {
						await WaitForExitAfterTerminationAsync(
							process
						).ConfigureAwait( false );
					} else {
						TryDisconnectRedirectedStreams(
							process,
							startInfo
						);
					}
					var exitCode = TryGetExitCode(
						process
					);
					termination = timedOut
						? ProcessTermination.TimedOut( exitCode )
						: ProcessTermination.Canceled( exitCode )
					;
				}
			}
			return new ProcessResult(
				true,
				identity,
				termination,
				this._clock.GetElapsedTime(
					startedTimestamp,
					this._clock.GetTimestamp()
				),
				Decode(
					capturedOutput,
					options.OutputEncoding
				),
				Decode(
					capturedError,
					options.OutputEncoding
				)
			);
		} catch {
			inputCancellation.Cancel();
			forwardingCancellation.Cancel();
			var terminationRequested = TryKill(
				process,
				ProcessCancellationPolicy.KillProcessTree == options.CancellationPolicy
			);
			if ( terminationRequested ) {
				await WaitForExitAfterTerminationAsync(
					process
				).ConfigureAwait( false );
			} else {
				TryDisconnectRedirectedStreams(
					process,
					startInfo
				);
			}
			throw;
		} finally {
			timeoutDelayCancellation?.Cancel();
			inputCancellation.Cancel();
			forwardingCancellation.Cancel();
			ObserveCompletion(
				timeoutTask,
				inputTask,
				outputTask,
				errorTask
			);
		}
	}

	private async Task TriggerTimeoutAsync(
		TimeSpan timeout,
		CancellationTokenSource timeoutCancellation,
		CancellationToken cancellationToken
	) {
		try {
			await this._clock.DelayAsync(
				timeout,
				cancellationToken
			).ConfigureAwait( false );
			if ( !cancellationToken.IsCancellationRequested ) {
				timeoutCancellation.Cancel();
			}
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
		}
	}

	private ProcessResult CreateLaunchFailure(
		long startedTimestamp,
		string message,
		ProcessLaunchFailureKind failureKind
	) => new(
		false,
		null,
		ProcessTermination.LaunchFailed(
			message,
			failureKind
		),
		this._clock.GetElapsedTime(
			startedTimestamp,
			this._clock.GetTimestamp()
		),
		null,
		null
	);

	private static ProcessLaunchFailureKind ClassifyLaunchFailure(
		Exception exception,
		string? workingDirectory
	) => exception switch {
		DirectoryNotFoundException when !string.IsNullOrEmpty( workingDirectory )
			&& !Directory.Exists( workingDirectory ) => ProcessLaunchFailureKind.CannotInvoke,
		FileNotFoundException or DirectoryNotFoundException => ProcessLaunchFailureKind.NotFound,
		Win32Exception win32Exception when ( ( 2 == win32Exception.NativeErrorCode ) || ( 3 == win32Exception.NativeErrorCode ) ) => ProcessLaunchFailureKind.NotFound,
		_ => ProcessLaunchFailureKind.CannotInvoke
	};

	private static ProcessEnvironment BuildEffectiveEnvironment(
		ProcessRunOptions options
	) {
		var explicitEnvironment = options.Environment;
		var builder = null == explicitEnvironment
			? new ProcessEnvironmentBuilder(
				!options.ClearEnvironment
			)
			: ProcessEnvironment.CreateEmptyBuilder()
		;
		if ( null != explicitEnvironment ) {
			foreach ( var pair in explicitEnvironment.Variables ) {
				builder.Set(
					pair.Key,
					pair.Value
				);
			}
		}
		foreach ( var pair in options.EnvironmentVariables ) {
			if ( null == pair.Value ) {
				builder.Remove(
					pair.Key
				);
			} else {
				builder.Set(
					pair.Key,
					pair.Value
				);
			}
		}
		return builder.Build();
	}

	private static ProcessStartInfo BuildStartInfo(
		ProcessRunOptions options,
		string executable,
		ProcessEnvironment environment
	) {
		var startInfo = new ProcessStartInfo {
			FileName = executable,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardInput = null != options.StandardInput,
			RedirectStandardOutput = null != options.StandardOutput || options.CaptureStandardOutput,
			RedirectStandardError = null != options.StandardError || options.CaptureStandardError
		};
		if ( !string.IsNullOrEmpty( options.WorkingDirectory ) ) {
			startInfo.WorkingDirectory = options.WorkingDirectory;
		}
		foreach ( var argument in options.Arguments ) {
			startInfo.ArgumentList.Add(
				argument
			);
		}
		startInfo.Environment.Clear();
		foreach ( var pair in environment.Variables ) {
			startInfo.Environment[ pair.Key ] = pair.Value;
		}
		return startInfo;
	}

	private static async Task ForwardInputAsync(
		Stream source,
		Stream destination,
		CancellationToken cancellationToken
	) {
		try {
			await source.CopyToAsync(
				destination,
				cancellationToken
			).ConfigureAwait( false );
			await destination.FlushAsync(
				cancellationToken
			).ConfigureAwait( false );
		} catch ( IOException ) {
			// The child may close standard input after reading all data it needs.
		} finally {
			try {
				await destination.DisposeAsync().ConfigureAwait( false );
			} catch ( IOException ) {
			} catch ( ObjectDisposedException ) {
			}
		}
	}

	private static async Task ForwardOutputAsync(
		Stream source,
		Stream? destination,
		MemoryStream? capture,
		CancellationToken cancellationToken
	) {
		var buffer = new byte[ 65536 ];
		Exception? forwardingFailure = null;
		while ( true ) {
			var read = await source.ReadAsync(
				buffer.AsMemory(),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				break;
			}
			if ( null != capture ) {
				lock ( capture ) {
					capture.Write(
						buffer,
						0,
						read
					);
				}
			}
			if ( null != destination && null == forwardingFailure ) {
				try {
					await destination.WriteAsync(
						buffer.AsMemory(
							0,
							read
						),
						cancellationToken
					).ConfigureAwait( false );
				} catch ( Exception exception ) {
					forwardingFailure = exception;
				}
			}
		}
		if ( null != destination && null == forwardingFailure ) {
			try {
				await destination.FlushAsync(
					cancellationToken
				).ConfigureAwait( false );
			} catch ( Exception exception ) {
				forwardingFailure = exception;
			}
		}
		if ( null != forwardingFailure ) {
			ExceptionDispatchInfo.Capture(
				forwardingFailure
			).Throw();
		}
	}

	private static void ObserveCompletion(
		params Task[] tasks
	) => _ = IgnoreCancellationAsync(
		tasks
	);

	private static async Task IgnoreCancellationAsync(
		params Task[] tasks
	) {
		try {
			await Task.WhenAll(
				tasks
			).ConfigureAwait( false );
		} catch ( OperationCanceledException ) {
		} catch ( IOException ) {
		} catch ( ObjectDisposedException ) {
		} catch ( Exception ) {
		}
	}

	private static bool TryKill(
		Process process,
		bool entireProcessTree
	) {
		try {
			if ( process.HasExited ) {
				return true;
			}
			process.Kill(
				entireProcessTree
			);
			return true;
		} catch ( PlatformNotSupportedException ) when ( entireProcessTree ) {
			return TryKillSingleProcess(
				process
			);
		} catch ( NotSupportedException ) when ( entireProcessTree ) {
			return TryKillSingleProcess(
				process
			);
		} catch ( InvalidOperationException ) {
			return true;
		} catch ( Win32Exception ) {
			return false;
		}
	}

	private static bool TryKillSingleProcess(
		Process process
	) {
		try {
			if ( process.HasExited ) {
				return true;
			}
			process.Kill();
			return true;
		} catch ( InvalidOperationException ) {
			return true;
		} catch ( Win32Exception ) {
			return false;
		}
	}

	private static async Task WaitForExitAfterTerminationAsync(
		Process process
	) {
		try {
			await process.WaitForExitAsync().ConfigureAwait( false );
		} catch ( InvalidOperationException ) {
		} catch ( Win32Exception ) {
		}
	}

	private static void TryDisconnectRedirectedStreams(
		Process process,
		ProcessStartInfo startInfo
	) {
		if ( startInfo.RedirectStandardInput ) {
			TryDispose(
				process.StandardInput
			);
		}
		if ( startInfo.RedirectStandardOutput ) {
			TryDispose(
				process.StandardOutput
			);
		}
		if ( startInfo.RedirectStandardError ) {
			TryDispose(
				process.StandardError
			);
		}
	}

	private static void TryDispose(
		IDisposable disposable
	) {
		try {
			disposable.Dispose();
		} catch ( IOException ) {
		} catch ( ObjectDisposedException ) {
		}
	}

	private static int? TryGetExitCode(
		Process process
	) {
		try {
			return process.HasExited
				? process.ExitCode
				: null
			;
		} catch ( InvalidOperationException ) {
			return null;
		} catch ( Win32Exception ) {
			return null;
		}
	}

	private static string? Decode(
		MemoryStream? stream,
		System.Text.Encoding encoding
	) {
		if ( null == stream ) {
			return null;
		}
		lock ( stream ) {
			return encoding.GetString(
				stream.GetBuffer(),
				0,
				checked( (int)stream.Length )
			);
		}
	}
}
