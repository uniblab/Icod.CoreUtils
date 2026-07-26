namespace Icod.CoreUtils.Shared.Platform;

/// <summary>
/// Identifies an operating-system capability used by core utilities.
/// </summary>
public enum PlatformFeature {
	/// <summary>Unix permission-bit access.</summary>
	UnixFileModes,
	/// <summary>Symbolic-link creation and inspection.</summary>
	SymbolicLinks,
	/// <summary>Hard-link creation.</summary>
	HardLinks,
	/// <summary>File owner and group manipulation.</summary>
	FileOwnership,
	/// <summary>SELinux or equivalent security contexts.</summary>
	SecurityContexts,
	/// <summary>Arbitrary POSIX-style process signals.</summary>
	ProcessSignals,
	/// <summary>Effective numeric user and group identity.</summary>
	EffectiveUserIdentity,
	/// <summary>Filesystem capacity and free-space reporting.</summary>
	FileSystemStatistics
}
