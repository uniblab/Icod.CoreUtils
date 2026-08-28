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

using System.Globalization;
using Icod.CommandFramework.Terminal;

/// <summary>Identifies one executable profile hosted by the shared directory-listing engine.</summary>
public enum DirectoryListingProfile {
	/// <summary>The ordinary <c>ls</c> profile.</summary>
	Ls,
	/// <summary>The column-oriented <c>dir</c> profile.</summary>
	Dir,
	/// <summary>The long-format <c>vdir</c> profile.</summary>
	VDir
}

/// <summary>Specifies the primary output layout.</summary>
public enum DirectoryListingFormat {
	/// <summary>One entry per line.</summary>
	SingleColumn,
	/// <summary>Terminal-width-aware vertical columns.</summary>
	Columns,
	/// <summary>Terminal-width-aware horizontal columns.</summary>
	HorizontalColumns,
	/// <summary>Comma-separated output.</summary>
	Commas,
	/// <summary>Long metadata output.</summary>
	Long
}

/// <summary>Specifies the entry ordering key.</summary>
public enum DirectoryListingSort {
	/// <summary>Locale-sensitive file-name order.</summary>
	Name,
	/// <summary>Do not sort.</summary>
	None,
	/// <summary>Largest logical size first.</summary>
	Size,
	/// <summary>Newest selected timestamp first.</summary>
	Time,
	/// <summary>Locale-sensitive extension order.</summary>
	Extension,
	/// <summary>Natural version order.</summary>
	Version,
	/// <summary>Widest presented name first.</summary>
	Width
}

/// <summary>Specifies which timestamp is displayed and used by time sorting.</summary>
public enum DirectoryListingTimeField {
	/// <summary>Last data modification time.</summary>
	Modification,
	/// <summary>Last access time.</summary>
	Access,
	/// <summary>Last metadata-change time.</summary>
	Change,
	/// <summary>Birth or creation time.</summary>
	Birth
}

/// <summary>Specifies the suffix appended to classified file names.</summary>
public enum DirectoryListingIndicatorStyle {
	/// <summary>No suffix.</summary>
	None,
	/// <summary>Append only a slash to directories.</summary>
	Slash,
	/// <summary>Append file-type suffixes without executable stars.</summary>
	FileType,
	/// <summary>Append the full classification suffix set.</summary>
	Classify
}

/// <summary>Specifies pathname-indirection dereference behavior.</summary>
public enum DirectoryListingDereferenceMode {
	/// <summary>Use the GNU command-line default: follow a command-line link to a directory when listing its contents.</summary>
	Default,
	/// <summary>Follow eligible command-line pathname indirection only.</summary>
	CommandLine,
	/// <summary>Follow only command-line pathname indirection that resolves to a directory.</summary>
	CommandLineDirectory,
	/// <summary>Follow all eligible pathname indirection.</summary>
	Always,
	/// <summary>Never follow pathname indirection.</summary>
	Never
}

/// <summary>Contains parsed options for one directory-listing invocation.</summary>
public sealed class DirectoryListingOptions {
	private DirectoryListingOptions( DirectoryListingProfile profile ) {
		this.Profile = profile;
	}

