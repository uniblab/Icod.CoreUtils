using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.Usage;

/// <summary>Describes one filesystem for <c>df</c>-style reporting.</summary>
public sealed class FileSystemUsageSnapshot {
	/// <summary>Initializes one filesystem usage observation.</summary>
	public FileSystemUsageSnapshot(
		string sourcePath,
		string deviceName,
		FileSystemInformation information,
		bool isLocal
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( sourcePath );
		ArgumentException.ThrowIfNullOrWhiteSpace( deviceName );
		SourcePath = sourcePath;
		DeviceName = deviceName;
		Information = information ?? throw new ArgumentNullException( nameof( information ) );
		IsLocal = isLocal;
	}

	/// <summary>Gets the path used to query the filesystem.</summary>
	public string SourcePath { get; }
	/// <summary>Gets the best available device or volume name.</summary>
	public string DeviceName { get; }
	/// <summary>Gets authoritative filesystem information.</summary>
	public FileSystemInformation Information { get; }
	/// <summary>Gets whether the host classified the filesystem as local.</summary>
	public bool IsLocal { get; }
	/// <summary>Gets the total inode count where the host exposes it.</summary>
	public FileSystemMetadataValue<ulong> TotalInodes { get; init; }
	/// <summary>Gets the free inode count where the host exposes it.</summary>
	public FileSystemMetadataValue<ulong> FreeInodes { get; init; }
	/// <summary>Gets the caller-available inode count where the host exposes it.</summary>
	public FileSystemMetadataValue<ulong> AvailableInodes { get; init; }
	/// <summary>Gets the stable filesystem identity.</summary>
	public FileSystemIdentity Identity => Information.Identity;
}
