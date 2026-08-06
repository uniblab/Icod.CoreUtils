namespace Icod.CoreUtils.Shred;

using System.Globalization;

/// <summary>Specifies how a named target is removed after overwriting.</summary>
public enum ShredRemovalMode {
	/// <summary>Leave the target name in place.</summary>
	None = 0,
	/// <summary>Delete the target without first changing its name.</summary>
	Unlink = 1,
	/// <summary>Obfuscate and shorten the target name before deletion.</summary>
	Wipe = 2,
	/// <summary>Obfuscate and shorten the target name, synchronizing each metadata change when supported.</summary>
	WipeSync = 3
}

/// <summary>Represents a controlled command-line usage error.</summary>
public sealed class ShredUsageException : Exception {
	/// <summary>Initializes an exception with a user-facing diagnostic.</summary>
	/// <param name="message">The diagnostic message.</param>
	public ShredUsageException( string message ) : base( message ) { }
}

/// <summary>Contains the parsed options for one <c>shred</c> invocation.</summary>
public sealed class ShredOptions {
	/// <summary>Gets whether permissions may be changed to permit overwriting.</summary>
	public bool Force { get; private set; }
	/// <summary>Gets the number of overwrite iterations, excluding a requested final zero pass.</summary>
	public int Iterations { get; private set; } = 3;
	/// <summary>Gets the optional external random-source path.</summary>
	public string? RandomSourcePath { get; private set; }
	/// <summary>Gets the requested overwrite size, or <see langword="null"/> to use the target length.</summary>
	public ulong? Size { get; private set; }
	/// <summary>Gets the post-overwrite removal policy.</summary>
	public ShredRemovalMode RemovalMode { get; private set; }
	/// <summary>Gets whether progress is written to standard error.</summary>
	public bool Verbose { get; private set; }
	/// <summary>Gets whether regular-file size rounding is disabled.</summary>
	public bool Exact { get; private set; }
	/// <summary>Gets whether an additional final zero pass is requested.</summary>
	public bool Zero { get; private set; }
	/// <summary>Gets whether help output was requested.</summary>
	public bool Help { get; private set; }
	/// <summary>Gets whether version output was requested.</summary>
	public bool Version { get; private set; }
	/// <summary>Gets the target operands.</summary>
	public List<string> Targets { get; } = new();

	/// <summary>Parses GNU-style short and long <c>shred</c> options.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The parsed option model.</returns>
	/// <exception cref="ShredUsageException">An option or operand is invalid.</exception>
	public static ShredOptions Parse( IReadOnlyList<string> args ) {
		ArgumentNullException.ThrowIfNull( args );
		var options = new ShredOptions();
		var operandsOnly = false;

		for ( var index = 0; index < args.Count; index++ ) {
			var argument = args[ index ];
			if ( operandsOnly || argument == "-" || !argument.StartsWith( "-", StringComparison.Ordinal ) ) {
				options.Targets.Add( argument );
				continue;
			}

			if ( argument == "--" ) {
				operandsOnly = true;
				continue;
			}

			if ( argument.StartsWith( "--", StringComparison.Ordinal ) ) {
				ParseLongOption( options, argument, args, ref index );
				continue;
			}

			ParseShortOptions( options, argument, args, ref index );
		}

		if ( !options.Help && !options.Version && options.Targets.Count == 0 ) {
			throw new ShredUsageException( "missing file operand" );
		}

		if ( !options.Help && !options.Version
			&& options.RemovalMode != ShredRemovalMode.None
			&& options.Targets.Contains( "-", StringComparer.Ordinal ) ) {
			throw new ShredUsageException( "cannot remove standard output" );
		}

		return options;
	}

	private static void ParseLongOption( ShredOptions options, string argument, IReadOnlyList<string> args, ref int index ) {
		var equals = argument.IndexOf( '=' );
		var name = equals < 0 ? argument[ 2.. ] : argument[ 2..equals ];
		var attachedValue = equals < 0 ? null : argument[ (equals + 1).. ];

		switch ( name ) {
			case "force":
				RequireNoValue( name, attachedValue );
				options.Force = true;
				break;
			case "iterations":
				options.Iterations = ParseIterations( TakeRequiredValue( name, attachedValue, args, ref index ) );
				break;
			case "random-source":
				options.RandomSourcePath = TakeRequiredValue( name, attachedValue, args, ref index );
				if ( options.RandomSourcePath.Length == 0 ) {
					throw new ShredUsageException( "option '--random-source' requires a nonempty file name" );
				}
				break;
			case "size":
				options.Size = ParseSize( TakeRequiredValue( name, attachedValue, args, ref index ) );
				break;
			case "remove":
				options.RemovalMode = attachedValue is null
					? ShredRemovalMode.WipeSync
					: ParseRemovalMode( attachedValue );
				break;
			case "verbose":
				RequireNoValue( name, attachedValue );
				options.Verbose = true;
				break;
			case "exact":
				RequireNoValue( name, attachedValue );
				options.Exact = true;
				break;
			case "zero":
				RequireNoValue( name, attachedValue );
				options.Zero = true;
				break;
			case "help":
				RequireNoValue( name, attachedValue );
				options.Help = true;
				break;
			case "version":
				RequireNoValue( name, attachedValue );
				options.Version = true;
				break;
			default:
				throw new ShredUsageException( string.Concat( "unrecognized option '--", name, "'" ) );
		}
	}

