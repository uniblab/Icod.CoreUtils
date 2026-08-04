namespace Icod.LineEditor.Sed;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Processes;

// Responsibility: in-place editing and pathname handling.
public static partial class Command {

	private static async Task<ExecutionResult> ProcessInPlaceAsync(
		string path,
		Options options,
		SedProgram program,
		SedTextCodec textCodec,
		TextWriter stderr,
		CancellationToken cancellationToken
	) {
		var editPath = ResolveInPlacePath( path, options.FollowSymlinks );
		var directory = Path.GetDirectoryName( editPath ) ?? ".";
		var temporaryPath = Path.Combine(
			directory,
			$".sed.{Path.GetRandomFileName()}.tmp"
		);
		var attributes = File.GetAttributes( editPath );
		UnixFileMode? unixMode = null;
		if ( !OperatingSystem.IsWindows() ) {
			unixMode = File.GetUnixFileMode( editPath );
		}

		try {
			ExecutionResult result;
			using ( var outputStream = new FileStream(
				temporaryPath,
				FileMode.CreateNew,
				FileAccess.Write,
				FileShare.None,
				8192,
				useAsync: true
			) )
			using ( var input = new InputSequence(
				new SourceSpec[] { new SourceSpec( editPath ) },
				Stream.Null,
				options.NullData,
				textCodec
			) ) {
				var environment = new ExecutionEnvironment(
					outputStream,
					textCodec,
					stderr,
					options.SuppressAutomaticPrint,
					options.NullData,
					options.ListWidth,
					options.Debug,
					options.Unbuffered
				);
				try {
					result = await ExecuteAsync(
						program,
						input,
						environment,
						cancellationToken
					).ConfigureAwait( false );
				} finally {
					await environment.DisposeAsync( cancellationToken ).ConfigureAwait( false );
				}
			}

			if ( null != options.BackupSuffix && 0 < options.BackupSuffix.Length ) {
				var backupPath = BuildBackupPath( editPath, options.BackupSuffix );
				if ( File.Exists( backupPath ) ) {
					File.Delete( backupPath );
				}
				File.Move( editPath, backupPath );
			} else {
				if ( 0 != ( attributes & FileAttributes.ReadOnly ) ) {
					File.SetAttributes( editPath, attributes & ~FileAttributes.ReadOnly );
				}
				File.Delete( editPath );
			}

			File.Move( temporaryPath, editPath );
			File.SetAttributes( editPath, attributes & ~FileAttributes.ReparsePoint );
			if ( !OperatingSystem.IsWindows() && unixMode.HasValue ) {
				File.SetUnixFileMode( editPath, unixMode.Value );
			}
			return result;
		} catch {
			if ( File.Exists( temporaryPath ) ) {
				File.Delete( temporaryPath );
			}
			throw;
		}
	}

	private static string BuildBackupPath(
		string path,
		string suffix
	) {
		return suffix.Contains(
			"*",
			StringComparison.Ordinal
		)
			? suffix.Replace(
				"*",
				path,
				StringComparison.Ordinal
			)
			: string.Concat(
				path,
				suffix
			)
		;
	}

	private static string ResolveInPlacePath(
		string path,
		bool followSymlinks
	) {
		if ( !followSymlinks ) {
			return path;
		}
		var info = new FileInfo(
			path
		);
		var target = info.ResolveLinkTarget(
			returnFinalTarget: true
		);
		return target?.FullName ?? path;
	}


}
