/*
	pinky
	Print concise user information.
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

namespace Icod.CoreUtils.Pinky;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>pinky</c> command for reporting concise user-session information.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the <c>pinky</c> command with the supplied command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments supplied to <c>pinky</c>.</param>
    /// <returns>A task whose result is the command exit status.</returns>
    public static Task<int> Main(string[] args) => Command.RunAsync(args);
}
