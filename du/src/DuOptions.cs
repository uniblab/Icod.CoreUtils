using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.FileSystem.Usage;

namespace Icod.CoreUtils.DU;

/// <summary>Represents parsed <c>du</c> command-line options.</summary>
public sealed class DuOptions {
	/// <summary>Gets whether nondirectory entries are printed.</summary>
	public bool All { get; set; }
	/// <summary>Gets whether logical rather than allocated size is counted.</summary>
	public bool ApparentSize { get; set; }
	/// <summary>Gets whether a grand total is printed.</summary>
	public bool Total { get; set; }
	/// <summary>Gets whether hard-link names are counted independently.</summary>
	public bool CountLinks { get; set; }
	/// <summary>Gets whether inode counts replace byte counts.</summary>
	public bool Inodes { get; set; }
	/// <summary>Gets whether descendant directory totals are separate.</summary>
	public bool SeparateDirectories { get; set; }
	/// <summary>Gets whether traversal stays on the root filesystem.</summary>
	public bool OneFileSystem { get; set; }
	/// <summary>Gets whether only root totals are printed.</summary>
	public bool Summarize { get; set; }
	/// <summary>Gets whether output records are NUL terminated.</summary>
	public bool NullTerminate { get; set; }
	/// <summary>Gets whether timestamps are printed.</summary>
	public bool ShowTime { get; set; }
	/// <summary>Gets whether help was requested.</summary>
	public bool ShowHelp { get; set; }
	/// <summary>Gets whether version was requested.</summary>
	public bool ShowVersion { get; set; }
	/// <summary>Gets the symbolic-link traversal policy.</summary>
	public SymbolicLinkTraversalMode SymbolicLinkMode { get; set; } = SymbolicLinkTraversalMode.Never;
	/// <summary>Gets the maximum displayed depth.</summary>
	public int? MaximumDepth { get; set; }
	/// <summary>Gets whether a maximum depth was explicitly supplied.</summary>
	public bool MaximumDepthSpecified { get; set; }
	/// <summary>Gets a signed display threshold.</summary>
	public long? Threshold { get; set; }
	/// <summary>Gets the timestamp selector.</summary>
	public string TimeField { get; set; } = "mtime";
	/// <summary>Gets the timestamp style.</summary>
	public string TimeStyle { get; set; } = "long-iso";
	/// <summary>Gets an explicit size policy.</summary>
	public UsageSizePolicy? SizePolicy { get; set; }
	/// <summary>Gets pathname operands.</summary>
	public List<string> Paths { get; } = new();
	/// <summary>Gets exclusion patterns.</summary>
	public List<string> ExcludePatterns { get; } = new();
	/// <summary>Gets a NUL-delimited input source.</summary>
	public string? Files0From { get; set; }
}

/// <summary>Represents a controlled <c>du</c> usage failure.</summary>
public sealed class DuUsageException : Exception {
	/// <summary>Initializes a usage failure.</summary>
	public DuUsageException( string message ) : base( message ) { }
}

/// <summary>Parses the GNU <c>du</c> option vocabulary used by Batch 47.</summary>
public static class DuOptionParser {
	/// <summary>Parses one invocation.</summary>
	public static DuOptions Parse( IReadOnlyList<string> arguments ) {
		ArgumentNullException.ThrowIfNull( arguments );
		var options = new DuOptions();
		var operandsOnly = false;
		for ( var index = 0; index < arguments.Count; index++ ) {
			var argument = arguments[ index ];
			if ( operandsOnly || argument.Length < 2 || argument[ 0 ] != '-' || argument == "-" ) {
				options.Paths.Add( argument );
				continue;
			}
			if ( argument == "--" ) {
				operandsOnly = true;
				continue;
			}
			if ( argument.StartsWith( "--", StringComparison.Ordinal ) ) ParseLong( argument[ 2.. ], arguments, ref index, options );
			else ParseShort( argument, arguments, ref index, options );
		}
		if ( options.Summarize && options.All ) throw new DuUsageException( "cannot both summarize and show all entries" );
		if ( options.Summarize && options.MaximumDepthSpecified && options.MaximumDepth is > 0 ) throw new DuUsageException( "summarize conflicts with --max-depth" );
		return options;
	}

	private static void ParseLong( string argument, IReadOnlyList<string> arguments, ref int index, DuOptions options ) {
		var separator = argument.IndexOf( '=' );
		var name = separator >= 0 ? argument[ ..separator ] : argument;
		var value = separator >= 0 ? argument[ (separator + 1).. ] : null;
		switch ( name ) {
			case "null": options.NullTerminate = true; return;
			case "all": options.All = true; return;
			case "apparent-size": options.ApparentSize = true; return;
			case "block-size": options.SizePolicy = Blocks( ReadValue( name, value, arguments, ref index ) ); return;
			case "bytes": options.ApparentSize = true; options.SizePolicy = Blocks( 1 ); return;
			case "total": options.Total = true; return;
			case "dereference-args": options.SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly; return;
			case "dereference": options.SymbolicLinkMode = SymbolicLinkTraversalMode.Always; return;
			case "count-links": options.CountLinks = true; return;
			case "human-readable": options.SizePolicy = new UsageSizePolicy( UsageSizeStyle.HumanReadable, 1 ); return;
			case "inodes": options.Inodes = true; return;
			case "no-dereference": options.SymbolicLinkMode = SymbolicLinkTraversalMode.Never; return;
			case "separate-dirs": options.SeparateDirectories = true; return;
			case "si": options.SizePolicy = new UsageSizePolicy( UsageSizeStyle.Si, 1 ); return;
			case "summarize": options.Summarize = true; options.MaximumDepth = 0; return;
			case "one-file-system": options.OneFileSystem = true; return;
			case "max-depth": options.MaximumDepth = Nonnegative( "max-depth", ReadValue( name, value, arguments, ref index ) ); options.MaximumDepthSpecified = true; return;
			case "threshold": options.Threshold = SignedSize( ReadValue( name, value, arguments, ref index ) ); return;
			case "exclude": options.ExcludePatterns.Add( ReadValue( name, value, arguments, ref index ) ); return;
			case "exclude-from": ReadExcludeFile( ReadValue( name, value, arguments, ref index ), options ); return;
			case "files0-from": options.Files0From = ReadValue( name, value, arguments, ref index ); return;
			case "time": options.ShowTime = true; if ( value is not null ) options.TimeField = ValidateTimeField( value ); return;
			case "time-style": options.ShowTime = true; options.TimeStyle = ValidateTimeStyle( ReadValue( name, value, arguments, ref index ) ); return;
			case "help": options.ShowHelp = true; return;
			case "version": options.ShowVersion = true; return;
			default: throw new DuUsageException( $"unrecognized option '--{name}'" );
		}
	}

