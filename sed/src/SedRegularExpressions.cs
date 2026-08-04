namespace Icod.LineEditor.Sed;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Processes;

// Responsibility: temporary regular-expression translation.
public static partial class Command {

	private static Regex CreateRegex(
		string pattern,
		bool extendedRegularExpressions,
		RegexOptions options
	) {
		var translated = TranslatePosixClasses(
			extendedRegularExpressions
				? pattern
				: TranslateBasicRegularExpression(
					pattern
				)
		);
		return new Regex(
			translated,
			options
		);
	}

	private static string TranslateBasicRegularExpression(
		string pattern
	) {
		var output = new StringBuilder(
			pattern.Length
		);
		var inCharacterClass = false;

		for (
			var index = 0;
			index < pattern.Length;
			index++
		) {
			var character = pattern[ index ];
			if ( '\\' == character ) {
				if ( index + 1 >= pattern.Length ) {
					output.Append(
						'\\'
					);
					break;
				}

				var escaped = pattern[ ++index ];
				switch ( escaped ) {
					case '(':
					case ')':
					case '{':
					case '}':
					case '+':
					case '?':
					case '|':
						output.Append(
							escaped
						);
						break;
					default:
						output.Append(
							'\\'
						);
						output.Append(
							escaped
						);
						break;
				}
			} else if ( '[' == character ) {
				inCharacterClass = true;
				output.Append(
					character
				);
			} else if (
				']' == character
				&& inCharacterClass
			) {
				inCharacterClass = false;
				output.Append(
					character
				);
			} else if (
				!inCharacterClass
				&& (
					'(' == character
					|| ')' == character
					|| '{' == character
					|| '}' == character
					|| '+' == character
					|| '?' == character
					|| '|' == character
				)
			) {
				output.Append(
					'\\'
				);
				output.Append(
					character
				);
			} else {
				output.Append(
					character
				);
			}
		}

		return output.ToString();
	}

	private static string TranslatePosixClasses(
		string pattern
	) {
		return pattern
			.Replace( "[[:alnum:]]", "[A-Za-z0-9]" )
			.Replace( "[[:alpha:]]", "[A-Za-z]" )
			.Replace( "[[:blank:]]", "[ \\t]" )
			.Replace( "[[:cntrl:]]", "[\\x00-\\x1F\\x7F]" )
			.Replace( "[[:digit:]]", "[0-9]" )
			.Replace( "[[:graph:]]", "[\\x21-\\x7E]" )
			.Replace( "[[:lower:]]", "[a-z]" )
			.Replace( "[[:print:]]", "[\\x20-\\x7E]" )
			.Replace( "[[:punct:]]", "[!-/:-@\\[-`{-~]" )
			.Replace( "[[:space:]]", "\\s" )
			.Replace( "[[:upper:]]", "[A-Z]" )
			.Replace( "[[:xdigit:]]", "[A-Fa-f0-9]" )
		;
	}

}
