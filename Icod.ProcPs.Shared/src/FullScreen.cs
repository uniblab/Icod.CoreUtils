namespace Icod.ProcPs.Shared;

using System.Runtime.InteropServices;
using System.Text;
using Icod.CoreUtils.Shared.Terminal;

/// <summary>Abstracts one writable terminal used by a ProcPs full-screen command.</summary>
public interface IProcFullScreenTerminal : IAsyncDisposable {
	/// <summary>Gets a display name for diagnostics.</summary>
	string DisplayName { get; }
	/// <summary>Gets whether the endpoint is an interactive terminal.</summary>
	bool IsInteractive { get; }
	/// <summary>Gets the terminal dimensions observed at the time of the call.</summary>
	/// <returns>The current positive terminal dimensions.</returns>
	TerminalDimensions GetDimensions();
	/// <summary>Begins full-screen presentation.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A value task representing the presentation transition.</returns>
	ValueTask BeginAsync( CancellationToken cancellationToken = default );
	/// <summary>Writes one complete frame at the terminal home position.</summary>
	/// <param name="frame">The complete frame payload.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A value task representing the frame write.</returns>
	ValueTask WriteFrameAsync( string frame, CancellationToken cancellationToken = default );
	/// <summary>Restores terminal presentation state.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A value task representing the restoration.</returns>
	ValueTask RestoreAsync( CancellationToken cancellationToken = default );
	/// <summary>Restores terminal presentation synchronously before a process suspension signal takes effect.</summary>
	void RestoreForSuspend();
}

/// <summary>Creates writable full-screen terminal endpoints.</summary>
public interface IProcFullScreenTerminalFactory {
	/// <summary>Opens the selected terminal, or wraps standard output when no terminal path is supplied.</summary>
	/// <param name="terminalPath">Optional selected terminal path.</param>
	/// <param name="standardOutput">Optional caller-owned standard-output stream.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A value task whose result is the opened full-screen terminal endpoint.</returns>
	ValueTask<IProcFullScreenTerminal> OpenAsync(
		string? terminalPath,
		Stream? standardOutput,
		CancellationToken cancellationToken = default
	);
}

/// <summary>Reports asynchronous terminal lifecycle events to a ProcPs refresh loop.</summary>
public interface IProcFullScreenSignalSource : IDisposable {
	/// <summary>Gets a token canceled when the process receives an interactive termination request.</summary>
	/// <value>The termination token.</value>
	CancellationToken TerminationToken { get; }
	/// <summary>Consumes one pending resize notification.</summary>
	/// <returns><see langword="true"/> when a resize notification was pending; otherwise, <see langword="false"/>.</returns>
	bool ConsumeResize();
	/// <summary>Consumes one pending resume notification.</summary>
	/// <returns><see langword="true"/> when a resume notification was pending; otherwise, <see langword="false"/>.</returns>
	bool ConsumeResume();
}

/// <summary>Creates terminal lifecycle signal observers.</summary>
public interface IProcFullScreenSignalSourceFactory {
	/// <summary>Creates a signal observer and invokes <paramref name="restoreForSuspend"/> before a native suspend continues.</summary>
	/// <param name="restoreForSuspend">Synchronous restoration callback invoked before suspension.</param>
	/// <returns>The terminal lifecycle signal source.</returns>
	IProcFullScreenSignalSource Create( Action restoreForSuspend );
}

/// <summary>Creates full-screen terminal endpoints from the host standard output or a selected terminal path.</summary>
public sealed class SystemProcFullScreenTerminalFactory : IProcFullScreenTerminalFactory {
	/// <summary>Gets the shared system factory.</summary>
	public static SystemProcFullScreenTerminalFactory Instance {
		get;
	} = new();

	private SystemProcFullScreenTerminalFactory() {
	}

