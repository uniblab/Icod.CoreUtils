using Path = global::System.IO.Path;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CommandFramework.FileSystem.Traversal;
using Icod.CoreUtils.Shared.Temporary;

namespace Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;

/// <summary>Provides the Windows, Linux, macOS, and best-effort FreeBSD E6 filesystem boundary.</summary>
public sealed class SystemTransactionalReplacementFileSystem : ITransactionalReplacementFileSystem {
	private const int BufferSize = 128 * 1024;
	private const uint ReplaceFileWriteThrough = 0x00000001;
	private const uint MoveFileReplaceExisting = 0x00000001;
	private const uint MoveFileWriteThrough = 0x00000008;
	private readonly IFileSystemMetadataProvider metadataProvider;
	private readonly IFileSystemMutationProvider mutationProvider;
	private readonly IFileSystemOperations fileSystemOperations;
	private readonly SecureTemporaryObjectCreator temporaryCreator;

	/// <summary>Gets the shared system implementation.</summary>
	public static SystemTransactionalReplacementFileSystem Instance { get; } = new();

	/// <summary>Initializes the system implementation.</summary>
	public SystemTransactionalReplacementFileSystem()
		: this(
			SystemFileSystemMetadataProvider.Instance,
			SystemFileSystemMutationProvider.Instance,
			SystemFileSystemOperations.Instance,
			SecureTemporaryObjectCreator.System
		) {
	}

	/// <summary>Initializes an implementation over injected E3, E4, durability, and temporary-file providers.</summary>
	/// <param name="metadataProvider">The authoritative E3 provider.</param>
	/// <param name="mutationProvider">The race-aware E4 provider.</param>
	/// <param name="fileSystemOperations">The durable-flush provider.</param>
	/// <param name="temporaryCreator">The secure temporary-object creator.</param>
	public SystemTransactionalReplacementFileSystem(
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		IFileSystemOperations fileSystemOperations,
		SecureTemporaryObjectCreator temporaryCreator
	) {
		this.metadataProvider = metadataProvider ?? throw new ArgumentNullException( nameof( metadataProvider ) );
		this.mutationProvider = mutationProvider ?? throw new ArgumentNullException( nameof( mutationProvider ) );
		this.fileSystemOperations = fileSystemOperations ?? throw new ArgumentNullException( nameof( fileSystemOperations ) );
		this.temporaryCreator = temporaryCreator ?? throw new ArgumentNullException( nameof( temporaryCreator ) );
		var nativeAtomic = OperatingSystem.IsWindows()
			|| OperatingSystem.IsLinux()
			|| OperatingSystem.IsMacOS()
			|| OperatingSystem.IsFreeBSD();
		Capabilities = new TransactionalReplacementCapabilities(
			nativeAtomic,
			nativeAtomic,
			nativeAtomic,
			nativeAtomic
		);
	}

	/// <inheritdoc/>
	public TransactionalReplacementCapabilities Capabilities { get; }

	/// <inheritdoc/>
	public async ValueTask<TransactionalReplacementObservation> ObserveAsync(
		string path,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		try {
			var metadata = await metadataProvider.GetMetadataAsync(
				path,
				dereferenceMode,
				cancellationToken
			).ConfigureAwait( false );
			var length = FileSystemEntryKind.File == metadata.Kind
				? new FileInfo( path ).Length
				: (long?)null;
			var modificationTime = metadata.ModificationTime.IsAvailable
				? metadata.ModificationTime.GetRequiredValue()
				: (DateTimeOffset?)null;
			return new TransactionalReplacementObservation( path, true, metadata ) {
				Length = length,
				ModificationTime = modificationTime
			};
		} catch ( FileNotFoundException ) {
			return new TransactionalReplacementObservation( path, false, null );
		} catch ( DirectoryNotFoundException ) {
			return new TransactionalReplacementObservation( path, false, null );
		}
	}

