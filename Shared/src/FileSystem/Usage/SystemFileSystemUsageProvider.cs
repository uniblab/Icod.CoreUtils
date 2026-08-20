using System.Runtime.InteropServices;
using Icod.CoreUtils.Shared.FileSystem.Metadata;

namespace Icod.CoreUtils.Shared.FileSystem.Usage;

/// <summary>Provides filesystem capacity and inode observations through Shared metadata and narrow host APIs.</summary>
public sealed class SystemFileSystemUsageProvider : IFileSystemUsageProvider {
	private readonly IFileSystemMetadataProvider metadataProvider;

	/// <summary>Gets the shared system provider.</summary>
	public static SystemFileSystemUsageProvider Instance { get; } = new( SystemFileSystemMetadataProvider.Instance );

	/// <summary>Initializes a provider over an injectable metadata provider.</summary>
	public SystemFileSystemUsageProvider( IFileSystemMetadataProvider metadataProvider ) {
		this.metadataProvider = metadataProvider ?? throw new ArgumentNullException( nameof( metadataProvider ) );
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<FileSystemUsageSnapshot>> GetFileSystemsAsync(
		IReadOnlyList<string> paths,
		bool includeUnavailable,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( paths );
		var candidates = paths.Count > 0
			? paths.Select( static path => new Candidate( path, null ) ).ToArray()
			: EnumerateDrives( includeUnavailable ).ToArray();
		var results = new List<FileSystemUsageSnapshot>();
		var deduplicate = paths.Count == 0 && !includeUnavailable;
		var identities = new HashSet<string>( OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal );
		foreach ( var candidate in candidates ) {
			cancellationToken.ThrowIfCancellationRequested();
			FileSystemInformation information;
			try {
				information = await metadataProvider.GetFileSystemInformationAsync(
					candidate.Path,
					cancellationToken
				).ConfigureAwait( false );
			} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
				throw;
			} catch ( Exception exception ) when (
				paths.Count == 0
				&& exception is (IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
			) {
				continue;
			}
			var mountPoint = information.MountPoint.IsAvailable
				? information.MountPoint.GetRequiredValue()
				: System.IO.Path.GetPathRoot( System.IO.Path.GetFullPath( candidate.Path ) ) ?? candidate.Path;
			var key = information.Identity.IsAvailable ? information.Identity.ToString() : mountPoint;
			if ( deduplicate && !identities.Add( key ) ) {
				continue;
			}
			var drive = candidate.Drive ?? FindContainingDrive( mountPoint );
			var deviceName = GetDeviceName( drive, mountPoint, information );
			var inodes = TryGetInodes( candidate.Path );
			var snapshot = new FileSystemUsageSnapshot(
				candidate.Path,
				deviceName,
				information,
				IsLocal( drive )
			) {
				TotalInodes = inodes.Total,
				FreeInodes = inodes.Free,
				AvailableInodes = inodes.Available
			};
			results.Add( snapshot );
		}
		return results;
	}

	private static IReadOnlyList<Candidate> EnumerateDrives( bool includeUnavailable ) {
		DriveInfo[] drives;
		try {
			drives = DriveInfo.GetDrives();
		} catch {
			return Array.Empty<Candidate>();
		}
		var candidates = new List<Candidate>( drives.Length );
		foreach ( var drive in drives ) {
			try {
				if ( drive.IsReady || includeUnavailable ) {
					candidates.Add( new Candidate( drive.RootDirectory.FullName, drive ) );
				}
			} catch {
				// Ignore host entries that cannot be queried safely.
			}
		}
		return candidates;
	}

	private static DriveInfo? FindContainingDrive( string path ) {
		var fullPath = System.IO.Path.GetFullPath( path );
		DriveInfo[] drives;
		try {
			drives = DriveInfo.GetDrives();
		} catch {
			return null;
		}
		DriveInfo? best = null;
		var bestLength = -1;
		foreach ( var drive in drives ) {
			try {
				var root = System.IO.Path.GetFullPath( drive.RootDirectory.FullName );
				if ( root.Length > bestLength && IsWithinRoot( fullPath, root ) ) {
					best = drive;
					bestLength = root.Length;
				}
			} catch {
				// Continue searching the remaining drives.
			}
		}
		return best;
	}

	private static bool IsWithinRoot( string path, string root ) {
		var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		if ( path.Equals( root, comparison ) ) {
			return true;
		}
		var rootWithSeparator = System.IO.Path.EndsInDirectorySeparator( root )
			? root
			: string.Concat( root, System.IO.Path.DirectorySeparatorChar );
		return path.StartsWith( rootWithSeparator, comparison );
	}

	private static bool IsLocal( DriveInfo? drive ) {
		if ( drive is null ) {
			return true;
		}
		try {
			return drive.DriveType != DriveType.Network;
		} catch {
			return true;
		}
	}

	private static string GetDeviceName( DriveInfo? drive, string mountPoint, FileSystemInformation information ) {
		if ( drive is not null ) {
			try {
				if ( !string.IsNullOrWhiteSpace( drive.Name ) ) {
					return drive.Name;
				}
			} catch {
				// Use the metadata fallback below.
			}
		}
		if ( information.VolumeName.IsAvailable && !string.IsNullOrWhiteSpace( information.VolumeName.GetRequiredValue() ) ) {
			return information.VolumeName.GetRequiredValue();
		}
		return mountPoint;
	}

	private static InodeValues TryGetInodes( string path ) {
		if ( OperatingSystem.IsWindows() ) {
			return InodeValues.Unsupported( "Windows does not expose filesystem inode pools." );
		}
		try {
			if ( StatVfs( path, out var statistics ) != 0 ) {
				return InodeValues.Unavailable( $"statvfs failed with errno {Marshal.GetLastPInvokeError()}." );
			}
			return new InodeValues(
				FileSystemMetadataValue<ulong>.Available( statistics.Files ),
				FileSystemMetadataValue<ulong>.Available( statistics.FilesFree ),
				FileSystemMetadataValue<ulong>.Available( statistics.FilesAvailable )
			);
		} catch ( Exception exception ) when (
			exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException
		) {
			return InodeValues.Unsupported( exception.Message );
		}
	}

	[DllImport( "libc", EntryPoint = "statvfs", SetLastError = true )]
	private static extern int StatVfs( string path, out PosixStatVfs statistics );

#pragma warning disable CS0649
	[StructLayout( LayoutKind.Sequential )]
	private struct PosixStatVfs {
		public ulong BlockSize;
		public ulong FragmentSize;
		public ulong Blocks;
		public ulong BlocksFree;
		public ulong BlocksAvailable;
		public ulong Files;
		public ulong FilesFree;
		public ulong FilesAvailable;
		public ulong FileSystemIdentifier;
		public ulong Flags;
		public ulong MaximumNameLength;
		public int Spare0;
		public int Spare1;
		public int Spare2;
		public int Spare3;
		public int Spare4;
		public int Spare5;
	}
#pragma warning restore CS0649

	private readonly record struct Candidate( string Path, DriveInfo? Drive );
	private readonly record struct InodeValues(
		FileSystemMetadataValue<ulong> Total,
		FileSystemMetadataValue<ulong> Free,
		FileSystemMetadataValue<ulong> Available
	) {
		/// <summary>Creates unsupported inode values.</summary>
		public static InodeValues Unsupported( string message ) => new(
			FileSystemMetadataValue<ulong>.Unsupported( message ),
			FileSystemMetadataValue<ulong>.Unsupported( message ),
			FileSystemMetadataValue<ulong>.Unsupported( message )
		);
		/// <summary>Creates unavailable inode values.</summary>
		public static InodeValues Unavailable( string message ) => new(
			FileSystemMetadataValue<ulong>.Unavailable( message ),
			FileSystemMetadataValue<ulong>.Unavailable( message ),
			FileSystemMetadataValue<ulong>.Unavailable( message )
		);
	}
}