	/// <summary>Gets the selected executable profile.</summary>
	public DirectoryListingProfile Profile { get; }
	/// <summary>Gets or sets the output layout.</summary>
	public DirectoryListingFormat Format { get; internal set; }
	/// <summary>Gets or sets the ordering key.</summary>
	public DirectoryListingSort Sort { get; internal set; } = DirectoryListingSort.Name;
	/// <summary>Gets or sets the timestamp selector.</summary>
	public DirectoryListingTimeField TimeField { get; internal set; } = DirectoryListingTimeField.Modification;
	/// <summary>Gets or sets the indicator style.</summary>
	public DirectoryListingIndicatorStyle IndicatorStyle { get; internal set; }
	/// <summary>Gets or sets the dereference policy.</summary>
	public DirectoryListingDereferenceMode DereferenceMode { get; internal set; } = DirectoryListingDereferenceMode.Default;
	/// <summary>Gets or sets the terminal color policy.</summary>
	public TerminalColorMode ColorMode { get; internal set; } = TerminalColorMode.Never;
	/// <summary>Gets or sets an explicit filename-quoting style.</summary>
	public FileNameQuotingStyle? QuotingStyle { get; internal set; }
	/// <summary>Gets or sets an explicit control-character policy.</summary>
	public ControlCharacterPresentation? ControlCharacters { get; internal set; }
	/// <summary>Gets or sets whether dot-prefixed entries and synthetic dot entries are shown.</summary>
	public bool ShowAll { get; internal set; }
	/// <summary>Gets or sets whether hidden entries are shown except synthetic dot entries.</summary>
	public bool AlmostAll { get; internal set; }
	/// <summary>Gets or sets whether backup names ending in a tilde are omitted.</summary>
	public bool IgnoreBackups { get; internal set; }
	/// <summary>Gets patterns that are always ignored.</summary>
	public IList<string> IgnorePatterns { get; } = new List<string>();
	/// <summary>Gets patterns hidden unless an all option is active.</summary>
	public IList<string> HidePatterns { get; } = new List<string>();
	/// <summary>Gets or sets whether directories themselves are listed instead of their contents.</summary>
	public bool ListDirectoriesThemselves { get; internal set; }
	/// <summary>Gets or sets recursive traversal.</summary>
	public bool Recursive { get; internal set; }
	/// <summary>Gets or sets reverse ordering.</summary>
	public bool Reverse { get; internal set; }
	/// <summary>Gets or sets directory-first grouping.</summary>
	public bool GroupDirectoriesFirst { get; internal set; }
	/// <summary>Gets or sets human-readable powers-of-1024 sizes.</summary>
	public bool HumanReadable { get; internal set; }
	/// <summary>Gets or sets SI powers-of-1000 sizes.</summary>
	public bool SiUnits { get; internal set; }
	/// <summary>Gets or sets inode-number output.</summary>
	public bool ShowInode { get; internal set; }
	/// <summary>Gets or sets allocated-block output.</summary>
	public bool ShowBlocks { get; internal set; }
	/// <summary>Gets or sets numeric owner and group identifiers.</summary>
	public bool NumericIds { get; internal set; }
	/// <summary>Gets or sets owner-field suppression in long format.</summary>
	public bool SuppressOwner { get; internal set; }
	/// <summary>Gets or sets group-field suppression in long format.</summary>
	public bool SuppressGroup { get; internal set; }
	/// <summary>Gets or sets author-field output.</summary>
	public bool ShowAuthor { get; internal set; }
	/// <summary>Gets or sets the output width.</summary>
	public int? Width { get; internal set; }
	/// <summary>Gets or sets the tab stop width.</summary>
	public int TabSize { get; internal set; } = 8;
	/// <summary>Gets or sets the display block size.</summary>
	public ulong BlockSize { get; internal set; } = 1024;
	/// <summary>Gets or sets the time style.</summary>
	public string TimeStyle { get; internal set; } = "locale";
	/// <summary>Gets or sets whether the command line explicitly selected a time style.</summary>
	internal bool TimeStyleSpecified { get; set; }
	/// <summary>Gets or sets whether command help should be printed.</summary>
	public bool ShowHelp { get; internal set; }
	/// <summary>Gets or sets whether command version should be printed.</summary>
	public bool ShowVersion { get; internal set; }
	/// <summary>Gets non-option operands.</summary>
	public IList<string> Operands { get; } = new List<string>();

	/// <summary>Creates terminal-sensitive defaults for one executable profile.</summary>
	/// <param name="profile">The executable profile.</param>
	/// <param name="presentation">The output presentation snapshot.</param>
	/// <returns>New mutable invocation options.</returns>
	public static DirectoryListingOptions CreateDefaults(
		DirectoryListingProfile profile,
		TerminalPresentationSnapshot presentation
	) {
		ArgumentNullException.ThrowIfNull( presentation );
		var options = new DirectoryListingOptions( profile );
		options.Format = profile switch {
			DirectoryListingProfile.Ls => presentation.IsTerminal
				? DirectoryListingFormat.Columns
				: DirectoryListingFormat.SingleColumn,
			DirectoryListingProfile.Dir => DirectoryListingFormat.Columns,
			DirectoryListingProfile.VDir => DirectoryListingFormat.Long,
			_ => throw new ArgumentOutOfRangeException( nameof( profile ) )
		};
		if ( profile is DirectoryListingProfile.Dir or DirectoryListingProfile.VDir ) {
			options.QuotingStyle = FileNameQuotingStyle.Escape;
		}
		return options;
	}
}

