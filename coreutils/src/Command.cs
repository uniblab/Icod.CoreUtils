/*
	coreutils
	Route Icod.CoreUtils utility commands from a single executable.
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

namespace Icod.CoreUtils.Router;

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Icod.CommandFramework.Diagnostics;

/// <summary>Routes <c>coreutils COMMAND [args...]</c> to the managed CoreUtils commands.</summary>
public static class Command {

	private const int UsageError = 2;

	private static readonly string VersionText = $"coreutils (Icod.CoreUtils) {GetVersionText()}";

	private static readonly string[] CommandNames = [
		"arch",
		"b2sum",
		"base32",
		"base64",
		"basename",
		"basenc",
		"cat",
		"chcon",
		"chgrp",
		"chmod",
		"chown",
		"chroot",
		"cksum",
		"comm",
		"cp",
		"csplit",
		"cut",
		"date",
		"dd",
		"df",
		"dir",
		"dircolors",
		"dirname",
		"du",
		"echo",
		"env",
		"expand",
		"expr",
		"factor",
		"false",
		"fmt",
		"fold",
		"groups",
		"head",
		"hostid",
		"hostname",
		"id",
		"install",
		"join",
		"link",
		"ln",
		"logname",
		"ls",
		"md5sum",
		"mkdir",
		"mkfifo",
		"mknod",
		"mktemp",
		"mv",
		"nice",
		"nl",
		"nohup",
		"nproc",
		"numfmt",
		"od",
		"paste",
		"pathchk",
		"pinky",
		"pr",
		"printenv",
		"printf",
		"ptx",
		"pwd",
		"readlink",
		"realpath",
		"rm",
		"rmdir",
		"runcon",
		"seq",
		"sha1sum",
		"sha224sum",
		"sha256sum",
		"sha384sum",
		"sha512sum",
		"shred",
		"shuf",
		"sleep",
		"sort",
		"split",
		"stat",
		"stdbuf",
		"stty",
		"sum",
		"sync",
		"tac",
		"tail",
		"tee",
		"test",
		"timeout",
		"touch",
		"tr",
		"true",
		"truncate",
		"tsort",
		"tty",
		"uname",
		"unexpand",
		"uniq",
		"unlink",
		"users",
		"vdir",
		"wc",
		"who",
		"whoami",
		"yes"
	];

	/// <summary>Runs the multi-command router.</summary>
	/// <param name="arguments">Router arguments.</param>
	/// <returns>A task whose result is the selected command exit status.</returns>
	public static async Task<int> RunAsync(
		string[] arguments
	) {
		ArgumentNullException.ThrowIfNull(
			arguments
		);

		if ( 0 == arguments.Length ) {
			await Console.Error.WriteLineAsync(
				"coreutils: missing command; use --help to list supported commands"
			).ConfigureAwait( false );
			await Console.Error.WriteAsync(
				BuildHelpText()
			).ConfigureAwait( false );
			return UsageError;
		}

		var commandName = arguments[ 0 ];
		if (
			"--help" == commandName
			|| "-h" == commandName
		) {
			await Console.Out.WriteAsync(
				BuildHelpText()
			).ConfigureAwait( false );
			return 0;
		}
		if (
			"--version" == commandName
			|| "-v" == commandName
		) {
			await Console.Out.WriteLineAsync(
				VersionText
			).ConfigureAwait( false );
			return 0;
		}

		if ( !IsKnownCommand( commandName ) ) {
			await Console.Error.WriteLineAsync(
				$"coreutils: unknown command '{commandName}'; use --help to list supported commands"
			).ConfigureAwait( false );
			return UsageError;
		}

		var commandArguments = CopyCommandArguments(
			arguments
		);

		var hostedExitStatus = await TryRunExecutableEntryPointAsync(
			commandName,
			commandArguments
		).ConfigureAwait( false );
		if ( hostedExitStatus.HasValue ) {
			return hostedExitStatus.Value;
		}

		// Keep the direct command path as an in-process fallback for a referenced
		// command assembly that does not expose an executable entry point. Normal
		// CoreUtils packaging routes through Program.Main so the standalone process
		// boundary and the router process boundary remain identical.
		return commandName switch {
			"arch" => await Icod.CoreUtils.Arch.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"b2sum" => await Icod.CoreUtils.B2Sum.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"base32" => await Icod.CoreUtils.Base32.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"base64" => await Icod.CoreUtils.Base64.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"basename" => await Icod.CoreUtils.BaseName.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"basenc" => await Icod.CoreUtils.BasEnc.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"cat" => await Icod.CoreUtils.Cat.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"chcon" => await Icod.CoreUtils.ChCon.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"chgrp" => await Icod.CoreUtils.ChGrp.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"chmod" => await Icod.CoreUtils.ChMod.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"chown" => await Icod.CoreUtils.ChOwn.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"chroot" => await Icod.CoreUtils.ChRoot.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"cksum" => await Icod.CoreUtils.CkSum.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"comm" => await Icod.CoreUtils.Comm.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"cp" => await Icod.CoreUtils.Cp.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"csplit" => await Icod.CoreUtils.CSplit.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"cut" => await Icod.CoreUtils.Cut.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"date" => await Icod.CoreUtils.Date.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"dd" => await Icod.CoreUtils.DD.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"df" => await Icod.CoreUtils.Df.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"dir" => await Icod.CoreUtils.Dir.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"dircolors" => await Icod.CoreUtils.DirColors.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"dirname" => await Icod.CoreUtils.DirName.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"du" => await Icod.CoreUtils.DU.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"echo" => await Icod.CoreUtils.Echo.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"env" => await Icod.CoreUtils.Env.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"expand" => await Icod.CoreUtils.Expand.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"expr" => await Icod.CoreUtils.Expr.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"factor" => await Icod.CoreUtils.Factor.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"false" => await Icod.CoreUtils.False.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"fmt" => await Icod.CoreUtils.Fmt.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"fold" => await Icod.CoreUtils.Fold.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"groups" => await Icod.CoreUtils.Groups.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"head" => await Icod.CoreUtils.Head.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"hostid" => await Icod.CoreUtils.HostId.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"hostname" => await Icod.CoreUtils.HostName.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"id" => await Icod.CoreUtils.ID.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"install" => await Icod.CoreUtils.Install.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"join" => await Icod.CoreUtils.Join.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"link" => await Icod.CoreUtils.Link.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"ln" => await Icod.CoreUtils.Ln.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"logname" => await Icod.CoreUtils.LogName.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"ls" => await Icod.CoreUtils.Ls.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"md5sum" => await Icod.CoreUtils.MD5Sum.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"mkdir" => await Icod.CoreUtils.MkDir.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"mkfifo" => await Icod.CoreUtils.MkFifo.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"mknod" => await Icod.CoreUtils.MkNod.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"mktemp" => await Icod.CoreUtils.MkTemp.Command.RunAsync(
				commandArguments,
				CommandContext.CreateConsole(
					"mktemp"
				)
			).ConfigureAwait( false ),
			"mv" => await Icod.CoreUtils.Mv.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"nice" => await Icod.CoreUtils.Nice.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"nl" => await Icod.CoreUtils.NL.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"nohup" => await Icod.CoreUtils.Nohup.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"nproc" => await Icod.CoreUtils.NProc.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"numfmt" => await Icod.CoreUtils.NumFmt.Command.RunAsync(
				commandArguments,
				CommandContext.CreateConsole(
					"numfmt"
				)
			).ConfigureAwait( false ),
			"od" => await Icod.CoreUtils.Od.Command.RunAsync(
				commandArguments,
				CommandContext.CreateConsole(
					"od"
				)
			).ConfigureAwait( false ),
			"paste" => await Icod.CoreUtils.Paste.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"pathchk" => await Icod.CoreUtils.PathChk.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"pinky" => await Icod.CoreUtils.Pinky.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"pr" => await Icod.CoreUtils.Pr.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"printenv" => await Icod.CoreUtils.PrintEnv.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"printf" => await Icod.CoreUtils.Printf.Command.RunAsync(
				commandArguments,
				CommandContext.CreateConsole(
					"printf"
				)
			).ConfigureAwait( false ),
			"ptx" => await Icod.CoreUtils.Ptx.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"pwd" => await Icod.CoreUtils.Pwd.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"readlink" => await Icod.CoreUtils.ReadLink.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"realpath" => await Icod.CoreUtils.RealPath.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"rm" => await Icod.CoreUtils.Rm.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"rmdir" => await Icod.CoreUtils.RmDir.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"runcon" => await Icod.CoreUtils.RunCon.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"seq" => await Icod.CoreUtils.Seq.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"sha1sum" => await Icod.CoreUtils.Sha1Sum.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"sha224sum" => await Icod.CoreUtils.Sha224Sum.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"sha256sum" => await Icod.CoreUtils.Sha256Sum.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"sha384sum" => await Icod.CoreUtils.Sha384Sum.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"sha512sum" => await Icod.CoreUtils.Sha512Sum.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"shred" => await Icod.CoreUtils.Shred.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"shuf" => await Icod.CoreUtils.Shuf.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"sleep" => await Icod.CoreUtils.Sleep.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"sort" => await Icod.CoreUtils.Sort.Command.RunAsync(
				commandArguments,
				CommandContext.CreateConsole(
					"sort"
				)
			).ConfigureAwait( false ),
			"split" => await Icod.CoreUtils.Split.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"stat" => await Icod.CoreUtils.Stat.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"stdbuf" => await Icod.CoreUtils.StdBuf.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"stty" => await Icod.CoreUtils.Stty.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"sum" => await Icod.CoreUtils.Sum.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"sync" => await Icod.CoreUtils.Sync.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"tac" => await Icod.CoreUtils.Tac.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"tail" => await Icod.CoreUtils.Tail.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"tee" => await Icod.CoreUtils.Tee.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"test" => await Icod.CoreUtils.Test.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"timeout" => await Icod.CoreUtils.Timeout.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"touch" => await Icod.CoreUtils.Touch.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"tr" => await Icod.CoreUtils.Tr.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"true" => await Icod.CoreUtils.True.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"truncate" => await Icod.CoreUtils.Truncate.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"tsort" => await Icod.CoreUtils.TSort.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"tty" => await Icod.CoreUtils.Tty.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"uname" => await Icod.CoreUtils.UName.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"unexpand" => await Icod.CoreUtils.Unexpand.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"uniq" => await Icod.CoreUtils.Uniq.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"unlink" => await Icod.CoreUtils.Unlink.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"users" => await Icod.CoreUtils.Users.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"vdir" => await Icod.CoreUtils.VDir.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"wc" => await Icod.CoreUtils.WC.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"who" => await Icod.CoreUtils.Who.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"whoami" => await Icod.CoreUtils.WhoAmI.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			"yes" => await Icod.CoreUtils.Yes.Command.RunAsync(
				commandArguments
			).ConfigureAwait( false ),
			_ => throw new InvalidOperationException(
				"Known command dispatch was incomplete."
			)
		};
	}

	private static async Task<int?> TryRunExecutableEntryPointAsync(
		string commandName,
		string[] commandArguments
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			commandName
		);
		ArgumentNullException.ThrowIfNull(
			commandArguments
		);

		var commandAssembly = Assembly.Load(
			new AssemblyName(
				commandName
			)
		);
		var entryPoint = commandAssembly.EntryPoint;
		if ( null == entryPoint ) {
			return null;
		}

		object?[]? invocationArguments;
		var parameters = entryPoint.GetParameters();
		if ( 0 == parameters.Length ) {
			invocationArguments = null;
		} else if (
			1 == parameters.Length
			&& typeof( string[] ) == parameters[ 0 ].ParameterType
		) {
			invocationArguments = [ commandArguments ];
		} else {
			throw new InvalidOperationException(
				$"Command assembly '{commandName}' has an unsupported entry-point signature."
			);
		}

		object? invocationResult = null;
		try {
			invocationResult = entryPoint.Invoke(
				null,
				invocationArguments
			);
		} catch ( TargetInvocationException exception ) when (
			null != exception.InnerException
		) {
			ExceptionDispatchInfo.Capture(
				exception.InnerException
			).Throw();
		}

		if ( invocationResult is int exitStatus ) {
			return exitStatus;
		}
		if ( invocationResult is Task<int> asynchronousExitStatus ) {
			return await asynchronousExitStatus.ConfigureAwait( false );
		}

		throw new InvalidOperationException(
			$"Command assembly '{commandName}' returned an unsupported entry-point result."
		);
	}

	private static string BuildHelpText() {
		var builder = new StringBuilder();
		builder.AppendLine(
			"Usage:"
		);
		builder.AppendLine(
			" coreutils COMMAND [OPTION]... [ARG]..."
		);
		builder.AppendLine();
		builder.AppendLine(
			"Commands:"
		);
		foreach ( var commandName in CommandNames ) {
			builder.Append(
				' '
			);
			builder.AppendLine(
				commandName
			);
		}
		builder.AppendLine();
		builder.AppendLine(
			"Router options:"
		);
		builder.AppendLine(
			" -h, --help       display this help and exit"
		);
		builder.AppendLine(
			" -v, --version    output version information and exit"
		);
		builder.AppendLine();
		builder.AppendLine(
			"Run 'coreutils COMMAND --help' for command-specific help."
		);
		return builder.ToString();
	}

	private static string GetVersionText() {
		var assembly = typeof( Command ).Assembly;
		var informationalVersion = assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion;
		if ( !string.IsNullOrWhiteSpace( informationalVersion ) ) {
			var metadataSeparator = informationalVersion.IndexOf(
				'+'
			);
			if ( 0 <= metadataSeparator ) {
				return informationalVersion[ ..metadataSeparator ];
			}
			return informationalVersion;
		}

		var assemblyVersion = assembly.GetName().Version;
		if ( assemblyVersion is null ) {
			return "unknown";
		}
		return assemblyVersion.ToString(
			3
		);
	}

	private static bool IsKnownCommand(
		string commandName
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			commandName
		);

		return 0 <= Array.BinarySearch(
			CommandNames,
			commandName,
			StringComparer.Ordinal
		);
	}

	private static string[] CopyCommandArguments(
		IReadOnlyList<string> arguments
	) {
		ArgumentNullException.ThrowIfNull(
			arguments
		);

		var commandArguments = new string[ arguments.Count - 1 ];
		for ( var index = 1; index < arguments.Count; index++ ) {
			commandArguments[ index - 1 ] = arguments[ index ];
		}
		return commandArguments;
	}

}