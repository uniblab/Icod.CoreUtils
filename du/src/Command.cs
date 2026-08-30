using System.Globalization;
using System.Text;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Traversal;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.FileSystem.Usage;
using Icod.CoreUtils.Shared.Presentation;

namespace Icod.CoreUtils.DU;

/// <summary>
/// Implements <c>du</c>, reporting allocated, apparent, or inode usage.
/// Usage: <c>du [OPTION]... [FILE]...</c>.
/// </summary>
public static class Command {
	/// <summary>Runs the command synchronously.</summary>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) =>
		RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();

	/// <summary>Runs the command asynchronously.</summary>
	public static async Task<int> RunAsync(
		IReadOnlyList<string> args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		IFileSystemMetadataProvider? metadataProvider = null,
		IReadOnlyFileSystemProvider? readOnlyProvider = null,
		IEnvironmentVariableProvider? environmentProvider = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		metadataProvider ??= SystemFileSystemMetadataProvider.Instance;
		readOnlyProvider ??= SystemReadOnlyFileSystemProvider.Instance;
		environmentProvider ??= SystemEnvironmentVariableProvider.Instance;
		DuOptions options;
		try {
			options = DuOptionParser.Parse( args );
		} catch ( Exception exception ) when ( exception is DuUsageException or FormatException ) {
			await stderr.WriteLineAsync( $"du: {exception.Message}" ).ConfigureAwait( false );
			await stderr.WriteLineAsync( "Try 'du --help' for more information." ).ConfigureAwait( false );
			return 2;
		}
		if ( options.ShowHelp ) {
			await PrintHelpAsync( stdout ).ConfigureAwait( false );
			return 0;
		}
		if ( options.ShowVersion ) {
			await stdout.WriteLineAsync( "du (Icod.CoreUtils) 1.0" ).ConfigureAwait( false );
			return 0;
		}
		UsageSizePolicy sizePolicy;
		try {
			sizePolicy = options.Inodes
				? new UsageSizePolicy( UsageSizeStyle.Blocks, 1 )
				: UsageSizePolicy.Resolve( options.SizePolicy, "DU_BLOCK_SIZE", environmentProvider );
		} catch ( FormatException exception ) {
			await stderr.WriteLineAsync( $"du: {exception.Message}" ).ConfigureAwait( false );
			return 2;
		}
		List<string> paths;
		try {
			paths = await ResolvePathsAsync( options, stdin, cancellationToken ).ConfigureAwait( false );
		} catch ( Exception exception ) when ( exception is DuUsageException or IOException or UnauthorizedAccessException ) {
			await stderr.WriteLineAsync( $"du: {exception.Message}" ).ConfigureAwait( false );
			return 1;
		}
		if ( paths.Count == 0 && options.Files0From is null ) {
			paths.Add( "." );
		}
		if ( options.Files0From is null ) {
			var expansion = await PathnameOperandExpander.ExpandAsync(
				paths,
				cancellationToken: cancellationToken
			).ConfigureAwait( false );
			paths = expansion.Operands.ToList();
		}
		var calculator = new DiskUsageCalculator( metadataProvider, readOnlyProvider );
		var calculationOptions = new DiskUsageCalculationOptions {
			ApparentSize = options.ApparentSize,
			CountLinks = options.CountLinks,
			Inodes = options.Inodes,
			SeparateDirectories = options.SeparateDirectories,
			OneFileSystem = options.OneFileSystem,
			SymbolicLinkMode = options.SymbolicLinkMode,
			TimeField = ResolveTimeField( options.TimeField ),
			ExcludePatterns = options.ExcludePatterns
		};
		var exitCode = 0;
		ulong grandTotal = 0;
		foreach ( var path in paths ) {
			DiskUsageCalculation calculation;
			try {
				calculation = await calculator.CalculateAsync( path, calculationOptions, cancellationToken ).ConfigureAwait( false );
			} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException ) {
				await stderr.WriteLineAsync( $"du: cannot access '{path}': {exception.Message}" ).ConfigureAwait( false );
				exitCode = 1;
				continue;
			}
			foreach ( var diagnostic in calculation.Diagnostics ) {
				await stderr.WriteLineAsync( $"du: cannot read '{diagnostic.Path}': {diagnostic.Message}" ).ConfigureAwait( false );
				exitCode = 1;
			}
			var rootResult = calculation.Entries.LastOrDefault( entry => entry.Depth == 0 );
			if ( rootResult is not null ) {
				try {
					grandTotal = checked( grandTotal + rootResult.Value );
				} catch ( OverflowException ) {
					await stderr.WriteLineAsync( "du: grand total exceeds UInt64" ).ConfigureAwait( false );
					exitCode = 1;
				}
			}
			foreach ( var entry in calculation.Entries ) {
				if ( !ShouldPrint( entry, options ) ) continue;
				await WriteEntryAsync( stdout, entry, sizePolicy, options ).ConfigureAwait( false );
			}
		}
		if ( options.Total ) {
			await WriteEntryAsync( stdout, new DiskUsageResult( "total", 0, grandTotal, null, true ), sizePolicy, options ).ConfigureAwait( false );
		}
		return exitCode;
	}

	private static DiskUsageTimeField ResolveTimeField( string value ) => value switch {
		"atime" or "access" or "use" => DiskUsageTimeField.Access,
		"ctime" or "status" => DiskUsageTimeField.Change,
		"birth" or "creation" => DiskUsageTimeField.Birth,
		_ => DiskUsageTimeField.Modification
	};

	private static bool ShouldPrint( DiskUsageResult entry, DuOptions options ) {
		if ( options.MaximumDepth is int maximumDepth && entry.Depth > maximumDepth ) return false;
		if ( !entry.IsDirectory && !options.All && entry.Depth > 0 ) return false;
		if ( options.Summarize && entry.Depth != 0 ) return false;
		if ( options.Threshold is long threshold ) {
			if ( threshold >= 0 && entry.Value < (ulong)threshold ) return false;
			if ( threshold < 0 && entry.Value > (ulong)-threshold ) return false;
		}
		return true;
	}

	private static async Task WriteEntryAsync( TextWriter output, DiskUsageResult entry, UsageSizePolicy policy, DuOptions options ) {
		await output.WriteAsync( policy.Format( entry.Value ) ).ConfigureAwait( false );
		await output.WriteAsync( "\t" ).ConfigureAwait( false );
		if ( options.ShowTime ) {
			await output.WriteAsync( FormatTime( entry.LatestTime, options.TimeStyle ) ).ConfigureAwait( false );
			await output.WriteAsync( "\t" ).ConfigureAwait( false );
		}
		await output.WriteAsync( entry.Path ).ConfigureAwait( false );
		await output.WriteAsync( options.NullTerminate ? "\0" : Environment.NewLine ).ConfigureAwait( false );
	}

	private static string FormatTime( DateTimeOffset? value, string style ) {
		if ( value is null ) return "-";
		if ( style.StartsWith( '+' ) ) return FormatCustomTime( value.Value, style[ 1.. ] );
		return style switch {
			"full-iso" => value.Value.ToString( "yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture ),
			"iso" => value.Value.ToString( "yyyy-MM-dd", CultureInfo.InvariantCulture ),
			"locale" => value.Value.ToString( CultureInfo.CurrentCulture ),
			_ => value.Value.ToString( "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture )
		};
	}

	private static string FormatCustomTime( DateTimeOffset value, string format ) {
		var output = new StringBuilder( format.Length + 16 );
		for ( var index = 0; index < format.Length; index++ ) {
			if ( format[ index ] != '%' || index + 1 >= format.Length ) {
				output.Append( format[ index ] );
				continue;
			}
			var directive = format[ ++index ];
			output.Append( directive switch {
				'%' => "%",
				'a' => value.ToString( "ddd", CultureInfo.CurrentCulture ),
				'A' => value.ToString( "dddd", CultureInfo.CurrentCulture ),
				'b' or 'h' => value.ToString( "MMM", CultureInfo.CurrentCulture ),
				'B' => value.ToString( "MMMM", CultureInfo.CurrentCulture ),
				'c' => value.ToString( CultureInfo.CurrentCulture ),
				'C' => (value.Year / 100).ToString( "00", CultureInfo.InvariantCulture ),
				'd' => value.ToString( "dd", CultureInfo.InvariantCulture ),
				'D' => value.ToString( "MM/dd/yy", CultureInfo.InvariantCulture ),
				'e' => value.Day < 10 ? string.Concat( " ", value.Day.ToString( CultureInfo.InvariantCulture ) ) : value.Day.ToString( CultureInfo.InvariantCulture ),
				'F' => value.ToString( "yyyy-MM-dd", CultureInfo.InvariantCulture ),
				'g' => (ISOWeek.GetYear( value.DateTime ) % 100).ToString( "00", CultureInfo.InvariantCulture ),
				'G' => ISOWeek.GetYear( value.DateTime ).ToString( "0000", CultureInfo.InvariantCulture ),
				'H' => value.ToString( "HH", CultureInfo.InvariantCulture ),
				'I' => value.ToString( "hh", CultureInfo.InvariantCulture ),
				'j' => value.DayOfYear.ToString( "000", CultureInfo.InvariantCulture ),
				'm' => value.ToString( "MM", CultureInfo.InvariantCulture ),
				'M' => value.ToString( "mm", CultureInfo.InvariantCulture ),
				'n' => Environment.NewLine,
				'N' => string.Concat( (value.Ticks % TimeSpan.TicksPerSecond).ToString( "0000000", CultureInfo.InvariantCulture ), "00" ),
				'p' => value.ToString( "tt", CultureInfo.CurrentCulture ),
				'r' => value.ToString( "hh:mm:ss tt", CultureInfo.CurrentCulture ),
				'R' => value.ToString( "HH:mm", CultureInfo.InvariantCulture ),
				's' => value.ToUnixTimeSeconds().ToString( CultureInfo.InvariantCulture ),
				'S' => value.ToString( "ss", CultureInfo.InvariantCulture ),
				't' => "\t",
				'T' => value.ToString( "HH:mm:ss", CultureInfo.InvariantCulture ),
				'u' => (((int)value.DayOfWeek + 6) % 7 + 1).ToString( CultureInfo.InvariantCulture ),
				'V' => ISOWeek.GetWeekOfYear( value.DateTime ).ToString( "00", CultureInfo.InvariantCulture ),
				'w' => ((int)value.DayOfWeek).ToString( CultureInfo.InvariantCulture ),
				'x' => value.ToString( "d", CultureInfo.CurrentCulture ),
				'X' => value.ToString( "T", CultureInfo.CurrentCulture ),
				'y' => value.ToString( "yy", CultureInfo.InvariantCulture ),
				'Y' => value.ToString( "yyyy", CultureInfo.InvariantCulture ),
				'z' => value.ToString( "zzz", CultureInfo.InvariantCulture ).Replace( ":", string.Empty, StringComparison.Ordinal ),
				'Z' => value.ToString( "zzz", CultureInfo.InvariantCulture ),
				_ => string.Concat( "%", directive )
			} );
		}
		return output.ToString();
	}

	private static async Task<List<string>> ResolvePathsAsync( DuOptions options, TextReader input, CancellationToken cancellationToken ) {
		if ( options.Files0From is null ) return new List<string>( options.Paths );
		if ( options.Paths.Count > 0 ) throw new DuUsageException( "file operands cannot be combined with --files0-from" );
		StreamReader? ownedReader = null;
		var source = input;
		if ( options.Files0From != "-" ) {
			ownedReader = new StreamReader(
				new FileStream( options.Files0From, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous ),
				new UTF8Encoding( false, true ),
				detectEncodingFromByteOrderMarks: true
			);
			source = ownedReader;
		}
		try {
			var paths = new List<string>();
			var current = new StringBuilder();
			var buffer = new char[ 4096 ];
			while ( true ) {
				var read = await source.ReadAsync( buffer.AsMemory(), cancellationToken ).ConfigureAwait( false );
				if ( read == 0 ) break;
				for ( var index = 0; index < read; index++ ) {
					if ( buffer[ index ] != '\0' ) {
						current.Append( buffer[ index ] );
						continue;
					}
					if ( current.Length == 0 ) {
						throw new DuUsageException( $"{options.Files0From}: invalid zero-length file name" );
					}
					paths.Add( current.ToString() );
					current.Clear();
				}
			}
			if ( current.Length > 0 ) paths.Add( current.ToString() );
			return paths;
		} finally {
			if ( ownedReader is not null ) {
				ownedReader.Dispose();
			}
		}
	}

	private static async Task PrintHelpAsync( TextWriter output ) {
		await output.WriteLineAsync( "Usage: du [OPTION]... [FILE]..." ).ConfigureAwait( false );
		await output.WriteLineAsync( "Summarize device usage of the set of FILEs, recursively for directories." ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -0, --null                end each output line with NUL" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -a, --all                 write counts for all files" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --apparent-size       print apparent rather than allocated sizes" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -B, --block-size=SIZE     scale sizes by SIZE before printing" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -b, --bytes               apparent size in one-byte units" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -c, --total               produce a grand total" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -d, --max-depth=N         print a directory total only at depth N or less" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -h, --human-readable      print powers of 1024" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --inodes              list inode usage instead of block usage" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -k, -m                    use 1 KiB or 1 MiB output blocks" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -H, -L, -P                follow command-line, all, or no symbolic links" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -l, --count-links         count sizes many times for hard links" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -S, --separate-dirs       exclude subdirectory totals from parents" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --si                  print powers of 1000" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -s, --summarize           display only a total for each argument" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -t, --threshold=SIZE      exclude entries outside the threshold" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -x, --one-file-system     skip directories on different file systems" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -X, --exclude-from=FILE   read exclusion patterns from FILE" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --exclude=PATTERN     exclude matching files" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --files0-from=F       summarize NUL-terminated names from F" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --time[=WORD]         show the selected latest timestamp" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --help                display this help and exit" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --version             output version information and exit" ).ConfigureAwait( false );
	}
}
