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

/// <summary>Serializes stable records into sorted-run files and reads them back sequentially.</summary>
/// <typeparam name="T">The run value type.</typeparam>
public interface IExternalRunCodec<T> {
	/// <summary>Writes one stable item to a run stream.</summary>
	/// <param name="destination">The caller-owned destination stream.</param>
	/// <param name="item">The stable item.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task representing the write.</returns>
	ValueTask WriteAsync(
		Stream destination,
		StableItem<T> item,
		CancellationToken cancellationToken = default
	);

	/// <summary>Reads the next stable item from a run stream.</summary>
	/// <param name="source">The caller-owned source stream.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The next-item result, or an end-of-stream result.</returns>
	ValueTask<ExternalRunReadResult<T>> ReadAsync(
		Stream source,
		CancellationToken cancellationToken = default
	);
}

/// <summary>Describes one sequential run-codec read.</summary>
/// <typeparam name="T">The run value type.</typeparam>
/// <param name="HasItem">Whether a complete item was read.</param>
/// <param name="Item">The item when <paramref name="HasItem"/> is <see langword="true"/>.</param>
public readonly record struct ExternalRunReadResult<T>(
	bool HasItem,
	StableItem<T>? Item
) {
	/// <summary>Creates a successful item result.</summary>
	/// <param name="item">The item.</param>
	/// <returns>The item result.</returns>
	public static ExternalRunReadResult<T> FromItem( StableItem<T> item ) {
		ArgumentNullException.ThrowIfNull( item );
		return new( true, item );
	}

	/// <summary>Creates an end-of-stream result.</summary>
	/// <returns>The end-of-stream result.</returns>
	public static ExternalRunReadResult<T> EndOfStream() {
		return new( false, null );
	}
}
