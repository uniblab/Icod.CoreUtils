/*
	paste
	Merge corresponding lines of files.
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

namespace Icod.CoreUtils.Paste;

using Icod.CommandFramework.Diagnostics;

/// <summary>Provides the process entry point for <c>paste</c>.</summary>
public static class Program {
	/// <summary>Runs the command using process standard streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static async Task<int> Main( string[] args ) {
		ArgumentNullException.ThrowIfNull( args );
		using var cancellation = new CancellationTokenSource();
		ConsoleCancelEventHandler handler = ( _, eventArgs ) => {
			eventArgs.Cancel = true;
			cancellation.Cancel();
		};
		Console.CancelKeyPress += handler;
		try {
			return await Command.RunAsync(
				args,
				CommandContext.CreateConsole(
					"paste",
					cancellation.Token
				)
			).ConfigureAwait( false );
		} finally {
			Console.CancelKeyPress -= handler;
		}
	}
}