	/// <inheritdoc />
	public ValueTask<IProcFullScreenTerminal> OpenAsync(
		string? terminalPath,
		Stream? standardOutput,
		CancellationToken cancellationToken = default
	) {
		if ( null != terminalPath ) {
			ArgumentException.ThrowIfNullOrWhiteSpace( terminalPath );
		}
		cancellationToken.ThrowIfCancellationRequested();
		if ( null != terminalPath ) {
			var stream = new FileStream(
				terminalPath,
				FileMode.Open,
				FileAccess.Write,
				FileShare.ReadWrite | FileShare.Delete,
				4096,
				FileOptions.Asynchronous
			);
			return ValueTask.FromResult<IProcFullScreenTerminal>(
				new SystemProcFullScreenTerminal(
					stream,
					ownsStream: true,
					interactive: true,
					standardOutputTerminalProvider: null,
					displayName: terminalPath
				)
			);
		}
		var output = standardOutput ?? Console.OpenStandardOutput();
		var terminalProvider = SystemTerminalDeviceProvider.Instance;
		var observation = terminalProvider.Observe( TerminalStreamKind.StandardOutput );
		return ValueTask.FromResult<IProcFullScreenTerminal>(
			new SystemProcFullScreenTerminal(
				output,
				ownsStream: null == standardOutput,
				interactive: observation.IsTerminal,
				standardOutputTerminalProvider: terminalProvider,
				displayName: "standard output"
			)
		);
	}
}

/// <summary>Observes POSIX terminal signals and Windows console cancellation for ProcPs full-screen commands.</summary>
public sealed class SystemProcFullScreenSignalSourceFactory : IProcFullScreenSignalSourceFactory {
	/// <summary>Gets the shared system signal-source factory.</summary>
	public static SystemProcFullScreenSignalSourceFactory Instance {
		get;
	} = new();

	private SystemProcFullScreenSignalSourceFactory() {
	}

	/// <inheritdoc />
	public IProcFullScreenSignalSource Create( Action restoreForSuspend ) {
		ArgumentNullException.ThrowIfNull( restoreForSuspend );
		return new SystemProcFullScreenSignalSource( restoreForSuspend );
	}
}

/// <summary>Implements one system-backed ProcPs full-screen terminal endpoint.</summary>
internal sealed class SystemProcFullScreenTerminal : IProcFullScreenTerminal {
	private static readonly byte[] BeginSequence = Encoding.ASCII.GetBytes( "\u001b[?25l\u001b[H" );
	private static readonly byte[] HomeSequence = Encoding.ASCII.GetBytes( "\u001b[H" );
	private static readonly byte[] RestoreSequence = Encoding.ASCII.GetBytes( "\u001b[0m\u001b[?25h" );
	private readonly Stream _stream;
	private readonly bool _ownsStream;
	private readonly ITerminalDeviceProvider? _standardOutputTerminalProvider;
	private int _disposed;

	/// <inheritdoc />
	public string DisplayName { get; }
	/// <inheritdoc />
	public bool IsInteractive { get; }

	/// <summary>Initializes a system-backed full-screen terminal endpoint.</summary>
	/// <param name="stream">Writable terminal stream.</param>
	/// <param name="ownsStream">Whether disposal owns <paramref name="stream"/>.</param>
	/// <param name="interactive">Whether the endpoint is interactive.</param>
	/// <param name="standardOutputTerminalProvider">Optional provider for standard-output terminal observations.</param>
	/// <param name="displayName">Diagnostic display name.</param>
	internal SystemProcFullScreenTerminal(
		Stream stream,
		bool ownsStream,
		bool interactive,
		ITerminalDeviceProvider? standardOutputTerminalProvider,
		string displayName
	) {
		ArgumentNullException.ThrowIfNull( stream );
		ArgumentException.ThrowIfNullOrWhiteSpace( displayName );
		this._stream = stream;
		this._ownsStream = ownsStream;
		this.IsInteractive = interactive;
		this._standardOutputTerminalProvider = standardOutputTerminalProvider;
		this.DisplayName = displayName;
	}

