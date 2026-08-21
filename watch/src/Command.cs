// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.ProcPs.Watch;

using System.Globalization;
using System.Text;
using Icod.CommandFramework.Processes;
using Icod.CommandFramework.Terminal;
using Icod.CommandFramework.Time;
using Icod.ProcPs.Shared;
/// <summary>Implements the procps-ng compatible <c>watch</c> command.</summary>
public static class Command {
	private const int Success = 0;
	private const int Failure = 1;
	private const int ExecutionFailure = 2;
	private const int Canceled = 130;
	private const double MinimumIntervalSeconds = 0.1d;
	private const double MaximumIntervalSeconds = 60d * 60d * 24d * 31d;
	private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds( 2d );
	private const string VersionText = "watch from procps-ng 4.0.6";
	/// <summary>Runs <c>watch</c> synchronously.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdout">Optional standard-output stream.</param>
	/// <param name="stderr">Optional standard-error stream.</param>
	/// <returns>The process exit status.</returns>
	public static int Run( string[] args, Stream? stdout = null, Stream? stderr = null ) {
		ArgumentNullException.ThrowIfNull( args );
		return RunAsync( args, stdout, stderr ).GetAwaiter().GetResult();
	}
	/// <summary>Runs <c>watch</c> asynchronously with injectable process, terminal, clock, and environment providers.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdout">Optional standard-output stream.</param>
	/// <param name="stderr">Optional standard-error stream.</param>
	/// <param name="processExecutor">Optional child-process executor.</param>
	/// <param name="terminalFactory">Optional full-screen terminal factory.</param>
	/// <param name="signalSourceFactory">Optional terminal-lifecycle signal-source factory.</param>
	/// <param name="clock">Optional monotonic clock used for cadence.</param>
	/// <param name="environmentVariableProvider">Optional environment-variable provider.</param>
	/// <param name="wallClockProvider">Optional wall-clock provider used only by the header.</param>
	/// <param name="hostNameProvider">Optional host-name provider used only by the header.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task whose result is the procps-compatible exit status.</returns>
	public static async Task<int> RunAsync(
		IReadOnlyList<string> args,
		Stream? stdout = null,
		Stream? stderr = null,
		IProcessExecutor? processExecutor = null,
		IProcFullScreenTerminalFactory? terminalFactory = null,
		IProcFullScreenSignalSourceFactory? signalSourceFactory = null,
		IMonotonicClock? clock = null,
		Func<string, string?>? environmentVariableProvider = null,
		Func<DateTimeOffset>? wallClockProvider = null,
		Func<string>? hostNameProvider = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		var output = stdout ?? Console.OpenStandardOutput();
		var errorOutput = stderr ?? Console.OpenStandardError();
		var executor = processExecutor ?? SystemProcessExecutor.Instance;
		var terminals = terminalFactory ?? SystemProcFullScreenTerminalFactory.Instance;
		var signalSources = signalSourceFactory ?? SystemProcFullScreenSignalSourceFactory.Instance;
		var monotonicClock = clock ?? SystemMonotonicClock.Instance;
		var environment = environmentVariableProvider ?? Environment.GetEnvironmentVariable;
		var wallClock = wallClockProvider ?? GetCurrentTime;
		var hostName = hostNameProvider ?? GetHostName;
		var parsed = Parse( args, environment );
		if ( null != parsed.Error ) {
			await WriteTextAsync(
				errorOutput,
				$"watch: {parsed.Error}{Environment.NewLine}",
				cancellationToken
			).ConfigureAwait( false );
			await WriteUsageAsync( errorOutput, cancellationToken ).ConfigureAwait( false );
			return Failure;
		}
		if ( parsed.Help ) {
			await WriteUsageAsync( output, cancellationToken ).ConfigureAwait( false );
			return Success;
		}
		if ( parsed.Version ) {
			await WriteTextAsync(
				output,
				$"{VersionText}{Environment.NewLine}",
				cancellationToken
			).ConfigureAwait( false );
			return Success;
		}
		IProcFullScreenTerminal? terminal = null;
		IProcFullScreenSignalSource? signals = null;
		var beganPresentation = false;
		try {
			terminal = await terminals.OpenAsync(
				terminalPath: null,
				standardOutput: output,
				cancellationToken
			).ConfigureAwait( false );
			if ( !terminal.IsInteractive ) {
				await WriteTextAsync(
					errorOutput,
					$"watch: standard output is not a terminal{Environment.NewLine}",
					cancellationToken
				).ConfigureAwait( false );
				return Failure;
			}
			var dimensions = GetDimensions( terminal, environment );
			if ( !IsUsableDimensions( dimensions, parsed.NoTitle ) ) {
				await WriteTextAsync(
					errorOutput,
					$"watch: screen is too small or too large{Environment.NewLine}",
					cancellationToken
				).ConfigureAwait( false );
				return Failure;
			}
			beganPresentation = true;
			await terminal.BeginAsync( cancellationToken ).ConfigureAwait( false );
			signals = signalSources.Create( terminal.RestoreForSuspend );
			using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken,
				signals.TerminationToken
			);
			var refreshToken = linkedCancellation.Token;
			var startedTimestamp = monotonicClock.GetTimestamp();
			var iteration = 0L;
			var unchangedCycles = 1L;
			WatchScreen? previousScreen = null;
			bool[]? permanentDifferences = null;
			string? previousRawOutput = null;
			var previousStatus = 0;
			var previousElapsed = TimeSpan.Zero;
			while ( true ) {
				refreshToken.ThrowIfCancellationRequested();
				if ( 0L < iteration ) {
					var delay = ( parsed.Precise )
						? PreciseDelay( monotonicClock, startedTimestamp, parsed.Interval, iteration )
						: parsed.Interval
					;
					if ( TimeSpan.Zero < delay ) {
						await monotonicClock.DelayAsync( delay, refreshToken ).ConfigureAwait( false );
					}
				}
				if ( signals.ConsumeResume() ) {
					await terminal.BeginAsync( refreshToken ).ConfigureAwait( false );
				}
				var resizeSignaled = signals.ConsumeResize();
				var observedDimensions = GetDimensions( terminal, environment );
				var resized = resizeSignaled || observedDimensions != dimensions;
				if ( resized ) {
					dimensions = observedDimensions;
					if ( !IsUsableDimensions( dimensions, parsed.NoTitle ) ) {
						await WriteTextAsync(
							errorOutput,
							$"watch: screen is too small or too large{Environment.NewLine}",
							refreshToken
						).ConfigureAwait( false );
						return Failure;
					}
					previousScreen = null;
					permanentDifferences = null;
					unchangedCycles = 1L;
					if ( parsed.NoRerun && null != previousRawOutput ) {
						var redraw = WatchScreen.Create(
							previousRawOutput,
							dimensions,
							parsed.NoTitle,
							parsed.NoWrap,
							parsed.Color
						);
						var redrawFrame = BuildFrame(
							redraw,
							parsed,
							previousStatus,
							previousElapsed,
							hostName(),
							wallClock(),
							highlights: null
						);
						await terminal.WriteFrameAsync( redrawFrame, refreshToken ).ConfigureAwait( false );
						previousScreen = redraw;
						iteration++;
						continue;
					}
				}
				using var capture = new MergedCaptureStream();
				var processOptions = BuildProcessOptions( parsed, capture );
				ProcessResult processResult;
				try {
					processResult = await executor.RunAsync( processOptions, refreshToken ).ConfigureAwait( false );
				} catch ( OperationCanceledException ) {
					throw;
				} catch ( Exception exception ) when (
					exception is ArgumentException
					or IOException
					or InvalidOperationException
					or NotSupportedException
					or UnauthorizedAccessException
				) {
					await WriteFailureAsync( errorOutput, exception.Message ).ConfigureAwait( false );
					return ExecutionFailure;
				}
				if ( refreshToken.IsCancellationRequested || processResult.WasCanceled ) {
					return Canceled;
				}
				var status = processResult.Termination.ToPortableExitCode();
				var childOutput = capture.GetText();
				if ( 0 == childOutput.Length ) {
					childOutput = string.Concat(
						processResult.StandardOutput ?? string.Empty,
						processResult.StandardError ?? string.Empty
					);
				}
				if ( ProcessTerminationKind.LaunchFailed == processResult.Termination.Kind
					&& string.IsNullOrEmpty( childOutput )
					&& !string.IsNullOrWhiteSpace( processResult.Termination.Message ) ) {
					childOutput = string.Concat(
						"watch: ",
						processResult.Termination.Message,
						Environment.NewLine
					);
				}
				var screen = WatchScreen.Create(
					childOutput,
					dimensions,
					parsed.NoTitle,
					parsed.NoWrap,
					parsed.Color
				);
				var changed = null != previousScreen && !screen.VisibleEquals( previousScreen );
				var highlights = ( parsed.Differences && null != previousScreen )
					? screen.GetDifferences( previousScreen )
					: null
				;
				if ( parsed.PermanentDifferences && null != highlights ) {
					if ( null == permanentDifferences || permanentDifferences.Length != highlights.Length ) {
						permanentDifferences = new bool[ highlights.Length ];
					}
					for ( var index = 0; index < highlights.Length; index++ ) {
						permanentDifferences[ index ] |= highlights[ index ];
					}
					highlights = permanentDifferences;
				}
				var frame = BuildFrame(
					screen,
					parsed,
					status,
					processResult.Elapsed,
					hostName(),
					wallClock(),
					highlights
				);
				if ( parsed.Beep && 0 != status ) {
					frame = string.Concat( "\a", frame );
				}
				await terminal.WriteFrameAsync( frame, refreshToken ).ConfigureAwait( false );
				previousRawOutput = childOutput;
				previousStatus = status;
				previousElapsed = processResult.Elapsed;
				if ( parsed.ErrorExit && 0 != status ) {
					return status;
				}
				if ( null != previousScreen ) {
					if ( parsed.ChangeExit && changed ) {
						return Success;
					}
					if ( null != parsed.EqualExitCycles ) {
						if ( changed ) {
							unchangedCycles = 1L;
						} else {
							if ( unchangedCycles >= parsed.EqualExitCycles.Value ) {
								return Success;
							}
							unchangedCycles++;
						}
					}
				}
				previousScreen = screen;
				iteration++;
			}
		} catch ( OperationCanceledException ) {
			return Canceled;
		} catch ( UnauthorizedAccessException exception ) {
			await WriteFailureAsync( errorOutput, exception.Message ).ConfigureAwait( false );
			return Failure;
		} catch ( IOException exception ) {
			await WriteFailureAsync( errorOutput, exception.Message ).ConfigureAwait( false );
			return Failure;
		} catch ( ArgumentException exception ) {
			await WriteFailureAsync( errorOutput, exception.Message ).ConfigureAwait( false );
			return Failure;
		} catch ( NotSupportedException exception ) {
			await WriteFailureAsync( errorOutput, exception.Message ).ConfigureAwait( false );
			return Failure;
		} finally {
			try {
				signals?.Dispose();
			} catch ( ObjectDisposedException ) {
			}
			if ( null != terminal ) {
				if ( beganPresentation ) {
					try {
						await terminal.RestoreAsync( CancellationToken.None ).ConfigureAwait( false );
					} catch ( IOException ) {
					} catch ( ObjectDisposedException ) {
					} catch ( NotSupportedException ) {
					}
				}
				try {
					await terminal.DisposeAsync().ConfigureAwait( false );
				} catch ( IOException ) {
				} catch ( ObjectDisposedException ) {
				} catch ( NotSupportedException ) {
				}
			}
		}
	}
	private static ProcessRunOptions BuildProcessOptions( ParsedArguments parsed, Stream capture ) {
		ArgumentNullException.ThrowIfNull( parsed );
		ArgumentNullException.ThrowIfNull( capture );
		ProcessRunOptions options;
		if ( parsed.Exec ) {
			options = new ProcessRunOptions( parsed.Command[ 0 ] );
			for ( var index = 1; index < parsed.Command.Count; index++ ) {
				options.Arguments.Add( parsed.Command[ index ] );
			}
		} else if ( OperatingSystem.IsWindows() ) {
			options = new ProcessRunOptions( "cmd.exe" );
			options.Arguments.Add( "/D" );
			options.Arguments.Add( "/S" );
			options.Arguments.Add( "/C" );
			options.Arguments.Add( string.Join( " ", parsed.Command ) );
		} else {
			options = new ProcessRunOptions( "/bin/sh" );
			options.Arguments.Add( "-c" );
			options.Arguments.Add( string.Join( " ", parsed.Command ) );
		}
		options.ResolveExecutable = true;
		options.ReturnLaunchFailureResult = true;
		options.StandardOutput = capture;
		options.StandardError = capture;
		return options;
	}
	private static TimeSpan PreciseDelay(
		IMonotonicClock clock,
		long startedTimestamp,
		TimeSpan interval,
		long iteration
	) {
		ArgumentNullException.ThrowIfNull( clock );
		ArgumentOutOfRangeException.ThrowIfNegative( iteration );
		var dueTicks = checked( interval.Ticks * iteration );
		var due = TimeSpan.FromTicks( dueTicks );
		var elapsed = clock.GetElapsedTime( startedTimestamp, clock.GetTimestamp() );
		return ( due > elapsed )
			? due - elapsed
			: TimeSpan.Zero
		;
	}
	private static TerminalDimensions GetDimensions(
		IProcFullScreenTerminal terminal,
		Func<string, string?> environmentVariableProvider
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( environmentVariableProvider );
		var dimensions = terminal.GetDimensions();
		var width = ParsePositiveDimension( environmentVariableProvider( "COLUMNS" ) ) ?? dimensions.Width;
		var height = ParsePositiveDimension( environmentVariableProvider( "LINES" ) ) ?? dimensions.Height;
		return new TerminalDimensions( width, height );
	}
	private static int? ParsePositiveDimension( string? text ) {
		if ( null == text ) {
			return null;
		}
		if ( int.TryParse( text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value ) && 0 < value ) {
			return value;
		}
		return null;
	}
	private static bool IsUsableDimensions( TerminalDimensions dimensions, bool noTitle ) {
		var minimumHeight = ( noTitle )
			? 1
			: 3
		;
		return 2 <= dimensions.Width
			&& minimumHeight <= dimensions.Height
			&& int.MaxValue >= (long)dimensions.Width * dimensions.Height;
	}
	private static string BuildFrame(
		WatchScreen screen,
		ParsedArguments parsed,
		int status,
		TimeSpan elapsed,
		string hostName,
		DateTimeOffset now,
		bool[]? highlights
	) {
		ArgumentNullException.ThrowIfNull( screen );
		ArgumentNullException.ThrowIfNull( parsed );
		ArgumentNullException.ThrowIfNull( hostName );
		var builder = new StringBuilder();
		if ( !parsed.NoTitle ) {
			var intervalText = parsed.Interval.TotalSeconds.ToString( "0.###", CultureInfo.InvariantCulture );
			var commandText = string.Join( " ", parsed.Command );
			var left = $"Every {intervalText}s: {commandText}";
			var right = $"{hostName}: {now:HH:mm:ss}";
			AppendPaddedRow( builder, ComposeHeaderRow( left, right, screen.Width ), screen.Width );
			var lower = $"Elapsed: {elapsed.TotalSeconds.ToString( "0.###", CultureInfo.InvariantCulture )}s  Exit: {status}";
			AppendPaddedRow( builder, lower, screen.Width );
		}
		screen.AppendBody( builder, parsed.Color, highlights );
		return builder.ToString();
	}
	private static DateTimeOffset GetCurrentTime() => DateTimeOffset.Now;
	private static string GetHostName() => Environment.MachineName;
	private static string ComposeHeaderRow( string left, string right, int width ) {
		ArgumentNullException.ThrowIfNull( left );
		ArgumentNullException.ThrowIfNull( right );
		if ( width <= left.Length ) {
			return left[ ..width ];
		}
		if ( width <= right.Length + 1 ) {
			return left;
		}
		var availableLeft = width - right.Length - 1;
		var leftText = ( left.Length > availableLeft )
			? left[ ..availableLeft ]
			: left
		;
		return string.Concat(
			leftText,
			new string( ' ', width - leftText.Length - right.Length ),
			right
		);
	}
	private static void AppendPaddedRow( StringBuilder builder, string text, int width ) {
		ArgumentNullException.ThrowIfNull( builder );
		ArgumentNullException.ThrowIfNull( text );
		if ( text.Length >= width ) {
			builder.Append( text.AsSpan( 0, width ) );
			return;
		}
		builder.Append( text );
		builder.Append( ' ', width - text.Length );
	}
	private static ParsedArguments Parse(
		IReadOnlyList<string> args,
		Func<string, string?> environmentVariableProvider
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( environmentVariableProvider );
		var interval = DefaultInterval;
		var environmentInterval = environmentVariableProvider( "WATCH_INTERVAL" );
		if ( null != environmentInterval ) {
			if ( !TryParseInterval( environmentInterval, out interval ) ) {
				return ParsedArguments.Failed( "Could not parse interval from WATCH_INTERVAL" );
			}
		}
		var beep = false;
		var color = false;
		var differences = false;
		var permanentDifferences = false;
		var errorExit = false;
		var follow = false;
		var changeExit = false;
		long? equalExitCycles = null;
		var precise = false;
		var noRerun = false;
		var noTitle = false;
		var noWrap = false;
		var exec = false;
		string? shotsDirectory = null;
		var index = 0;
		for ( ; index < args.Count; index++ ) {
			var argument = args[ index ];
			if ( "--" == argument ) {
				index++;
				break;
			}
			if ( !argument.StartsWith( '-' ) || "-" == argument ) {
				break;
			}
			if ( "--help" == argument || "-h" == argument ) {
				return ParsedArguments.ForHelp();
			}
			if ( "--version" == argument || "-v" == argument ) {
				return ParsedArguments.ForVersion();
			}
			if ( "--beep" == argument || "-b" == argument ) {
				beep = true;
				continue;
			}
			if ( "--color" == argument || "-c" == argument ) {
				color = true;
				continue;
			}
			if ( "--no-color" == argument || "-C" == argument ) {
				color = false;
				continue;
			}
			if ( "--differences" == argument || "-d" == argument ) {
				differences = true;
				continue;
			}
			if ( argument.StartsWith( "--differences=", StringComparison.Ordinal ) ) {
				var mode = argument[ "--differences=".Length.. ];
				if ( !string.Equals( mode, "permanent", StringComparison.OrdinalIgnoreCase ) ) {
					return ParsedArguments.Failed( "invalid differences mode" );
				}
				differences = true;
				permanentDifferences = true;
				continue;
			}
			if ( argument.StartsWith( "-d", StringComparison.Ordinal ) && 2 < argument.Length ) {
				var mode = argument[ 2.. ].TrimStart( '=' );
				if ( !string.Equals( mode, "permanent", StringComparison.OrdinalIgnoreCase ) ) {
					return ParsedArguments.Failed( "invalid differences mode" );
				}
				differences = true;
				permanentDifferences = true;
				continue;
			}
			if ( "--errexit" == argument || "-e" == argument ) {
				errorExit = true;
				continue;
			}
			if ( "--follow" == argument || "-f" == argument ) {
				follow = true;
				continue;
			}
			if ( "--chgexit" == argument || "-g" == argument ) {
				changeExit = true;
				continue;
			}
			if ( TryRequiredOptionValue( args, ref index, argument, "-q", "--equexit", out var equalText, out var equalError ) ) {
				if ( null != equalError ) {
					return ParsedArguments.Failed( equalError );
				}
				if ( !long.TryParse( equalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles ) ) {
					return ParsedArguments.Failed( "failed to parse argument" );
				}
				equalExitCycles = Math.Max( 1L, cycles );
				continue;
			}
			if ( TryRequiredOptionValue( args, ref index, argument, "-n", "--interval", out var intervalText, out var intervalError ) ) {
				if ( null != intervalError ) {
					return ParsedArguments.Failed( intervalError );
				}
				if ( !TryParseInterval( intervalText, out interval ) ) {
					return ParsedArguments.Failed( "failed to parse argument" );
				}
				continue;
			}
			if ( "--precise" == argument || "-p" == argument ) {
				precise = true;
				continue;
			}
			if ( "--no-rerun" == argument || "-r" == argument ) {
				noRerun = true;
				continue;
			}
			if ( TryRequiredOptionValue( args, ref index, argument, "-s", "--shotsdir", out var shotsText, out var shotsError ) ) {
				if ( null != shotsError ) {
					return ParsedArguments.Failed( shotsError );
				}
				shotsDirectory = shotsText;
				continue;
			}
			if ( "--no-title" == argument || "-t" == argument ) {
				noTitle = true;
				continue;
			}
			if ( "--no-wrap" == argument || "-w" == argument ) {
				noWrap = true;
				continue;
			}
			if ( "--exec" == argument || "-x" == argument ) {
				exec = true;
				continue;
			}
			return ParsedArguments.Failed( $"unrecognized option '{argument}'" );
		}
		if ( index >= args.Count ) {
			return ParsedArguments.Failed( "missing command" );
		}
		if ( follow && ( differences || changeExit || null != equalExitCycles ) ) {
			return ParsedArguments.Failed( "follow option conflicts with change and exit options" );
		}
		var command = new List<string>( args.Count - index );
		for ( ; index < args.Count; index++ ) {
			command.Add( args[ index ] );
		}
		return new ParsedArguments(
			interval,
			beep,
			color,
			differences,
			permanentDifferences,
			errorExit,
			follow,
			changeExit,
			equalExitCycles,
			precise,
			noRerun,
			shotsDirectory,
			noTitle,
			noWrap,
			exec,
			command,
			Help: false,
			Version: false,
			Error: null
		);
	}
	private static bool TryParseInterval( string text, out TimeSpan interval ) {
		ArgumentNullException.ThrowIfNull( text );
		var normalized = text.Replace( ',', '.' );
		if ( !double.TryParse( normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds ) || !double.IsFinite( seconds ) ) {
			interval = default;
			return false;
		}
		seconds = Math.Clamp( seconds, MinimumIntervalSeconds, MaximumIntervalSeconds );
		interval = TimeSpan.FromSeconds( seconds );
		return true;
	}
	private static bool TryRequiredOptionValue(
		IReadOnlyList<string> args,
		ref int index,
		string argument,
		string shortName,
		string longName,
		out string value,
		out string? error
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( argument );
		ArgumentNullException.ThrowIfNull( shortName );
		ArgumentNullException.ThrowIfNull( longName );
		value = string.Empty;
		error = null;
		if ( argument == shortName || argument == longName ) {
			if ( index + 1 >= args.Count ) {
				error = $"option '{argument}' requires an argument";
				return true;
			}
			value = args[ ++index ];
			return true;
		}
		if ( argument.StartsWith( shortName, StringComparison.Ordinal ) && shortName.Length < argument.Length ) {
			value = argument[ shortName.Length.. ].TrimStart( '=' );
			return true;
		}
		var prefix = $"{longName}=";
		if ( argument.StartsWith( prefix, StringComparison.Ordinal ) ) {
			value = argument[ prefix.Length.. ];
			return true;
		}
		return false;
	}
	private static Task WriteUsageAsync( Stream output, CancellationToken cancellationToken ) {
		ArgumentNullException.ThrowIfNull( output );
		return WriteTextAsync( output, HelpText(), cancellationToken );
	}
	private static string HelpText() => string.Join(
		Environment.NewLine,
		"Usage:",
		" watch [options] command",
		string.Empty,
		"Options:",
		" -b, --beep                  beep if command has a non-zero exit",
		" -c, --color                 interpret ANSI color and style sequences",
		" -C, --no-color              do not interpret ANSI color/style sequences",
		" -d, --differences[=permanent]",
		"                              highlight changes between updates",
		" -e, --errexit               exit if command has a non-zero exit",
		" -f, --follow                follow output without change/exit comparisons",
		" -g, --chgexit               exit when visible command output changes",
		" -q, --equexit <cycles>      exit after visible output is unchanged for cycles",
		" -n, --interval <secs>       seconds between updates",
		" -p, --precise               include command running time in the interval",
		" -r, --no-rerun              do not rerun command because of a resize",
		" -s, --shotsdir <dir>        reserve screenshot directory compatibility",
		" -t, --no-title              turn off the header",
		" -w, --no-wrap               truncate long lines instead of wrapping",
		" -x, --exec                  execute command directly instead of through a shell",
		" -h, --help                  display this help and exit",
		" -v, --version               output version information and exit",
		string.Empty
	);
	private static async Task WriteTextAsync( Stream stream, string text, CancellationToken cancellationToken ) {
		ArgumentNullException.ThrowIfNull( stream );
		ArgumentNullException.ThrowIfNull( text );
		var bytes = Encoding.UTF8.GetBytes( text );
		await stream.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
		await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
	}
	private static async Task WriteFailureAsync( Stream stderr, string message ) {
		ArgumentNullException.ThrowIfNull( stderr );
		ArgumentNullException.ThrowIfNull( message );
		try {
			await WriteTextAsync(
				stderr,
				$"watch: {message}{Environment.NewLine}",
				CancellationToken.None
			).ConfigureAwait( false );
		} catch ( IOException ) {
		} catch ( ObjectDisposedException ) {
		}
	}
	private sealed record ParsedArguments(
		TimeSpan Interval,
		bool Beep,
		bool Color,
		bool Differences,
		bool PermanentDifferences,
		bool ErrorExit,
		bool Follow,
		bool ChangeExit,
		long? EqualExitCycles,
		bool Precise,
		bool NoRerun,
		string? ShotsDirectory,
		bool NoTitle,
		bool NoWrap,
		bool Exec,
		IReadOnlyList<string> Command,
		bool Help,
		bool Version,
		string? Error
	) {
		public static ParsedArguments ForHelp() => Empty( help: true, version: false, error: null );
		public static ParsedArguments ForVersion() => Empty( help: false, version: true, error: null );
		public static ParsedArguments Failed( string error ) {
			ArgumentException.ThrowIfNullOrWhiteSpace( error );
			return Empty( help: false, version: false, error: error );
		}
		private static ParsedArguments Empty( bool help, bool version, string? error ) => new(
			DefaultInterval,
			false,
			false,
			false,
			false,
			false,
			false,
			false,
			null,
			false,
			false,
			null,
			false,
			false,
			false,
			Array.Empty<string>(),
			help,
			version,
			error
		);
	}
}
/// <summary>Represents one visible watch body independent of terminal I/O.</summary>
internal sealed class WatchScreen {
	private const string ResetStyle = "\u001b[0m";
	private readonly WatchCell[] _cells;
	/// <summary>Gets the screen width.</summary>
	internal int Width { get; }
	/// <summary>Gets the body height.</summary>
	internal int Height { get; }
	private WatchScreen( int width, int height, WatchCell[] cells ) {
		if ( 1 > width ) {
			throw new ArgumentOutOfRangeException( nameof( width ) );
		}
		if ( 1 > height ) {
			throw new ArgumentOutOfRangeException( nameof( height ) );
		}
		ArgumentNullException.ThrowIfNull( cells );
		if ( cells.Length != checked( width * height ) ) {
			throw new ArgumentException( "Cell count does not match the requested screen geometry.", nameof( cells ) );
		}
		this.Width = width;
		this.Height = height;
		this._cells = cells;
	}
	/// <summary>Builds the visible body for captured child output.</summary>
	internal static WatchScreen Create(
		string output,
		TerminalDimensions dimensions,
		bool noTitle,
		bool noWrap,
		bool preserveColor
	) {
		ArgumentNullException.ThrowIfNull( output );
		var headerHeight = ( noTitle )
			? 0
			: 2
		;
		var bodyHeight = dimensions.Height - headerHeight;
		if ( 1 > dimensions.Width || 1 > bodyHeight ) {
			throw new ArgumentOutOfRangeException( nameof( dimensions ) );
		}
		var cells = new WatchCell[ checked( dimensions.Width * bodyHeight ) ];
		for ( var index = 0; index < cells.Length; index++ ) {
			cells[ index ] = new WatchCell( ' ', null );
		}
		var row = 0;
		var column = 0;
		string? style = null;
		var skipUntilNewline = false;
		for ( var index = 0; index < output.Length && row < bodyHeight; index++ ) {
			var character = output[ index ];
			if ( '\u001b' == character && index + 1 < output.Length && '[' == output[ index + 1 ] ) {
				var end = index + 2;
				while ( end < output.Length ) {
					var candidate = output[ end ];
					if ( '@' <= candidate && '~' >= candidate ) {
						break;
					}
					end++;
				}
				if ( end < output.Length ) {
					if ( preserveColor && 'm' == output[ end ] ) {
						var sequence = output[ index..( end + 1 ) ];
						style = ( "\u001b[0m" == sequence || "\u001b[m" == sequence )
							? null
							: sequence
						;
					}
					index = end;
					continue;
				}
			}
			if ( '\n' == character ) {
				row++;
				column = 0;
				skipUntilNewline = false;
				continue;
			}
			if ( '\r' == character ) {
				column = 0;
				skipUntilNewline = false;
				continue;
			}
			if ( skipUntilNewline ) {
				continue;
			}
			if ( '\t' == character ) {
				var spaces = 8 - ( column % 8 );
				for ( var count = 0; count < spaces && row < bodyHeight; count++ ) {
					WriteCell( cells, dimensions.Width, bodyHeight, ref row, ref column, ' ', style, noWrap, ref skipUntilNewline );
				}
				continue;
			}
			if ( char.IsControl( character ) ) {
				continue;
			}
			WriteCell( cells, dimensions.Width, bodyHeight, ref row, ref column, character, style, noWrap, ref skipUntilNewline );
		}
		return new WatchScreen( dimensions.Width, bodyHeight, cells );
	}
	/// <summary>Compares visible characters, excluding style metadata and off-screen output.</summary>
	internal bool VisibleEquals( WatchScreen other ) {
		ArgumentNullException.ThrowIfNull( other );
		if ( this.Width != other.Width || this.Height != other.Height ) {
			return false;
		}
		for ( var index = 0; index < this._cells.Length; index++ ) {
			if ( this._cells[ index ].Character != other._cells[ index ].Character ) {
				return false;
			}
		}
		return true;
	}
	/// <summary>Returns a visible-cell difference mask.</summary>
	internal bool[] GetDifferences( WatchScreen other ) {
		ArgumentNullException.ThrowIfNull( other );
		if ( this.Width != other.Width || this.Height != other.Height ) {
			throw new ArgumentException( "Screen geometries must match for difference calculation.", nameof( other ) );
		}
		var result = new bool[ this._cells.Length ];
		for ( var index = 0; index < result.Length; index++ ) {
			result[ index ] = this._cells[ index ].Character != other._cells[ index ].Character;
		}
		return result;
	}
	/// <summary>Appends fixed-geometry body cells with optional ANSI SGR and difference highlighting.</summary>
	internal void AppendBody( StringBuilder builder, bool preserveColor, bool[]? highlights ) {
		ArgumentNullException.ThrowIfNull( builder );
		if ( null != highlights && highlights.Length != this._cells.Length ) {
			throw new ArgumentException( "Highlight count does not match visible cell count.", nameof( highlights ) );
		}
		string? activeStyle = null;
		var highlighted = false;
		var displayCellCount = Math.Max( 0, this._cells.Length - 1 );
		for ( var index = 0; index < displayCellCount; index++ ) {
			var cell = this._cells[ index ];
			var shouldHighlight = null != highlights && highlights[ index ];
			var targetStyle = ( preserveColor )
				? cell.Style
				: null
			;
			if ( shouldHighlight != highlighted || !string.Equals( targetStyle, activeStyle, StringComparison.Ordinal ) ) {
				builder.Append( ResetStyle );
				if ( null != targetStyle ) {
					builder.Append( targetStyle );
				}
				if ( shouldHighlight ) {
					builder.Append( "\u001b[7m" );
				}
				activeStyle = targetStyle;
				highlighted = shouldHighlight;
			}
			builder.Append( cell.Character );
		}
		if ( null != activeStyle || highlighted ) {
			builder.Append( ResetStyle );
		}
	}
	private static void WriteCell(
		WatchCell[] cells,
		int width,
		int height,
		ref int row,
		ref int column,
		char character,
		string? style,
		bool noWrap,
		ref bool skipUntilNewline
	) {
		ArgumentNullException.ThrowIfNull( cells );
		if ( row >= height ) {
			return;
		}
		if ( column >= width ) {
			if ( noWrap ) {
				skipUntilNewline = true;
				return;
			}
			row++;
			column = 0;
			if ( row >= height ) {
				return;
			}
		}
		cells[ ( row * width ) + column ] = new WatchCell( character, style );
		column++;
	}
	private sealed record WatchCell( char Character, string? Style );
}
/// <summary>Captures standard output and error into one arrival-ordered UTF-8 byte stream.</summary>
internal sealed class MergedCaptureStream : Stream {
	private readonly MemoryStream _buffer = new();
	private readonly SemaphoreSlim _gate = new( 1, 1 );
	private int _disposed;
	/// <inheritdoc />
	public override bool CanRead => false;
	/// <inheritdoc />
	public override bool CanSeek => false;
	/// <inheritdoc />
	public override bool CanWrite => 0 == Volatile.Read( ref this._disposed );
	/// <inheritdoc />
	public override long Length {
		get {
			this.ThrowIfDisposed();
			lock ( this._buffer ) {
				return this._buffer.Length;
			}
		}
	}
	/// <inheritdoc />
	public override long Position {
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}
	/// <summary>Gets the captured UTF-8 text.</summary>
	internal string GetText() {
		this.ThrowIfDisposed();
		lock ( this._buffer ) {
			return Encoding.UTF8.GetString( this._buffer.ToArray() );
		}
	}
	/// <inheritdoc />
	public override void Flush() {
		this.ThrowIfDisposed();
	}
	/// <inheritdoc />
	public override Task FlushAsync( CancellationToken cancellationToken ) {
		this.ThrowIfDisposed();
		cancellationToken.ThrowIfCancellationRequested();
		return Task.CompletedTask;
	}
	/// <inheritdoc />
	public override int Read( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
	/// <inheritdoc />
	public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
	/// <inheritdoc />
	public override void SetLength( long value ) => throw new NotSupportedException();
	/// <inheritdoc />
	public override void Write( byte[] buffer, int offset, int count ) {
		ArgumentNullException.ThrowIfNull( buffer );
		this.ThrowIfDisposed();
		lock ( this._buffer ) {
			this._buffer.Write( buffer, offset, count );
		}
	}
	/// <inheritdoc />
	public override void Write( ReadOnlySpan<byte> buffer ) {
		this.ThrowIfDisposed();
		lock ( this._buffer ) {
			this._buffer.Write( buffer );
		}
	}
	/// <inheritdoc />
	public override async ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) {
		this.ThrowIfDisposed();
		await this._gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this._buffer.Write( buffer.Span );
		} finally {
			this._gate.Release();
		}
	}
	/// <inheritdoc />
	protected override void Dispose( bool disposing ) {
		if ( 0 != Interlocked.Exchange( ref this._disposed, 1 ) ) {
			return;
		}
		if ( disposing ) {
			this._gate.Dispose();
			this._buffer.Dispose();
		}
		base.Dispose( disposing );
	}
	private void ThrowIfDisposed() {
		ObjectDisposedException.ThrowIf( 0 != Volatile.Read( ref this._disposed ), this );
	}
}
