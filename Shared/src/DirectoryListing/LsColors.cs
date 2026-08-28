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
using System.Text;

/// <summary>Represents one parsed GNU <c>LS_COLORS</c> database.</summary>
public sealed class LsColors {
	private readonly Dictionary<string, string> indicators;
	private readonly List<LsColorPattern> patterns;

	private LsColors(
		Dictionary<string, string> indicators,
		List<LsColorPattern> patterns
	) {
		this.indicators = indicators;
		this.patterns = patterns;
	}

	/// <summary>Gets an empty color database.</summary>
	public static LsColors Empty { get; } = new(
		new Dictionary<string, string>( StringComparer.Ordinal ),
		new List<LsColorPattern>()
	);

	/// <summary>Gets the parsed indicator mappings.</summary>
	public IReadOnlyDictionary<string, string> Indicators => this.indicators;
	/// <summary>Gets the parsed file-name rules in source order.</summary>
	public IReadOnlyList<LsColorPattern> Patterns => this.patterns;

	/// <summary>Parses an <c>LS_COLORS</c> value.</summary>
	/// <param name="value">The environment value.</param>
	/// <returns>The parsed database.</returns>
	/// <exception cref="FormatException">The value contains a malformed entry or escape.</exception>
	public static LsColors Parse( string? value ) {
		if ( string.IsNullOrEmpty( value ) ) {
			return Empty;
		}
		var indicators = new Dictionary<string, string>( StringComparer.Ordinal );
		var patterns = new List<LsColorPattern>();
		foreach ( var rawEntry in SplitEscaped( value, ':' ) ) {
			if ( 0 == rawEntry.Length ) {
				continue;
			}
			var equals = FindUnescaped( rawEntry, '=' );
			if ( equals <= 0 ) {
				throw new FormatException( $"invalid LS_COLORS entry '{rawEntry}'" );
			}
			var key = Decode( rawEntry[ ..equals ] );
			var sequence = Decode( rawEntry[ ( equals + 1 ).. ] );
			if ( key.StartsWith( "*", StringComparison.Ordinal ) ) {
				patterns.Add( new LsColorPattern( key, sequence ) );
			} else {
				indicators[ key ] = sequence;
			}
		}
		return new LsColors( indicators, patterns );
	}

	/// <summary>Creates a database from already-decoded entries.</summary>
	/// <param name="entries">Indicator and pattern entries in source order.</param>
	/// <returns>The immutable color database.</returns>
	public static LsColors Create( IEnumerable<KeyValuePair<string, string>> entries ) {
		ArgumentNullException.ThrowIfNull( entries );
		var indicators = new Dictionary<string, string>( StringComparer.Ordinal );
		var patterns = new List<LsColorPattern>();
		foreach ( var entry in entries ) {
			ArgumentException.ThrowIfNullOrEmpty( entry.Key );
			if ( entry.Key.StartsWith( "*", StringComparison.Ordinal ) ) {
				patterns.Add( new LsColorPattern( entry.Key, entry.Value ?? string.Empty ) );
			} else {
				indicators[ entry.Key ] = entry.Value ?? string.Empty;
			}
		}
		return new LsColors( indicators, patterns );
	}

	/// <summary>Gets a decoded indicator sequence.</summary>
	/// <param name="indicator">The two-letter GNU indicator.</param>
	/// <param name="sequence">The decoded sequence.</param>
	/// <returns><see langword="true"/> when the indicator is present.</returns>
	public bool TryGetIndicator( string indicator, out string sequence ) {
		ArgumentException.ThrowIfNullOrEmpty( indicator );
		if ( this.indicators.TryGetValue( indicator, out var found ) ) {
			sequence = found;
			return true;
		}
		sequence = string.Empty;
		return false;
	}

	/// <summary>Resolves the style for a file name and fallback indicator.</summary>
	/// <param name="fileName">The unquoted file name.</param>
	/// <param name="indicator">The fallback type indicator.</param>
	/// <returns>The decoded style sequence, or an empty string.</returns>
	public string ResolveStyle( string fileName, string indicator ) {
		ArgumentNullException.ThrowIfNull( fileName );
		if ( indicator is "fi" or "ex" or "mh" ) {
			for ( var index = this.patterns.Count - 1; index >= 0; index-- ) {
				var pattern = this.patterns[ index ];
				if ( GlobMatcher.IsMatch( fileName, pattern.Pattern ) ) {
					return pattern.Sequence;
				}
			}
		}
		if ( this.indicators.TryGetValue( indicator, out var sequence ) ) {
			return sequence;
		}
		return this.indicators.TryGetValue( "fi", out sequence ) ? sequence : string.Empty;
	}

