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

/// <summary>Describes one sorted temporary run.</summary>
public sealed class ExternalRun {
	/// <summary>Initializes a sorted-run descriptor.</summary>
	/// <param name="path">The run pathname.</param>
	/// <param name="itemCount">The exact number of serialized items.</param>
	public ExternalRun(
		string path,
		long itemCount
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		ArgumentOutOfRangeException.ThrowIfNegative( itemCount );
		this.Path = path;
		this.ItemCount = itemCount;
	}

	/// <summary>Gets the run pathname.</summary>
	public string Path { get; }

	/// <summary>Gets the exact number of serialized items.</summary>
	public long ItemCount { get; }
}
