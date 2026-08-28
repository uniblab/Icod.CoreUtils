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

/// <summary>Defines one reusable string-valued comparison key for a record type.</summary>
/// <typeparam name="T">The compared record type.</typeparam>
public sealed class SortKeyRule<T> {
	/// <summary>Initializes a comparison key rule.</summary>
	/// <param name="selector">The function that extracts the key string.</param>
	/// <param name="comparer">The string comparer, such as an <see cref="ICollationProvider"/>.</param>
	/// <param name="descending">Whether this key reverses the comparison direction.</param>
	public SortKeyRule(
		Func<T, string> selector,
		IComparer<string> comparer,
		bool descending = false
	) {
		ArgumentNullException.ThrowIfNull( selector );
		ArgumentNullException.ThrowIfNull( comparer );
		this.Selector = selector;
		this.Comparer = comparer;
		this.Descending = descending;
	}

	/// <summary>Gets the function that extracts the key string.</summary>
	public Func<T, string> Selector { get; }

	/// <summary>Gets the comparer applied to extracted strings.</summary>
	public IComparer<string> Comparer { get; }

	/// <summary>Gets whether this key reverses the comparison direction.</summary>
	public bool Descending { get; }

	/// <summary>Compares two records by this key.</summary>
	/// <param name="x">The first record.</param>
	/// <param name="y">The second record.</param>
	/// <returns>A signed comparison result.</returns>
	public int Compare( T x, T y ) {
		var result = this.Comparer.Compare(
			this.Selector( x ),
			this.Selector( y )
		);
		return this.Descending ? Reverse( result ) : result;
	}

	private static int Reverse( int value ) {
		return 0 > value ? 1 : 0 < value ? -1 : 0;
	}
}

/// <summary>Compares records through an ordered set of reusable key rules and an optional fallback.</summary>
/// <typeparam name="T">The compared record type.</typeparam>
public sealed class CompositeSortKeyComparer<T> : IComparer<T> {
	private readonly IReadOnlyList<SortKeyRule<T>> rules;
	private readonly IComparer<T>? fallbackComparer;

	/// <summary>Initializes a composite sort-key comparer.</summary>
	/// <param name="rules">The key rules in comparison precedence.</param>
	/// <param name="fallbackComparer">The optional final whole-record comparer.</param>
	public CompositeSortKeyComparer(
		IEnumerable<SortKeyRule<T>> rules,
		IComparer<T>? fallbackComparer = null
	) {
		ArgumentNullException.ThrowIfNull( rules );
		this.rules = rules.ToArray();
		if ( this.rules.Any( value => null == value ) ) {
			throw new ArgumentException( "Sort-key rules cannot contain null.", nameof( rules ) );
		}
		this.fallbackComparer = fallbackComparer;
	}

	/// <summary>Gets the immutable key rules in comparison precedence.</summary>
	public IReadOnlyList<SortKeyRule<T>> Rules => this.rules;

	/// <inheritdoc/>
	public int Compare( T? x, T? y ) {
		if ( ReferenceEquals( x, y ) ) {
			return 0;
		}
		if ( null == x ) {
			return -1;
		}
		if ( null == y ) {
			return 1;
		}
		foreach ( var rule in this.rules ) {
			var result = rule.Compare( x, y );
			if ( 0 != result ) {
				return result;
			}
		}
		return this.fallbackComparer?.Compare( x, y ) ?? 0;
	}
}
