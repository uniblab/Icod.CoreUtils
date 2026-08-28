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

using System.Numerics;

/// <summary>
/// Associates an exact suffix with a positive integer multiplier.
/// </summary>
public sealed class NumericSuffix {

	/// <summary>Gets the exact suffix text.</summary>
	public string Name {
		get;
	}

	/// <summary>Gets the positive multiplier.</summary>
	public BigInteger Multiplier {
		get;
	}

	/// <summary>
	/// Creates a validated suffix.
	/// </summary>
	public NumericSuffix(
		string name,
		BigInteger multiplier
	) {
		ArgumentNullException.ThrowIfNull(
			name
		);
		if ( multiplier <= BigInteger.Zero ) {
			throw new ArgumentOutOfRangeException(
				nameof( multiplier ),
				"A suffix multiplier must be positive."
			);
		}

		this.Name = name;
		this.Multiplier = multiplier;
	}

	/// <summary>
	/// Creates a validated suffix.
	/// </summary>
	public NumericSuffix(
		string name,
		long multiplier
	) : this(
		name,
		new BigInteger( multiplier )
	) {
	}

}