	/// <inheritdoc/>
	public ValueTask<bool> AnyNumberedBackupExistsAsync(
		string destinationPath,
		int maximumNumberedBackup,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( destinationPath );
		if ( 1 > maximumNumberedBackup ) {
			throw new ArgumentOutOfRangeException( nameof( maximumNumberedBackup ) );
		}
		cancellationToken.ThrowIfCancellationRequested();
		var fullPath = System.IO.Path.GetFullPath( destinationPath );
		var directory = System.IO.Path.GetDirectoryName( fullPath );
		if ( string.IsNullOrEmpty( directory ) ) {
			directory = Directory.GetCurrentDirectory();
		}
		if ( !Directory.Exists( directory ) ) {
			return ValueTask.FromResult( false );
		}
		var prefix = string.Concat( System.IO.Path.GetFileName( fullPath ), ".~" );
		var comparison = OperatingSystem.IsWindows()
			? StringComparison.OrdinalIgnoreCase
			: StringComparison.Ordinal;
		foreach ( var entryPath in Directory.EnumerateFileSystemEntries( directory ) ) {
			cancellationToken.ThrowIfCancellationRequested();
			var name = System.IO.Path.GetFileName( entryPath );
			if ( !name.StartsWith( prefix, comparison ) || !name.EndsWith( '~' ) ) {
				continue;
			}
			var numberText = name.AsSpan( prefix.Length, name.Length - prefix.Length - 1 );
			if ( int.TryParse( numberText, NumberStyles.None, CultureInfo.InvariantCulture, out var number )
				&& 1 <= number
				&& number <= maximumNumberedBackup ) {
				return ValueTask.FromResult( true );
			}
		}
		return ValueTask.FromResult( false );
	}

