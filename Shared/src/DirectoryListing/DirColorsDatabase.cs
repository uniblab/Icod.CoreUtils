/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace Icod.CoreUtils.Shared.DirectoryListing;

using System.Text;
using Icod.CoreUtils.Shared.Presentation;

/// <summary>Identifies the shell syntax emitted by <c>dircolors</c>.</summary>
public enum DirColorsShell {
	/// <summary>Bourne-compatible shell syntax.</summary>
	Bourne,
	/// <summary>C-shell-compatible syntax.</summary>
	Csh
}

/// <summary>Represents one controlled dircolors database diagnostic.</summary>
/// <param name="Source">The source name.</param>
/// <param name="Line">The one-based line number.</param>
/// <param name="Message">The diagnostic message.</param>
public sealed record DirColorsDiagnostic( string Source, int Line, string Message );

/// <summary>Represents one terminal-specific dircolors compilation.</summary>
/// <param name="Colors">The compiled LS_COLORS database.</param>
/// <param name="Diagnostics">Diagnostics active for the selected terminal section.</param>
public sealed record DirColorsCompilation(
	LsColors Colors,
	IReadOnlyList<DirColorsDiagnostic> Diagnostics
);

/// <summary>Represents a parsed GNU dircolors database.</summary>
public sealed class DirColorsDatabase {
	private static readonly IReadOnlyDictionary<string, string> KeywordIndicators =
		new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase ) {
			[ "NORMAL" ] = "no",
			[ "NORM" ] = "no",
			[ "RESET" ] = "rs",
			[ "FILE" ] = "fi",
			[ "DIR" ] = "di",
			[ "LNK" ] = "ln",
			[ "LINK" ] = "ln",
			[ "SYMLINK" ] = "ln",
			[ "ORPHAN" ] = "or",
			[ "MISSING" ] = "mi",
			[ "FIFO" ] = "pi",
			[ "PIPE" ] = "pi",
			[ "SOCK" ] = "so",
			[ "SOCKET" ] = "so",
			[ "DOOR" ] = "do",
			[ "BLK" ] = "bd",
			[ "BLOCK" ] = "bd",
			[ "CHR" ] = "cd",
			[ "CHAR" ] = "cd",
			[ "EXEC" ] = "ex",
			[ "SUID" ] = "su",
			[ "SETUID" ] = "su",
			[ "SGID" ] = "sg",
			[ "SETGID" ] = "sg",
			[ "CAPABILITY" ] = "ca",
			[ "MULTIHARDLINK" ] = "mh",
			[ "STICKY_OTHER_WRITABLE" ] = "tw",
			[ "OWT" ] = "tw",
			[ "OTHER_WRITABLE" ] = "ow",
			[ "OWR" ] = "ow",
			[ "STICKY" ] = "st",
			[ "LEFT" ] = "lc",
			[ "LEFTCODE" ] = "lc",
			[ "RIGHT" ] = "rc",
			[ "RIGHTCODE" ] = "rc",
			[ "END" ] = "ec",
			[ "ENDCODE" ] = "ec",
			[ "CLRTOEOL" ] = "cl"
		};

	private readonly List<DirColorsDirective> directives = new();
	private readonly List<string> terminalPatterns = new();
	private readonly List<string> colorTerminalPatterns = new();
	private readonly List<DirColorsDiagnostic> diagnostics = new();

	private DirColorsDatabase( string sourceName ) {
		this.SourceName = sourceName;
	}

	/// <summary>Gets the source name used in diagnostics.</summary>
	public string SourceName { get; }
	/// <summary>Gets TERM selectors in source order.</summary>
	public IReadOnlyList<string> TerminalPatterns => this.terminalPatterns;
	/// <summary>Gets COLORTERM selectors in source order.</summary>
	public IReadOnlyList<string> ColorTerminalPatterns => this.colorTerminalPatterns;
	/// <summary>Gets parser diagnostics.</summary>
	public IReadOnlyList<DirColorsDiagnostic> Diagnostics => this.diagnostics;

	/// <summary>Gets the built-in GNU-compatible database text.</summary>
	public static string BuiltInDatabase { get; } = string.Join( '\n', new[] {
		"# Configuration file for dircolors, a utility to help you set the LS_COLORS",
		"# environment variable used by GNU ls with the --color option.",
		"TERM *color*",
		"TERM xterm*",
		"TERM screen*",
		"TERM tmux*",
		"TERM linux",
		"TERM cygwin",
		"TERM *-256color",
		"COLORTERM ?*",
		"NORMAL 00",
		"RESET 0",
		"FILE 00",
		"DIR 01;34",
		"LINK 01;36",
		"MULTIHARDLINK 00",
		"FIFO 40;33",
		"SOCK 01;35",
		"DOOR 01;35",
		"BLK 40;33;01",
		"CHR 40;33;01",
		"ORPHAN 40;31;01",
		"MISSING 00",
		"SETUID 37;41",
		"SETGID 30;43",
		"CAPABILITY 30;41",
		"STICKY_OTHER_WRITABLE 30;42",
		"OTHER_WRITABLE 34;42",
		"STICKY 37;44",
		"EXEC 01;32",
		".tar 01;31",
		".tgz 01;31",
		".arc 01;31",
		".arj 01;31",
		".taz 01;31",
		".lha 01;31",
		".lz4 01;31",
		".lzh 01;31",
		".lzma 01;31",
		".tlz 01;31",
		".txz 01;31",
		".tzo 01;31",
		".t7z 01;31",
		".zip 01;31",
		".z 01;31",
		".dz 01;31",
		".gz 01;31",
		".lrz 01;31",
		".lz 01;31",
		".lzo 01;31",
		".xz 01;31",
		".zst 01;31",
		".tzst 01;31",
		".bz2 01;31",
		".bz 01;31",
		".tbz 01;31",
		".tbz2 01;31",
		".tz 01;31",
		".deb 01;31",
		".rpm 01;31",
		".jar 01;31",
		".war 01;31",
		".ear 01;31",
		".sar 01;31",
		".rar 01;31",
		".alz 01;31",
		".ace 01;31",
		".zoo 01;31",
		".cpio 01;31",
		".7z 01;31",
		".rz 01;31",
		".cab 01;31",
		".wim 01;31",
		".swm 01;31",
		".dwm 01;31",
		".esd 01;31",
		".jpg 01;35",
		".jpeg 01;35",
		".mjpg 01;35",
		".mjpeg 01;35",
		".gif 01;35",
		".bmp 01;35",
		".pbm 01;35",
		".pgm 01;35",
		".ppm 01;35",
		".tga 01;35",
		".xbm 01;35",
		".xpm 01;35",
		".tif 01;35",
		".tiff 01;35",
		".png 01;35",
		".svg 01;35",
		".svgz 01;35",
		".mng 01;35",
		".pcx 01;35",
		".mov 01;35",
		".mpg 01;35",
		".mpeg 01;35",
		".m2v 01;35",
		".mkv 01;35",
		".webm 01;35",
		".webp 01;35",
		".ogm 01;35",
		".mp4 01;35",
		".m4v 01;35",
		".mp4v 01;35",
		".vob 01;35",
		".qt 01;35",
		".nuv 01;35",
		".wmv 01;35",
		".asf 01;35",
		".rm 01;35",
		".rmvb 01;35",
		".flc 01;35",
		".avi 01;35",
		".fli 01;35",
		".flv 01;35",
		".gl 01;35",
		".dl 01;35",
		".xcf 01;35",
		".xwd 01;35",
		".yuv 01;35",
		".cgm 01;35",
		".emf 01;35",
		".ogv 01;35",
		".ogx 01;35",
		".aac 00;36",
		".au 00;36",
		".flac 00;36",
		".m4a 00;36",
		".mid 00;36",
		".midi 00;36",
		".mka 00;36",
		".mp3 00;36",
		".mpc 00;36",
		".ogg 00;36",
		".ra 00;36",
		".wav 00;36",
		".oga 00;36",
		".opus 00;36",
		".spx 00;36",
		".xspf 00;36",
		string.Empty
	} );

	/// <summary>Parses a database from text.</summary>
	/// <param name="reader">The database reader.</param>
	/// <param name="sourceName">The diagnostic source name.</param>
	/// <returns>The parsed database.</returns>
	public static DirColorsDatabase Parse( TextReader reader, string sourceName = "<stdin>" ) {
		ArgumentNullException.ThrowIfNull( reader );
		ArgumentException.ThrowIfNullOrWhiteSpace( sourceName );
		var database = new DirColorsDatabase( sourceName );
		var lineNumber = 0;
		string? line;
		while ( null != ( line = reader.ReadLine() ) ) {
			lineNumber++;
			database.ParseLine( line, lineNumber );
		}
		return database;
	}

	/// <summary>Parses a database asynchronously from text.</summary>
	/// <param name="reader">The database reader.</param>
	/// <param name="sourceName">The diagnostic source name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The parsed database.</returns>
	public static async Task<DirColorsDatabase> ParseAsync(
		TextReader reader,
		string sourceName = "<stdin>",
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( reader );
		ArgumentException.ThrowIfNullOrWhiteSpace( sourceName );
		var database = new DirColorsDatabase( sourceName );
		var lineNumber = 0;
		string? line;
		while ( null != ( line = await reader.ReadLineAsync( cancellationToken ).ConfigureAwait( false ) ) ) {
			lineNumber++;
			database.ParseLine( line, lineNumber );
		}
		return database;
	}

	/// <summary>Parses the built-in database.</summary>
	/// <returns>The built-in parsed database.</returns>
	public static DirColorsDatabase ParseBuiltIn() {
		return Parse( new StringReader( BuiltInDatabase ), "<built-in>" );
	}

	/// <summary>Compiles the database for terminal selectors.</summary>
	/// <param name="terminalName">The TERM value.</param>
	/// <param name="colorTerminalName">The COLORTERM value.</param>
	/// <returns>The reusable LS_COLORS database.</returns>
	public LsColors Compile( string? terminalName, string? colorTerminalName ) {
		return this.CompileWithDiagnostics( terminalName, colorTerminalName ).Colors;
	}

	/// <summary>Compiles the database and returns selector-sensitive diagnostics.</summary>
	/// <param name="terminalName">The TERM value.</param>
	/// <param name="colorTerminalName">The COLORTERM value.</param>
	/// <returns>The colors and diagnostics for the selected terminal section.</returns>
	public DirColorsCompilation CompileWithDiagnostics( string? terminalName, string? colorTerminalName ) {
		var entries = new List<KeyValuePair<string, string>>();
		var compilationDiagnostics = new List<DirColorsDiagnostic>( this.diagnostics );
		var state = DirColorsSelectionState.Global;
		var terminal = string.IsNullOrEmpty( terminalName ) ? "none" : terminalName;
		var colorTerminal = colorTerminalName ?? string.Empty;
		foreach ( var directive in this.directives ) {
			if ( directive.Kind is DirColorsDirectiveKind.Terminal or DirColorsDirectiveKind.ColorTerminal ) {
				if ( DirColorsSelectionState.TerminalSure != state ) {
					var candidate = DirColorsDirectiveKind.Terminal == directive.Kind ? terminal : colorTerminal;
					state = GlobMatcher.IsMatch( candidate, directive.Value )
						? DirColorsSelectionState.TerminalSure
						: DirColorsSelectionState.TerminalNo;
				}
				continue;
			}
			if ( DirColorsSelectionState.TerminalSure == state ) {
				state = DirColorsSelectionState.TerminalYes;
			}
			if ( DirColorsSelectionState.TerminalNo == state ) {
				continue;
			}
			switch ( directive.Kind ) {
				case DirColorsDirectiveKind.Entry:
					entries.Add( new KeyValuePair<string, string>( directive.Key, directive.Value ) );
					break;
				case DirColorsDirectiveKind.Unknown:
					compilationDiagnostics.Add( new DirColorsDiagnostic(
						this.SourceName,
						directive.Line,
						$"unrecognized keyword '{directive.Key}'"
					) );
					break;
			}
		}
		return new DirColorsCompilation( LsColors.Create( entries ), compilationDiagnostics );
	}

	private void ParseLine( string original, int lineNumber ) {
		var line = StripComment( original ).Trim();
		if ( 0 == line.Length ) {
			return;
		}
		var separator = 0;
		while ( separator < line.Length && !char.IsWhiteSpace( line[ separator ] ) ) {
			separator++;
		}
		var keyword = line[ ..separator ];
		while ( separator < line.Length && char.IsWhiteSpace( line[ separator ] ) ) {
			separator++;
		}
		if ( separator >= line.Length ) {
			this.diagnostics.Add( new DirColorsDiagnostic( this.SourceName, lineNumber, $"missing value for '{keyword}'" ) );
			return;
		}
		var rawValue = Unquote( line[ separator.. ].Trim() );
		try {
			if ( keyword.Equals( "TERM", StringComparison.OrdinalIgnoreCase ) ) {
				var pattern = LsColors.Decode( rawValue );
				this.terminalPatterns.Add( pattern );
				this.directives.Add( new DirColorsDirective( DirColorsDirectiveKind.Terminal, keyword, pattern, lineNumber ) );
				return;
			}
			if ( keyword.Equals( "COLORTERM", StringComparison.OrdinalIgnoreCase ) ) {
				var pattern = LsColors.Decode( rawValue );
				this.colorTerminalPatterns.Add( pattern );
				this.directives.Add( new DirColorsDirective( DirColorsDirectiveKind.ColorTerminal, keyword, pattern, lineNumber ) );
				return;
			}
			if ( keyword.Equals( "OPTIONS", StringComparison.OrdinalIgnoreCase )
				|| keyword.Equals( "COLOR", StringComparison.OrdinalIgnoreCase )
				|| keyword.Equals( "EIGHTBIT", StringComparison.OrdinalIgnoreCase ) ) {
				this.directives.Add( new DirColorsDirective( DirColorsDirectiveKind.Ignored, keyword, rawValue, lineNumber ) );
				return;
			}
			if ( KeywordIndicators.TryGetValue( keyword, out var indicator ) ) {
				this.directives.Add( new DirColorsDirective( DirColorsDirectiveKind.Entry, indicator, LsColors.Decode( rawValue ), lineNumber ) );
				return;
			}
			if ( keyword.StartsWith( ".", StringComparison.Ordinal ) ) {
				this.directives.Add( new DirColorsDirective( DirColorsDirectiveKind.Entry, "*" + LsColors.Decode( keyword ), LsColors.Decode( rawValue ), lineNumber ) );
				return;
			}
			if ( keyword.StartsWith( "*", StringComparison.Ordinal ) ) {
				this.directives.Add( new DirColorsDirective( DirColorsDirectiveKind.Entry, LsColors.Decode( keyword ), LsColors.Decode( rawValue ), lineNumber ) );
				return;
			}
			this.directives.Add( new DirColorsDirective( DirColorsDirectiveKind.Unknown, keyword, rawValue, lineNumber ) );
		} catch ( FormatException exception ) {
			this.diagnostics.Add( new DirColorsDiagnostic( this.SourceName, lineNumber, exception.Message ) );
		}
	}

	private enum DirColorsDirectiveKind {
		Terminal,
		ColorTerminal,
		Entry,
		Ignored,
		Unknown
	}

	private enum DirColorsSelectionState {
		Global,
		TerminalNo,
		TerminalYes,
		TerminalSure
	}

	private sealed record DirColorsDirective(
		DirColorsDirectiveKind Kind,
		string Key,
		string Value,
		int Line
	);

	private static string StripComment( string line ) {
		var escaped = false;
		char quote = '\0';
		for ( var index = 0; index < line.Length; index++ ) {
			var current = line[ index ];
			if ( escaped ) {
				escaped = false;
				continue;
			}
			if ( '\\' == current ) {
				escaped = true;
				continue;
			}
			if ( '\0' != quote ) {
				if ( quote == current ) {
					quote = '\0';
				}
				continue;
			}
			if ( current is '\'' or '"' ) {
				quote = current;
				continue;
			}
			if ( '#' == current ) {
				return line[ ..index ];
			}
		}
		return line;
	}

	private static string Unquote( string value ) {
		if ( value.Length >= 2 && ( ( '\'' == value[ 0 ] && '\'' == value[ ^1 ] ) || ( '"' == value[ 0 ] && '"' == value[ ^1 ] ) ) ) {
			return value[ 1..^1 ];
		}
		return value;
	}
}

