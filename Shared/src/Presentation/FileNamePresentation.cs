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

namespace Icod.CoreUtils.Shared.Presentation;

using System.Text;

/// <summary>
/// Specifies the supported GNU filename-quoting styles used by directory
/// listing presentation.
/// </summary>
public enum FileNameQuotingStyle {
	/// <summary>Emit the value without quoting.</summary>
	Literal,
	/// <summary>Quote only values requiring POSIX-shell protection.</summary>
	Shell,
	/// <summary>Quote every value for a POSIX-compatible shell.</summary>
	ShellAlways,
	/// <summary>Use shell quoting plus <c>$'...'</c> control escapes.</summary>
	ShellEscape,
	/// <summary>Always use shell or <c>$'...'</c> quoting.</summary>
	ShellEscapeAlways,
	/// <summary>Use a double-quoted C string literal.</summary>
	C,
	/// <summary>Use a C string literal only when escaping is required.</summary>
	CMaybe,
	/// <summary>Use C escapes without surrounding quotation marks.</summary>
	Escape,
	/// <summary>Use C-locale double quotation.</summary>
	CLocale,
	/// <summary>Use locale-style quotation.</summary>
	Locale
}

/// <summary>
/// Specifies how literal control characters are presented before or during
/// filename quoting.
/// </summary>
public enum ControlCharacterPresentation {
	/// <summary>Preserve control characters as supplied.</summary>
	Preserve,
	/// <summary>Replace control characters with question marks.</summary>
	ReplaceWithQuestionMark,
	/// <summary>Require an escape-capable quoting style to represent control characters.</summary>
	Escape
}

/// <summary>
/// Represents resolved filename-quoting and control-character policy.
/// </summary>
public sealed class FileNamePresentationPolicy {

	/// <summary>
	/// Initializes filename-presentation policy.
	/// </summary>
	public FileNamePresentationPolicy(
		FileNameQuotingStyle quotingStyle,
		ControlCharacterPresentation controlCharacters
	) {
		if ( !Enum.IsDefined(
			quotingStyle
		) ) {
			throw new ArgumentOutOfRangeException(
				nameof( quotingStyle ),
				quotingStyle,
				"Unknown filename quoting style."
			);
		}
		if ( !Enum.IsDefined(
			controlCharacters
		) ) {
			throw new ArgumentOutOfRangeException(
				nameof( controlCharacters ),
				controlCharacters,
				"Unknown control-character policy."
			);
		}
		if (
			ControlCharacterPresentation.Escape == controlCharacters
			&& !UsesEscapeSyntax(
				quotingStyle
			)
		) {
			throw new ArgumentException(
				"Escape control-character presentation requires an escape-capable quoting style.",
				nameof( controlCharacters )
			);
		}
		this.QuotingStyle = quotingStyle;
		this.ControlCharacters = controlCharacters;
	}

	/// <summary>Gets the quoting style.</summary>
	public FileNameQuotingStyle QuotingStyle {
		get;
	}

	/// <summary>Gets the control-character policy.</summary>
	public ControlCharacterPresentation ControlCharacters {
		get;
	}

	/// <summary>
	/// Resolves default listing policy from explicit values, <c>QUOTING_STYLE</c>,
	/// and terminal attachment.
	/// </summary>
	public static FileNamePresentationPolicy ResolveDefault(
		OutputPresentationSnapshot presentation,
		FileNameQuotingStyle? requestedStyle = null,
		ControlCharacterPresentation? requestedControlCharacters = null
	) {
		ArgumentNullException.ThrowIfNull(
			presentation
		);
		var style = requestedStyle;
		if (
			style is null
			&& TryParseQuotingStyle(
				presentation.Environment.QuotingStyle,
				out var environmentStyle
			)
		) {
			style = environmentStyle;
		}
		style ??= presentation.IsTerminal
			? FileNameQuotingStyle.ShellEscape
			: FileNameQuotingStyle.Literal
		;

		var controls = requestedControlCharacters
			?? DefaultControlCharacterPolicy(
				style.Value,
				presentation.IsTerminal
			)
		;

		return new FileNamePresentationPolicy(
			style.Value,
			controls
		);
	}

