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

using System.Globalization;
using Icod.CommandFramework.Terminal;

namespace Icod.CoreUtils.Shared.FileSystem.Usage;

/// <summary>Identifies how byte and inode counts are presented.</summary>
public enum UsageSizeStyle {
	/// <summary>Print integral counts in a selected output unit.</summary>
	Blocks = 0,
	/// <summary>Print powers-of-1024 human-readable values.</summary>
	HumanReadable = 1,
	/// <summary>Print powers-of-1000 human-readable values.</summary>
	Si = 2
}

/// <summary>Represents the resolved GNU block-size and human-format policy.</summary>
public readonly record struct UsageSizePolicy( UsageSizeStyle Style, ulong BlockSize ) {
	/// <summary>Gets the conventional 1 KiB block policy.</summary>
	public static UsageSizePolicy Default { get; } = new( UsageSizeStyle.Blocks, 1024 );

	/// <summary>Formats one nonnegative byte or inode count.</summary>
	/// <param name="value">The count to format.</param>
	/// <returns>The formatted value.</returns>
	public string Format( ulong value ) => Style switch {
		UsageSizeStyle.HumanReadable => FormatHuman( value, 1024 ),
		UsageSizeStyle.Si => FormatHuman( value, 1000 ),
		_ => DivideRoundUp( value, BlockSize ).ToString( CultureInfo.InvariantCulture )
	};

	/// <summary>Parses one GNU-style size specification.</summary>
	/// <param name="text">The size text.</param>
	/// <returns>The byte multiplier.</returns>
	/// <exception cref="FormatException">The size is malformed or outside <see cref="ulong"/>.</exception>
	public static ulong ParseBlockSize( string text ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( text );
		var value = text.Trim();
		var digitCount = 0;
		while ( digitCount < value.Length && char.IsAsciiDigit( value[ digitCount ] ) ) {
			digitCount++;
		}
		var numberText = 0 == digitCount ? "1" : value[ ..digitCount ];
		var suffix = value[ digitCount.. ];
		if ( !ulong.TryParse( numberText, NumberStyles.None, CultureInfo.InvariantCulture, out var number ) || 0 == number ) {
			throw new FormatException( $"invalid block size '{text}'" );
		}
		var multiplier = ParseSuffix( suffix, text );
		try {
			return checked( number * multiplier );
		} catch ( OverflowException exception ) {
			throw new FormatException( $"block size '{text}' is too large", exception );
		}
	}

	/// <summary>Resolves command-line and environment block-size precedence.</summary>
	/// <param name="explicitPolicy">An explicit command-line policy, when present.</param>
	/// <param name="commandEnvironmentName">The command-specific environment variable.</param>
	/// <param name="environment">Environment provider.</param>
	/// <returns>The resolved policy.</returns>
	public static UsageSizePolicy Resolve(
		UsageSizePolicy? explicitPolicy,
		string commandEnvironmentName,
		IEnvironmentVariableProvider environment
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( commandEnvironmentName );
		ArgumentNullException.ThrowIfNull( environment );
		if ( explicitPolicy is UsageSizePolicy policy ) {
			return policy;
		}
		foreach ( var name in new[] { commandEnvironmentName, "BLOCK_SIZE", "BLOCKSIZE" } ) {
			var value = environment.GetValue( name );
			if ( string.IsNullOrWhiteSpace( value ) ) {
				continue;
			}
			if ( IsHumanKeyword( value, out var style ) ) {
				return new UsageSizePolicy( style, 1 );
			}
			return new UsageSizePolicy( UsageSizeStyle.Blocks, ParseBlockSize( value ) );
		}
		return string.IsNullOrEmpty( environment.GetValue( "POSIXLY_CORRECT" ) )
			? Default
			: new UsageSizePolicy( UsageSizeStyle.Blocks, 512 );
	}

	private static bool IsHumanKeyword( string value, out UsageSizeStyle style ) {
		if ( value.Equals( "human-readable", StringComparison.OrdinalIgnoreCase ) ) {
			style = UsageSizeStyle.HumanReadable;
			return true;
		}
		if ( value.Equals( "si", StringComparison.OrdinalIgnoreCase ) ) {
			style = UsageSizeStyle.Si;
			return true;
		}
		style = default;
		return false;
	}

	private static ulong ParseSuffix( string suffix, string original ) {
		if ( suffix.Length == 0 ) {
			return 1;
		}
		var power = char.ToUpperInvariant( suffix[ 0 ] ) switch {
			'K' => 1,
			'M' => 2,
			'G' => 3,
			'T' => 4,
			'P' => 5,
			'E' => 6,
			'Z' => 7,
			'Y' => 8,
			'R' => 9,
			'Q' => 10,
			'B' when suffix.Length == 1 => 0,
			_ => -1
		};
		if ( power < 0 ) {
			throw new FormatException( $"invalid block-size suffix in '{original}'" );
		}
		var binary = suffix.Length == 1 || suffix.Equals( string.Concat( suffix[ 0 ], "iB" ), StringComparison.OrdinalIgnoreCase );
		var decimalSuffix = suffix.Equals( string.Concat( suffix[ 0 ], "B" ), StringComparison.OrdinalIgnoreCase );
		if ( !binary && !decimalSuffix ) {
			throw new FormatException( $"invalid block-size suffix in '{original}'" );
		}
		var radix = decimalSuffix ? 1000UL : 1024UL;
		var result = 1UL;
		try {
			for ( var index = 0; index < power; index++ ) {
				result = checked( result * radix );
			}
			return result;
		} catch ( OverflowException exception ) {
			throw new FormatException( $"block-size suffix in '{original}' is too large", exception );
		}
	}

	private static string FormatHuman( ulong value, ulong radix ) {
		if ( value < radix ) {
			return value.ToString( CultureInfo.InvariantCulture );
		}
		var suffixes = new[] { "K", "M", "G", "T", "P", "E", "Z", "Y" };
		decimal scaled = value;
		var suffixIndex = -1;
		while ( scaled >= radix && suffixIndex < suffixes.Length - 1 ) {
			scaled /= radix;
			suffixIndex++;
		}
		var rounded = scaled < 10 ? decimal.Round( scaled, 1, MidpointRounding.AwayFromZero ) : decimal.Ceiling( scaled );
		return string.Concat(
			rounded.ToString( rounded < 10 && rounded != decimal.Truncate( rounded ) ? "0.0" : "0", CultureInfo.InvariantCulture ),
			suffixes[ suffixIndex ]
		);
	}

	private static ulong DivideRoundUp( ulong value, ulong divisor ) => 0 == value
		? 0
		: checked( ((value - 1) / divisor) + 1 );
}