	private static void ParseShort( string argument, IReadOnlyList<string> arguments, ref int index, DuOptions options ) {
		for ( var offset = 1; offset < argument.Length; offset++ ) {
			switch ( argument[ offset ] ) {
				case '0': options.NullTerminate = true; break;
				case 'a': options.All = true; break;
				case 'b': options.ApparentSize = true; options.SizePolicy = Blocks( 1 ); break;
				case 'c': options.Total = true; break;
				case 'D': options.SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly; break;
				case 'h': options.SizePolicy = new UsageSizePolicy( UsageSizeStyle.HumanReadable, 1 ); break;
				case 'H': options.SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly; break;
				case 'k': options.SizePolicy = Blocks( 1024 ); break;
				case 'L': options.SymbolicLinkMode = SymbolicLinkTraversalMode.Always; break;
				case 'l': options.CountLinks = true; break;
				case 'm': options.SizePolicy = Blocks( 1024UL * 1024 ); break;
				case 'P': options.SymbolicLinkMode = SymbolicLinkTraversalMode.Never; break;
				case 'S': options.SeparateDirectories = true; break;
				case 's': options.Summarize = true; options.MaximumDepth = 0; break;
				case 'x': options.OneFileSystem = true; break;
				case 'B': options.SizePolicy = Blocks( ReadShortValue( argument, ref offset, arguments, ref index, 'B' ) ); break;
				case 'd': options.MaximumDepth = Nonnegative( "max-depth", ReadShortValue( argument, ref offset, arguments, ref index, 'd' ) ); options.MaximumDepthSpecified = true; break;
				case 't': options.Threshold = SignedSize( ReadShortValue( argument, ref offset, arguments, ref index, 't' ) ); break;
				case 'X': ReadExcludeFile( ReadShortValue( argument, ref offset, arguments, ref index, 'X' ), options ); break;
				default: throw new DuUsageException( $"invalid option -- '{argument[ offset ]}'" );
			}
		}
	}

	private static UsageSizePolicy Blocks( string value ) => Blocks( UsageSizePolicy.ParseBlockSize( value ) );
	private static UsageSizePolicy Blocks( ulong value ) => new( UsageSizeStyle.Blocks, value );
	private static int Nonnegative( string name, string text ) => int.TryParse( text, out var value ) && value >= 0 ? value : throw new DuUsageException( $"invalid {name}: '{text}'" );
	private static long SignedSize( string text ) {
		var negative = text.StartsWith( '-' );
		var positive = text.StartsWith( '+' );
		var magnitudeText = negative || positive ? text[ 1.. ] : text;
		var magnitude = magnitudeText == "0" ? 0 : UsageSizePolicy.ParseBlockSize( magnitudeText );
		if ( magnitude > long.MaxValue ) throw new DuUsageException( $"invalid threshold: '{text}'" );
		return negative ? -(long)magnitude : (long)magnitude;
	}
	private static string ReadValue( string name, string? value, IReadOnlyList<string> arguments, ref int index ) {
		if ( value is not null ) return value;
		if ( ++index >= arguments.Count ) throw new DuUsageException( $"option '--{name}' requires an argument" );
		return arguments[ index ];
	}
	private static string ReadShortValue( string argument, ref int offset, IReadOnlyList<string> arguments, ref int index, char option ) {
		if ( offset + 1 < argument.Length ) {
			var value = argument[ (offset + 1).. ];
			offset = argument.Length;
			return value;
		}
		if ( ++index >= arguments.Count ) throw new DuUsageException( $"option requires an argument -- '{option}'" );
		return arguments[ index ];
	}
	private static void ReadExcludeFile( string path, DuOptions options ) {
		try {
			foreach ( var line in File.ReadLines( path ) ) if ( line.Length > 0 ) options.ExcludePatterns.Add( line );
		} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or ArgumentException ) {
			throw new DuUsageException( $"cannot read exclude file '{path}': {exception.Message}" );
		}
	}
	private static string ValidateTimeField( string value ) => value switch {
		"atime" or "access" or "use" or "ctime" or "status" => value,
		_ => throw new DuUsageException( $"invalid time field: '{value}'" )
	};
	private static string ValidateTimeStyle( string value ) => value is "full-iso" or "long-iso" or "iso" || value.StartsWith( '+' )
		? value
		: throw new DuUsageException( $"invalid time style: '{value}'" );
}