/// <summary>Reports a stable command-line usage failure.</summary>
public sealed class DirectoryListingUsageException : Exception {
	/// <summary>Initializes a usage failure.</summary>
	/// <param name="message">The diagnostic text.</param>
	public DirectoryListingUsageException( string message ) : base( message ) {
	}
}

/// <summary>Parses the shared GNU directory-listing option vocabulary.</summary>
public static class DirectoryListingOptionParser {
	/// <summary>Parses one invocation.</summary>
	/// <param name="profile">The executable profile.</param>
	/// <param name="arguments">Command-line arguments.</param>
	/// <param name="presentation">The output presentation snapshot.</param>
	/// <returns>Parsed options.</returns>
	public static DirectoryListingOptions Parse(
		DirectoryListingProfile profile,
		IReadOnlyList<string> arguments,
		TerminalPresentationSnapshot presentation
	) {
		ArgumentNullException.ThrowIfNull( arguments );
		var options = DirectoryListingOptions.CreateDefaults( profile, presentation );
		var operandsOnly = false;
		for ( var index = 0; index < arguments.Count; index++ ) {
			var argument = arguments[ index ];
			if ( operandsOnly || ( argument.Length < 2 ) || ( '-' != argument[ 0 ] ) ) {
				options.Operands.Add( argument );
				continue;
			}
			if ( "--" == argument ) {
				operandsOnly = true;
				continue;
			}
			if ( argument.StartsWith( "--", StringComparison.Ordinal ) ) {
				ParseLongOption( argument[ 2.. ], arguments, ref index, options );
				continue;
			}
			ParseShortOptions( argument, arguments, ref index, options );
		}
		if ( 0 == options.Operands.Count ) {
			options.Operands.Add( "." );
		}
		return options;
	}

	private static void ParseLongOption(
		string argument,
		IReadOnlyList<string> arguments,
		ref int argumentIndex,
		DirectoryListingOptions options
	) {
		var separator = argument.IndexOf( '=' );
		var name = 0 <= separator ? argument[ ..separator ] : argument;
		var value = 0 <= separator ? argument[ ( separator + 1 ).. ] : null;
		switch ( name ) {
			case "help": options.ShowHelp = true; return;
			case "version": options.ShowVersion = true; return;
			case "all": options.ShowAll = true; return;
			case "almost-all": options.AlmostAll = true; return;
			case "ignore-backups": options.IgnoreBackups = true; return;
			case "directory": options.ListDirectoriesThemselves = true; return;
			case "recursive": options.Recursive = true; return;
			case "reverse": options.Reverse = true; return;
			case "human-readable": options.HumanReadable = true; options.SiUnits = false; return;
			case "si": options.HumanReadable = true; options.SiUnits = true; return;
			case "inode": options.ShowInode = true; return;
			case "size": options.ShowBlocks = true; return;
			case "numeric-uid-gid": options.NumericIds = true; options.Format = DirectoryListingFormat.Long; return;
			case "no-group": options.SuppressGroup = true; options.Format = DirectoryListingFormat.Long; return;
			case "author": options.ShowAuthor = true; options.Format = DirectoryListingFormat.Long; return;
			case "group-directories-first": options.GroupDirectoriesFirst = true; return;
			case "dereference": options.DereferenceMode = DirectoryListingDereferenceMode.Always; return;
			case "dereference-command-line": options.DereferenceMode = DirectoryListingDereferenceMode.CommandLine; return;
			case "dereference-command-line-symlink-to-dir": options.DereferenceMode = DirectoryListingDereferenceMode.CommandLineDirectory; return;
			case "no-dereference": options.DereferenceMode = DirectoryListingDereferenceMode.Never; return;
			case "full-time": options.Format = DirectoryListingFormat.Long; options.TimeStyle = "full-iso"; options.TimeStyleSpecified = true; return;
			case "classify": options.IndicatorStyle = DirectoryListingIndicatorStyle.Classify; return;
			case "file-type": options.IndicatorStyle = DirectoryListingIndicatorStyle.FileType; return;
			case "escape": options.QuotingStyle = FileNameQuotingStyle.Escape; options.ControlCharacters = ControlCharacterPresentation.Escape; return;
			case "hide-control-chars": options.ControlCharacters = ControlCharacterPresentation.ReplaceWithQuestionMark; return;
			case "show-control-chars": options.ControlCharacters = ControlCharacterPresentation.Preserve; return;
			case "quote-name": options.QuotingStyle = FileNameQuotingStyle.C; options.ControlCharacters = null; return;
			case "literal": options.QuotingStyle = FileNameQuotingStyle.Literal; options.ControlCharacters = ControlCharacterPresentation.Preserve; return;
			case "color": options.ColorMode = ParseColorMode( value ?? "always" ); return;
			case "format": options.Format = ParseFormat( ReadLongValue( name, value, arguments, ref argumentIndex ) ); return;
			case "sort": options.Sort = ParseSort( ReadLongValue( name, value, arguments, ref argumentIndex ) ); return;
			case "time": options.TimeField = ParseTimeField( ReadLongValue( name, value, arguments, ref argumentIndex ) ); return;
			case "time-style": options.TimeStyle = ValidateTimeStyle( ReadLongValue( name, value, arguments, ref argumentIndex ) ); options.TimeStyleSpecified = true; return;
			case "quoting-style":
				var quotingStyle = ReadLongValue( name, value, arguments, ref argumentIndex );
				if ( !FileNamePresentationPolicy.TryParseQuotingStyle( quotingStyle, out var style ) ) {
					throw new DirectoryListingUsageException( $"invalid argument '{quotingStyle}' for '--quoting-style'" );
				}
				options.QuotingStyle = style;
				options.ControlCharacters = null;
				return;
			case "indicator-style": options.IndicatorStyle = ParseIndicatorStyle( ReadLongValue( name, value, arguments, ref argumentIndex ) ); return;
			case "hide": options.HidePatterns.Add( ReadLongValue( name, value, arguments, ref argumentIndex ) ); return;
			case "ignore": options.IgnorePatterns.Add( ReadLongValue( name, value, arguments, ref argumentIndex ) ); return;
			case "width": options.Width = ParsePositiveInt( name, ReadLongValue( name, value, arguments, ref argumentIndex ) ); return;
			case "tabsize": options.TabSize = ParseNonnegativeInt( name, ReadLongValue( name, value, arguments, ref argumentIndex ) ); return;
			case "block-size": options.BlockSize = ParseBlockSize( ReadLongValue( name, value, arguments, ref argumentIndex ) ); return;
			default: throw new DirectoryListingUsageException( $"unrecognized option '--{name}'" );
		}
	}