	private static void ParseShortOptions( ShredOptions options, string argument, IReadOnlyList<string> args, ref int index ) {
		for ( var offset = 1; offset < argument.Length; offset++ ) {
			var option = argument[ offset ];
			switch ( option ) {
				case 'f':
					options.Force = true;
					break;
				case 'u':
					options.RemovalMode = ShredRemovalMode.WipeSync;
					break;
				case 'v':
					options.Verbose = true;
					break;
				case 'x':
					options.Exact = true;
					break;
				case 'z':
					options.Zero = true;
					break;
				case 'n':
				case 's': {
					var value = offset + 1 < argument.Length
						? argument[ (offset + 1).. ]
						: TakeFollowingValue( option, args, ref index );
					if ( option == 'n' ) {
						options.Iterations = ParseIterations( value );
					} else {
						options.Size = ParseSize( value );
					}
					return;
				}
				default:
					throw new ShredUsageException( string.Concat( "invalid option -- '", option, "'" ) );
			}
		}
	}

	private static string TakeFollowingValue( char option, IReadOnlyList<string> args, ref int index ) {
		if ( index + 1 >= args.Count ) {
			throw new ShredUsageException( string.Concat( "option requires an argument -- '", option, "'" ) );
		}

		index++;
		return args[ index ];
	}

	private static string TakeRequiredValue( string name, string? attachedValue, IReadOnlyList<string> args, ref int index ) {
		if ( attachedValue is not null ) {
			return attachedValue;
		}

		if ( index + 1 >= args.Count ) {
			throw new ShredUsageException( string.Concat( "option '--", name, "' requires an argument" ) );
		}

		index++;
		return args[ index ];
	}

	private static void RequireNoValue( string name, string? value ) {
		if ( value is not null ) {
			throw new ShredUsageException( string.Concat( "option '--", name, "' does not allow an argument" ) );
		}
	}

	private static int ParseIterations( string value ) {
		if ( !int.TryParse( value, NumberStyles.None, CultureInfo.InvariantCulture, out var result ) || result < 0 ) {
			throw new ShredUsageException( string.Concat( "invalid number of passes: '", value, "'" ) );
		}

		return result;
	}

	private static ShredRemovalMode ParseRemovalMode( string value ) => value switch {
		"unlink" => ShredRemovalMode.Unlink,
		"wipe" => ShredRemovalMode.Wipe,
		"wipesync" => ShredRemovalMode.WipeSync,
		_ => throw new ShredUsageException( string.Concat( "invalid argument '", value, "' for '--remove'" ) )
	};

	/// <summary>Parses a GNU block-size operand into a byte count.</summary>
	/// <param name="value">The numeric value and optional suffix.</param>
	/// <returns>The byte count.</returns>
	/// <exception cref="ShredUsageException">The value is malformed or overflows.</exception>
	public static ulong ParseSize( string value ) {
		if ( string.IsNullOrWhiteSpace( value ) ) {
			throw new ShredUsageException( "invalid file size: empty value" );
		}

		var digitCount = 0;
		while ( digitCount < value.Length && char.IsAsciiDigit( value[ digitCount ] ) ) {
			digitCount++;
		}

		if ( digitCount == 0 || !ulong.TryParse( value.AsSpan( 0, digitCount ), NumberStyles.None, CultureInfo.InvariantCulture, out var number ) ) {
			throw new ShredUsageException( string.Concat( "invalid file size: '", value, "'" ) );
		}

		var suffix = value[ digitCount.. ];
		return ApplySuffix( number, suffix, value );
	}

	private static ulong ApplySuffix( ulong number, string suffix, string original ) {
		if ( suffix.Length == 0 || suffix is "B" or "c" ) {
			return number;
		}

		ulong basis;
		int exponent;
		if ( suffix == "b" ) {
			basis = 512;
			exponent = 1;
		} else if ( suffix == "w" ) {
			basis = 2;
			exponent = 1;
		} else {
			var binary = true;
			var unit = suffix[ 0 ];
			if ( suffix.Length > 1 ) {
				var tail = suffix[ 1.. ];
				if ( tail == "B" ) {
					binary = false;
				} else if ( tail != "iB" ) {
					throw new ShredUsageException( string.Concat( "invalid file size: '", original, "'" ) );
				}
			}

			exponent = unit switch {
				'K' or 'k' => 1,
				'M' => 2,
				'G' => 3,
				'T' => 4,
				'P' => 5,
				'E' => 6,
				'Z' => 7,
				'Y' => 8,
				'R' => 9,
				'Q' => 10,
				_ => -1
			};
			if ( exponent < 0 ) {
				throw new ShredUsageException( string.Concat( "invalid file size: '", original, "'" ) );
			}
			basis = binary ? 1024UL : 1000UL;
		}

		var result = number;
		try {
			for ( var index = 0; index < exponent; index++ ) {
				result = checked( result * basis );
			}
		} catch ( OverflowException ) {
			throw new ShredUsageException( string.Concat( "file size is too large: '", original, "'" ) );
		}
		return result;
	}
}
