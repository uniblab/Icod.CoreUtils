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

namespace Icod.CoreUtils.Shared.Numerics;

using System.Collections.ObjectModel;
using System.Numerics;

/// <summary>
/// Provides exact, culture-independent integer suffix multipliers.
/// </summary>
public sealed class NumericSuffixTable {

	private readonly ReadOnlyDictionary<string, BigInteger> myMultipliers;

	/// <summary>Gets a suffix table containing only the empty suffix.</summary>
	public static NumericSuffixTable None {
		get;
	} = new NumericSuffixTable(
		new NumericSuffix(
			string.Empty,
			BigInteger.One
		)
	);

	/// <summary>Gets GNU-style count suffixes through Q/QiB.</summary>
	public static NumericSuffixTable GnuCounts {
		get;
	} = CreateGnuCounts();

	/// <summary>
	/// Initializes a suffix table.
	/// </summary>
	public NumericSuffixTable(
		params NumericSuffix[] suffixes
	) : this(
		(IEnumerable<NumericSuffix>)suffixes
	) {
	}

	/// <summary>
	/// Initializes a suffix table.
	/// </summary>
	public NumericSuffixTable(
		IEnumerable<NumericSuffix> suffixes
	) {
		ArgumentNullException.ThrowIfNull(
			suffixes
		);
		var output = new Dictionary<string, BigInteger>(
			StringComparer.Ordinal
		);
		foreach ( var suffix in suffixes ) {
			ArgumentNullException.ThrowIfNull(
				suffix
			);
			if ( suffix.Multiplier <= BigInteger.Zero ) {
				throw new ArgumentOutOfRangeException(
					nameof( suffixes ),
					$"Suffix '{suffix.Name}' must have a positive multiplier."
				);
			}
			if (
				!output.TryAdd(
					suffix.Name,
					suffix.Multiplier
				)
			) {
				throw new ArgumentException(
					$"Duplicate suffix '{suffix.Name}'.",
					nameof( suffixes )
				);
			}
		}
		this.myMultipliers = new ReadOnlyDictionary<string, BigInteger>(
			output
		);
	}

	/// <summary>
	/// Looks up an exact suffix.
	/// </summary>
	public bool TryGetMultiplier(
		string suffix,
		out BigInteger multiplier
	) {
		return this.myMultipliers.TryGetValue(
			suffix,
			out multiplier
		);
	}

	private static NumericSuffixTable CreateGnuCounts() {
		var suffixes = new List<NumericSuffix> {
			new( string.Empty, BigInteger.One ),
			new( "b", new BigInteger( 512 ) )
		};
		var names = new string[ 10 ] {
			"K", "M", "G", "T", "P", "E", "Z", "Y", "R", "Q"
		};
		for (
			var index = 0;
			index < names.Length;
			index++
		) {
			var exponent = index + 1;
			suffixes.Add(
				new NumericSuffix(
					0 == index
						? "kB"
						: string.Concat( names[ index ], "B" ),
					BigInteger.Pow( 1000, exponent )
				)
			);
			suffixes.Add(
				new NumericSuffix(
					names[ index ],
					BigInteger.One << ( 10 * exponent )
				)
			);
			suffixes.Add(
				new NumericSuffix(
					string.Concat( names[ index ], "iB" ),
					BigInteger.One << ( 10 * exponent )
				)
			);
		}
		return new NumericSuffixTable(
			suffixes
		);
	}

}
