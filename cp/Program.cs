/*
	cp
	Copy files and directories.
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

namespace Icod.CoreUtils.Cp;

/// <summary>
/// Provides the <c>cp</c> process entry point. Usage: <c>cp [OPTION]... SOURCE... DEST</c>.
/// </summary>
public static class Program {
	/// <summary>Runs the command.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The process exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args ).AsTask();

	/// <summary>Writes the command usage synopsis.</summary>
	/// <param name="writer">The destination writer.</param>
	public static void WriteUsage( TextWriter writer ) {
		ArgumentNullException.ThrowIfNull( writer );
		writer.WriteLine( "Usage: cp [OPTION]... SOURCE... DEST" );
	}
}
