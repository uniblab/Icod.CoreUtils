namespace Icod.CoreUtils.Shared.Processes;

using System.Diagnostics;

/// <summary>
/// Executes child processes without shell quoting, blocking waits, or redirected-stream deadlocks.
/// </summary>
public static class ProcessRunner {

	/// <summary>
	/// Runs a child process asynchronously.
	/// </summary>
	public static async Task<ProcessResult> RunAsync(
		ProcessRunOptions options,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			options
		);
		ArgumentNullException.ThrowIfNull(
			options.OutputEncoding
		);
		if ( cancellationToken.IsCancellationRequested ) {
			return new ProcessResult(
				null,
				true,
				null,
				null
			);
		}

		var startInfo = new ProcessStartInfo {
			FileName = options.FileName,
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
		if ( options.ClearEnvironment ) {
			startInfo.Environment.Clear();
		}
		foreach ( var pair in options.EnvironmentVariables ) {
			var value = pair.Value;
			if ( null == value ) {
				startInfo.Environment.Remove(
					pair.Key
				);
			} else {
				startInfo.Environment[ pair.Key ] = value;
			}
		}

		using var process = new Process {
			StartInfo = startInfo,
			EnableRaisingEvents = true
		};
		if ( !process.Start() ) {
			throw new InvalidOperationException(
				$"Unable to start process '{options.FileName}'."
			);
		}

		using var capturedOutput = options.CaptureStandardOutput
			? new MemoryStream()
			: null
		;
		using var capturedError = options.CaptureStandardError
			? new MemoryStream()
			: null
		;

		using var inputCancellation = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken
		);
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
				cancellationToken
			)
			: Task.CompletedTask
		;
		var errorTask = startInfo.RedirectStandardError
			? ForwardOutputAsync(
				process.StandardError.BaseStream,
				options.StandardError,
				capturedError,
				cancellationToken
			)
			: Task.CompletedTask
		;

		var canceled = false;
		try {
			await process.WaitForExitAsync(
				cancellationToken
			).ConfigureAwait( false );
			inputCancellation.Cancel();
			await Task.WhenAll(
				outputTask,
				errorTask
			).ConfigureAwait( false );
			await IgnoreCancellationAsync(
				inputTask
			).ConfigureAwait( false );
		} catch ( OperationCanceledException ) {
			canceled = true;
			inputCancellation.Cancel();
			TryKill(
				process,
				options.KillEntireProcessTreeOnCancellation
			);
			await process.WaitForExitAsync().ConfigureAwait( false );
			await IgnoreCancellationAsync(
				inputTask,
				outputTask,
				errorTask
			).ConfigureAwait( false );
		}

		return new ProcessResult(
			process.HasExited
				? process.ExitCode
				: null,
			canceled,
			Decode(
				capturedOutput,
				options.OutputEncoding
			),
			Decode(
				capturedError,
				options.OutputEncoding
			)
		);
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
			await destination.DisposeAsync().ConfigureAwait( false );
		}
	}

	private static async Task ForwardOutputAsync(
		Stream source,
		Stream? destination,
		MemoryStream? capture,
		CancellationToken cancellationToken
	) {
		var buffer = new byte[ 65536 ];
		while ( true ) {
			var read = await source.ReadAsync(
				buffer.AsMemory(),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				break;
			}
			if ( null != destination ) {
				await destination.WriteAsync(
					buffer.AsMemory(
						0,
						read
					),
					cancellationToken
				).ConfigureAwait( false );
			}
			if ( null != capture ) {
				await capture.WriteAsync(
					buffer.AsMemory(
						0,
						read
					),
					cancellationToken
				).ConfigureAwait( false );
			}
		}
		if ( null != destination ) {
			await destination.FlushAsync(
				cancellationToken
			).ConfigureAwait( false );
		}
	}

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
		}
	}

	private static void TryKill(
		Process process,
		bool entireProcessTree
	) {
		try {
			if ( !process.HasExited ) {
				process.Kill(
					entireProcessTree
				);
			}
		} catch ( PlatformNotSupportedException ) when ( entireProcessTree ) {
			TryKillSingleProcess(
				process
			);
		} catch ( NotSupportedException ) when ( entireProcessTree ) {
			TryKillSingleProcess(
				process
			);
		} catch ( InvalidOperationException ) {
		} catch ( System.ComponentModel.Win32Exception ) {
		}
	}

	private static void TryKillSingleProcess(
		Process process
	) {
		try {
			if ( !process.HasExited ) {
				process.Kill();
			}
		} catch ( InvalidOperationException ) {
		} catch ( System.ComponentModel.Win32Exception ) {
		}
	}

	private static string? Decode(
		MemoryStream? stream,
		System.Text.Encoding encoding
	) {
		if ( null == stream ) {
			return null;
		}
		return encoding.GetString(
			stream.GetBuffer(),
			0,
			checked( (int)stream.Length )
		);
	}

}
