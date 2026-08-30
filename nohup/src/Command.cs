namespace Icod.CoreUtils.Nohup;

using System.Text;
using Icod.Processes;
using Icod.Terminal;

/// <summary>
/// Implements GNU <c>nohup</c> 9.11 behavior.
/// </summary>
public static class Command {
	private const int DefaultInternalFailure = 125;
	private const int PosixInternalFailure = 127;
	private static readonly Encoding Utf8 = new UTF8Encoding( false );

	/// <summary>Runs GNU <c>nohup</c> asynchronously.</summary>
	public static async Task<int> RunAsync(
		string[] args,
		Stream? stdin = null,
		Stream? stdout = null,
		Stream? stderr = null,
		ITerminalControlProvider? terminalProvider = null,
		IProcessExecutor? processExecutor = null,
		INohupOutputFileProvider? outputFileProvider = null,
		INohupStandardStreamStateProvider? standardStreamStateProvider = null,
		ProcessEnvironment? sourceEnvironment = null,
		CancellationToken cancellationToken = default,
		Func<Stream>? standardOutputFactory = null,
		TextWriter? commandOutput = null,
		TextWriter? commandError = null,
		bool replaceCurrentProcess = false
	) {
		ArgumentNullException.ThrowIfNull( args );
		var environment = sourceEnvironment ?? ProcessEnvironment.CreateInheritedBuilder().Build();
		var internalFailure = environment.Variables.ContainsKey( "POSIXLY_CORRECT" ) ? PosixInternalFailure : DefaultInternalFailure;
		var terminal = terminalProvider ?? SystemTerminalControlProvider.Instance;
		var executor = processExecutor ?? SystemProcessExecutor.Instance;
		var files = outputFileProvider ?? SystemNohupOutputFileProvider.Instance;
		var standardStreams = standardStreamStateProvider ?? SystemNohupStandardStreamStateProvider.Instance;
		var openStandardOutput = standardOutputFactory ?? Console.OpenStandardOutput;
		var operands = new List<string>();
		if ( 0 < args.Length ) {
			var token = args[ 0 ];
			if ( "--help" == token ) {
				await WriteAsync( stdout, commandOutput, string.Concat( NormalizeLineEndings( HelpText ), Environment.NewLine ), cancellationToken ).ConfigureAwait( false );
				return 0;
			}
			if ( "--version" == token ) {
				await WriteAsync( stdout, commandOutput, string.Concat( "nohup (Icod.CoreUtils) 9.11", Environment.NewLine ), cancellationToken ).ConfigureAwait( false );
				return 0;
			}
			if ( "--" == token ) {
				operands.AddRange( args.Skip( 1 ) );
			} else if ( token.StartsWith( '-' ) && "-" != token ) {
				await WriteDiagnosticAsync( stderr, commandError, $"nohup: unrecognized option '{token}'", cancellationToken ).ConfigureAwait( false );
				await WriteDiagnosticAsync( stderr, commandError, "Try 'nohup --help' for more information.", cancellationToken ).ConfigureAwait( false );
				return internalFailure;
			} else {
				operands.AddRange( args );
			}
		}
		if ( 0 == operands.Count ) {
			await WriteDiagnosticAsync( stderr, commandError, "nohup: missing operand", cancellationToken ).ConfigureAwait( false );
			await WriteDiagnosticAsync( stderr, commandError, "Try 'nohup --help' for more information.", cancellationToken ).ConfigureAwait( false );
			return internalFailure;
		}

		var inputObservation = terminal.Observe( TerminalEndpoint.StandardInput );
		var outputObservation = terminal.Observe( TerminalEndpoint.StandardOutput );
		var errorObservation = terminal.Observe( TerminalEndpoint.StandardError );
		var inputTerminal = inputObservation.IsAvailable && inputObservation.GetRequiredValue().IsTerminal;
		var outputTerminal = outputObservation.IsAvailable && outputObservation.GetRequiredValue().IsTerminal;
		var errorTerminal = errorObservation.IsAvailable && errorObservation.GetRequiredValue().IsTerminal;
		var outputClosed = !outputTerminal && null == stdout && standardStreams.IsStandardOutputClosed();
		var useNativeStandardDescriptors = !OperatingSystem.IsWindows()
			&& null == stdin
			&& null == stdout
			&& null == stderr
		;
		NohupOutputDestination? destination = null;
		SynchronizedWriteStream? sharedOutput = null;
		int? nativeOutputDescriptor = null;
		try {
			if ( outputTerminal || ( errorTerminal && outputClosed ) ) {
				using var reservation = outputClosed
					? standardStreams.ReserveClosedStandardOutput()
					: null
				;
				destination = TryOpenDestination( files, environment, out var openError );
				if ( null == destination ) {
					await WriteDiagnosticAsync( stderr, commandError, $"nohup: {openError}", cancellationToken ).ConfigureAwait( false );
					return internalFailure;
				}
				if ( useNativeStandardDescriptors ) {
					nativeOutputDescriptor = destination.PosixFileDescriptor;
				}
				if ( !nativeOutputDescriptor.HasValue ) {
					sharedOutput = new SynchronizedWriteStream( destination.Stream );
				}
			}

			var useUnreadableStandardInput = inputTerminal && !OperatingSystem.IsWindows();
			Stream? childInput = inputTerminal && OperatingSystem.IsWindows() ? Stream.Null : stdin;
			Stream? childOutput = outputTerminal && nativeOutputDescriptor.HasValue
				? null
				: outputTerminal
					? sharedOutput
					: stdout
			;
			Stream? childError;
			if ( errorTerminal ) {
				if ( outputTerminal || outputClosed ) {
					childError = nativeOutputDescriptor.HasValue
						? null
						: sharedOutput
					;
				} else if ( null != stdout ) {
					childError = stdout;
				} else if ( useNativeStandardDescriptors ) {
					childError = null;
				} else {
					sharedOutput = new SynchronizedWriteStream( openStandardOutput(), false );
					childOutput = sharedOutput;
					childError = sharedOutput;
				}
			} else {
				childError = stderr;
			}

			if ( inputTerminal && !outputTerminal && !errorTerminal ) {
				await WriteDiagnosticAsync( stderr, commandError, "nohup: ignoring input", cancellationToken ).ConfigureAwait( false );
			}
			if ( outputTerminal && null != destination ) {
				var prefix = inputTerminal ? "ignoring input and " : string.Empty;
				await WriteDiagnosticAsync( stderr, commandError, $"nohup: {prefix}appending output to '{destination.Path}'", cancellationToken ).ConfigureAwait( false );
			}
			if ( errorTerminal && !outputTerminal ) {
				var prefix = inputTerminal ? "ignoring input and " : string.Empty;
				var action = outputClosed && null != destination
					? $"appending standard error to '{destination.Path}'"
					: "redirecting standard error to standard output"
				;
				await WriteDiagnosticAsync( stderr, commandError, $"nohup: {prefix}{action}", cancellationToken ).ConfigureAwait( false );
			}

			var argumentZero = ( replaceCurrentProcess && !OperatingSystem.IsWindows() )
				? operands[ 0 ]
				: null
			;
			var runOptions = new ProcessRunOptions( operands[ 0 ] ) {
				ArgumentZero = argumentZero,
				CancellationPolicy = ProcessCancellationPolicy.LeaveRunning,
				Environment = environment,
				ReplaceCurrentProcess = replaceCurrentProcess,
				ResolveExecutable = true,
				ReturnLaunchFailureResult = true,
				StandardInput = childInput,
				StandardOutput = childOutput,
				StandardError = childError,
				UseUnreadableStandardInput = useUnreadableStandardInput
			};
			if ( nativeOutputDescriptor.HasValue ) {
				var destinationDescriptor = outputTerminal
					? 1
					: 2
				;
				runOptions.PosixFileDescriptorDuplications.Add(
					new PosixFileDescriptorDuplication(
						nativeOutputDescriptor.Value,
						destinationDescriptor,
						closeSource: true
					)
				);
			}
			if ( useNativeStandardDescriptors
				&& errorTerminal
				&& !outputClosed
				&& ( !outputTerminal || nativeOutputDescriptor.HasValue )
			) {
				runOptions.PosixFileDescriptorDuplications.Add(
					new PosixFileDescriptorDuplication(
						1,
						2
					)
				);
			}
			if ( !OperatingSystem.IsWindows() ) {
				var hup = ProcessSignalCatalog.Parse( "HUP" );
				if ( hup.Succeeded && null != hup.Value ) {
					var signalPolicy = new ProcessLaunchSignalPolicy();
					signalPolicy.SetDisposition( hup.Value, ProcessSignalLaunchDisposition.Ignored );
					runOptions.SignalPolicy = signalPolicy;
				}
			}
			foreach ( var argument in operands.Skip( 1 ) ) runOptions.Arguments.Add( argument );

			ProcessResult result;
			try {
				result = await executor.RunAsync( runOptions, cancellationToken ).ConfigureAwait( false );
			} catch ( OperationCanceledException ) {
				return internalFailure;
			} catch ( Exception exception ) {
				await WriteDiagnosticAsync( stderr, commandError, $"nohup: failed to run command '{operands[ 0 ]}': {exception.Message}", CancellationToken.None ).ConfigureAwait( false );
				return internalFailure;
			}
			if ( ProcessTerminationKind.LaunchFailed == result.Termination.Kind ) {
				await WriteDiagnosticAsync(
					stderr,
					commandError,
					$"nohup: failed to run command '{operands[ 0 ]}': {result.Termination.Message ?? "cannot execute"}",
					CancellationToken.None
				).ConfigureAwait( false );
			}
			return result.Termination.ToPortableExitCode();
		} finally {
			if ( null != sharedOutput ) await sharedOutput.DisposeAsync().ConfigureAwait( false );
			else if ( null != destination ) await destination.DisposeAsync().ConfigureAwait( false );
		}
	}

