namespace Icod.CoreUtils.Shared.FileSystem.Usage;

/// <summary>Supplies injectable filesystem-capacity observations for reporting commands.</summary>
public interface IFileSystemUsageProvider {
	/// <summary>Observes all mounted filesystems or those containing selected paths.</summary>
	/// <param name="paths">Selected paths; an empty collection requests mounted filesystems.</param>
	/// <param name="includeUnavailable">Whether unavailable or unready drives should be retained when possible.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The filesystem observations.</returns>
	Task<IReadOnlyList<FileSystemUsageSnapshot>> GetFileSystemsAsync(
		IReadOnlyList<string> paths,
		bool includeUnavailable,
		CancellationToken cancellationToken = default
	);
}
