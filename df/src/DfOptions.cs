using Icod.CoreUtils.Shared.FileSystem.Usage;

namespace Icod.CoreUtils.Df;

/// <summary>Represents parsed <c>df</c> command-line options.</summary>
public sealed class DfOptions {
	/// <summary>Gets whether unavailable filesystems are included.</summary>
	public bool All { get; set; }
	/// <summary>Gets whether inode statistics are printed.</summary>
	public bool Inodes { get; set; }
	/// <summary>Gets whether only local filesystems are printed.</summary>
	public bool Local { get; set; }
	/// <summary>Gets whether filesystem types are printed.</summary>
	public bool PrintType { get; set; }
	/// <summary>Gets whether a total row is printed.</summary>
	public bool Total { get; set; }
	/// <summary>Gets whether POSIX portable headings are requested.</summary>
	public bool Portability { get; set; }
	/// <summary>Gets whether filesystems should be synchronized before observation.</summary>
	public bool Synchronize { get; set; }
	/// <summary>Gets whether help was requested.</summary>
	public bool ShowHelp { get; set; }
	/// <summary>Gets whether version was requested.</summary>
	public bool ShowVersion { get; set; }
	/// <summary>Gets an explicit command-line size policy.</summary>
	public UsageSizePolicy? SizePolicy { get; set; }
	/// <summary>Gets included filesystem types.</summary>
	public List<string> IncludedTypes { get; } = new();
	/// <summary>Gets excluded filesystem types.</summary>
	public List<string> ExcludedTypes { get; } = new();
	/// <summary>Gets whether the output-field form was requested.</summary>
	public bool OutputRequested { get; set; }
	/// <summary>Gets selected output fields, or an empty list for every available field.</summary>
	public List<string> OutputFields { get; } = new();
	/// <summary>Gets pathname operands.</summary>
	public List<string> Paths { get; } = new();
}

/// <summary>Represents a controlled <c>df</c> usage failure.</summary>
public sealed class DfUsageException : Exception {
	/// <summary>Initializes a usage failure.</summary>
	public DfUsageException( string message ) : base( message ) { }
}

/// <summary>Parses the GNU <c>df</c> option vocabulary used by Batch 47.</summary>
public static class DfOptionParser {
	/// <summary>Parses one invocation.</summary>
	public static DfOptions Parse( IReadOnlyList<string> arguments ) {
		ArgumentNullException.ThrowIfNull( arguments );
		var options = new DfOptions();
		var operandsOnly = false;
		for ( var index = 0; index < arguments.Count; index++ ) {
			var argument = arguments[ index ];
			if ( operandsOnly || argument.Length < 2 || argument[ 0 ] != '-' ) {
				options.Paths.Add( argument );
				continue;
			}
			if ( argument == "--" ) {
				operandsOnly = true;
				continue;
			}
			if ( argument.StartsWith( "--", StringComparison.Ordinal ) ) {
				ParseLong( argument[ 2.. ], arguments, ref index, options );
			} else {
				ParseShort( argument, arguments, ref index, options );
			}
		}
		if ( options.OutputRequested && options.Inodes ) throw new DfUsageException( "options -i and --output are mutually exclusive" );
		if ( options.OutputRequested && options.Portability ) throw new DfUsageException( "options -P and --output are mutually exclusive" );
		if ( options.OutputRequested && options.PrintType ) throw new DfUsageException( "options -T and --output are mutually exclusive" );
		return options;
	}

	private static void ParseLong( string argument, IReadOnlyList<string> arguments, ref int index, DfOptions options ) {
		var separator = argument.IndexOf( '=' );
		var name = separator >= 0 ? argument[ ..separator ] : argument;
		var value = separator >= 0 ? argument[ (separator + 1).. ] : null;
		switch ( name ) {
			case "all": options.All = true; return;
			case "block-size": options.SizePolicy = Blocks( ReadValue( name, value, arguments, ref index ) ); return;
			case "human-readable": options.SizePolicy = new UsageSizePolicy( UsageSizeStyle.HumanReadable, 1 ); return;
			case "si": options.SizePolicy = new UsageSizePolicy( UsageSizeStyle.Si, 1 ); return;
			case "inodes": options.Inodes = true; return;
			case "local": options.Local = true; return;
			case "portability": options.Portability = true; return;
			case "print-type": options.PrintType = true; return;
			case "total": options.Total = true; return;
			case "type": options.IncludedTypes.Add( ReadValue( name, value, arguments, ref index ) ); return;
			case "exclude-type": options.ExcludedTypes.Add( ReadValue( name, value, arguments, ref index ) ); return;
			case "output":
				options.OutputRequested = true;
				if ( value is not null ) {
					foreach ( var rawField in value.Split( ',' ) ) {
						options.OutputFields.Add( ValidateField( rawField.Trim() ) );
					}
				}
				return;
			case "sync": options.Synchronize = true; return;
			case "no-sync": options.Synchronize = false; return;
			case "help": options.ShowHelp = true; return;
			case "version": options.ShowVersion = true; return;
			default: throw new DfUsageException( $"unrecognized option '--{name}'" );
		}
	}

	private static void ParseShort( string argument, IReadOnlyList<string> arguments, ref int index, DfOptions options ) {
		for ( var offset = 1; offset < argument.Length; offset++ ) {
			switch ( argument[ offset ] ) {
				case 'a': options.All = true; break;
				case 'h': options.SizePolicy = new UsageSizePolicy( UsageSizeStyle.HumanReadable, 1 ); break;
				case 'H': options.SizePolicy = new UsageSizePolicy( UsageSizeStyle.Si, 1 ); break;
				case 'i': options.Inodes = true; break;
				case 'k': options.SizePolicy = Blocks( 1024 ); break;
				case 'l': options.Local = true; break;
				case 'P': options.Portability = true; break;
				case 'T': options.PrintType = true; break;
				case 'B': options.SizePolicy = Blocks( UsageSizePolicy.ParseBlockSize( ReadShortValue( argument, ref offset, arguments, ref index, 'B' ) ) ); break;
				case 't': options.IncludedTypes.Add( ReadShortValue( argument, ref offset, arguments, ref index, 't' ) ); break;
				case 'x': options.ExcludedTypes.Add( ReadShortValue( argument, ref offset, arguments, ref index, 'x' ) ); break;
				case 'v': break;
				default: throw new DfUsageException( $"invalid option -- '{argument[ offset ]}'" );
			}
		}
	}

	private static UsageSizePolicy Blocks( string value ) => Blocks( UsageSizePolicy.ParseBlockSize( value ) );
	private static UsageSizePolicy Blocks( ulong value ) => new( UsageSizeStyle.Blocks, value );
	private static string ReadValue( string name, string? value, IReadOnlyList<string> arguments, ref int index ) {
		if ( value is not null ) return value;
		if ( ++index >= arguments.Count ) throw new DfUsageException( $"option '--{name}' requires an argument" );
		return arguments[ index ];
	}
	private static string ReadShortValue( string argument, ref int offset, IReadOnlyList<string> arguments, ref int index, char option ) {
		if ( offset + 1 < argument.Length ) {
			var value = argument[ (offset + 1).. ];
			offset = argument.Length;
			return value;
		}
		if ( ++index >= arguments.Count ) throw new DfUsageException( $"option requires an argument -- '{option}'" );
		return arguments[ index ];
	}
	private static string ValidateField( string field ) => field switch {
		"source" or "fstype" or "itotal" or "iused" or "iavail" or "ipcent" or
		"size" or "used" or "avail" or "pcent" or "file" or "target" => field,
		_ => throw new DfUsageException( $"unknown output field: '{field}'" )
	};
}