	/// <inheritdoc/>
	public ValueTask<string> CreateSiblingTemporaryFileAsync(
		string destinationPath,
		string purpose,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( destinationPath );
		ArgumentException.ThrowIfNullOrWhiteSpace( purpose );
		if ( purpose.Any( character => !char.IsAsciiLetterOrDigit( character ) && '-' != character ) ) {
			throw new ArgumentException( "The temporary-file purpose contains an unsafe filename character.", nameof( purpose ) );
		}
		var directory = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath( destinationPath ) );
		if ( string.IsNullOrEmpty( directory ) ) {
			directory = Directory.GetCurrentDirectory();
		}
		var basename = System.IO.Path.GetFileName( destinationPath );
		if ( string.IsNullOrEmpty( basename ) ) {
			basename = "artifact";
		}
		var templateText = string.Concat( ".", basename, ".icod-e6-", purpose, "-XXXXXXXXXXXX" );
		if ( !TemporaryNameTemplate.TryParse( templateText, null, out var template, out var errorMessage ) ) {
			throw new IOException( errorMessage ?? "Could not create a secure sibling template." );
		}
		var result = temporaryCreator.Create(
			template!.WithDirectory( directory ),
			TemporaryObjectKind.File,
			cancellationToken
		);
		if ( !result.IsSuccess ) {
			throw new IOException( result.ErrorMessage ?? "Secure sibling temporary-file creation failed." );
		}
		return ValueTask.FromResult( result.Path! );
	}

	/// <inheritdoc/>
	public async ValueTask WriteTemporaryFileAsync(
		string path,
		TransactionalReplacementContentWriter writer,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		ArgumentNullException.ThrowIfNull( writer );
		await using var stream = OpenTemporaryForWrite( path );
		await writer( stream, cancellationToken ).ConfigureAwait( false );
	}

	/// <inheritdoc/>
	public async ValueTask CopyTemporaryFileAsync(
		string sourcePath,
		string destinationPath,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( sourcePath );
		ArgumentException.ThrowIfNullOrWhiteSpace( destinationPath );
		await using var source = new FileStream(
			sourcePath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read,
			BufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
		await using var destination = OpenTemporaryForWrite( destinationPath );
		await source.CopyToAsync( destination, BufferSize, cancellationToken ).ConfigureAwait( false );
	}

	/// <inheritdoc/>
	public async ValueTask<TransactionalReplacementDurabilityResult> FlushFileAsync(
		string path,
		CancellationToken cancellationToken = default
	) {
		var result = await fileSystemOperations.FlushFileAsync(
			path,
			FileFlushMode.DataAndMetadata,
			cancellationToken
		).ConfigureAwait( false );
		if ( result.Succeeded ) {
			return new TransactionalReplacementDurabilityResult( TransactionalReplacementDurability.Durable );
		}
		if ( !result.Supported ) {
			return new TransactionalReplacementDurabilityResult(
				TransactionalReplacementDurability.Unsupported,
				result.Message
			);
		}
		throw new IOException( result.Message ?? "Staged file flush failed.", result.Exception );
	}

	/// <inheritdoc/>
	public ValueTask<TransactionalReplacementCommitResult> CommitFileAsync(
		string stagedPath,
		string destinationPath,
		bool replaceExisting,
		bool allowNonAtomicFallback,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( stagedPath );
		ArgumentException.ThrowIfNullOrWhiteSpace( destinationPath );
		cancellationToken.ThrowIfCancellationRequested();
		try {
			if ( OperatingSystem.IsWindows() ) {
				CommitWindows( stagedPath, destinationPath, replaceExisting );
				return ValueTask.FromResult(
					new TransactionalReplacementCommitResult( TransactionalReplacementAtomicity.Atomic )
				);
			}
			if ( OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD() ) {
				CommitUnix( stagedPath, destinationPath, replaceExisting );
				return ValueTask.FromResult(
					new TransactionalReplacementCommitResult( TransactionalReplacementAtomicity.Atomic )
				);
			}
		} catch ( EntryPointNotFoundException ) when ( allowNonAtomicFallback ) {
			return ValueTask.FromResult( CommitPortable( stagedPath, destinationPath, replaceExisting ) );
		} catch ( DllNotFoundException ) when ( allowNonAtomicFallback ) {
			return ValueTask.FromResult( CommitPortable( stagedPath, destinationPath, replaceExisting ) );
		}
		if ( !allowNonAtomicFallback ) {
			throw new PlatformNotSupportedException( "Atomic sibling-file publication is unavailable on this platform." );
		}
		return ValueTask.FromResult( CommitPortable( stagedPath, destinationPath, replaceExisting ) );
	}

	/// <inheritdoc/>
	public async ValueTask<TransactionalReplacementCommitResult> DeleteFileAsync(
		string path,
		FileSystemMutationPrecondition precondition,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		ArgumentNullException.ThrowIfNull( precondition );
		var result = await mutationProvider.RemoveFileAsync(
			path,
			precondition,
			cancellationToken
		).ConfigureAwait( false );
		if ( !result.Succeeded ) {
			throw new IOException( result.Message ?? string.Concat( "Cannot remove ", path, "." ), result.Exception );
		}
		var atomicity = OperatingSystem.IsWindows()
			|| OperatingSystem.IsLinux()
			|| OperatingSystem.IsMacOS()
			|| OperatingSystem.IsFreeBSD()
			? TransactionalReplacementAtomicity.Atomic
			: TransactionalReplacementAtomicity.Unknown;
		return new TransactionalReplacementCommitResult( atomicity );
	}

	/// <inheritdoc/>
	public ValueTask ApplyMetadataAsync(
		string path,
		FileSystemMetadata sourceMetadata,
		RecursiveMetadataPreservationPlan plan,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		ArgumentNullException.ThrowIfNull( sourceMetadata );
		ArgumentNullException.ThrowIfNull( plan );
		return ApplyMetadataCoreAsync( path, sourceMetadata, plan, cancellationToken );
	}

	/// <inheritdoc/>
	public ValueTask RestoreMetadataAsync(
		string path,
		FileSystemMetadata originalMetadata,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		ArgumentNullException.ThrowIfNull( originalMetadata );
		var plan = RecursiveMetadataPreservationPlan.Create(
			originalMetadata,
			RecursiveMetadataFields.Mode
				| RecursiveMetadataFields.Ownership
				| RecursiveMetadataFields.Timestamps
				| RecursiveMetadataFields.Attributes,
			RecursiveMetadataFields.None
		);
		return ApplyMetadataCoreAsync( path, originalMetadata, plan, cancellationToken );
	}

	/// <inheritdoc/>
	public async ValueTask<TransactionalReplacementDurabilityResult> FlushContainingDirectoryAsync(
		string path,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		var directory = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath( path ) );
		if ( string.IsNullOrEmpty( directory ) ) {
			directory = Directory.GetCurrentDirectory();
		}
		var result = await fileSystemOperations.FlushFileAsync(
			directory,
			FileFlushMode.DataAndMetadata,
			cancellationToken
		).ConfigureAwait( false );
		if ( result.Succeeded ) {
			return new TransactionalReplacementDurabilityResult( TransactionalReplacementDurability.Durable );
		}
		if ( !result.Supported ) {
			return new TransactionalReplacementDurabilityResult(
				TransactionalReplacementDurability.Unsupported,
				result.Message
			);
		}
		throw new IOException( result.Message ?? "Containing-directory flush failed.", result.Exception );
	}

	/// <inheritdoc/>
	public ValueTask DeleteTemporaryFileAsync(
		string path,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		cancellationToken.ThrowIfCancellationRequested();
		File.Delete( path );
		return ValueTask.CompletedTask;
	}

	private async ValueTask ApplyMetadataCoreAsync(
		string path,
		FileSystemMetadata sourceMetadata,
		RecursiveMetadataPreservationPlan plan,
		CancellationToken cancellationToken
	) {
		var fields = plan.Requested & plan.Available;
		var current = await metadataProvider.GetMetadataAsync(
			path,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		var precondition = FileSystemMutationPrecondition.FromObservation(
			current.Kind,
			current.EntryIdentity,
			PathDereferenceMode.NoFollow
		);
		if ( fields.HasFlag( RecursiveMetadataFields.Ownership ) ) {
			if ( mutationProvider.Capabilities.CanSetOwnership ) {
				var result = await mutationProvider.SetOwnershipAsync(
					path,
					sourceMetadata.UserId.GetRequiredValue(),
					sourceMetadata.GroupId.GetRequiredValue(),
					PathDereferenceMode.NoFollow,
					precondition,
					cancellationToken
				).ConfigureAwait( false );
				RequireMutationSuccess(
					result,
					"ownership",
					plan.Required.HasFlag( RecursiveMetadataFields.Ownership )
				);
			} else if ( plan.Required.HasFlag( RecursiveMetadataFields.Ownership ) ) {
				throw new PlatformNotSupportedException( "Required ownership preservation is unavailable." );
			}
		}
		if ( fields.HasFlag( RecursiveMetadataFields.Mode ) ) {
			if ( mutationProvider.Capabilities.CanSetModes ) {
				var result = await mutationProvider.SetModeAsync(
					path,
					new PosixFileMode( checked( (int)(sourceMetadata.Mode.GetRequiredValue() & 0x0fffU) ) ),
					PathDereferenceMode.NoFollow,
					precondition,
					cancellationToken
				).ConfigureAwait( false );
				RequireMutationSuccess(
					result,
					"mode",
					plan.Required.HasFlag( RecursiveMetadataFields.Mode )
				);
			} else if ( plan.Required.HasFlag( RecursiveMetadataFields.Mode ) ) {
				throw new PlatformNotSupportedException( "Required mode preservation is unavailable." );
			}
		}
		var timestampRequest = new FileTimestampMutationRequest {
			AccessTime = fields.HasFlag( RecursiveMetadataFields.AccessTime )
				? FileTimestampChange.At( sourceMetadata.AccessTime.GetRequiredValue() )
				: FileTimestampChange.Unchanged,
			ModificationTime = fields.HasFlag( RecursiveMetadataFields.ModificationTime )
				? FileTimestampChange.At( sourceMetadata.ModificationTime.GetRequiredValue() )
				: FileTimestampChange.Unchanged,
			BirthTime = fields.HasFlag( RecursiveMetadataFields.BirthTime )
				? FileTimestampChange.At( sourceMetadata.BirthTime.GetRequiredValue() )
				: FileTimestampChange.Unchanged
		};
		if ( timestampRequest.HasChanges ) {
			var result = await metadataProvider.SetTimestampsAsync(
				path,
				timestampRequest,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			if ( !result.Succeeded ) {
				var requiredTimestamps = plan.Required & RecursiveMetadataFields.Timestamps;
				if ( result.Supported || RecursiveMetadataFields.None != requiredTimestamps ) {
					throw new IOException( result.Message ?? "Timestamp preservation failed.", result.Exception );
				}
			}
		}
		if ( fields.HasFlag( RecursiveMetadataFields.Attributes ) ) {
			File.SetAttributes( path, sourceMetadata.Attributes.GetRequiredValue() );
		}
	}

	private static FileStream OpenTemporaryForWrite( string path ) {
		var stream = new FileStream(
			path,
			FileMode.Open,
			FileAccess.Write,
			FileShare.None,
			BufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough
		);
		stream.SetLength( 0 );
		return stream;
	}

	private static void RequireMutationSuccess(
		FileSystemMutationResult result,
		string metadataClass,
		bool required
	) {
		if ( result.Succeeded || (!result.Supported && !required) ) {
			return;
		}
		throw new IOException(
			result.Message ?? string.Concat( "Could not preserve ", metadataClass, "." ),
			result.Exception
		);
	}

	private static void CommitWindows( string stagedPath, string destinationPath, bool replaceExisting ) {
		if ( replaceExisting ) {
			if ( NativeMethods.ReplaceFileWindows(
				destinationPath,
				stagedPath,
				null,
				ReplaceFileWriteThrough,
				IntPtr.Zero,
				IntPtr.Zero
			) ) {
				return;
			}
			throw CreateNativeIOException( "ReplaceFileW failed" );
		}
		if ( NativeMethods.MoveFileWindows( stagedPath, destinationPath, MoveFileWriteThrough ) ) {
			return;
		}
		throw CreateNativeIOException( "MoveFileExW failed" );
	}

	private static void CommitUnix( string stagedPath, string destinationPath, bool replaceExisting ) {
		if ( replaceExisting ) {
			var renamed = OperatingSystem.IsMacOS()
				? NativeMethods.RenameMacOs( stagedPath, destinationPath )
				: OperatingSystem.IsFreeBSD()
					? NativeMethods.RenameFreeBsd( stagedPath, destinationPath )
					: NativeMethods.RenameLinux( stagedPath, destinationPath );
			if ( 0 == renamed ) {
				return;
			}
			throw CreateNativeIOException( "rename failed" );
		}
		File.Move( stagedPath, destinationPath, overwrite: false );
	}

	private static TransactionalReplacementCommitResult CommitPortable(
		string stagedPath,
		string destinationPath,
		bool replaceExisting
	) {
		File.Move( stagedPath, destinationPath, replaceExisting );
		return new TransactionalReplacementCommitResult(
			TransactionalReplacementAtomicity.NonAtomic,
			"Atomic sibling-file publication is unavailable; a portable move fallback was used."
		);
	}

	private static IOException CreateNativeIOException( string operation ) {
		var error = Marshal.GetLastPInvokeError();
		return new IOException(
			string.Concat( operation, ": ", new Win32Exception( error ).Message )
		);
	}

	private static class NativeMethods {
		/// <summary>Invokes Windows atomic replacement.</summary>
		[DllImport(
			"kernel32.dll",
			EntryPoint = "ReplaceFileW",
			CharSet = CharSet.Unicode,
			SetLastError = true
		)]
		[return: MarshalAs( UnmanagedType.Bool )]
		public static extern bool ReplaceFileWindows(
			string replacedFileName,
			string replacementFileName,
			string? backupFileName,
			uint replaceFlags,
			IntPtr exclude,
			IntPtr reserved
		);

		/// <summary>Invokes Windows move publication.</summary>
		[DllImport(
			"kernel32.dll",
			EntryPoint = "MoveFileExW",
			CharSet = CharSet.Unicode,
			SetLastError = true
		)]
		[return: MarshalAs( UnmanagedType.Bool )]
		public static extern bool MoveFileWindows(
			string existingFileName,
			string newFileName,
			uint flags
		);

		/// <summary>Invokes Linux rename.</summary>
		[DllImport( "libc", EntryPoint = "rename", CharSet = CharSet.Ansi, SetLastError = true )]
		public static extern int RenameLinux( string oldPath, string newPath );
		/// <summary>Invokes macOS rename.</summary>
		[DllImport( "libSystem.B.dylib", EntryPoint = "rename", CharSet = CharSet.Ansi, SetLastError = true )]
		public static extern int RenameMacOs( string oldPath, string newPath );
		/// <summary>Invokes FreeBSD rename.</summary>
		[DllImport( "libc", EntryPoint = "rename", CharSet = CharSet.Ansi, SetLastError = true )]
		public static extern int RenameFreeBsd( string oldPath, string newPath );

	}
}
