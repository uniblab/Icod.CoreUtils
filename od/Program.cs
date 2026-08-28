/*
	od
	Dump files in octal and other formats.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace Icod.CoreUtils.Od;

using Icod.CommandFramework.Diagnostics;

/// <summary>
/// Provides the <c>od</c> process entry point.
/// </summary>
public static class Program {
	/// <summary>
	/// Runs <c>od</c> against the process console streams.
	/// </summary>
	public static Task<int> Main(
		string[] args
	) {
		return Command.RunAsync(
			args,
			CommandContext.CreateConsole( "od" )
		);
	}
}
