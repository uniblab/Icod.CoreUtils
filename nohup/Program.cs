/*
	nohup
	Run a command immune to hangups.
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

namespace Icod.CoreUtils.Nohup;

using Icod.Processes;
using Icod.Terminal;

/// <summary>Entry point for GNU <c>nohup</c>.</summary>
internal static class Program {
	/// <summary>Runs GNU <c>nohup</c>.</summary>
	public static async Task<int> Main(
		string[] args
	) {
		ArgumentNullException.ThrowIfNull( args );

		return await Command.RunAsync(
			args,
			stdin: null,
			stdout: null,
			stderr: null,
			terminalProvider: SystemTerminalControlProvider.Instance,
			processExecutor: SystemProcessExecutor.Instance,
			outputFileProvider: SystemNohupOutputFileProvider.Instance,
			standardStreamStateProvider: SystemNohupStandardStreamStateProvider.Instance,
			sourceEnvironment: ProcessEnvironment.CreateInheritedBuilder().Build(),
			standardOutputFactory: Console.OpenStandardOutput,
			commandOutput: Console.Out,
			commandError: Console.Error
		).ConfigureAwait( false );
	}
}
