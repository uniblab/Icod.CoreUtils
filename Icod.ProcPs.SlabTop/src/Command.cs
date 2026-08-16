// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.ProcPs.SlabTop;

using System.Globalization;
using System.Text;
using Icod.CoreUtils.Shared.Terminal;
using Icod.ProcPs.Shared;
/// <summary>Implements the procps-ng compatible <c>slabtop</c> command.</summary>
public static class Command {
	private const int Success = 0;
	private const int Failure = 1;
	private const int Canceled = 130;
	private static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds( 3d );
	private const string VersionText = "slabtop from procps-ng 4.0.6";
	/// <summary>Runs <c>slabtop</c> synchronously.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdout">Optional standard-output stream.</param>
	/// <param name="stderr">Optional standard-error stream.</param>
	/// <returns>The process exit status.</returns>
	public static int Run( string[] args, Stream? stdout = null, Stream? stderr = null ) {
		ArgumentNullException.ThrowIfNull( args );
		return RunAsync( args, stdout, stderr ).GetAwaiter().GetResult();
	}
	/// <summary>Runs <c>slabtop</c> asynchronously with injectable providers and terminal lifecycle services.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdout">Optional standard-output stream.</param>
	/// <param name="stderr">Optional standard-error stream.</param>
	/// <param name="slabProvider">Optional slab allocator provider.</param>
	/// <param name="sampler">Optional monotonic sampler.</param>
	/// <param name="terminalFactory">Optional full-screen terminal factory.</param>
	/// <param name="signalSourceFactory">Optional full-screen signal-source factory.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static async Task<int> RunAsync(
		IReadOnlyList<string> args,
		Stream? stdout = null,
		Stream? stderr = null,
		IProcSlabProvider? slabProvider = null,
		ProcSampler? sampler = null,
		IProcFullScreenTerminalFactory? terminalFactory = null,
		IProcFullScreenSignalSourceFactory? signalSourceFactory = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		var output = stdout ?? Console.OpenStandardOutput();
		var errorOutput = stderr ?? Console.OpenStandardError();
		var provider = slabProvider ?? SystemProcSlabProvider.Instance;
		var refreshSampler = sampler ?? ProcSampler.CreateSystem();
		var terminals = terminalFactory ?? SystemProcFullScreenTerminalFactory.Instance;
		var signalSources = signalSourceFactory ?? SystemProcFullScreenSignalSourceFactory.Instance;
		var parsed = Parse( args );
		if ( null != parsed.Error ) {
			await WriteTextAsync( errorOutput, $"slabtop: {parsed.Error}{Environment.NewLine}", cancellationToken ).ConfigureAwait( false );
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
			return await RunOnceAsync( provider, parsed, output, errorOutput, cancellationToken ).ConfigureAwait( false );
		}
		return await RunInteractiveAsync(
			provider,
			refreshSampler,
			terminals,
			signalSources,
			parsed,
			output,
			errorOutput,
			cancellationToken
		).ConfigureAwait( false );
	}
	private static async Task<int> RunOnceAsync(
		IProcSlabProvider provider,
		ParsedArguments parsed,
		Stream output,
		Stream errorOutput,
		CancellationToken cancellationToken
	) {
		var observed = await provider.GetSlabsAsync( cancellationToken ).ConfigureAwait( false );
		if ( !observed.HasValue ) {
			await WriteUnavailableAsync( errorOutput, observed, cancellationToken ).ConfigureAwait( false );
			return Failure;
		}
		var text = SlabTopRenderer.Render( observed.Value, parsed.Sort, parsed.Human );
		await WriteTextAsync( output, text, cancellationToken ).ConfigureAwait( false );
		return Success;
	}
	private static async Task<int> RunInteractiveAsync(
		IProcSlabProvider provider,
		ProcSampler sampler,
		IProcFullScreenTerminalFactory terminalFactory,
		IProcFullScreenSignalSourceFactory signalSourceFactory,
		ParsedArguments parsed,
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
					$"slabtop: standard output is not a terminal; use --once for batch output{Environment.NewLine}",
					cancellationToken
				).ConfigureAwait( false );
				return Failure;
			}
			var dimensions = terminal.GetDimensions();
			if ( !IsUsableDimensions( dimensions ) ) {
				await WriteTextAsync( errorOutput, $"slabtop: screen too small or too large{Environment.NewLine}", cancellationToken ).ConfigureAwait( false );
				return Failure;
			}
			beganPresentation = true;
			await terminal.BeginAsync( cancellationToken ).ConfigureAwait( false );
			signals = signalSourceFactory.Create( terminal.RestoreForSuspend );
			using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource( cancellationToken, signals.TerminationToken );
			var refreshToken = linkedCancellation.Token;
			await foreach ( var sample in sampler.RefreshAsync(
				provider.GetSlabsAsync,
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
					await WriteTextAsync( errorOutput, $"slabtop: screen too small or too large{Environment.NewLine}", refreshToken ).ConfigureAwait( false );
					return Failure;
				}
				if ( !sample.Value.HasValue ) {
					await WriteUnavailableAsync( errorOutput, sample.Value, refreshToken ).ConfigureAwait( false );
					return Failure;
				}
				var frame = SlabTopRenderer.RenderFrame( sample.Value.Value, parsed.Sort, parsed.Human, dimensions );
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
		var delaySpecified = false;
		var once = false;
		var human = false;
		var sort = SlabSortCriterion.Objects;
		for ( var index = 0; index < args.Count; index++ ) {
			var argument = args[ index ];
			if ( "-h" == argument || "--help" == argument ) {
				return ParsedArguments.ForHelp();
			}
			if ( "-V" == argument || "--version" == argument ) {
				return ParsedArguments.ForVersion();
			}
			if ( "--human" == argument ) {
				human = true;
				continue;
			}
			if ( "-o" == argument || "--once" == argument ) {
				if ( delaySpecified ) {
					return ParsedArguments.Failed( "Cannot combine -d and -o options" );
				}
				once = true;
				continue;
			}
			if ( TryOptionValue( args, ref index, argument, "-d", "--delay", out var delayText, out var delayError ) ) {
				if ( null != delayError ) {
					return ParsedArguments.Failed( delayError );
				}
				if ( once ) {
					return ParsedArguments.Failed( "Cannot combine -d and -o options" );
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
				delaySpecified = true;
				continue;
			}
			if ( TryOptionValue( args, ref index, argument, "-s", "--sort", out var sortText, out var sortError ) ) {
				if ( null != sortError ) {
					return ParsedArguments.Failed( sortError );
				}
				if ( string.IsNullOrEmpty( sortText ) ) {
					return ParsedArguments.Failed( "sort criterion cannot be empty" );
				}
				sort = ParseSort( sortText[ 0 ] );
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
		return new ParsedArguments( delay, once, human, sort, Help: false, Version: false, Error: null );
	}
	private static SlabSortCriterion ParseSort( char criterion ) {
		return char.ToLowerInvariant( criterion ) switch {
			'a' => SlabSortCriterion.ActiveObjects,
			'b' => SlabSortCriterion.ObjectsPerSlab,
			'c' => SlabSortCriterion.CacheSize,
			'l' => SlabSortCriterion.Slabs,
			'v' => SlabSortCriterion.ActiveSlabs,
			'n' => SlabSortCriterion.Name,
			'o' => SlabSortCriterion.Objects,
			'p' => SlabSortCriterion.PagesPerSlab,
			's' => SlabSortCriterion.ObjectSize,
			'u' => SlabSortCriterion.Utilization,
			_ => SlabSortCriterion.Objects
		};
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
		return 40 <= dimensions.Width
			&& 9 <= dimensions.Height
			&& int.MaxValue >= (long)dimensions.Width * dimensions.Height;
	}
	private static async Task WriteUnavailableAsync<T>( Stream stderr, ProcObservedValue<T> observed, CancellationToken cancellationToken ) {
		ArgumentNullException.ThrowIfNull( stderr );
		ArgumentNullException.ThrowIfNull( observed );
		var diagnostic = observed.Diagnostic;
		if ( string.IsNullOrWhiteSpace( diagnostic ) ) {
			diagnostic = "slab allocator information is unavailable on this host";
		}
		await WriteTextAsync( stderr, $"slabtop: {diagnostic}{Environment.NewLine}", cancellationToken ).ConfigureAwait( false );
	}
	private static Task WriteUsageAsync( Stream output, CancellationToken cancellationToken ) {
		ArgumentNullException.ThrowIfNull( output );
		return WriteTextAsync( output, HelpText(), cancellationToken );
	}
	private static string HelpText() {
		return string.Join(
			Environment.NewLine,
			"Usage:",
			" slabtop [options]",
			string.Empty,
			"Options:",
			" -d, --delay <secs>  delay updates",
			" -s, --sort <char>   specify sort criteria",
			" -o, --once          only display once, then exit",
			"     --human         display human-readable output",
			" -h, --help          display this help and exit",
			" -V, --version       output version information and exit",
			string.Empty,
			"Valid sort criteria: a b c l v n o p s u",
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
			await WriteTextAsync( stderr, $"slabtop: {message}{Environment.NewLine}", CancellationToken.None ).ConfigureAwait( false );
		} catch ( IOException ) {
		} catch ( ObjectDisposedException ) {
		}
	}
	private sealed record ParsedArguments(
		TimeSpan Delay,
		bool Once,
		bool Human,
		SlabSortCriterion Sort,
		bool Help,
		bool Version,
		string? Error
	) {
		public static ParsedArguments ForHelp() {
			return new ParsedArguments( DefaultDelay, false, false, SlabSortCriterion.Objects, true, false, null );
		}
		public static ParsedArguments ForVersion() {
			return new ParsedArguments( DefaultDelay, false, false, SlabSortCriterion.Objects, false, true, null );
		}
		public static ParsedArguments Failed( string error ) {
			ArgumentException.ThrowIfNullOrWhiteSpace( error );
			return new ParsedArguments( DefaultDelay, false, false, SlabSortCriterion.Objects, false, false, error );
		}
	}
}
/// <summary>Identifies the procps-ng slabtop sort criterion.</summary>
internal enum SlabSortCriterion {
	/// <summary>Sort by active objects descending.</summary>
	ActiveObjects,
	/// <summary>Sort by objects per slab descending.</summary>
	ObjectsPerSlab,
	/// <summary>Sort by total cache size descending.</summary>
	CacheSize,
	/// <summary>Sort by total slabs descending.</summary>
	Slabs,
	/// <summary>Sort by active slabs descending.</summary>
	ActiveSlabs,
	/// <summary>Sort by name ascending.</summary>
	Name,
	/// <summary>Sort by total objects descending.</summary>
	Objects,
	/// <summary>Sort by pages per slab descending.</summary>
	PagesPerSlab,
	/// <summary>Sort by object size descending.</summary>
	ObjectSize,
	/// <summary>Sort by object utilization descending.</summary>
	Utilization
}
/// <summary>Renders procps-ng-style slabtop reports independently from terminal I/O.</summary>
internal static class SlabTopRenderer {
	/// <summary>Renders an unbounded one-shot slab report.</summary>
	/// <param name="entries">Slab-cache entries.</param>
	/// <param name="sort">Sort criterion.</param>
	/// <param name="human">Whether to use human-readable sizes.</param>
	/// <returns>The report text including a final host line terminator.</returns>
	internal static string Render( IReadOnlyList<ProcSlabCacheEntry> entries, SlabSortCriterion sort, bool human ) {
		ArgumentNullException.ThrowIfNull( entries );
		var lines = BuildLines( entries, sort, human );
		return string.Concat( string.Join( Environment.NewLine, lines ), Environment.NewLine );
	}
	/// <summary>Renders a bounded full-screen slab report.</summary>
	/// <param name="entries">Slab-cache entries.</param>
	/// <param name="sort">Sort criterion.</param>
	/// <param name="human">Whether to use human-readable sizes.</param>
	/// <param name="dimensions">Terminal dimensions.</param>
	/// <returns>A frame that exactly occupies the terminal dimensions.</returns>
	internal static string RenderFrame( IReadOnlyList<ProcSlabCacheEntry> entries, SlabSortCriterion sort, bool human, TerminalDimensions dimensions ) {
		ArgumentNullException.ThrowIfNull( entries );
		if ( 1 > dimensions.Width || 1 > dimensions.Height ) {
			throw new ArgumentOutOfRangeException( nameof( dimensions ) );
		}
		var source = BuildLines( entries, sort, human );
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
	private static IReadOnlyList<string> BuildLines( IReadOnlyList<ProcSlabCacheEntry> entries, SlabSortCriterion sort, bool human ) {
		var ordered = SortEntries( entries, sort ).ToArray();
		var totalObjects = Sum( entries, static entry => entry.TotalObjects );
		var activeObjects = Sum( entries, static entry => entry.ActiveObjects );
		var totalSlabs = Sum( entries, static entry => entry.TotalSlabs );
		var activeSlabs = Sum( entries, static entry => entry.ActiveSlabs );
		var activeCaches = (ulong)entries.Count( static entry => 0UL < entry.ActiveObjects );
		var totalCaches = (ulong)entries.Count;
		var activeSize = Sum( entries, static entry => ActiveCacheSize( entry ) );
		var totalSize = Sum( entries, static entry => CacheSize( entry ) );
		var minObjectSize = 0UL;
		var maxObjectSize = 0UL;
		double averageObjectSize = 0d;
		if ( 0 < entries.Count ) {
			minObjectSize = entries.Min( static entry => entry.ObjectSizeBytes );
			maxObjectSize = entries.Max( static entry => entry.ObjectSizeBytes );
			averageObjectSize = entries.Average( static entry => (double)entry.ObjectSizeBytes );
		}
		var lines = new List<string> {
			$" Active / Total Objects (% used)       : {activeObjects} / {totalObjects} ({Percent( activeObjects, totalObjects ):0.0}%)",
			$" Active / Total Slabs (% used)         : {activeSlabs} / {totalSlabs} ({Percent( activeSlabs, totalSlabs ):0.0}%)",
			$" Active / Total Caches (% used)        : {activeCaches} / {totalCaches} ({Percent( activeCaches, totalCaches ):0.0}%)",
			$" Active / Total Size (% used)          : {FormatBytes( activeSize, human )} / {FormatBytes( totalSize, human )} ({Percent( activeSize, totalSize ):0.0}%)",
			$" Minimum / Average / Maximum Object    : {FormatBytes( minObjectSize, human )} / {FormatBytes( (ulong)Math.Round( averageObjectSize ), human )} / {FormatBytes( maxObjectSize, human )}",
			string.Empty,
			"    OBJS   ACTIVE  USE OBJ SIZE  SLABS OBJ/SLAB CACHE SIZE NAME"
		};
		foreach ( var entry in ordered ) {
			lines.Add(
				string.Concat(
					$"{entry.TotalObjects,8} {entry.ActiveObjects,8} {Percent( entry.ActiveObjects, entry.TotalObjects ),4:0}% ",
					$"{FormatBytes( entry.ObjectSizeBytes, human ),8} {entry.TotalSlabs,6} {entry.ObjectsPerSlab,8} ",
					$"{FormatBytes( CacheSize( entry ), human ),10} {entry.Name}"
				)
			);
		}
		return lines;
	}
	private static IEnumerable<ProcSlabCacheEntry> SortEntries( IReadOnlyList<ProcSlabCacheEntry> entries, SlabSortCriterion sort ) {
		return sort switch {
			SlabSortCriterion.ActiveObjects => entries.OrderByDescending( static entry => entry.ActiveObjects ).ThenBy( static entry => entry.Name, StringComparer.Ordinal ),
			SlabSortCriterion.ObjectsPerSlab => entries.OrderByDescending( static entry => entry.ObjectsPerSlab ).ThenBy( static entry => entry.Name, StringComparer.Ordinal ),
			SlabSortCriterion.CacheSize => entries.OrderByDescending( static entry => CacheSize( entry ) ).ThenBy( static entry => entry.Name, StringComparer.Ordinal ),
			SlabSortCriterion.Slabs => entries.OrderByDescending( static entry => entry.TotalSlabs ).ThenBy( static entry => entry.Name, StringComparer.Ordinal ),
			SlabSortCriterion.ActiveSlabs => entries.OrderByDescending( static entry => entry.ActiveSlabs ).ThenBy( static entry => entry.Name, StringComparer.Ordinal ),
			SlabSortCriterion.Name => entries.OrderBy( static entry => entry.Name, StringComparer.Ordinal ),
			SlabSortCriterion.PagesPerSlab => entries.OrderByDescending( static entry => entry.PagesPerSlab ).ThenBy( static entry => entry.Name, StringComparer.Ordinal ),
			SlabSortCriterion.ObjectSize => entries.OrderByDescending( static entry => entry.ObjectSizeBytes ).ThenBy( static entry => entry.Name, StringComparer.Ordinal ),
			SlabSortCriterion.Utilization => entries.OrderByDescending( static entry => Percent( entry.ActiveObjects, entry.TotalObjects ) ).ThenBy( static entry => entry.Name, StringComparer.Ordinal ),
			_ => entries.OrderByDescending( static entry => entry.TotalObjects ).ThenBy( static entry => entry.Name, StringComparer.Ordinal )
		};
	}
	private static ulong CacheSize( ProcSlabCacheEntry entry ) {
		return SlabBytes( entry.TotalSlabs, entry.PagesPerSlab );
	}
	private static ulong ActiveCacheSize( ProcSlabCacheEntry entry ) {
		return SlabBytes( entry.ActiveSlabs, entry.PagesPerSlab );
	}
	private static ulong SlabBytes( ulong slabs, ulong pagesPerSlab ) {
		return SaturatingMultiply(
			SaturatingMultiply( slabs, pagesPerSlab ),
			(ulong)Math.Max( 1, Environment.SystemPageSize )
		);
	}
	private static ulong Sum( IEnumerable<ProcSlabCacheEntry> entries, Func<ProcSlabCacheEntry, ulong> selector ) {
		ulong total = 0UL;
		foreach ( var entry in entries ) {
			total = SaturatingAdd( total, selector( entry ) );
		}
		return total;
	}
	private static double Percent( ulong active, ulong total ) {
		if ( 0UL == total ) {
			return 0d;
		}
		return 100d * active / total;
	}
	private static string FormatBytes( ulong bytes, bool human ) {
		if ( !human ) {
			return $"{bytes / 1024d:0.00}K";
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
	private static ulong SaturatingMultiply( ulong left, ulong right ) {
		if ( 0UL != right && ulong.MaxValue / right < left ) {
			return ulong.MaxValue;
		}
		return left * right;
	}
}
