namespace Icod.CoreUtils.NProc;

using System.Globalization;

/// <summary>Represents parsed <c>nproc</c> command-line options.</summary>
public sealed record NProcOptions {
	/// <summary>Gets whether all installed processors are requested.</summary>
	public bool All { get; private init; }

	/// <summary>Gets the number of processors to ignore when possible.</summary>
	public ulong Ignore { get; private init; }

	/// <summary>Gets whether help was requested.</summary>
	public bool Help { get; private init; }

	/// <summary>Gets whether version information was requested.</summary>
	public bool Version { get; private init; }

	/// <summary>Parses command-line options.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The parsed options.</returns>
	/// <exception cref="NProcUsageException">An option or operand is invalid.</exception>
	public static NProcOptions Parse( IReadOnlyList<string> args ) {
		ArgumentNullException.ThrowIfNull( args );
		var all = false;
		var ignore = 0UL;
		var help = false;
		var version = false;
		var optionParsing = true;
		for ( var index = 0; index < args.Count; index++ ) {
			var argument = args[index];
			if ( optionParsing && argument == "--" ) {
				optionParsing = false;
				continue;
			}
			if ( optionParsing && argument == "--all" ) {
				all = true;
				continue;
			}
			if ( optionParsing && argument == "--help" ) {
				help = true;
				continue;
			}
			if ( optionParsing && argument == "--version" ) {
				version = true;
				continue;
			}
			if ( optionParsing && argument.StartsWith( "--ignore=", StringComparison.Ordinal ) ) {
				ignore = ParseIgnore( argument[9..] );
				continue;
			}
			if ( optionParsing && argument == "--ignore" ) {
				if ( ++index >= args.Count ) {
					throw new NProcUsageException( "option '--ignore' requires an argument" );
				}
				ignore = ParseIgnore( args[index] );
				continue;
			}
			if ( optionParsing && argument.StartsWith( "-", StringComparison.Ordinal ) ) {
				throw new NProcUsageException( string.Concat( "unrecognized option '", argument, "'" ) );
			}
			throw new NProcUsageException( string.Concat( "extra operand '", argument, "'" ) );
		}
		return new NProcOptions {
			All = all,
			Ignore = ignore,
			Help = help,
			Version = version
		};
	}

	private static ulong ParseIgnore( string text ) {
		if (
			text.Length == 0
			|| !text.All( static character => character is >= '0' and <= '9' )
			|| !ulong.TryParse( text, NumberStyles.None, CultureInfo.InvariantCulture, out var value )
		) {
			throw new NProcUsageException( string.Concat( "invalid number: '", text, "'" ) );
		}
		return value;
	}
}

/// <summary>Reports invalid <c>nproc</c> command-line usage.</summary>
public sealed class NProcUsageException : Exception {
	/// <summary>Initializes a usage exception.</summary>
	/// <param name="message">The diagnostic message.</param>
	public NProcUsageException( string message ) : base( message ) { }
}