	/// <summary>Wraps presented text in the configured terminal color controls.</summary>
	/// <param name="presentedText">Already-quoted text.</param>
	/// <param name="style">The decoded SGR style.</param>
	/// <returns>The colored or unchanged value.</returns>
	public string Apply( string presentedText, string style ) {
		ArgumentNullException.ThrowIfNull( presentedText );
		if ( string.IsNullOrEmpty( style ) ) {
			return presentedText;
		}
		var left = this.indicators.TryGetValue( "lc", out var configuredLeft )
			? configuredLeft
			: "\u001b[";
		var right = this.indicators.TryGetValue( "rc", out var configuredRight )
			? configuredRight
			: "m";
		var end = this.indicators.TryGetValue( "ec", out var configuredEnd )
			? configuredEnd
			: left + ( this.indicators.TryGetValue( "rs", out var reset ) ? reset : "0" ) + right;
		return left + style + right + presentedText + end;
	}

	/// <summary>Serializes the database as an escaped <c>LS_COLORS</c> value.</summary>
	/// <returns>The environment value.</returns>
	public string Serialize() {
		var entries = new List<string>( this.indicators.Count + this.patterns.Count );
		foreach ( var entry in this.indicators ) {
			entries.Add( Encode( entry.Key ) + "=" + Encode( entry.Value ) );
		}
		foreach ( var pattern in this.patterns ) {
			entries.Add( Encode( pattern.Pattern ) + "=" + Encode( pattern.Sequence ) );
		}
		return string.Join( ':', entries );
	}

	/// <summary>Decodes GNU dircolors caret and backslash escapes.</summary>
	/// <param name="value">The escaped value.</param>
	/// <returns>The decoded value.</returns>
	public static string Decode( string value ) {
		ArgumentNullException.ThrowIfNull( value );
		var builder = new StringBuilder( value.Length );
		for ( var index = 0; index < value.Length; index++ ) {
			var current = value[ index ];
			if ( '^' == current ) {
				if ( ++index >= value.Length ) {
					throw new FormatException( "unterminated caret escape" );
				}
				var escaped = value[ index ];
				builder.Append( '?' == escaped ? '\u007f' : (char)( char.ToUpperInvariant( escaped ) & 0x1f ) );
				continue;
			}
			if ( '\\' != current ) {
				builder.Append( current );
				continue;
			}
			if ( ++index >= value.Length ) {
				throw new FormatException( "unterminated backslash escape" );
			}
			current = value[ index ];
			switch ( current ) {
				case 'a': builder.Append( '\a' ); break;
				case 'b': builder.Append( '\b' ); break;
				case 'e': case 'E': builder.Append( '\u001b' ); break;
				case 'f': builder.Append( '\f' ); break;
				case 'n': builder.Append( '\n' ); break;
				case 'r': builder.Append( '\r' ); break;
				case 't': builder.Append( '\t' ); break;
				case 'v': builder.Append( '\v' ); break;
				case '_': builder.Append( ' ' ); break;
				case 'x': case 'X':
					builder.Append( ReadHexEscape( value, ref index ) );
					break;
				default:
					if ( current is >= '0' and <= '7' ) {
						builder.Append( ReadOctalEscape( value, ref index ) );
					} else {
						builder.Append( current );
					}
					break;
			}
		}
		return builder.ToString();
	}

	private static char ReadHexEscape( string value, ref int index ) {
		var start = index + 1;
		var end = start;
		while ( end < value.Length && end < start + 2 && Uri.IsHexDigit( value[ end ] ) ) {
			end++;
		}
		if ( start == end ) {
			throw new FormatException( "hex escape requires at least one digit" );
		}
		index = end - 1;
		return (char)int.Parse( value[ start..end ], NumberStyles.HexNumber, CultureInfo.InvariantCulture );
	}

	private static char ReadOctalEscape( string value, ref int index ) {
		var start = index;
		var end = start + 1;
		while ( end < value.Length && end < start + 3 && value[ end ] is >= '0' and <= '7' ) {
			end++;
		}
		index = end - 1;
		return (char)Convert.ToInt32( value[ start..end ], 8 );
	}

