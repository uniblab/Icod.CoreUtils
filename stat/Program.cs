/*
	stat
	Display file or file system status.
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

namespace Icod.CoreUtils.Stat;

/// <summary>
/// Hosts <c>stat</c>. Usage: <c>stat [OPTION]... FILE...</c>.
/// </summary>
public static class Program {
	/// <summary>Runs the command-line entry point.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );

	/// <summary>Writes the command usage text.</summary>
	/// <param name="writer">The destination writer.</param>
	/// <returns>A task representing the asynchronous write.</returns>
	public static Task WriteUsageAsync( TextWriter writer ) {
		ArgumentNullException.ThrowIfNull( writer );
		return writer.WriteLineAsync( "Usage: stat [OPTION]... FILE..." );
	}
}
