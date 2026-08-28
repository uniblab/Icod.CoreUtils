/*
	dircolors
	Output commands to set color options for directory listings.
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

namespace Icod.CoreUtils.DirColors;

/// <summary>Hosts the <c>dircolors</c> executable. Usage: <c>dircolors [OPTION]... [FILE]</c>.</summary>
public static class Program {
	/// <summary>Runs the command.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <returns>The asynchronous process exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync( args );
	}
}
