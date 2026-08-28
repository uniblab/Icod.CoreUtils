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

namespace Icod.CoreUtils.Shared.Checksums;

/// <summary>
/// Represents a completed checksum computation.
/// </summary>
/// <param name="Algorithm">The algorithm value.</param>
/// <param name="Digest">The digest value.</param>
/// <param name="NumericValue">The numeric value value.</param>
/// <param name="Length">The length value.</param>
/// <param name="BlockCount">The block count value.</param>
public sealed record ChecksumComputation(
	ChecksumAlgorithmKind Algorithm,
	byte[]? Digest,
	ulong? NumericValue,
	long Length,
	long BlockCount
);