	private static void ParseShortOptions(
		string argument,
		IReadOnlyList<string> arguments,
		ref int argumentIndex,
		DirectoryListingOptions options
	) {
		for ( var offset = 1; offset < argument.Length; offset++ ) {
			var option = argument[ offset ];
			switch ( option ) {
				case 'a': options.ShowAll = true; break;
				case 'A': options.AlmostAll = true; break;
				case 'B': options.IgnoreBackups = true; break;
				case 'd': options.ListDirectoriesThemselves = true; break;
				case 'R': options.Recursive = true; break;
				case 'r': options.Reverse = true; break;
				case 'l': options.Format = DirectoryListingFormat.Long; break;
				case 'g': options.Format = DirectoryListingFormat.Long; options.SuppressOwner = true; break;
				case 'G': options.SuppressGroup = true; break;
				case 'o': options.Format = DirectoryListingFormat.Long; options.SuppressGroup = true; break;
				case 'n': options.Format = DirectoryListingFormat.Long; options.NumericIds = true; break;
				case 'h': options.HumanReadable = true; options.SiUnits = false; break;
				case 'i': options.ShowInode = true; break;
				case 's': options.ShowBlocks = true; break;
				case 'S': options.Sort = DirectoryListingSort.Size; break;
				case 't': options.Sort = DirectoryListingSort.Time; break;
				case 'X': options.Sort = DirectoryListingSort.Extension; break;
				case 'v': options.Sort = DirectoryListingSort.Version; break;
				case 'U': options.Sort = DirectoryListingSort.None; break;
				case 'f': options.Sort = DirectoryListingSort.None; options.ShowAll = true; break;
				case '1': options.Format = DirectoryListingFormat.SingleColumn; break;
				case 'C': options.Format = DirectoryListingFormat.Columns; break;
				case 'x': options.Format = DirectoryListingFormat.HorizontalColumns; break;
				case 'm': options.Format = DirectoryListingFormat.Commas; break;
				case 'F': options.IndicatorStyle = DirectoryListingIndicatorStyle.Classify; break;
				case 'p': options.IndicatorStyle = DirectoryListingIndicatorStyle.Slash; break;
				case 'b': options.QuotingStyle = FileNameQuotingStyle.Escape; options.ControlCharacters = ControlCharacterPresentation.Escape; break;
				case 'q': options.ControlCharacters = ControlCharacterPresentation.ReplaceWithQuestionMark; break;
				case 'Q': options.QuotingStyle = FileNameQuotingStyle.C; options.ControlCharacters = null; break;
				case 'N': options.QuotingStyle = FileNameQuotingStyle.Literal; options.ControlCharacters = ControlCharacterPresentation.Preserve; break;
				case 'H': options.DereferenceMode = DirectoryListingDereferenceMode.CommandLine; break;
				case 'L': options.DereferenceMode = DirectoryListingDereferenceMode.Always; break;
				case 'P': options.DereferenceMode = DirectoryListingDereferenceMode.Never; break;
				case 'c': options.TimeField = DirectoryListingTimeField.Change; break;
				case 'u': options.TimeField = DirectoryListingTimeField.Access; break;
				case 'k': options.BlockSize = 1024; options.ShowBlocks = true; break;
				case 'I':
					options.IgnorePatterns.Add( ReadShortValue( argument, ref offset, arguments, ref argumentIndex, option ) );
					break;
				case 'w':
					options.Width = ParsePositiveInt( "width", ReadShortValue( argument, ref offset, arguments, ref argumentIndex, option ) );
					break;
				case 'T':
					options.TabSize = ParseNonnegativeInt( "tabsize", ReadShortValue( argument, ref offset, arguments, ref argumentIndex, option ) );
					break;
				default: throw new DirectoryListingUsageException( $"invalid option -- '{option}'" );
			}
		}
	}

