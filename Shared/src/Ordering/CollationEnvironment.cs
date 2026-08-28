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

namespace Icod.CoreUtils.Shared.Ordering;

using System.Globalization;

/// <summary>Resolves collation profiles using POSIX locale-variable precedence.</summary>
public static class CollationEnvironment {
	/// <summary>Resolves the current process collation profile in <c>LC_ALL</c>, <c>LC_COLLATE</c>, then <c>LANG</c> precedence.</summary>
	/// <param name="fallbackCulture">The culture used when no locale variable is set, or <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>.</param>
	/// <returns>The locale-resolution result.</returns>
	public static CollationResolutionResult ResolveCurrent(
		CultureInfo? fallbackCulture = null
	) {
		return Resolve(
			Environment.GetEnvironmentVariable( "LC_ALL" ),
			Environment.GetEnvironmentVariable( "LC_COLLATE" ),
			Environment.GetEnvironmentVariable( "LANG" ),
			fallbackCulture
		);
	}

	/// <summary>Resolves a collation profile from explicit POSIX locale-variable values.</summary>
	/// <param name="lcAll">The <c>LC_ALL</c> value.</param>
	/// <param name="lcCollate">The <c>LC_COLLATE</c> value.</param>
	/// <param name="lang">The <c>LANG</c> value.</param>
	/// <param name="fallbackCulture">The culture used when all values are empty, or <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>.</param>
	/// <returns>The locale-resolution result.</returns>
	public static CollationResolutionResult Resolve(
		string? lcAll,
		string? lcCollate,
		string? lang,
		CultureInfo? fallbackCulture = null
	) {
		var selected = FirstNonempty( lcAll, lcCollate, lang );
		if ( null == selected ) {
			return CollationResolutionResult.Succeeded(
				CollationProfile.CreateCulture( fallbackCulture ?? CultureInfo.CurrentCulture )
			);
		}
		if ( IsBytewiseLocale( selected ) ) {
			return CollationResolutionResult.Succeeded(
				CollationProfile.CreateBytewise()
			);
		}
		var normalized = NormalizeCultureName( selected );
		if ( string.IsNullOrEmpty( normalized ) ) {
			return CollationResolutionResult.Failed(
				string.Concat( "unsupported collation locale: ", selected )
			);
		}
		try {
			return CollationResolutionResult.Succeeded(
				CollationProfile.CreateCulture( CultureInfo.GetCultureInfo( normalized ) )
			);
		} catch ( CultureNotFoundException ) {
			return CollationResolutionResult.Failed(
				string.Concat( "unsupported collation locale: ", selected )
			);
		}
	}

	private static string? FirstNonempty( params string?[] values ) {
		foreach ( var value in values ) {
			if ( !string.IsNullOrWhiteSpace( value ) ) {
				return value.Trim();
			}
		}
		return null;
	}

	private static bool IsBytewiseLocale( string value ) {
		return string.Equals( value, "C", StringComparison.OrdinalIgnoreCase )
			|| string.Equals( value, "POSIX", StringComparison.OrdinalIgnoreCase )
			|| value.StartsWith( "C.", StringComparison.OrdinalIgnoreCase )
			|| value.StartsWith( "C@", StringComparison.OrdinalIgnoreCase );
	}

	private static string NormalizeCultureName( string value ) {
		var end = value.Length;
		var encoding = value.IndexOf( '.' );
		if ( 0 <= encoding ) {
			end = encoding;
		}
		var modifier = value.IndexOf( '@' );
		if ( ( 0 <= modifier ) && ( modifier < end ) ) {
			end = modifier;
		}
		return value[..end].Replace( '_', '-' );
	}
}