/// <summary>Implements the GNU <c>dircolors</c> executable over the shared parser.</summary>
public static class DirColorsCommand {
	/// <summary>Runs <c>dircolors</c>.</summary>
	/// <param name="arguments">Command-line arguments.</param>
	/// <param name="standardInput">Standard input.</param>
	/// <param name="standardOutput">Standard output.</param>
	/// <param name="standardError">Standard error.</param>
	/// <param name="environment">Environment provider.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The process exit status.</returns>
	public static async Task<int> RunAsync(
		IReadOnlyList<string> arguments,
		TextReader standardInput,
		TextWriter standardOutput,
		TextWriter standardError,
		IEnvironmentVariableProvider? environment = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( arguments );
		ArgumentNullException.ThrowIfNull( standardInput );
		ArgumentNullException.ThrowIfNull( standardOutput );
		ArgumentNullException.ThrowIfNull( standardError );
		environment ??= SystemEnvironmentVariableProvider.Instance;
		var shell = (DirColorsShell?)null;
		var printDatabase = false;
		var printLsColors = false;
		var operand = (string?)null;
		var operandsOnly = false;
		foreach ( var argument in arguments ) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( operandsOnly ) {
				if ( operand is not null ) {
					return await UsageErrorAsync( standardError, $"extra operand '{argument}'" ).ConfigureAwait( false );
				}
				operand = argument;
				continue;
			}
			switch ( argument ) {
				case "--": operandsOnly = true; break;
				case "-b": case "--sh": case "--bourne-shell": shell = DirColorsShell.Bourne; break;
				case "-c": case "--csh": case "--c-shell": shell = DirColorsShell.Csh; break;
				case "-p": case "--print-database": printDatabase = true; break;
				case "--print-ls-colors": printLsColors = true; break;
				case "--help": await PrintHelpAsync( standardOutput ).ConfigureAwait( false ); return 0;
				case "--version": await standardOutput.WriteLineAsync( "dircolors (Icod.CoreUtils) 1.0" ).ConfigureAwait( false ); return 0;
				default:
					if ( argument.StartsWith( '-' ) && "-" != argument ) {
						return await UsageErrorAsync( standardError, $"unrecognized option '{argument}'" ).ConfigureAwait( false );
					}
					if ( operand is not null ) {
						return await UsageErrorAsync( standardError, $"extra operand '{argument}'" ).ConfigureAwait( false );
					}
					operand = argument;
					break;
			}
		}
		if ( printDatabase ) {
			if ( shell is not null ) {
				return await UsageErrorAsync( standardError, "options to output shell code are incompatible with '--print-database'" ).ConfigureAwait( false );
			}
			if ( printLsColors ) {
				return await UsageErrorAsync( standardError, "options '--print-database' and '--print-ls-colors' are mutually exclusive" ).ConfigureAwait( false );
			}
			if ( operand is not null ) {
				return await UsageErrorAsync( standardError, $"extra operand '{operand}'" ).ConfigureAwait( false );
			}
			await standardOutput.WriteAsync( DirColorsDatabase.BuiltInDatabase ).ConfigureAwait( false );
			return 0;
		}
		if ( printLsColors && shell is not null ) {
			return await UsageErrorAsync( standardError, "options to output shell code are incompatible with '--print-ls-colors'" ).ConfigureAwait( false );
		}