	/// <summary>Encodes a decoded key or sequence for an <c>LS_COLORS</c> value.</summary>
	/// <param name="value">The decoded value.</param>
	/// <returns>The GNU-compatible escaped value.</returns>
	public static string Encode( string value ) {
		ArgumentNullException.ThrowIfNull( value );
		var builder = new StringBuilder( value.Length );
		foreach ( var character in value ) {
			switch ( character ) {
				case '\u001b': builder.Append( "\\e" ); break;
				case '\\': builder.Append( "\\\\" ); break;
				case ':': builder.Append( "\\:" ); break;
				case '=': builder.Append( "\\=" ); break;
				case '\n': builder.Append( "\\n" ); break;
				case '\r': builder.Append( "\\r" ); break;
				case '\t': builder.Append( "\\t" ); break;
				default:
					if ( char.IsControl( character ) ) {
						builder.Append( '\\' );
						builder.Append( Convert.ToString( character, 8 ).PadLeft( 3, '0' ) );
					} else {
						builder.Append( character );
					}
					break;
			}
		}
		return builder.ToString();
	}

	private static IEnumerable<string> SplitEscaped( string value, char delimiter ) {
		var builder = new StringBuilder();
		var escaped = false;
		foreach ( var character in value ) {
			if ( escaped ) {
				builder.Append( '\\' );
				builder.Append( character );
				escaped = false;
				continue;
			}
			if ( '\\' == character ) {
				escaped = true;
				continue;
			}
			if ( delimiter == character ) {
				yield return builder.ToString();
				builder.Clear();
			} else {
				builder.Append( character );
			}
		}
		if ( escaped ) {
			builder.Append( '\\' );
		}
		yield return builder.ToString();
	}

	private static int FindUnescaped( string value, char target ) {
		var escaped = false;
		for ( var index = 0; index < value.Length; index++ ) {
			if ( escaped ) {
				escaped = false;
				continue;
			}
			if ( '\\' == value[ index ] ) {
				escaped = true;
				continue;
			}
			if ( target == value[ index ] ) {
				return index;
			}
		}
		return -1;
	}
}

/// <summary>Represents one decoded file-name color rule.</summary>
/// <param name="Pattern">The GNU glob pattern, normally beginning with <c>*</c>.</param>
/// <param name="Sequence">The decoded terminal style sequence.</param>
public sealed record LsColorPattern( string Pattern, string Sequence );

/// <summary>Provides the small GNU glob subset shared by listing filters and color rules.</summary>
public static class GlobMatcher {
	/// <summary>Matches text using <c>*</c>, <c>?</c>, and bracket expressions.</summary>
	/// <param name="text">The candidate text.</param>
	/// <param name="pattern">The glob pattern.</param>
	/// <returns>Whether the complete text matches.</returns>
	public static bool IsMatch( string text, string pattern ) {
		ArgumentNullException.ThrowIfNull( text );
		ArgumentNullException.ThrowIfNull( pattern );
		return Match( text, 0, pattern, 0 );
	}

	private static bool Match( string text, int textIndex, string pattern, int patternIndex ) {
		while ( patternIndex < pattern.Length ) {
			var token = pattern[ patternIndex++ ];
			if ( '*' == token ) {
				while ( patternIndex < pattern.Length && '*' == pattern[ patternIndex ] ) {
					patternIndex++;
				}
				if ( patternIndex == pattern.Length ) {
					return true;
				}
				for ( var candidate = textIndex; candidate <= text.Length; candidate++ ) {
					if ( Match( text, candidate, pattern, patternIndex ) ) {
						return true;
					}
				}
				return false;
			}
			if ( textIndex >= text.Length ) {
				return false;
			}
			if ( '?' == token ) {
				textIndex++;
				continue;
			}
			if ( '[' == token ) {
				var end = pattern.IndexOf( ']', patternIndex );
				if ( end < 0 ) {
					if ( '[' != text[ textIndex++ ] ) {
						return false;
					}
					continue;
				}
				var negate = patternIndex < end && ( pattern[ patternIndex ] is '!' or '^' );
				if ( negate ) {
					patternIndex++;
				}
				var matched = false;
				while ( patternIndex < end ) {
					var first = pattern[ patternIndex++ ];
					if ( patternIndex + 1 < end && '-' == pattern[ patternIndex ] ) {
						patternIndex++;
						var last = pattern[ patternIndex++ ];
						matched |= text[ textIndex ] >= first && text[ textIndex ] <= last;
					} else {
						matched |= text[ textIndex ] == first;
					}
				}
				patternIndex = end + 1;
				if ( negate == matched ) {
					return false;
				}
				textIndex++;
				continue;
			}
			if ( token != text[ textIndex++ ] ) {
				return false;
			}
		}
		return textIndex == text.Length;
	}
}