	/// <inheritdoc />
	public TerminalDimensions GetDimensions() {
		this.ThrowIfDisposed();
		if ( null != this._standardOutputTerminalProvider && this.IsInteractive ) {
			var observation = this._standardOutputTerminalProvider.Observe( TerminalStreamKind.StandardOutput );
			if ( observation.Dimensions is TerminalDimensions terminalDimensions ) {
				return terminalDimensions;
			}
		}
		if ( this._stream is FileStream fileStream && TryGetUnixDimensions( fileStream, out var dimensions ) ) {
			return dimensions;
		}
		return new TerminalDimensions( 80, 25 );
	}

	/// <inheritdoc />
	public async ValueTask BeginAsync( CancellationToken cancellationToken = default ) {
		this.ThrowIfDisposed();
		if ( this.IsInteractive ) {
			await this._stream.WriteAsync( BeginSequence, cancellationToken ).ConfigureAwait( false );
			await this._stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
		}
	}

	/// <inheritdoc />
	public async ValueTask WriteFrameAsync( string frame, CancellationToken cancellationToken = default ) {
		ArgumentNullException.ThrowIfNull( frame );
		this.ThrowIfDisposed();
		await this._stream.WriteAsync( HomeSequence, cancellationToken ).ConfigureAwait( false );
		var bytes = Encoding.UTF8.GetBytes( frame );
		await this._stream.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
		await this._stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
	}

	/// <inheritdoc />
	public async ValueTask RestoreAsync( CancellationToken cancellationToken = default ) {
		if ( 0 != Volatile.Read( ref this._disposed ) ) {
			return;
		}
		if ( this.IsInteractive ) {
			await this._stream.WriteAsync( RestoreSequence, cancellationToken ).ConfigureAwait( false );
			await this._stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
		}
	}