	private static string ReadShortValue(
		string argument,
		ref int offset,
		IReadOnlyList<string> arguments,
		ref int argumentIndex,
		char option
	) {
		if ( offset + 1 < argument.Length ) {
			var value = argument[ ( offset + 1 ).. ];
			offset = argument.Length;
			return value;
		}
		if ( argumentIndex + 1 >= arguments.Count ) {
			throw new DirectoryListingUsageException( $"option requires an argument -- '{option}'" );
		}
		argumentIndex++;
		return arguments[ argumentIndex ];
	}

	private static string ReadLongValue(
		string option,
		string? inlineValue,
		IReadOnlyList<string> arguments,
		ref int argumentIndex
	) {
		if ( inlineValue is not null ) {
			return inlineValue;
		}
		if ( argumentIndex + 1 >= arguments.Count ) {
			throw new DirectoryListingUsageException( $"option '--{option}' requires an argument" );
		}
		argumentIndex++;
		return arguments[ argumentIndex ];
	}

	private static TerminalColorMode ParseColorMode( string value ) => value.ToLowerInvariant() switch {
		"never" or "no" or "none" => TerminalColorMode.Never,
		"auto" or "tty" or "if-tty" => TerminalColorMode.Auto,
		"always" or "yes" or "force" => TerminalColorMode.Always,
		_ => throw new DirectoryListingUsageException( $"invalid argument '{value}' for '--color'" )
	};

	private static DirectoryListingFormat ParseFormat( string value ) => value.ToLowerInvariant() switch {
		"long" or "verbose" => DirectoryListingFormat.Long,
		"single-column" => DirectoryListingFormat.SingleColumn,
		"vertical" or "columns" => DirectoryListingFormat.Columns,
		"horizontal" or "across" => DirectoryListingFormat.HorizontalColumns,
		"commas" => DirectoryListingFormat.Commas,
		_ => throw new DirectoryListingUsageException( $"invalid argument '{value}' for '--format'" )
	};

	private static DirectoryListingSort ParseSort( string value ) => value.ToLowerInvariant() switch {
		"none" => DirectoryListingSort.None,
		"size" => DirectoryListingSort.Size,
		"time" => DirectoryListingSort.Time,
		"extension" => DirectoryListingSort.Extension,
		"version" => DirectoryListingSort.Version,
		"width" => DirectoryListingSort.Width,
		"name" => DirectoryListingSort.Name,
		_ => throw new DirectoryListingUsageException( $"invalid argument '{value}' for '--sort'" )
	};

