/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using Icod.CommandFramework.FileSystem.Metadata;

namespace Icod.CoreUtils.Shared.FileSystem.Usage;

/// <summary>Provides filesystem capacity and inode observations through the framework metadata provider.</summary>
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
			var snapshot = new FileSystemUsageSnapshot(
				candidate.Path,
				deviceName,
				information,
				IsLocal( drive )
			) {
				TotalInodes = information.TotalInodes,
				FreeInodes = information.FreeInodes,
				AvailableInodes = information.AvailableInodes
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

	private readonly record struct Candidate( string Path, DriveInfo? Drive );
}
