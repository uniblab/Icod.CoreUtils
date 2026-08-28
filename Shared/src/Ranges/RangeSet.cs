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

/// <summary>
/// Represents a sorted, normalized set of inclusive unsigned ranges.
/// </summary>
/// <remarks>
/// Overlapping ranges are merged. Adjacent ranges deliberately remain separate so callers such as GNU <c>cut --output-delimiter</c> can observe the beginning of each requested range.
/// </remarks>
public sealed class RangeSet {

	private readonly InclusiveRange[] myRanges;
	private readonly IReadOnlyList<InclusiveRange> myReadOnlyRanges;

	/// <summary>Initializes and normalizes a range set.</summary>
	/// <param name="ranges">The ranges to sort and normalize.</param>
	public RangeSet( IEnumerable<InclusiveRange> ranges ) {
		ArgumentNullException.ThrowIfNull( ranges );
		var ordered = ranges
			.OrderBy( value => value.Start )
			.ThenBy( value => value.End ?? ulong.MaxValue )
			.ToArray()
		;
		if ( 0 == ordered.Length ) {
			this.myRanges = [];
			this.myReadOnlyRanges = Array.AsReadOnly( this.myRanges );
			return;
		}
		var normalized = new List<InclusiveRange>( ordered.Length );
		var current = ordered[0];
		for ( var index = 1; index < ordered.Length; index++ ) {
			var next = ordered[index];
			if ( !current.End.HasValue || next.Start <= current.End.Value ) {
				if ( !current.End.HasValue || !next.End.HasValue ) {
					current = new InclusiveRange( current.Start, null );
				} else if ( current.End.Value < next.End.Value ) {
					current = new InclusiveRange( current.Start, next.End );
				}
				continue;
			}
			normalized.Add( current );
			current = next;
		}
		normalized.Add( current );
		this.myRanges = normalized.ToArray();
		this.myReadOnlyRanges = Array.AsReadOnly( this.myRanges );
	}

	/// <summary>Gets an empty range set.</summary>
	public static RangeSet Empty { get; } = new( Array.Empty<InclusiveRange>() );

	/// <summary>Gets the normalized ranges in ascending order.</summary>
	public IReadOnlyList<InclusiveRange> Ranges => this.myReadOnlyRanges;

	/// <summary>Gets the number of normalized ranges.</summary>
	public int Count => this.myRanges.Length;

	/// <summary>Determines whether the supplied position is selected.</summary>
	/// <param name="position">The position to test.</param>
	/// <returns><see langword="true"/> when a range contains the position.</returns>
	public bool Contains( ulong position ) {
		var low = 0;
		var high = this.myRanges.Length - 1;
		while ( low <= high ) {
			var middle = low + ( ( high - low ) / 2 );
			var range = this.myRanges[middle];
			if ( position < range.Start ) {
				high = middle - 1;
			} else if ( range.End.HasValue && range.End.Value < position ) {
				low = middle + 1;
			} else {
				return true;
			}
		}
		return false;
	}

	/// <summary>Determines whether the supplied position begins one normalized range.</summary>
	/// <param name="position">The position to test.</param>
	/// <returns><see langword="true"/> when a range starts at the position.</returns>
	public bool IsRangeStart( ulong position ) {
		var low = 0;
		var high = this.myRanges.Length - 1;
		while ( low <= high ) {
			var middle = low + ( ( high - low ) / 2 );
			var candidate = this.myRanges[middle].Start;
			if ( position < candidate ) {
				high = middle - 1;
			} else if ( candidate < position ) {
				low = middle + 1;
			} else {
				return true;
			}
		}
		return false;
	}

	/// <summary>Creates the complement within a lower-bounded domain.</summary>
	/// <param name="minimum">The first value in the domain.</param>
	/// <param name="maximum">The last value in the domain, or <see langword="null"/> for an unbounded domain.</param>
	/// <returns>A normalized range set containing every value in the domain not selected by this set.</returns>
	public RangeSet Complement(
		ulong minimum,
		ulong? maximum = null
	) {
		if ( maximum.HasValue && maximum.Value < minimum ) {
			throw new ArgumentOutOfRangeException( nameof( maximum ) );
		}
		var result = new List<InclusiveRange>();
		var next = minimum;
		var exhausted = false;
		foreach ( var range in this.myRanges ) {
			if ( maximum.HasValue && maximum.Value < next ) {
				exhausted = true;
				break;
			}
			if ( range.End.HasValue && range.End.Value < minimum ) {
				continue;
			}
			if ( maximum.HasValue && maximum.Value < range.Start ) {
				break;
			}
			var start = Math.Max( range.Start, minimum );
			if ( next < start ) {
				result.Add(
					new InclusiveRange(
						next,
						start - 1
					)
				);
			}
			if ( !range.End.HasValue ) {
				exhausted = true;
				break;
			}
			if ( ulong.MaxValue == range.End.Value ) {
				exhausted = true;
				break;
			}
			next = Math.Max( next, range.End.Value + 1 );
		}
		if ( !exhausted ) {
			if ( maximum.HasValue ) {
				if ( next <= maximum.Value ) {
					result.Add( new InclusiveRange( next, maximum ) );
				}
			} else {
				result.Add( new InclusiveRange( next, null ) );
			}
		}
		return new RangeSet( result );
	}

	/// <summary>Creates a mutable cursor optimized for monotonically increasing positions.</summary>
	/// <returns>A cursor initialized before the first normalized range.</returns>
	public RangeCursor CreateCursor() => new( this );

	/// <summary>Gets one normalized range for an in-assembly sequential cursor.</summary>
	/// <param name="index">The zero-based normalized-range index.</param>
	/// <returns>The range at the supplied index.</returns>
	internal InclusiveRange GetRange( int index ) => this.myRanges[index];

}