	private static NohupOutputDestination? TryOpenDestination(
		INohupOutputFileProvider files,
		ProcessEnvironment environment,
		out string? error
	) {
		error = null;
		Exception? currentFailure = null;
		try {
			return files.OpenAppend( "nohup.out" );
		} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or NotSupportedException ) {
			currentFailure = exception;
		}
		if ( environment.Variables.TryGetValue( "HOME", out var home ) && !string.IsNullOrEmpty( home ) ) {
			var fallback = System.IO.Path.Combine( home, "nohup.out" );
			try {
				return files.OpenAppend( fallback );
			} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or NotSupportedException ) {
				error = $"failed to open '{fallback}': {exception.Message}";
				return null;
			}
		}
		error = $"failed to open 'nohup.out': {currentFailure?.Message ?? "unknown error"}";
		return null;
	}

	private static async Task WriteAsync(
		Stream? stream,
		TextWriter? writer,
		string text,
		CancellationToken cancellationToken
	) {
		if ( null != writer ) {
			await writer.WriteAsync( text.AsMemory(), cancellationToken ).ConfigureAwait( false );
			await writer.FlushAsync( cancellationToken ).ConfigureAwait( false );
			return;
		}
		if ( null == stream ) {
			await Console.Out.WriteAsync( text.AsMemory(), cancellationToken ).ConfigureAwait( false );
			return;
		}
		var bytes = Utf8.GetBytes( text );
		await stream.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
		await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
	}

	private static async Task WriteDiagnosticAsync(
		Stream? stream,
		TextWriter? writer,
		string message,
		CancellationToken cancellationToken
	) {
		if ( null != writer ) {
			await writer.WriteLineAsync( message.AsMemory(), cancellationToken ).ConfigureAwait( false );
			await writer.FlushAsync( cancellationToken ).ConfigureAwait( false );
			return;
		}
		if ( null == stream ) {
			await Console.Error.WriteLineAsync( message.AsMemory(), cancellationToken ).ConfigureAwait( false );
			return;
		}
		var bytes = Utf8.GetBytes( string.Concat( message, Environment.NewLine ) );
		await stream.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
		await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
	}

	private static string NormalizeLineEndings( string value ) => "\n" == Environment.NewLine
		? value
		: value.Replace( "\n", Environment.NewLine, StringComparison.Ordinal )
	;

	private const string HelpText = """
Usage: nohup COMMAND [ARG]...
  or:  nohup OPTION
Run COMMAND, ignoring hangup signals.

      --help        display this help and exit
      --version     output version information and exit

If standard input is a terminal, redirect it from an unreadable file.
If standard output is a terminal, append output to 'nohup.out' if possible,
'$HOME/nohup.out' otherwise.
If standard error is a terminal, redirect it to standard output.
To save output to FILE, use 'nohup COMMAND > FILE'.
""";

	private sealed class SynchronizedWriteStream : Stream {
		private readonly Stream _inner;
		private readonly SemaphoreSlim _gate = new( 1, 1 );
		private readonly bool _ownsInner;

		/// <summary>Initializes a serialized writer around one destination stream.</summary>
		public SynchronizedWriteStream(
			Stream inner,
			bool ownsInner = true
		) {
			this._inner = inner;
			this._ownsInner = ownsInner;
		}

		/// <inheritdoc />
		public override bool CanRead => false;

		/// <inheritdoc />
		public override bool CanSeek => false;

		/// <inheritdoc />
		public override bool CanWrite => this._inner.CanWrite;

		/// <inheritdoc />
		public override long Length => this._inner.Length;

		/// <inheritdoc />
		public override long Position {
			get => this._inner.Position;
			set => throw new NotSupportedException();
		}

		/// <inheritdoc />
		public override void Flush() => this._inner.Flush();

		/// <inheritdoc />
		public override async Task FlushAsync(
			CancellationToken cancellationToken
		) {
			await this._gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
			try {
				await this._inner.FlushAsync( cancellationToken ).ConfigureAwait( false );
			} finally {
				this._gate.Release();
			}
		}

		/// <inheritdoc />
		public override int Read(
			byte[] buffer,
			int offset,
			int count
		) => throw new NotSupportedException();

		/// <inheritdoc />
		public override long Seek(
			long offset,
			SeekOrigin origin
		) => throw new NotSupportedException();

		/// <inheritdoc />
		public override void SetLength(
			long value
		) => throw new NotSupportedException();

		/// <inheritdoc />
		public override void Write(
			byte[] buffer,
			int offset,
			int count
		) {
			this._gate.Wait();
			try {
				this._inner.Write( buffer, offset, count );
			} finally {
				this._gate.Release();
			}
		}

		/// <inheritdoc />
		public override async ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			await this._gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
			try {
				await this._inner.WriteAsync( buffer, cancellationToken ).ConfigureAwait( false );
			} finally {
				this._gate.Release();
			}
		}

		/// <inheritdoc />
		protected override void Dispose(
			bool disposing
		) {
			if ( disposing ) {
				if ( this._ownsInner ) this._inner.Dispose();
				this._gate.Dispose();
			}
			base.Dispose( disposing );
		}

		/// <inheritdoc />
		public override async ValueTask DisposeAsync() {
			if ( this._ownsInner ) await this._inner.DisposeAsync().ConfigureAwait( false );
			this._gate.Dispose();
			GC.SuppressFinalize( this );
		}
	}
}