	private static DirectoryListingTimeField ParseTimeField( string value ) => value.ToLowerInvariant() switch {
		"atime" or "access" or "use" => DirectoryListingTimeField.Access,
		"ctime" or "status" => DirectoryListingTimeField.Change,
		"birth" or "creation" => DirectoryListingTimeField.Birth,
		"mtime" or "modification" => DirectoryListingTimeField.Modification,
		_ => throw new DirectoryListingUsageException( $"invalid argument '{value}' for '--time'" )
	};

	private static DirectoryListingIndicatorStyle ParseIndicatorStyle( string value ) => value.ToLowerInvariant() switch {
		"none" => DirectoryListingIndicatorStyle.None,
		"slash" => DirectoryListingIndicatorStyle.Slash,
		"file-type" => DirectoryListingIndicatorStyle.FileType,
		"classify" => DirectoryListingIndicatorStyle.Classify,
		_ => throw new DirectoryListingUsageException( $"invalid argument '{value}' for '--indicator-style'" )
	};

	private static int ParsePositiveInt( string option, string value ) {
		if ( !int.TryParse( value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed ) || ( parsed <= 0 ) ) {
			throw new DirectoryListingUsageException( $"invalid number of columns '{value}' for '--{option}'" );
		}
		return parsed;
	}

	private static int ParseNonnegativeInt( string option, string value ) {
		if ( !int.TryParse( value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed ) || ( parsed < 0 ) ) {
			throw new DirectoryListingUsageException( $"invalid argument '{value}' for '--{option}'" );
		}
		return parsed;
	}

	/// <summary>Validates one GNU time-style name or custom format.</summary>
	/// <param name="value">The style value.</param>
	/// <returns>The validated value.</returns>
	public static string ValidateTimeStyle( string value ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( value );
		var candidate = value.Trim();
		if ( candidate.StartsWith( "+", StringComparison.Ordinal ) ) {
			return candidate;
		}
		var normalized = candidate.StartsWith( "posix-", StringComparison.OrdinalIgnoreCase )
			? candidate[ 6.. ]
			: candidate;
		if ( normalized.Equals( "locale", StringComparison.OrdinalIgnoreCase )
			|| normalized.Equals( "iso", StringComparison.OrdinalIgnoreCase )
			|| normalized.Equals( "long-iso", StringComparison.OrdinalIgnoreCase )
			|| normalized.Equals( "full-iso", StringComparison.OrdinalIgnoreCase ) ) {
			return candidate;
		}
		throw new DirectoryListingUsageException( $"invalid argument '{value}' for '--time-style'" );
	}

	/// <summary>Parses a GNU-style block-size quantity.</summary>
	/// <param name="value">The quantity.</param>
	/// <returns>The byte count.</returns>
	public static ulong ParseBlockSize( string value ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( value );
		var trimmed = value.Trim();
		var split = 0;
		while ( split < trimmed.Length && char.IsDigit( trimmed[ split ] ) ) {
			split++;
		}
		var numberText = 0 == split ? "1" : trimmed[ ..split ];
		if ( !ulong.TryParse( numberText, NumberStyles.None, CultureInfo.InvariantCulture, out var number ) || ( 0 == number ) ) {
			throw new DirectoryListingUsageException( $"invalid --block-size argument '{value}'" );
		}
		var suffix = trimmed[ split.. ].Trim();
		var binary = suffix.Contains( "i", StringComparison.OrdinalIgnoreCase )
			|| ( 0 != suffix.Length && !suffix.EndsWith( "B", StringComparison.OrdinalIgnoreCase ) );
		var radix = binary ? 1024UL : 1000UL;
		var normalized = suffix.TrimEnd( 'B', 'b' ).Replace( "i", string.Empty, StringComparison.OrdinalIgnoreCase );
		var power = normalized.ToUpperInvariant() switch {
			"" => 0,
			"K" => 1,
			"M" => 2,
			"G" => 3,
			"T" => 4,
			"P" => 5,
			"E" => 6,
			_ => throw new DirectoryListingUsageException( $"invalid --block-size argument '{value}'" )
		};
		try {
			for ( var index = 0; index < power; index++ ) {
				number = checked( number * radix );
			}
			return number;
		} catch ( OverflowException ) {
			throw new DirectoryListingUsageException( $"--block-size argument '{value}' is too large" );
		}
	}
}
