namespace Icod.CoreUtils.Shared.IO;

/// <summary>
/// Controls pathname wildcard expansion.
/// </summary>
public sealed class PathnameExpansionOptions {

	/// <summary>
	/// Gets the directory from which relative patterns are evaluated.
	/// </summary>
	public string BaseDirectory {
		get;
		init;
	} = Directory.GetCurrentDirectory();

	/// <summary>
	/// Gets whether directory symbolic links are traversed by <c>**</c>.
	/// </summary>
	public bool FollowDirectorySymlinks {
		get;
		init;
	}

	/// <summary>
	/// Gets whether directories may be returned as matches.
	/// </summary>
	public bool IncludeDirectories {
		get;
		init;
	}

	/// <summary>
	/// Gets whether files may be returned as matches.
	/// </summary>
	public bool IncludeFiles {
		get;
		init;
	} = true;

	/// <summary>
	/// Gets whether an unmatched wildcard pattern is retained as a literal.
	/// </summary>
	public bool PreserveUnmatchedPatterns {
		get;
		init;
	} = true;

}
