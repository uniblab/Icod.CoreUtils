/*
	env
	Run a program in a modified environment or print the environment.
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

namespace Icod.CoreUtils.Env;

/// <summary>Entry point for GNU <c>env</c>.</summary>
internal static class Program {
	/// <summary>Runs GNU <c>env</c>.</summary>
	public static async Task<int> Main(
		string[] args
	) {
		ArgumentNullException.ThrowIfNull( args );

		if ( !OperatingSystem.IsWindows() ) {
			// GNU env ultimately execvp()s its command. Replace this process on POSIX so
			// terminal signals, job control, PID identity, and configured signal policy
			// belong directly to the executed command.
			return await Command.RunAsync(
				args,
				stdin: null,
				stdout: null,
				stderr: null,
				replaceCurrentProcess: true
			).ConfigureAwait( false );
		}

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
			// Windows has no execve-equivalent process-image replacement. Null binary
			// streams still preserve the native standard handles for the child command.
			return await Command.RunAsync(
				args,
				stdin: null,
				stdout: null,
				stderr: null,
				cancellationToken: cancellation.Token
			).ConfigureAwait( false );
		} finally {
			Console.CancelKeyPress -= handler;
		}
	}
}
