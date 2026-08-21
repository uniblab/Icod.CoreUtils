// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.ProcPs.HugeTop;

using System.Globalization;
using System.Text;
using Icod.CommandFramework.Terminal;
using Icod.ProcPs.Shared;
/// <summary>Implements the procps-ng compatible <c>hugetop</c> command.</summary>
public static class Command {
	private const int Success = 0;
	private const int Failure = 1;
	private const int Canceled = 130;
	private static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds( 3d );
	private const string VersionText = "hugetop from procps-ng 4.0.6";
	/// <summary>Runs <c>hugetop</c> synchronously.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdout">Optional standard-output stream.</param>
	/// <param name="stderr">Optional standard-error stream.</param>
	/// <returns>The process exit status.</returns>
	public static int Run( string[] args, Stream? stdout = null, Stream? stderr = null ) {
		ArgumentNullException.ThrowIfNull( args );
		return RunAsync( args, stdout, stderr ).GetAwaiter().GetResult();
	}
	/// <summary>Runs <c>hugetop</c> asynchronously with injectable providers and terminal lifecycle services.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdout">Optional standard-output stream.</param>
	/// <param name="stderr">Optional standard-error stream.</param>
	/// <param name="hugePageProvider">Optional huge-page provider.</param>
	/// <param name="sampler">Optional monotonic sampler.</param>
	/// <param name="terminalFactory">Optional full-screen terminal factory.</param>
	/// <param name="signalSourceFactory">Optional full-screen signal-source factory.</param>
	/// <param name="wallClockProvider">Optional wall-clock provider used for the report heading.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static async Task<int> RunAsync(
		IReadOnlyList<string> args,
		Stream? stdout = null,
		Stream? stderr = null,
		IProcHugePageProvider? hugePageProvider = null,
		ProcSampler? sampler = null,
		IProcFullScreenTerminalFactory? terminalFactory = null,
		IProcFullScreenSignalSourceFactory? signalSourceFactory = null,
		Func<DateTimeOffset>? wallClockProvider = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		var output = stdout ?? Console.OpenStandardOutput();
		var errorOutput = stderr ?? Console.OpenStandardError();
		var provider = hugePageProvider ?? SystemProcHugePageProvider.Instance;
		var refreshSampler = sampler ?? ProcSampler.CreateSystem();
		var terminals = terminalFactory ?? SystemProcFullScreenTerminalFactory.Instance;
		var signalSources = signalSourceFactory ?? SystemProcFullScreenSignalSourceFactory.Instance;
		var wallClock = wallClockProvider ?? GetCurrentTime;
		var parsed = Parse( args );
		if ( null != parsed.Error ) {
			await WriteTextAsync( errorOutput, $"hugetop: {parsed.Error}{Environment.NewLine}", cancellationToken ).ConfigureAwait( false );
			await WriteUsageAsync( errorOutput, cancellationToken ).ConfigureAwait( false );
			return Failure;
		}
		if ( parsed.Help ) {
			await WriteUsageAsync( output, cancellationToken ).ConfigureAwait( false );
			return Success;
		}
		if ( parsed.Version ) {
			await WriteTextAsync( output, $"{VersionText}{Environment.NewLine}", cancellationToken ).ConfigureAwait( false );
			return Success;
		}
		if ( parsed.Once ) {
			return await RunOnceAsync( provider, parsed, wallClock, output, errorOutput, cancellationToken ).ConfigureAwait( false );
		}
		return await RunInteractiveAsync(
			provider,
			refreshSampler,
			terminals,
			signalSources,
			parsed,
			wallClock,
			output,
			errorOutput,
			cancellationToken
		).ConfigureAwait( false );
	}
	private static async Task<int> RunOnceAsync(
		IProcHugePageProvider provider,
		ParsedArguments parsed,
		Func<DateTimeOffset> wallClock,
		Stream output,
		Stream errorOutput,
		CancellationToken cancellationToken
	) {
		var observed = await provider.GetSnapshotAsync( cancellationToken ).ConfigureAwait( false );
		if ( !observed.HasValue ) {
			await WriteUnavailableAsync( errorOutput, observed, cancellationToken ).ConfigureAwait( false );
			return Failure;
		}
		var text = HugeTopRenderer.Render( observed.Value, parsed.Numa, parsed.Human, wallClock() );
		await WriteTextAsync( output, text, cancellationToken ).ConfigureAwait( false );
		return Success;
	}
	private static async Task<int> RunInteractiveAsync(
		IProcHugePageProvider provider,
		ProcSampler sampler,
		IProcFullScreenTerminalFactory terminalFactory,
		IProcFullScreenSignalSourceFactory signalSourceFactory,
		ParsedArguments parsed,
		Func<DateTimeOffset> wallClock,
		Stream output,
		Stream errorOutput,
		CancellationToken cancellationToken
	) {
		IProcFullScreenTerminal? terminal = null;
		IProcFullScreenSignalSource? signals = null;
		var beganPresentation = false;
		try {
			terminal = await terminalFactory.OpenAsync( null, output, cancellationToken ).ConfigureAwait( false );
			if ( !terminal.IsInteractive ) {
				await WriteTextAsync(
					errorOutput,
					$"hugetop: standard output is not a terminal; use --once for batch output{Environment.NewLine}",
					cancellationToken
				).ConfigureAwait( false );
				return Failure;
			}
			var dimensions = terminal.GetDimensions();
			if ( !IsUsableDimensions( dimensions ) ) {
				await WriteTextAsync( errorOutput, $"hugetop: screen too small or too large{Environment.NewLine}", cancellationToken ).ConfigureAwait( false );
				return Failure;
			}
			beganPresentation = true;
			await terminal.BeginAsync( cancellationToken ).ConfigureAwait( false );
			signals = signalSourceFactory.Create( terminal.RestoreForSuspend );
			using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource( cancellationToken, signals.TerminationToken );
			var refreshToken = linkedCancellation.Token;
			await foreach ( var sample in sampler.RefreshAsync(
				provider.GetSnapshotAsync,
				parsed.Delay,
				fireImmediately: true,
				cancellationToken: refreshToken
			).ConfigureAwait( false ) ) {
				if ( signals.ConsumeResume() ) {
					await terminal.BeginAsync( refreshToken ).ConfigureAwait( false );
				}
				if ( signals.ConsumeResize() ) {
					dimensions = terminal.GetDimensions();
				} else {
					var currentDimensions = terminal.GetDimensions();
					if ( currentDimensions != dimensions ) {
						dimensions = currentDimensions;
					}
				}
				if ( !IsUsableDimensions( dimensions ) ) {
					await WriteTextAsync( errorOutput, $"hugetop: screen too small or too large{Environment.NewLine}", refreshToken ).ConfigureAwait( false );
					return Failure;
				}
				if ( !sample.Value.HasValue ) {
					await WriteUnavailableAsync( errorOutput, sample.Value, refreshToken ).ConfigureAwait( false );
					return Failure;
				}
				var frame = HugeTopRenderer.RenderFrame( sample.Value.Value, parsed.Numa, parsed.Human, wallClock(), dimensions );
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
	}
	private static ParsedArguments Parse( IReadOnlyList<string> args ) {
		var delay = DefaultDelay;
		var numa = false;
		var once = false;
		var human = false;
		for ( var index = 0; index < args.Count; index++ ) {
			var argument = args[ index ];
			if ( "-h" == argument || "--help" == argument ) {
				return ParsedArguments.ForHelp();
			}
			if ( "-V" == argument || "--version" == argument ) {
				return ParsedArguments.ForVersion();
			}
			if ( "-n" == argument || "--numa" == argument ) {
				numa = true;
				continue;
			}
			if ( "-o" == argument || "--once" == argument ) {
				once = true;
				continue;
			}
			if ( "-H" == argument || "--human" == argument ) {
				human = true;
				continue;
			}
			if ( TryOptionValue( args, ref index, argument, "-d", "--delay", out var delayText, out var delayError ) ) {
				if ( null != delayError ) {
					return ParsedArguments.Failed( delayError );
				}
				if ( !long.TryParse( delayText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds ) ) {
					return ParsedArguments.Failed( "illegal delay" );
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
			if ( "--" == argument ) {
				if ( index + 1 < args.Count ) {
					return ParsedArguments.Failed( $"unexpected operand '{args[ index + 1 ]}'" );
				}
				break;
			}
			if ( argument.StartsWith( '-' ) ) {
				return ParsedArguments.Failed( $"unrecognized option '{argument}'" );
			}
			return ParsedArguments.Failed( $"unexpected operand '{argument}'" );
		}
		return new ParsedArguments( delay, numa, once, human, Help: false, Version: false, Error: null );
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
	private static bool IsUsableDimensions( TerminalDimensions dimensions ) {
		return 20 <= dimensions.Width
			&& 5 <= dimensions.Height
			&& int.MaxValue >= (long)dimensions.Width * dimensions.Height;
	}
	private static DateTimeOffset GetCurrentTime() {
		return DateTimeOffset.Now;
	}
	private static async Task WriteUnavailableAsync<T>( Stream stderr, ProcObservedValue<T> observed, CancellationToken cancellationToken ) {
		ArgumentNullException.ThrowIfNull( stderr );
		ArgumentNullException.ThrowIfNull( observed );
		var diagnostic = observed.Diagnostic;
		if ( string.IsNullOrWhiteSpace( diagnostic ) ) {
			diagnostic = "huge-page information is unavailable on this host";
		}
		await WriteTextAsync( stderr, $"hugetop: {diagnostic}{Environment.NewLine}", cancellationToken ).ConfigureAwait( false );
	}
	private static Task WriteUsageAsync( Stream output, CancellationToken cancellationToken ) {
		ArgumentNullException.ThrowIfNull( output );
		return WriteTextAsync( output, HelpText(), cancellationToken );
	}
	private static string HelpText() {
		return string.Join(
			Environment.NewLine,
			"Usage:",
			" hugetop [options]",
			string.Empty,
			"Options:",
			" -d, --delay <secs>  delay updates",
			" -n, --numa          display per-NUMA-node huge-page information",
			" -o, --once          only display once, then exit",
			" -H, --human         display human-readable output",
			" -h, --help          display this help and exit",
			" -V, --version       output version information and exit",
			string.Empty
		);
	}
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
			await WriteTextAsync( stderr, $"hugetop: {message}{Environment.NewLine}", CancellationToken.None ).ConfigureAwait( false );
		} catch ( IOException ) {
		} catch ( ObjectDisposedException ) {
		}
	}
	private sealed record ParsedArguments(
		TimeSpan Delay,
		bool Numa,
		bool Once,
		bool Human,
		bool Help,
		bool Version,
		string? Error
	) {
		public static ParsedArguments ForHelp() {
			return new ParsedArguments( DefaultDelay, false, false, false, true, false, null );
		}
		public static ParsedArguments ForVersion() {
			return new ParsedArguments( DefaultDelay, false, false, false, false, true, null );
		}
		public static ParsedArguments Failed( string error ) {
			ArgumentException.ThrowIfNullOrWhiteSpace( error );
			return new ParsedArguments( DefaultDelay, false, false, false, false, false, error );
		}
	}
}
/// <summary>Renders procps-ng-style hugetop reports independently from terminal I/O.</summary>
internal static class HugeTopRenderer {
	private const ulong Kibibyte = 1024UL;
	/// <summary>Renders an unbounded one-shot report.</summary>
	/// <param name="snapshot">Huge-page snapshot.</param>
	/// <param name="numa">Whether to preserve NUMA-node rows.</param>
	/// <param name="human">Whether to use human-readable byte units.</param>
	/// <param name="now">Wall-clock timestamp.</param>
	/// <returns>The report text including a final host line terminator.</returns>
	internal static string Render( ProcHugePageSnapshot snapshot, bool numa, bool human, DateTimeOffset now ) {
		ArgumentNullException.ThrowIfNull( snapshot );
		var lines = BuildLines( snapshot, numa, human, now );
		return string.Concat( string.Join( Environment.NewLine, lines ), Environment.NewLine );
	}
	/// <summary>Renders a bounded full-screen frame.</summary>
	/// <param name="snapshot">Huge-page snapshot.</param>
	/// <param name="numa">Whether to preserve NUMA-node rows.</param>
	/// <param name="human">Whether to use human-readable byte units.</param>
	/// <param name="now">Wall-clock timestamp.</param>
	/// <param name="dimensions">Terminal dimensions.</param>
	/// <returns>A frame that exactly occupies the terminal dimensions.</returns>
	internal static string RenderFrame( ProcHugePageSnapshot snapshot, bool numa, bool human, DateTimeOffset now, TerminalDimensions dimensions ) {
		ArgumentNullException.ThrowIfNull( snapshot );
		if ( 1 > dimensions.Width || 1 > dimensions.Height ) {
			throw new ArgumentOutOfRangeException( nameof( dimensions ) );
		}
		var source = BuildLines( snapshot, numa, human, now );
		var frameLines = new string[ dimensions.Height ];
		for ( var index = 0; index < dimensions.Height; index++ ) {
			var line = string.Empty;
			if ( index < source.Count ) {
				line = source[ index ];
			}
			if ( line.Length > dimensions.Width ) {
				line = line[ ..dimensions.Width ];
			}
			frameLines[ index ] = line.PadRight( dimensions.Width );
		}
		return string.Join( Environment.NewLine, frameLines );
	}
	private static IReadOnlyList<string> BuildLines( ProcHugePageSnapshot snapshot, bool numa, bool human, DateTimeOffset now ) {
		var lines = new List<string> {
			$"hugetop - {now.ToLocalTime():HH:mm:ss}"
		};
		if ( numa ) {
			foreach ( var node in snapshot.Nodes ) {
				lines.Add( FormatNode( $"node{node.NodeId}", node.Pools ) );
			}
		} else {
			lines.Add( FormatNode( "node(s)", AggregatePools( snapshot.Nodes ) ) );
		}
		lines.Add( string.Empty );
		lines.Add( "     PID     SHARED    PRIVATE COMMAND" );
		foreach ( var process in snapshot.Processes ) {
			var shared = FormatMemory( process.SharedBytes, human );
			var privateBytes = FormatMemory( process.PrivateBytes, human );
			lines.Add( $"{process.ProcessId,8} {shared,10} {privateBytes,10} {process.CommandName}" );
		}
		return lines;
	}
	private static IReadOnlyList<ProcHugePagePool> AggregatePools( IReadOnlyList<ProcHugePageNode> nodes ) {
		var totals = new SortedDictionary<ulong, (ulong Total, ulong Free)>();
		foreach ( var node in nodes ) {
			foreach ( var pool in node.Pools ) {
				if ( !totals.TryGetValue( pool.PageSizeBytes, out var current ) ) {
					current = ( 0UL, 0UL );
				}
				totals[ pool.PageSizeBytes ] = (
					SaturatingAdd( current.Total, pool.TotalPages ),
					SaturatingAdd( current.Free, pool.FreePages )
				);
			}
		}
		return totals.Select(
			static pair => new ProcHugePagePool( pair.Key, pair.Value.Total, Math.Min( pair.Value.Total, pair.Value.Free ) )
		).ToArray();
	}
	private static string FormatNode( string label, IReadOnlyList<ProcHugePagePool> pools ) {
		var values = pools.Select(
			pool => $"{FormatMemory( pool.PageSizeBytes, human: true )} - {pool.FreePages}/{pool.TotalPages}"
		);
		return $"{label}: {string.Join( ", ", values )}";
	}
	private static string FormatMemory( ulong bytes, bool human ) {
		if ( !human ) {
			return $"{bytes / Kibibyte}K";
		}
		string[] suffixes = [ "B", "Ki", "Mi", "Gi", "Ti", "Pi", "Ei" ];
		double value = bytes;
		var suffixIndex = 0;
		while ( 1024d <= value && suffixIndex + 1 < suffixes.Length ) {
			value /= 1024d;
			suffixIndex++;
		}
		if ( 10d <= value || 0 == suffixIndex ) {
			return $"{value:0}{suffixes[ suffixIndex ]}";
		}
		return $"{value:0.0}{suffixes[ suffixIndex ]}";
	}
	private static ulong SaturatingAdd( ulong left, ulong right ) {
		if ( ulong.MaxValue - left < right ) {
			return ulong.MaxValue;
		}
		return left + right;
	}
}
