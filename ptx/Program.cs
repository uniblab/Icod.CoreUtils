/*
	ptx
	Produce a permuted index of file contents.
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

namespace Icod.CoreUtils.Ptx;

using Icod.CommandFramework.Diagnostics;

/// <summary>Provides the <c>ptx [OPTION]... [INPUT]...</c> process entry point.</summary>
public static class Program {
	/// <summary>Runs the permuted-index command.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
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
			var binaryStdin = Console.OpenStandardInput();
			var binaryStdout = Console.OpenStandardOutput();
			var binaryStderr = Console.OpenStandardError();
			return await Command.RunAsync(
				args,
				new CommandContext(
					"ptx",
					Console.In,
					Console.Out,
					Console.Error,
					binaryStdin,
					binaryStdout,
					binaryStderr,
					cancellation.Token
				)
			).ConfigureAwait( false );
		} finally {
			Console.CancelKeyPress -= handler;
		}
	}
}
