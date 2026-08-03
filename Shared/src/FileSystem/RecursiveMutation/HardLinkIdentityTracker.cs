using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;

/// <summary>Identifies the result of recording one E1 entry identity.</summary>
public enum HardLinkIdentityDisposition {
	/// <summary>No stable identity was supplied.</summary>
	Unavailable = 0,
	/// <summary>This is the first pathname observed for the identity.</summary>
	First = 1,
	/// <summary>The identity was observed previously under another pathname.</summary>
	Repeated = 2
}

/// <summary>Retains the first source and destination pathnames observed for one stable identity.</summary>
/// <param name="SourcePath">The first source pathname observed for the identity.</param>
/// <param name="DestinationPath">The first mapped destination pathname, when the operation has a destination.</param>
public sealed record HardLinkIdentityAnchor(
	string SourcePath,
	string? DestinationPath
);

/// <summary>Tracks repeated non-directory identities so copying can preserve hard links across all operation roots.</summary>
public sealed class HardLinkIdentityTracker {
	private readonly Dictionary<FileSystemEntryIdentity, HardLinkIdentityAnchor> _firstEntries = new();

	/// <summary>Gets the number of distinct stable identities retained.</summary>
	public int Count => _firstEntries.Count;

	/// <summary>Records one identity and returns the first source/destination anchor when it is repeated.</summary>
	/// <param name="identity">The stable E1 entry identity.</param>
	/// <param name="sourcePath">The pathname at which the source identity was observed.</param>
	/// <param name="destinationPath">The optional destination pathname mapped for the source.</param>
	/// <param name="firstAnchor">The first retained anchor for a repeated identity; otherwise <see langword="null"/>.</param>
	/// <returns>The disposition of the supplied identity.</returns>
	public HardLinkIdentityDisposition Track(
		FileSystemEntryIdentity identity,
		string sourcePath,
		string? destinationPath,
		out HardLinkIdentityAnchor? firstAnchor
	) {
		ArgumentException.ThrowIfNullOrEmpty( sourcePath );
		if ( destinationPath is not null ) {
			ArgumentException.ThrowIfNullOrEmpty( destinationPath );
		}
		if ( !identity.IsAvailable ) {
			firstAnchor = null;
			return HardLinkIdentityDisposition.Unavailable;
		}
		if ( _firstEntries.TryGetValue( identity, out firstAnchor ) ) {
			return HardLinkIdentityDisposition.Repeated;
		}
		_firstEntries.Add( identity, new HardLinkIdentityAnchor( sourcePath, destinationPath ) );
		firstAnchor = null;
		return HardLinkIdentityDisposition.First;
	}

	/// <summary>Clears all retained identities.</summary>
	public void Clear() => _firstEntries.Clear();
}