		DirColorsDatabase database;
		try {
			if ( operand is not null ) {
				operand = await Icod.CoreUtils.Shared.FileSystem.Traversal.PathnameOperandExpander.ExpandSingularAsync(
					operand,
					cancellationToken: cancellationToken
				).ConfigureAwait( false );
			}
			if ( operand is null ) {
				database = DirColorsDatabase.ParseBuiltIn();
			} else if ( "-" == operand ) {
				database = await DirColorsDatabase.ParseAsync( standardInput, "-", cancellationToken ).ConfigureAwait( false );
			} else {
				using var reader = new StreamReader( operand, Encoding.UTF8, true );
				database = await DirColorsDatabase.ParseAsync( reader, operand, cancellationToken ).ConfigureAwait( false );
			}
		} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException ) {
			await standardError.WriteLineAsync( $"dircolors: {exception.Message}" ).ConfigureAwait( false );
			return 1;
		}
		var compilation = database.CompileWithDiagnostics(
			environment.GetValue( "TERM" ),
			environment.GetValue( "COLORTERM" )
		);
		foreach ( var diagnostic in compilation.Diagnostics ) {
			await standardError.WriteLineAsync( $"dircolors: {diagnostic.Source}:{diagnostic.Line}: {diagnostic.Message}" ).ConfigureAwait( false );
		}
		if ( 0 != compilation.Diagnostics.Count ) {
			return 1;
		}
		if ( printLsColors ) {
			await PrintLsColorsAsync( compilation.Colors, standardOutput ).ConfigureAwait( false );
			return 0;
		}
		var selectedShell = shell ?? InferShell( environment.GetValue( "SHELL" ) );
		if ( selectedShell is null ) {
			await standardError.WriteLineAsync( "dircolors: no SHELL environment variable, and no shell type option given" ).ConfigureAwait( false );
			return 1;
		}
		var quoted = QuoteForSingleQuotedShell( compilation.Colors.Serialize() );
		if ( DirColorsShell.Csh == selectedShell ) {
			await standardOutput.WriteLineAsync( $"setenv LS_COLORS '{quoted}'" ).ConfigureAwait( false );
		} else {
			await standardOutput.WriteLineAsync( $"LS_COLORS='{quoted}';" ).ConfigureAwait( false );
			await standardOutput.WriteLineAsync( "export LS_COLORS" ).ConfigureAwait( false );
		}
		return 0;
	}

	private static async Task PrintLsColorsAsync( LsColors colors, TextWriter output ) {
		foreach ( var entry in colors.Indicators ) {
			await output.WriteLineAsync( colors.Apply( string.Concat( entry.Key, "\t", LsColors.Encode( entry.Value ) ), entry.Value ) ).ConfigureAwait( false );
		}
		foreach ( var pattern in colors.Patterns ) {
			await output.WriteLineAsync( colors.Apply( string.Concat( pattern.Pattern, "\t", LsColors.Encode( pattern.Sequence ) ), pattern.Sequence ) ).ConfigureAwait( false );
		}
	}

	private static DirColorsShell? InferShell( string? shellPath ) {
		if ( string.IsNullOrWhiteSpace( shellPath ) ) {
			return null;
		}
		var normalized = shellPath.Replace( '\\', '/' );
		var separator = normalized.LastIndexOf( '/' );
		var shellName = ( separator >= 0 ? normalized[ ( separator + 1 ).. ] : normalized ).ToLowerInvariant();
		return shellName is "csh" or "tcsh" ? DirColorsShell.Csh : DirColorsShell.Bourne;
	}

	private static string QuoteForSingleQuotedShell( string value ) {
		return value.Replace( "'", "'\\''", StringComparison.Ordinal );
	}

	private static async Task<int> UsageErrorAsync( TextWriter error, string message ) {
		await error.WriteLineAsync( $"dircolors: {message}" ).ConfigureAwait( false );
		await error.WriteLineAsync( "Try 'dircolors --help' for more information." ).ConfigureAwait( false );
		return 1;
	}

	private static async Task PrintHelpAsync( TextWriter output ) {
		await output.WriteLineAsync( "Usage: dircolors [OPTION]... [FILE]" ).ConfigureAwait( false );
		await output.WriteLineAsync( "Output commands to set the LS_COLORS environment variable." ).ConfigureAwait( false );
		await output.WriteLineAsync().ConfigureAwait( false );
		await output.WriteLineAsync( "  -b, --sh, --bourne-shell    output Bourne shell code" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -c, --csh, --c-shell        output C shell code" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -p, --print-database        print the built-in database" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --print-ls-colors       display fully escaped colors for visual inspection" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --help                  display this help and exit" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --version               output version information and exit" ).ConfigureAwait( false );
		await output.WriteLineAsync( "If FILE is -, read the database from standard input." ).ConfigureAwait( false );
	}
}
