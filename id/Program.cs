/*
	id
	Print user and group information.
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

namespace Icod.CoreUtils.ID;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>id</c> command for reporting user and group identity information.
/// </summary>
public static class Program {
	/// <summary>
	/// Runs the <c>id</c> command with the supplied command-line arguments.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>id</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> Main(
		string[] args
	) {
		ArgumentNullException.ThrowIfNull( args );

		using var cancellation = new CancellationTokenSource();
		ConsoleCancelEventHandler handler = (
			object? sender,
			ConsoleCancelEventArgs eventArgs
		) => {
			eventArgs.Cancel = true;
			cancellation.Cancel();
		};
		Console.CancelKeyPress += handler;
		try {
			return await Command.RunAsync(
				args,
				stdin: Console.In,
				stdout: Console.Out,
				stderr: Console.Error,
				cancellationToken: cancellation.Token
			).ConfigureAwait( false );
		} finally {
			Console.CancelKeyPress -= handler;
		}
	}
}
