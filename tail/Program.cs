/*
	tail
	Output the last part of files.
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

namespace Icod.CoreUtils.Tail;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>tail</c> command for writing the trailing portion of files or standard input.
/// </summary>
public static class Program {

	/// <summary>
	/// Runs the <c>tail</c> command using both the text and binary process console streams, and converts a console interrupt into a cancellation request.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>tail</c>.</param>
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
			var binaryStdin = Console.OpenStandardInput();
			var binaryStdout = Console.OpenStandardOutput();
			return await Command.RunAsync(
				args,
				stdin: Console.In,
				stdout: Console.Out,
				stderr: Console.Error,
				stdinStream: binaryStdin,
				stdoutStream: binaryStdout,
				cancellationToken: cancellation.Token
			).ConfigureAwait( false );
		} finally {
			Console.CancelKeyPress -= handler;
		}
	}

}
