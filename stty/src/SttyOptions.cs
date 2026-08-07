namespace Icod.CoreUtils.Stty;

using System.Collections.ObjectModel;

/// <summary>Represents parsed <c>stty</c> command-line options and settings.</summary>
public sealed class SttyOptions {
	private readonly ReadOnlyCollection<string> settings;

	private SttyOptions(
		bool all,
		bool save,
		string? file,
		bool help,
		bool version,
		IEnumerable<string> settings
	) {
		this.All = all;
		this.Save = save;
		this.File = file;
		this.Help = help;
		this.Version = version;
		this.settings = Array.AsReadOnly( settings.ToArray() );
	}

	/// <summary>Gets whether all settings should be printed.</summary>
	public bool All { get; }

	/// <summary>Gets whether machine-readable state should be printed.</summary>
	public bool Save { get; }

	/// <summary>Gets the selected terminal device, or <see langword="null"/> for standard input.</summary>
	public string? File { get; }

	/// <summary>Gets whether help was requested.</summary>
	public bool Help { get; }

	/// <summary>Gets whether version information was requested.</summary>
	public bool Version { get; }

	/// <summary>Gets the ordered settings and report operands.</summary>
	public IReadOnlyList<string> Settings => this.settings;

	/// <summary>Parses command-line options.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The parsed options.</returns>
	/// <exception cref="SttyUsageException">An option combination is invalid.</exception>
	public static SttyOptions Parse( IReadOnlyList<string> args ) {
		ArgumentNullException.ThrowIfNull( args );
		var all = false;
		var save = false;
		var help = false;
		var version = false;
		string? file = null;
		var settings = new List<string>();
		var parsingOptions = true;

		for ( var index = 0; index < args.Count; ++index ) {
			var argument = args[ index ];
			if ( parsingOptions && argument == "--" ) {
				parsingOptions = false;
				continue;
			}
			if ( parsingOptions && argument is "-a" or "--all" ) {
				all = true;
				continue;
			}
			if ( parsingOptions && argument is "-g" or "--save" ) {
				save = true;
				continue;
			}
			if ( parsingOptions && argument is "-F" or "--file" ) {
				if ( ++index >= args.Count ) {
					throw new SttyUsageException( string.Concat( "option '", argument, "' requires an argument" ) );
				}
				file = RequireFile( args[ index ] );
				continue;
			}
			if ( parsingOptions && argument.StartsWith( "--file=", StringComparison.Ordinal ) ) {
				file = RequireFile( argument[ 7.. ] );
				continue;
			}
			if ( parsingOptions && argument == "--help" ) {
				help = true;
				continue;
			}
			if ( parsingOptions && argument == "--version" ) {
				version = true;
				continue;
			}
			if ( parsingOptions && argument.StartsWith( "--", StringComparison.Ordinal ) ) {
				throw new SttyUsageException( string.Concat( "unrecognized option '", argument, "'" ) );
			}
			settings.Add( argument );
		}

		if ( all && save ) {
			throw new SttyUsageException( "the options --all and --save are mutually exclusive" );
		}
		if ( ( all || save ) && ( 0 != settings.Count ) ) {
			throw new SttyUsageException( "an output option cannot be combined with mode settings" );
		}
		return new SttyOptions( all, save, file, help, version, settings );
	}

	private static string RequireFile( string value ) {
		if ( string.IsNullOrWhiteSpace( value ) ) {
			throw new SttyUsageException( "the selected terminal device cannot be empty" );
		}
		return value;
	}
}

/// <summary>Reports invalid <c>stty</c> command-line usage.</summary>
public sealed class SttyUsageException : Exception {
	/// <summary>Initializes a usage exception.</summary>
	/// <param name="message">The diagnostic message.</param>
	public SttyUsageException( string message ) : base( message ) { }
}
