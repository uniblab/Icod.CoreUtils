/*
	expr
	Evaluate expressions.
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

namespace Icod.CoreUtils.Expr;

using Icod.CommandFramework.Diagnostics;

/// <summary>Provides the executable entry point for <c>expr</c>.</summary>
public static class Program {
	/// <summary>Runs <c>expr</c> with the process console streams.</summary>
	/// <param name="args">The expression tokens.</param>
	/// <returns>The command exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync(
			args,
			CommandContext.CreateConsole( "expr" )
		);
	}
}