	/// <summary>
	/// Parses one GNU quoting-style name.
	/// </summary>
	public static bool TryParseQuotingStyle(
		string? value,
		out FileNameQuotingStyle style
	) {
		style = FileNameQuotingStyle.Literal;
		if ( value is null ) {
			return false;
		}
		return value.Trim().ToLowerInvariant() switch {
			"literal" => Set( FileNameQuotingStyle.Literal, out style ),
			"shell" => Set( FileNameQuotingStyle.Shell, out style ),
			"shell-always" => Set( FileNameQuotingStyle.ShellAlways, out style ),
			"shell-escape" => Set( FileNameQuotingStyle.ShellEscape, out style ),
			"shell-escape-always" => Set( FileNameQuotingStyle.ShellEscapeAlways, out style ),
			"c" => Set( FileNameQuotingStyle.C, out style ),
			"c-maybe" => Set( FileNameQuotingStyle.CMaybe, out style ),
			"escape" => Set( FileNameQuotingStyle.Escape, out style ),
			"clocale" => Set( FileNameQuotingStyle.CLocale, out style ),
			"locale" => Set( FileNameQuotingStyle.Locale, out style ),
			_ => false
		};
	}

	private static bool UsesEscapeSyntax(
		FileNameQuotingStyle style
	) {
		return style is FileNameQuotingStyle.ShellEscape
			or FileNameQuotingStyle.ShellEscapeAlways
			or FileNameQuotingStyle.C
			or FileNameQuotingStyle.CMaybe
			or FileNameQuotingStyle.Escape
			or FileNameQuotingStyle.CLocale
			or FileNameQuotingStyle.Locale
		;
	}

	private static ControlCharacterPresentation DefaultControlCharacterPolicy(
		FileNameQuotingStyle style,
		bool isTerminal
	) {
		if (
			style is FileNameQuotingStyle.C
				or FileNameQuotingStyle.CMaybe
				or FileNameQuotingStyle.Escape
				or FileNameQuotingStyle.CLocale
				or FileNameQuotingStyle.Locale
				or FileNameQuotingStyle.ShellEscape
				or FileNameQuotingStyle.ShellEscapeAlways
		) {
			return ControlCharacterPresentation.Escape;
		}
		return isTerminal
			? ControlCharacterPresentation.ReplaceWithQuestionMark
			: ControlCharacterPresentation.Preserve
		;
	}

	private static bool Set(
		FileNameQuotingStyle value,
		out FileNameQuotingStyle target
	) {
		target = value;
		return true;
	}

}

/// <summary>
/// Formats filenames according to resolved quoting and control-character policy.
/// </summary>
public static class FileNamePresenter {

	/// <summary>
	/// Formats one filename.
	/// </summary>
	public static string Present(
		string value,
		FileNamePresentationPolicy policy
	) {
		ArgumentNullException.ThrowIfNull(
			value
		);
		ArgumentNullException.ThrowIfNull(
			policy
		);
		var normalized = ApplyControlPolicy(
			value,
			policy.ControlCharacters
		);

		return policy.QuotingStyle switch {
			FileNameQuotingStyle.Literal => normalized,
			FileNameQuotingStyle.Shell => QuoteForShell( normalized, false ),
			FileNameQuotingStyle.ShellAlways => QuoteForShell( normalized, true ),
			FileNameQuotingStyle.ShellEscape => QuoteForShellEscape( normalized, false ),
			FileNameQuotingStyle.ShellEscapeAlways => QuoteForShellEscape( normalized, true ),
			FileNameQuotingStyle.C => QuoteAsC( normalized, true ),
			FileNameQuotingStyle.CMaybe => QuoteAsC(
				normalized,
				RequiresCEscape(
					normalized
				)
			),
			FileNameQuotingStyle.Escape => EscapeCContent( normalized, false ),
			FileNameQuotingStyle.CLocale => QuoteAsC( normalized, true ),
			FileNameQuotingStyle.Locale => QuoteForLocale( normalized ),
			_ => throw new ArgumentOutOfRangeException(
				nameof( policy ),
				policy.QuotingStyle,
				"Unknown filename quoting style."
			)
		};
	}

	private static string ApplyControlPolicy(
		string value,
		ControlCharacterPresentation policy
	) {
		return policy switch {
			ControlCharacterPresentation.Preserve => value,
			ControlCharacterPresentation.ReplaceWithQuestionMark => ReplaceControls( value ),
			ControlCharacterPresentation.Escape => value,
			_ => throw new ArgumentOutOfRangeException(
				nameof( policy ),
				policy,
				"Unknown control-character policy."
			)
		};
	}

	private static string ReplaceControls(
		string value
	) {
		StringBuilder? builder = null;
		for ( var index = 0; index < value.Length; ++index ) {
			var character = value[ index ];
			if ( IsSurrogatePairAt(
				value,
				index
			) ) {
				if ( builder is not null ) {
					builder.Append(
						character
					);
					builder.Append(
						value[ ++index ]
					);
				} else {
					++index;
				}
				continue;
			}
			if ( !IsControlOrInvalid(
				character
			) ) {
				builder?.Append(
					character
				);
				continue;
			}
			builder ??= new StringBuilder(
				value.Length
			).Append(
				value,
				0,
				index
			);
			builder.Append(
				'?'
			);
		}
		return builder?.ToString() ?? value;
	}

