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

/// <summary>
/// Provides exact suffix multipliers for floating-point operands.
/// </summary>
public sealed class FloatingSuffixTable {

	private readonly ReadOnlyDictionary<string, double> myMultipliers;

	/// <summary>Gets a suffix table for seconds, minutes, hours, and days.</summary>
	public static FloatingSuffixTable TimeSeconds {
		get;
	} = new FloatingSuffixTable(
		new Dictionary<string, double>( StringComparer.Ordinal ) {
			[ string.Empty ] = 1.0,
			[ "s" ] = 1.0,
			[ "m" ] = 60.0,
			[ "h" ] = 3600.0,
			[ "d" ] = 86400.0
		}
	);

	/// <summary>
	/// Initializes a floating suffix table.
	/// </summary>
	public FloatingSuffixTable(
		IReadOnlyDictionary<string, double> multipliers
	) {
		ArgumentNullException.ThrowIfNull(
			multipliers
		);
		var output = new Dictionary<string, double>(
			StringComparer.Ordinal
		);
		foreach ( var pair in multipliers ) {
			if (
				!double.IsFinite( pair.Value )
				|| pair.Value <= 0.0
			) {
				throw new ArgumentOutOfRangeException(
					nameof( multipliers ),
					$"Suffix '{pair.Key}' must have a positive finite multiplier."
				);
			}
			output.Add(
				pair.Key,
				pair.Value
			);
		}
		this.myMultipliers = new ReadOnlyDictionary<string, double>(
			output
		);
	}

	/// <summary>Looks up an exact suffix.</summary>
	public bool TryGetMultiplier(
		string suffix,
		out double multiplier
	) {
		return this.myMultipliers.TryGetValue(
			suffix,
			out multiplier
		);
	}
}
