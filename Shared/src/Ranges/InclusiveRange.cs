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

namespace Icod.CoreUtils.Shared.Ranges;

/// <summary>Represents an inclusive unsigned range whose upper endpoint may be open.</summary>
public readonly struct InclusiveRange : IEquatable<InclusiveRange> {

	/// <summary>Initializes an inclusive range.</summary>
	/// <param name="start">The inclusive lower endpoint.</param>
	/// <param name="end">The inclusive upper endpoint, or <see langword="null"/> for no upper bound.</param>
	public InclusiveRange(
		ulong start,
		ulong? end
	) {
		if ( end.HasValue && end.Value < start ) {
			throw new ArgumentOutOfRangeException(
				nameof( end ),
				"The upper endpoint cannot precede the lower endpoint."
			);
		}
		this.Start = start;
		this.End = end;
	}

	/// <summary>Gets the inclusive lower endpoint.</summary>
	public ulong Start { get; }

	/// <summary>Gets the inclusive upper endpoint, or <see langword="null"/> when the range is open-ended.</summary>
	public ulong? End { get; }

	/// <summary>Gets whether the range has no upper bound.</summary>
	public bool IsOpenEnded => !this.End.HasValue;

	/// <summary>Determines whether the supplied value belongs to this range.</summary>
	/// <param name="value">The value to test.</param>
	/// <returns><see langword="true"/> when the value is within the inclusive endpoints.</returns>
	public bool Contains( ulong value ) => this.Start <= value && ( !this.End.HasValue || value <= this.End.Value );

	/// <inheritdoc/>
	public bool Equals( InclusiveRange other ) => this.Start == other.Start && this.End == other.End;

	/// <inheritdoc/>
	public override bool Equals( object? obj ) => obj is InclusiveRange other && this.Equals( other );

	/// <inheritdoc/>
	public override int GetHashCode() => HashCode.Combine( this.Start, this.End );

	/// <summary>Determines whether two ranges contain equal endpoints.</summary>
	public static bool operator ==( InclusiveRange left, InclusiveRange right ) => left.Equals( right );

	/// <summary>Determines whether two ranges contain different endpoints.</summary>
	public static bool operator !=( InclusiveRange left, InclusiveRange right ) => !left.Equals( right );

}