	private static string QuoteForShell(
		string value,
		bool always
	) {
		if (
			!always
			&& IsShellSafe(
				value
			)
		) {
			return value;
		}
		if ( 0 == value.Length ) {
			return "''";
		}
		return string.Concat(
			"'",
			value.Replace(
				"'",
				"'\\''",
				StringComparison.Ordinal
			),
			"'"
		);
	}

	private static string QuoteForShellEscape(
		string value,
		bool always
	) {
		if ( HasControlOrInvalid(
			value
		) ) {
			return string.Concat(
				"$'",
				EscapeCContent(
					value,
					true
				),
				"'"
			);
		}
		return QuoteForShell(
			value,
			always
		);
	}

	private static string QuoteAsC(
		string value,
		bool quote
	) {
		var content = EscapeCContent(
			value,
			false
		);
		return quote
			? string.Concat(
				"\"",
				content,
				"\""
			)
			: content
		;
	}

	private static string QuoteForLocale(
		string value
	) {
		var content = EscapeCContent(
			value,
			true
		);
		return string.Concat(
			"'",
			content,
			"'"
		);
	}

	private static string EscapeCContent(
		string value,
		bool escapeSingleQuote
	) {
		var builder = new StringBuilder(
			value.Length
		);
		for ( var index = 0; index < value.Length; ++index ) {
			var character = value[ index ];
			if ( IsSurrogatePairAt(
				value,
				index
			) ) {
				builder.Append(
					character
				);
				builder.Append(
					value[ ++index ]
				);
				continue;
			}
			switch ( character ) {
				case '\a':
					builder.Append( "\\a" );
					break;
				case '\b':
					builder.Append( "\\b" );
					break;
				case '\t':
					builder.Append( "\\t" );
					break;
				case '\n':
					builder.Append( "\\n" );
					break;
				case '\v':
					builder.Append( "\\v" );
					break;
				case '\f':
					builder.Append( "\\f" );
					break;
				case '\r':
					builder.Append( "\\r" );
					break;
				case '\\':
					builder.Append( "\\\\" );
					break;
				case '"':
					builder.Append( "\\\"" );
					break;
				case '\'':
					if ( escapeSingleQuote ) {
						builder.Append( "\\'" );
					} else {
						builder.Append(
							character
						);
					}
					break;
				default:
					if ( char.IsSurrogate(
						character
					) ) {
						builder.Append(
							"\\u"
						);
						builder.Append(
							( (int)character ).ToString(
								"X4",
								System.Globalization.CultureInfo.InvariantCulture
							)
						);
					} else if ( char.IsControl(
						character
					) ) {
						builder.Append(
							'\\'
						);
						builder.Append(
							Convert.ToString(
								(int)character,
								8
							)!.PadLeft(
								3,
								'0'
							)
						);
					} else {
						builder.Append(
							character
						);
					}
					break;
			}
		}
		return builder.ToString();
	}

	private static bool RequiresCEscape(
		string value
	) {
		for ( var index = 0; index < value.Length; ++index ) {
			var character = value[ index ];
			if ( IsSurrogatePairAt(
				value,
				index
			) ) {
				++index;
				continue;
			}
			if (
				IsControlOrInvalid(
					character
				)
				|| '\\' == character
				|| '"' == character
			) {
				return true;
			}
		}
		return false;
	}

	private static bool IsShellSafe(
		string value
	) {
		if ( 0 == value.Length ) {
			return false;
		}
		foreach ( var character in value ) {
			if ( char.IsLetterOrDigit(
				character
			) ) {
				continue;
			}
			if ( 0 <= "_+-.,/:@%=".IndexOf(
				character
			) ) {
				continue;
			}
			return false;
		}
		return true;
	}

	private static bool HasControlOrInvalid(
		string value
	) {
		for ( var index = 0; index < value.Length; ++index ) {
			var character = value[ index ];
			if ( IsSurrogatePairAt(
				value,
				index
			) ) {
				++index;
				continue;
			}
			if ( IsControlOrInvalid(
				character
			) ) {
				return true;
			}
		}
		return false;
	}

	private static bool IsSurrogatePairAt(
		string value,
		int index
	) {
		return char.IsHighSurrogate(
			value[ index ]
		)
			&& ( index + 1 < value.Length )
			&& char.IsLowSurrogate(
				value[ index + 1 ]
			)
		;
	}

	private static bool IsControlOrInvalid(
		char character
	) {
		return char.IsControl(
			character
		) || char.IsSurrogate(
			character
		);
	}

}
