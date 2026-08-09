// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.ProcPs.Tload;

using System.Globalization;
using System.Text;
using Icod.CoreUtils.Shared.Terminal;
using Icod.ProcPs.Shared;

/// <summary>Implements the procps-ng compatible <c>tload</c> command.</summary>
public static class Command {
	private const int Success = 0;
	private const int Failure = 1;
	private const int Canceled = 130;
	private const double DefaultScale = 0d;
	private static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds( 5d );
	private const string VersionText = "tload from procps-ng 4.0.6";

	/// <summary>Runs <c>tload</c> synchronously.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdout">Optional standard-output stream.</param>
	/// <param name="stderr">Optional standard-error stream.</param>
	/// <returns>The process exit status.</returns>
	public static int Run( string[] args, Stream? stdout = null, Stream? stderr = null ) {
		ArgumentNullException.ThrowIfNull( args );
		return RunAsync( args, stdout, stderr ).GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>tload</c> asynchronously with injectable streams and ProcPs providers.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdout">Optional standard-output stream.</param>
	/// <param name="stderr">Optional standard-error stream.</param>
	/// <param name="metricsProvider">Optional ProcPs system-metrics provider.</param>
	/// <param name="sampler">Optional monotonic ProcPs sampler.</param>
	/// <param name="terminalFactory">Optional full-screen terminal factory.</param>
	/// <param name="signalSourceFactory">Optional terminal-lifecycle signal-source factory.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task whose result is the procps-compatible exit status.</returns>
	public static async Task<int> RunAsync(
		IReadOnlyList<string> args,
		Stream? stdout = null,
		Stream? stderr = null,
		IProcSystemMetricsProvider? metricsProvider = null,
		ProcSampler? sampler = null,
		IProcFullScreenTerminalFactory? terminalFactory = null,
		IProcFullScreenSignalSourceFactory? signalSourceFactory = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		var output = stdout ?? Console.OpenStandardOutput();
		var errorOutput = stderr ?? Console.OpenStandardError();
		var metrics = metricsProvider ?? SystemProcSystemMetricsProvider.Instance;
		var refreshSampler = sampler ?? ProcSampler.CreateSystem();
		var terminals = terminalFactory ?? SystemProcFullScreenTerminalFactory.Instance;
		var signalSources = signalSourceFactory ?? SystemProcFullScreenSignalSourceFactory.Instance;
		var parsed = Parse( args );
		if ( null != parsed.Error ) {
			await WriteTextAsync(
				errorOutput,
				$"tload: {parsed.Error}{Environment.NewLine}",
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
				parsed.TerminalPath,
				output,
				cancellationToken
			).ConfigureAwait( false );
			if ( !terminal.IsInteractive && null == parsed.TerminalPath ) {
				await WriteTextAsync(
					errorOutput,
					$"tload: standard output is not a terminal; specify a terminal operand{Environment.NewLine}",
					cancellationToken
				).ConfigureAwait( false );
				return Failure;
			}
			var dimensions = terminal.GetDimensions();
			if ( !IsUsableDimensions( dimensions ) ) {
				await WriteTextAsync(
					errorOutput,
					$"tload: screen too small or too large{Environment.NewLine}",
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
			var graph = new TloadGraphState( parsed.Scale );
			await foreach ( var sample in refreshSampler.RefreshAsync(
				CaptureLoadAveragesAsync,
				parsed.Delay,
				fireImmediately: true,
				cancellationToken: refreshToken
			).ConfigureAwait( false ) ) {
				if ( signals.ConsumeResume() ) {
					await terminal.BeginAsync( refreshToken ).ConfigureAwait( false );
				}
				var resized = signals.ConsumeResize();
				var currentDimensions = terminal.GetDimensions();
				if ( resized || currentDimensions != dimensions ) {
					dimensions = currentDimensions;
					if ( !IsUsableDimensions( dimensions ) ) {
						await WriteTextAsync(
							errorOutput,
							$"tload: screen too small or too large{Environment.NewLine}",
							refreshToken
						).ConfigureAwait( false );
						return Failure;
					}
					graph.Reset();
				}
				if ( !sample.Value.HasValue ) {
					var diagnostic = sample.Value.Diagnostic;
					if ( string.IsNullOrWhiteSpace( diagnostic ) ) {
						diagnostic = "load average is unavailable on this host";
					}
					await WriteTextAsync(
						errorOutput,
						$"tload: {diagnostic}{Environment.NewLine}",
						refreshToken
					).ConfigureAwait( false );
					return Failure;
				}
				if ( !IsValidLoad( sample.Value.Value ) ) {
					await WriteTextAsync(
						errorOutput,
						$"tload: load-average provider returned an invalid value{Environment.NewLine}",
						refreshToken
					).ConfigureAwait( false );
					return Failure;
				}
				var frame = graph.Render( sample.Value.Value, dimensions );
				await terminal.WriteFrameAsync( frame, refreshToken ).ConfigureAwait( false );
			}
			return Success;
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

		async Task<ProcObservedValue<ProcLoadAverages>> CaptureLoadAveragesAsync( CancellationToken token ) {
			var snapshot = await metrics.GetSnapshotAsync( token ).ConfigureAwait( false );
			return snapshot.LoadAverages;
		}
	}

	private static bool IsUsableDimensions( TerminalDimensions dimensions ) {
		return 2 <= dimensions.Width
			&& 2 <= dimensions.Height
			&& int.MaxValue >= (long)dimensions.Width * dimensions.Height;
	}

	private static bool IsValidLoad( ProcLoadAverages load ) {
		return double.IsFinite( load.OneMinute )
			&& double.IsFinite( load.FiveMinutes )
			&& double.IsFinite( load.FifteenMinutes )
			&& 0d <= load.OneMinute
			&& 0d <= load.FiveMinutes
			&& 0d <= load.FifteenMinutes;
	}

	private static ParsedArguments Parse( IReadOnlyList<string> args ) {
		var scale = DefaultScale;
		var delay = DefaultDelay;
		string? terminalPath = null;
		for ( var index = 0; index < args.Count; index++ ) {
			var argument = args[ index ];
			if ( "--" == argument ) {
				index++;
				for ( ; index < args.Count; index++ ) {
					if ( null != terminalPath ) {
						return ParsedArguments.Failed( "too many terminal operands" );
					}
					terminalPath = args[ index ];
				}
				break;
			}
			if ( "-h" == argument || "--help" == argument ) {
				return ParsedArguments.ForHelp();
			}
			if ( "-V" == argument || "--version" == argument ) {
				return ParsedArguments.ForVersion();
			}
			if ( TryOptionValue( args, ref index, argument, "-s", "--scale", out var scaleText, out var scaleError ) ) {
				if ( null != scaleError ) {
					return ParsedArguments.Failed( scaleError );
				}
				if ( !double.TryParse( scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out scale ) || !double.IsFinite( scale ) ) {
					return ParsedArguments.Failed( "failed to parse scale argument" );
				}
				if ( 0d > scale ) {
					return ParsedArguments.Failed( "scale cannot be negative" );
				}
				continue;
			}
			if ( TryOptionValue( args, ref index, argument, "-d", "--delay", out var delayText, out var delayError ) ) {
				if ( null != delayError ) {
					return ParsedArguments.Failed( delayError );
				}
				if ( !long.TryParse( delayText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds ) ) {
					return ParsedArguments.Failed( "failed to parse delay argument" );
				}
				if ( 1L > seconds ) {
					return ParsedArguments.Failed( "delay must be positive integer" );
				}
				if ( uint.MaxValue < seconds ) {
					return ParsedArguments.Failed( "too large delay value" );
				}
				delay = TimeSpan.FromSeconds( seconds );
				continue;
			}
			if ( argument.StartsWith( "-", StringComparison.Ordinal ) && "-" != argument ) {
				return ParsedArguments.Failed( $"unrecognized option '{argument}'" );
			}
			if ( null != terminalPath ) {
				return ParsedArguments.Failed( "too many terminal operands" );
			}
			terminalPath = argument;
		}
		return new ParsedArguments( scale, delay, terminalPath, help: false, version: false, error: null );
	}

	private static bool TryOptionValue(
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
			value = argument[ shortName.Length.. ];
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
		" tload [options] [tty]",
		string.Empty,
		"Options:",
		" -d, --delay <secs>  update delay in seconds",
		" -s, --scale <num>   vertical scale",
		" -h, --help          display this help and exit",
		" -V, --version       output version information and exit",
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
				$"tload: {message}{Environment.NewLine}",
				CancellationToken.None
			).ConfigureAwait( false );
		} catch ( IOException ) {
		} catch ( ObjectDisposedException ) {
		}
	}

	private sealed record ParsedArguments(
		double Scale,
		TimeSpan Delay,
		string? TerminalPath,
		bool Help,
		bool Version,
		string? Error
	) {
		public static ParsedArguments ForHelp() => new( DefaultScale, DefaultDelay, null, true, false, null );
		public static ParsedArguments ForVersion() => new( DefaultScale, DefaultDelay, null, false, true, null );
		public static ParsedArguments Failed( string error ) {
			ArgumentException.ThrowIfNullOrWhiteSpace( error );
			return new ParsedArguments( DefaultScale, DefaultDelay, null, false, false, error );
		}
	}
}

/// <summary>Maintains the procps-style scrolling load graph independently from terminal I/O.</summary>
internal sealed class TloadGraphState {
	private readonly double _configuredScale;
	private readonly List<GraphPoint> _history = new();
	private double _scaleFactor;

	/// <summary>Initializes graph state for the requested vertical scale.</summary>
	/// <param name="configuredScale">Configured vertical scale, or zero for automatic scaling.</param>
	internal TloadGraphState( double configuredScale ) {
		if ( 0d > configuredScale || !double.IsFinite( configuredScale ) ) {
			throw new ArgumentOutOfRangeException( nameof( configuredScale ) );
		}
		this._configuredScale = configuredScale;
	}

	/// <summary>Clears scrolling history after a terminal geometry change.</summary>
	internal void Reset() {
		this._history.Clear();
		this._scaleFactor = 0d;
	}

	/// <summary>Renders one complete terminal frame for the next load observation.</summary>
	/// <param name="load">Current one-, five-, and fifteen-minute load averages.</param>
	/// <param name="dimensions">Current terminal dimensions.</param>
	/// <returns>The complete frame payload excluding the terminal home sequence.</returns>
	internal string Render( ProcLoadAverages load, TerminalDimensions dimensions ) {
		ArgumentNullException.ThrowIfNull( load );
		if ( 2 > dimensions.Width || 2 > dimensions.Height ) {
			throw new ArgumentOutOfRangeException( nameof( dimensions ) );
		}
		var maximumScale = ( 0d < this._configuredScale )
			? this._configuredScale
			: dimensions.Height
		;
		if ( 0d >= this._scaleFactor ) {
			this._scaleFactor = maximumScale;
		} else if ( this._scaleFactor < maximumScale ) {
			this._scaleFactor *= 2d;
		}
		while ( dimensions.Height <= load.OneMinute * this._scaleFactor ) {
			this._scaleFactor /= 2d;
			if ( double.Epsilon >= this._scaleFactor ) {
				break;
			}
		}
		this._history.Add( new GraphPoint( load, this._scaleFactor ) );
		if ( dimensions.Width < this._history.Count ) {
			this._history.RemoveAt( 0 );
		}
		var size = checked( dimensions.Width * dimensions.Height );
		var buffer = new char[ size ];
		Array.Fill( buffer, ' ' );
		for ( var column = 0; column < this._history.Count; column++ ) {
			DrawColumn( buffer, dimensions, column, this._history[ column ] );
		}
		var label = string.Concat(
			" ",
			load.OneMinute.ToString( "F2", CultureInfo.InvariantCulture ),
			", ",
			load.FiveMinutes.ToString( "F2", CultureInfo.InvariantCulture ),
			", ",
			load.FifteenMinutes.ToString( "F2", CultureInfo.InvariantCulture )
		);
		var labelLength = Math.Min( label.Length, Math.Max( 0, size - 1 ) );
		label.AsSpan( 0, labelLength ).CopyTo( buffer );
		if ( labelLength < size - 1 ) {
			buffer[ labelLength ] = ' ';
		}
		return new string( buffer, 0, size - 1 );
	}

	private static void DrawColumn( char[] buffer, TerminalDimensions dimensions, int column, GraphPoint point ) {
		var lines = (int)( point.Load.OneMinute * point.Scale );
		var row = dimensions.Height - 1;
		while ( 0 < lines && 0 <= row ) {
			buffer[ ( row * dimensions.Width ) + column ] = '*';
			lines--;
			row--;
		}
		for ( var tick = 1; ; tick++ ) {
			row = dimensions.Height - (int)( tick * point.Scale );
			if ( 0 > row || dimensions.Height <= row ) {
				break;
			}
			var offset = ( row * dimensions.Width ) + column;
			buffer[ offset ] = ( ' ' == buffer[ offset ] )
				? '-'
				: '='
			;
		}
	}

	private sealed record GraphPoint( ProcLoadAverages Load, double Scale );
}