	/// <inheritdoc />
	public void RestoreForSuspend() {
		if ( 0 != Volatile.Read( ref this._disposed ) || !this.IsInteractive ) {
			return;
		}
		try {
			this._stream.Write( RestoreSequence );
			this._stream.Flush();
		} catch ( IOException ) {
		} catch ( ObjectDisposedException ) {
		} catch ( NotSupportedException ) {
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync() {
		if ( 0 != Interlocked.Exchange( ref this._disposed, 1 ) ) {
			return;
		}
		if ( this._ownsStream ) {
			await this._stream.DisposeAsync().ConfigureAwait( false );
		}
	}

	private static bool TryGetUnixDimensions( FileStream stream, out TerminalDimensions dimensions ) {
		dimensions = default;
		if ( OperatingSystem.IsWindows()
			|| OperatingSystem.IsBrowser()
			|| OperatingSystem.IsAndroid()
			|| OperatingSystem.IsIOS()
			|| OperatingSystem.IsTvOS() ) {
			return false;
		}
		try {
			var request = ( OperatingSystem.IsLinux() )
				? 0x5413UL
				: 0x40087468UL
			;
			var descriptor = stream.SafeFileHandle.DangerousGetHandle().ToInt32();
			if ( 0 != NativeIoctl( descriptor, request, out var windowSize ) ) {
				return false;
			}
			if ( 1 >= windowSize.Columns || 1 >= windowSize.Rows ) {
				return false;
			}
			dimensions = new TerminalDimensions( windowSize.Columns, windowSize.Rows );
			return true;
		} catch ( OverflowException ) {
			return false;
		} catch ( DllNotFoundException ) {
			return false;
		} catch ( EntryPointNotFoundException ) {
			return false;
		}
	}

	private void ThrowIfDisposed() {
		ObjectDisposedException.ThrowIf( 0 != Volatile.Read( ref this._disposed ), this );
	}

	[DllImport( "libc", EntryPoint = "ioctl", ExactSpelling = true, SetLastError = true )]
	private static extern int NativeIoctl( int descriptor, ulong request, out UnixWindowSize windowSize );

	[StructLayout( LayoutKind.Sequential )]
	private struct UnixWindowSize {
		public ushort Rows;
		public ushort Columns;
		public ushort PixelWidth;
		public ushort PixelHeight;
	}
}

/// <summary>Implements host signal observation for ProcPs full-screen refresh loops.</summary>
internal sealed class SystemProcFullScreenSignalSource : IProcFullScreenSignalSource {
	private readonly CancellationTokenSource _termination = new();
	private readonly List<IDisposable> _registrations = new();
	private ConsoleCancelEventHandler? _consoleCancelHandler;
	private int _resizePending;
	private int _resumePending;
	private int _disposed;

	/// <inheritdoc />
	public CancellationToken TerminationToken => this._termination.Token;

	/// <summary>Initializes the host signal observer.</summary>
	/// <param name="restoreForSuspend">Synchronous restoration callback invoked before suspension.</param>
	internal SystemProcFullScreenSignalSource( Action restoreForSuspend ) {
		ArgumentNullException.ThrowIfNull( restoreForSuspend );
		if ( OperatingSystem.IsWindows() ) {
			this._consoleCancelHandler = ( _, eventArgs ) => {
				eventArgs.Cancel = true;
				this.TryCancel();
			};
			Console.CancelKeyPress += this._consoleCancelHandler;
			return;
		}
		if ( OperatingSystem.IsBrowser()
			|| OperatingSystem.IsAndroid()
			|| OperatingSystem.IsIOS()
			|| OperatingSystem.IsTvOS() ) {
			return;
		}
		this._registrations.Add(
			PosixSignalRegistration.Create(
				PosixSignal.SIGWINCH,
				context => {
					context.Cancel = true;
					Interlocked.Exchange( ref this._resizePending, 1 );
				}
			)
		);
		this._registrations.Add(
			PosixSignalRegistration.Create(
				PosixSignal.SIGCONT,
				context => {
					context.Cancel = false;
					Interlocked.Exchange( ref this._resumePending, 1 );
				}
			)
		);
		this._registrations.Add(
			PosixSignalRegistration.Create(
				PosixSignal.SIGTSTP,
				context => {
					restoreForSuspend();
					context.Cancel = false;
				}
			)
		);
		this.RegisterTerminationSignal( PosixSignal.SIGINT );
		this.RegisterTerminationSignal( PosixSignal.SIGTERM );
		this.RegisterTerminationSignal( PosixSignal.SIGQUIT );
		this.RegisterTerminationSignal( PosixSignal.SIGHUP );
	}

	/// <inheritdoc />
	public bool ConsumeResize() => 0 != Interlocked.Exchange( ref this._resizePending, 0 );
	/// <inheritdoc />
	public bool ConsumeResume() => 0 != Interlocked.Exchange( ref this._resumePending, 0 );

	/// <inheritdoc />
	public void Dispose() {
		if ( 0 != Interlocked.Exchange( ref this._disposed, 1 ) ) {
			return;
		}
		if ( null != this._consoleCancelHandler ) {
			Console.CancelKeyPress -= this._consoleCancelHandler;
			this._consoleCancelHandler = null;
		}
		foreach ( var registration in this._registrations ) {
			registration.Dispose();
		}
		this._registrations.Clear();
		this._termination.Dispose();
	}

	private void RegisterTerminationSignal( PosixSignal signal ) {
		if ( OperatingSystem.IsBrowser()
			|| OperatingSystem.IsAndroid()
			|| OperatingSystem.IsIOS()
			|| OperatingSystem.IsTvOS()
			|| OperatingSystem.IsWindows() ) {
			return;
		}
		this._registrations.Add(
			PosixSignalRegistration.Create(
				signal,
				context => {
					context.Cancel = true;
					this.TryCancel();
				}
			)
		);
	}

	private void TryCancel() {
		if ( 0 != Volatile.Read( ref this._disposed ) ) {
			return;
		}
		try {
			this._termination.Cancel();
		} catch ( ObjectDisposedException ) {
		}
	}
}
