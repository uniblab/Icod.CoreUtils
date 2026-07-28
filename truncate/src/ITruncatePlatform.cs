namespace Icod.CoreUtils.Truncate;

using Icod.CoreUtils.Shared.Platform;

/// <summary>
/// Supplies platform-dependent file-size operations required by <c>truncate</c>.
/// </summary>
public interface ITruncatePlatform {

	/// <summary>
	/// Gets the preferred I/O block size for an open file.
	/// </summary>
	ValueTask<PlatformOperationResult<long>> GetIoBlockSizeAsync(
		FileStream file,
		string path,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Changes an open file's logical length, preserving sparse extension when supported.
	/// </summary>
	ValueTask<PlatformOperationResult> SetLengthAsync(
		FileStream file,
		long length,
		CancellationToken cancellationToken = default
	);
}
