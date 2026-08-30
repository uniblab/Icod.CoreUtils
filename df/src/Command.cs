using System.Globalization;
using System.Runtime.InteropServices;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.FileSystem.Usage;
using Icod.CoreUtils.Shared.Presentation;

namespace Icod.CoreUtils.Df;

/// <summary>
/// Implements <c>df</c>, reporting filesystem byte or inode capacity.
/// Usage: <c>df [OPTION]... [FILE]...</c>.
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
		IFileSystemUsageProvider? usageProvider = null,
		IEnvironmentVariableProvider? environmentProvider = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		_ = stdin;
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		usageProvider ??= SystemFileSystemUsageProvider.Instance;
		environmentProvider ??= SystemEnvironmentVariableProvider.Instance;
		DfOptions options;
		try {
			options = DfOptionParser.Parse( args );
		} catch ( Exception exception ) when ( exception is DfUsageException or FormatException ) {
			await stderr.WriteLineAsync( $"df: {exception.Message}" ).ConfigureAwait( false );
			await stderr.WriteLineAsync( "Try 'df --help' for more information." ).ConfigureAwait( false );
			return 2;
		}
		if ( options.ShowHelp ) {
			await PrintHelpAsync( stdout ).ConfigureAwait( false );
			return 0;
		}
		if ( options.ShowVersion ) {
			await stdout.WriteLineAsync( "df (Icod.CoreUtils) 1.0" ).ConfigureAwait( false );
			return 0;
		}
		UsageSizePolicy sizePolicy;
		try {
			sizePolicy = ResolveSizePolicy( options, environmentProvider );
		} catch ( FormatException exception ) {
			await stderr.WriteLineAsync( $"df: {exception.Message}" ).ConfigureAwait( false );
			return 2;
		}
		if ( options.Synchronize ) {
			if ( OperatingSystem.IsWindows() ) {
				await stderr.WriteLineAsync( "df: --sync is not supported on Windows" ).ConfigureAwait( false );
				return 1;
			}
			try {
				cancellationToken.ThrowIfCancellationRequested();
				SyncFileSystems();
			} catch ( Exception exception ) when (
				exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException
			) {
				await stderr.WriteLineAsync( $"df: cannot synchronize file systems: {exception.Message}" ).ConfigureAwait( false );
				return 1;
			}
		}
		IReadOnlyList<FileSystemUsageSnapshot> snapshots;
		try {
			IReadOnlyList<string> paths = options.Paths;
			if ( 0 < paths.Count ) {
				var expansion = await PathnameOperandExpander.ExpandAsync(
					paths,
					cancellationToken: cancellationToken
				).ConfigureAwait( false );
				paths = expansion.Operands;
			}
			snapshots = await usageProvider.GetFileSystemsAsync( paths, options.All, cancellationToken ).ConfigureAwait( false );
		} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException ) {
			await stderr.WriteLineAsync( $"df: {exception.Message}" ).ConfigureAwait( false );
			return 1;
		}
		var rows = snapshots.Where( snapshot => Matches( snapshot, options ) ).ToList();
		if ( options.Total && rows.Count > 0 ) {
			rows.Add( CreateTotal( rows ) );
		}
		var fields = ResolveFields( options );
		var table = rows.Select( snapshot => CreateRow( snapshot, fields, sizePolicy ) ).ToList();
		var headings = fields.Select( field => Heading( field, sizePolicy, options.Portability ) ).ToArray();
		var widths = Enumerable.Range( 0, fields.Count )
			.Select( index => Math.Max( headings[ index ].Length, table.Count == 0 ? 0 : table.Max( row => row[ index ].Length ) ) )
			.ToArray();
		await WriteRowAsync( stdout, headings, widths ).ConfigureAwait( false );
		foreach ( var row in table ) {
			await WriteRowAsync( stdout, row, widths ).ConfigureAwait( false );
		}
		return 0;
	}

	private static bool Matches( FileSystemUsageSnapshot snapshot, DfOptions options ) {
		if ( options.Local && !snapshot.IsLocal ) return false;
		var type = snapshot.Information.FileSystemType.IsAvailable
			? snapshot.Information.FileSystemType.GetRequiredValue()
			: string.Empty;
		if ( options.IncludedTypes.Count > 0 && !options.IncludedTypes.Contains( type, StringComparer.OrdinalIgnoreCase ) ) return false;
		return !options.ExcludedTypes.Contains( type, StringComparer.OrdinalIgnoreCase );
	}

	private static FileSystemUsageSnapshot CreateTotal( IReadOnlyList<FileSystemUsageSnapshot> rows ) {
		var total = Sum( rows, static row => row.Information.TotalBytes );
		var free = Sum( rows, static row => row.Information.FreeBytes );
		var available = Sum( rows, static row => row.Information.AvailableBytes );
		var info = new FileSystemInformation( "total", default ) {
			MountPoint = FileSystemMetadataValue<string>.Available( "-" ),
			FileSystemType = FileSystemMetadataValue<string>.Available( "-" ),
			TotalBytes = total,
			FreeBytes = free,
			AvailableBytes = available
		};
		return new FileSystemUsageSnapshot( "total", "total", info, true ) {
			TotalInodes = Sum( rows, static row => row.TotalInodes ),
			FreeInodes = Sum( rows, static row => row.FreeInodes ),
			AvailableInodes = Sum( rows, static row => row.AvailableInodes )
		};
	}

	private static FileSystemMetadataValue<ulong> Sum(
		IReadOnlyList<FileSystemUsageSnapshot> rows,
		Func<FileSystemUsageSnapshot, FileSystemMetadataValue<ulong>> selector
	) {
		if ( rows.Any( row => !selector( row ).IsAvailable ) ) return FileSystemMetadataValue<ulong>.Unavailable();
		try {
			return FileSystemMetadataValue<ulong>.Available( rows.Aggregate( 0UL, ( value, row ) => checked( value + selector( row ).GetRequiredValue() ) ) );
		} catch ( OverflowException ) {
			return FileSystemMetadataValue<ulong>.Unavailable( "total exceeds UInt64" );
		}
	}

	private static List<string> ResolveFields( DfOptions options ) {
		if ( options.OutputFields.Count > 0 ) return options.OutputFields;
		if ( options.OutputRequested ) {
			return new List<string> {
				"source", "fstype", "itotal", "iused", "iavail", "ipcent",
				"size", "used", "avail", "pcent", "file", "target"
			};
		}
		var fields = new List<string> { "source" };
		if ( options.PrintType ) fields.Add( "fstype" );
		if ( options.Inodes ) fields.AddRange( new[] { "itotal", "iused", "iavail", "ipcent" } );
		else fields.AddRange( new[] { "size", "used", "avail", "pcent" } );
		fields.Add( "target" );
		return fields;
	}

	private static string[] CreateRow( FileSystemUsageSnapshot snapshot, IReadOnlyList<string> fields, UsageSizePolicy policy ) =>
		fields.Select( field => Field( snapshot, field, policy ) ).ToArray();

	private static string Field( FileSystemUsageSnapshot snapshot, string field, UsageSizePolicy policy ) {
		var info = snapshot.Information;
		return field switch {
			"source" => snapshot.DeviceName,
			"fstype" => Text( info.FileSystemType ),
			"size" => Number( info.TotalBytes, policy ),
			"used" => Number( Difference( info.TotalBytes, info.FreeBytes ), policy ),
			"avail" => Number( info.AvailableBytes, policy ),
			"pcent" => Percent( Difference( info.TotalBytes, info.FreeBytes ), info.AvailableBytes ),
			"itotal" => Number( snapshot.TotalInodes, new UsageSizePolicy( UsageSizeStyle.Blocks, 1 ) ),
			"iused" => Number( Difference( snapshot.TotalInodes, snapshot.FreeInodes ), new UsageSizePolicy( UsageSizeStyle.Blocks, 1 ) ),
			"iavail" => Number( snapshot.AvailableInodes, new UsageSizePolicy( UsageSizeStyle.Blocks, 1 ) ),
			"ipcent" => PercentOfTotal( Difference( snapshot.TotalInodes, snapshot.FreeInodes ), snapshot.TotalInodes ),
			"file" => snapshot.SourcePath,
			"target" => Text( info.MountPoint ),
			_ => "-"
		};
	}

	private static UsageSizePolicy ResolveSizePolicy( DfOptions options, IEnvironmentVariableProvider environmentProvider ) {
		if ( options.SizePolicy is UsageSizePolicy explicitPolicy ) {
			return explicitPolicy;
		}
		if ( options.Portability ) {
			return string.IsNullOrEmpty( environmentProvider.GetValue( "POSIXLY_CORRECT" ) )
				? new UsageSizePolicy( UsageSizeStyle.Blocks, 1024 )
				: new UsageSizePolicy( UsageSizeStyle.Blocks, 512 );
		}
		return UsageSizePolicy.Resolve( null, "DF_BLOCK_SIZE", environmentProvider );
	}

	private static FileSystemMetadataValue<ulong> Difference( FileSystemMetadataValue<ulong> left, FileSystemMetadataValue<ulong> right ) =>
		left.IsAvailable && right.IsAvailable
			? FileSystemMetadataValue<ulong>.Available( left.GetRequiredValue() >= right.GetRequiredValue() ? left.GetRequiredValue() - right.GetRequiredValue() : 0 )
			: FileSystemMetadataValue<ulong>.Unavailable();
	private static string Number( FileSystemMetadataValue<ulong> value, UsageSizePolicy policy ) => value.IsAvailable ? policy.Format( value.GetRequiredValue() ) : "-";
	private static string Text( FileSystemMetadataValue<string> value ) => value.IsAvailable ? value.GetRequiredValue() : "-";
	private static string Percent( FileSystemMetadataValue<ulong> used, FileSystemMetadataValue<ulong> available ) {
		if ( !used.IsAvailable || !available.IsAvailable ) return "-";
		var usedValue = (decimal)used.GetRequiredValue();
		var denominator = usedValue + available.GetRequiredValue();
		if ( denominator == 0 ) return "-";
		return FormatPercent( usedValue, denominator );
	}

	private static string PercentOfTotal( FileSystemMetadataValue<ulong> used, FileSystemMetadataValue<ulong> total ) {
		if ( !used.IsAvailable || !total.IsAvailable || total.GetRequiredValue() == 0 ) return "-";
		return FormatPercent( used.GetRequiredValue(), total.GetRequiredValue() );
	}

	private static string FormatPercent( decimal used, decimal total ) => string.Concat(
		Math.Ceiling( used * 100m / total ).ToString( CultureInfo.InvariantCulture ),
		"%"
	);

	private static string Heading( string field, UsageSizePolicy policy, bool portability ) => field switch {
		"source" => "Filesystem",
		"fstype" => "Type",
		"size" => portability && policy.Style == UsageSizeStyle.Blocks
			? string.Concat( policy.BlockSize, "-blocks" )
			: "Size",
		"used" => "Used",
		"avail" => "Avail",
		"pcent" => portability ? "Capacity" : "Use%",
		"itotal" => "Inodes",
		"iused" => "IUsed",
		"iavail" => "IFree",
		"ipcent" => "IUse%",
		"file" => "File",
		"target" => "Mounted on",
		_ => field
	};
	private static async Task WriteRowAsync( TextWriter output, IReadOnlyList<string> values, IReadOnlyList<int> widths ) {
		for ( var index = 0; index < values.Count; index++ ) {
			if ( index > 0 ) await output.WriteAsync( " " ).ConfigureAwait( false );
			var numeric = index > 0 && values[ index ].Length > 0 && (char.IsDigit( values[ index ][ 0 ] ) || values[ index ] == "-");
			await output.WriteAsync( numeric ? values[ index ].PadLeft( widths[ index ] ) : values[ index ].PadRight( widths[ index ] ) ).ConfigureAwait( false );
		}
		await output.WriteLineAsync().ConfigureAwait( false );
	}

	[DllImport( "libc", EntryPoint = "sync" )]
	private static extern void SyncFileSystems();

	private static async Task PrintHelpAsync( TextWriter output ) {
		await output.WriteLineAsync( "Usage: df [OPTION]... [FILE]..." ).ConfigureAwait( false );
		await output.WriteLineAsync( "Show information about the file system containing each FILE, or all mounted file systems." ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -a, --all                 include otherwise omitted file systems" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -B, --block-size=SIZE     scale sizes by SIZE before printing" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -h, --human-readable      print powers of 1024" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -H, --si                  print powers of 1000" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -i, --inodes              list inode information instead of block usage" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -k                        use 1 KiB output blocks" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -l, --local               limit listing to local file systems" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -P, --portability         use the POSIX output format" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -T, --print-type          print file system type" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -t, --type=TYPE           limit listing to file systems of type TYPE" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -x, --exclude-type=TYPE   exclude file systems of type TYPE" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --no-sync             do not synchronize before observing (default)" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --output[=FIELD_LIST] select output fields" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --sync                synchronize before observing" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --total               produce a grand total" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --help                display this help and exit" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --version             output version information and exit" ).ConfigureAwait( false );
	}
}
