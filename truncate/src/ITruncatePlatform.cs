namespace Icod.CoreUtils.Truncate;

using Icod.CommandFramework.Platform;

/// <summary>
/// Defines platform-dependent file-length and preferred-I/O-block operations required by <c>truncate</c>.
/// </summary>
/// <remarks>
/// Operations return controlled capability results rather than exposing platform-specific exceptions to command orchestration.
/// </remarks>
public interface ITruncatePlatform {

	/// <summary>
	/// Gets the preferred I/O block size associated with an open file.
	/// </summary>
	/// <param name="file">The open file whose filesystem allocation preference is queried.</param>
	/// <param name="path">The file pathname used by platforms whose metadata APIs require a path.</param>
	/// <param name="cancellationToken">The token used to cancel the metadata query.</param>
	/// <returns>A capability result containing the positive preferred block size on success.</returns>
	ValueTask<PlatformOperationResult<long>> GetIoBlockSizeAsync(
		FileStream file,
		string path,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Changes an open file to the requested logical length, using sparse extension when available.
	/// </summary>
	/// <param name="file">The open writable file whose length is changed.</param>
	/// <param name="length">The requested non-negative logical length in bytes.</param>
	/// <param name="cancellationToken">The token used to cancel sparse extension or length adjustment.</param>
	/// <returns>A capability result describing success, unsupported semantics, or a controlled failure.</returns>
	ValueTask<PlatformOperationResult> SetLengthAsync(
		FileStream file,
		long length,
		CancellationToken cancellationToken = default
	);
}
