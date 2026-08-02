namespace Icod.Path;

/// <summary>Supplies no-follow canonical-path observations using the current host filesystem.</summary>
public sealed class SystemCanonicalPathFileSystemProvider : ICanonicalPathFileSystemProvider {
	private SystemCanonicalPathFileSystemProvider() {
	}

	/// <summary>Gets the shared system-provider instance.</summary>
	public static SystemCanonicalPathFileSystemProvider Instance { get; } = new();

	/// <inheritdoc/>
	public PathPlatformSemantics Semantics => PathPlatformSemantics.Host;

	/// <inheritdoc/>
	public string CurrentDirectory => Directory.GetCurrentDirectory();

	/// <inheritdoc/>
	public ValueTask<PathComponentObservation> ObserveAsync(
		string path,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		cancellationToken.ThrowIfCancellationRequested();
		try {
			var linkTarget = GetLinkTarget( path );
			if ( null != linkTarget ) {
				return ValueTask.FromResult(
					PathComponentObservation.Existing(
						path,
						CanonicalPathEntryKind.Unknown,
						isSymbolicLink: true,
						linkTarget: linkTarget,
						isReparsePoint: OperatingSystem.IsWindows()
					)
				);
			}
			var attributes = File.GetAttributes( path );
			var kind = 0 != ( attributes & FileAttributes.Directory )
				? CanonicalPathEntryKind.Directory
				: CanonicalPathEntryKind.File
			;
			return ValueTask.FromResult(
				PathComponentObservation.Existing(
					path,
					kind,
					isReparsePoint: 0 != ( attributes & FileAttributes.ReparsePoint )
				)
			);
		} catch ( FileNotFoundException ) {
			return ValueTask.FromResult( PathComponentObservation.Missing( path ) );
		} catch ( DirectoryNotFoundException ) {
			return ValueTask.FromResult( PathComponentObservation.Missing( path ) );
		} catch ( UnauthorizedAccessException exception ) {
			return ValueTask.FromResult(
				PathComponentObservation.Failed(
					new CanonicalPathFailure(
						CanonicalPathFailureCode.AccessDenied,
						path,
						"access to the pathname was denied",
						exception
					)
				)
			);
		} catch ( IOException exception ) {
			return ValueTask.FromResult(
				PathComponentObservation.Failed(
					new CanonicalPathFailure(
						CanonicalPathFailureCode.IoError,
						path,
						"the pathname could not be inspected",
						exception
					)
				)
			);
		} catch ( System.Security.SecurityException exception ) {
			return ValueTask.FromResult(
				PathComponentObservation.Failed(
					new CanonicalPathFailure(
						CanonicalPathFailureCode.AccessDenied,
						path,
						"access to the pathname was denied",
						exception
					)
				)
			);
		}
	}

	private static string? GetLinkTarget( string path ) {
		string? target = null;
		try {
			target = new FileInfo( path ).LinkTarget;
		} catch ( IOException ) {
		}
		if ( null != target ) {
			return target;
		}
		try {
			return new DirectoryInfo( path ).LinkTarget;
		} catch ( IOException ) {
			return null;
		}
	}
}
