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

namespace Icod.CoreUtils.Shared.FileSystem.Usage;

/// <summary>Supplies injectable filesystem-capacity observations for reporting commands.</summary>
public interface IFileSystemUsageProvider {
	/// <summary>Observes all mounted filesystems or those containing selected paths.</summary>
	/// <param name="paths">Selected paths; an empty collection requests mounted filesystems.</param>
	/// <param name="includeUnavailable">Whether unavailable or unready drives should be retained when possible.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The filesystem observations.</returns>
	Task<IReadOnlyList<FileSystemUsageSnapshot>> GetFileSystemsAsync(
		IReadOnlyList<string> paths,
		bool includeUnavailable,
		CancellationToken cancellationToken = default
	);
}
