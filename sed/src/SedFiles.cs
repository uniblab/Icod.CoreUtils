namespace Icod.LineEditor.Sed;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Icod.CoreUtils.Shared.Temporary;

// Responsibility: in-place editing and pathname handling.
public static partial class Command {

	private static Task<ExecutionResult> ProcessInPlaceAsync(
		string path,
		Options options,
		SedProgram program,
		SedTextCodec textCodec,
		TextWriter stderr,
		SedRuntimeCapabilities capabilities,
		CancellationToken cancellationToken
	) {
		return capabilities.InPlaceEditor.EditAsync(
			new SedInPlaceEditRequest(
				path,
				options.FollowSymlinks,
				options.BackupSuffix
			),
			async (
				editPath,
				outputStream,
				transformCancellationToken
			) => {
				using var input = new InputSequence(
					new SourceSpec[] { new SourceSpec( editPath ) },
					Stream.Null,
					options.NullData,
					textCodec
				);
				var environment = new ExecutionEnvironment(
					outputStream,
					textCodec,
					stderr,
					options.SuppressAutomaticPrint,
					options.NullData,
					options.ListWidth,
					options.Debug,
					options.Unbuffered,
					capabilities.Shell,
					capabilities.AuxiliaryFiles
				);
				try {
					return await ExecuteAsync(
						program,
						input,
						environment,
						transformCancellationToken
					).ConfigureAwait( false );
				} finally {
					await environment.DisposeAsync(
						transformCancellationToken
					).ConfigureAwait( false );
				}
			},
			cancellationToken
		);
	}

	/// <summary>
	/// Implements the temporary command-local in-place replacement mechanism.
	/// LE10 replaces this implementation with the shared E6 transaction model.
	/// </summary>
	internal sealed class SystemInPlaceEditor : IInPlaceEditor {

		private readonly SecureTemporaryObjectCreator myTemporaryObjects;

		/// <summary>Gets the host-backed singleton editor.</summary>
		public static SystemInPlaceEditor Instance { get; } = new(
			SecureTemporaryObjectCreator.System
		);

		/// <summary>Initializes an editor over an injectable secure temporary-object creator.</summary>
		public SystemInPlaceEditor(
			SecureTemporaryObjectCreator temporaryObjects
		) {
			this.myTemporaryObjects = temporaryObjects ?? throw new ArgumentNullException(
				nameof( temporaryObjects )
			);
		}

		/// <inheritdoc />
		public async Task<ExecutionResult> EditAsync(
			SedInPlaceEditRequest request,
			Func<string, Stream, CancellationToken, Task<ExecutionResult>> transformAsync,
			CancellationToken cancellationToken
		) {
			ArgumentNullException.ThrowIfNull( request );
			ArgumentNullException.ThrowIfNull( transformAsync );
			cancellationToken.ThrowIfCancellationRequested();

			var editPath = ResolveInPlacePath(
				request.Path,
				request.FollowSymlinks
			);
			var attributes = File.GetAttributes( editPath );
			UnixFileMode? unixMode = null;
			if ( !OperatingSystem.IsWindows() ) {
				unixMode = File.GetUnixFileMode( editPath );
			}

			var temporaryPath = this.CreateTemporaryPath(
				Path.GetDirectoryName( editPath ) ?? ".",
				cancellationToken
			);
			try {
				ExecutionResult result;
				await using ( var outputStream = new FileStream(
					temporaryPath,
					FileMode.Open,
					FileAccess.Write,
					FileShare.None,
					8192,
					useAsync: true
				) ) {
					result = await transformAsync(
						editPath,
						outputStream,
						cancellationToken
					).ConfigureAwait( false );
					await outputStream.FlushAsync(
						cancellationToken
					).ConfigureAwait( false );
				}

				if (
					null != request.BackupSuffix
					&& 0 < request.BackupSuffix.Length
				) {
					var backupPath = BuildBackupPath(
						editPath,
						request.BackupSuffix
					);
					if ( File.Exists( backupPath ) ) {
						File.Delete( backupPath );
					}
					File.Move( editPath, backupPath );
				} else {
					if ( 0 != ( attributes & FileAttributes.ReadOnly ) ) {
						File.SetAttributes(
							editPath,
							attributes & ~FileAttributes.ReadOnly
						);
					}
					File.Delete( editPath );
				}

				File.Move( temporaryPath, editPath );
				File.SetAttributes(
					editPath,
					attributes & ~FileAttributes.ReparsePoint
				);
				if (
					!OperatingSystem.IsWindows()
					&& unixMode.HasValue
				) {
					File.SetUnixFileMode( editPath, unixMode.Value );
				}
				return result;
			} catch {
				_ = this.myTemporaryObjects.TryDelete(
					temporaryPath,
					TemporaryObjectKind.File,
					out _
				);
				throw;
			}
		}

		private string CreateTemporaryPath(
			string directory,
			CancellationToken cancellationToken
		) {
			if ( !TemporaryNameTemplate.TryParse(
				Path.Combine( directory, ".sed.XXXXXXXXXX.tmp" ),
				explicitSuffix: null,
				out var template,
				out var parseError
			) ) {
				throw new IOException(
					parseError ?? "invalid in-place temporary template"
				);
			}
			var creation = this.myTemporaryObjects.Create(
				template!,
				TemporaryObjectKind.File,
				cancellationToken
			);
			if ( !creation.IsSuccess || null == creation.Path ) {
				throw new IOException(
					creation.ErrorMessage ?? "unable to create in-place temporary file"
				);
			}
			return creation.Path;
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
		var info = new FileInfo( path );
		var target = info.ResolveLinkTarget( returnFinalTarget: true );
		return target?.FullName ?? path;
	}

}
