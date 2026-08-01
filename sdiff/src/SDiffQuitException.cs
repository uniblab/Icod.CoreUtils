namespace Icod.DiffUtils.SDiff;

/// <summary>Signals an explicit interactive quit without committing the output transaction.</summary>
internal sealed class SDiffQuitException : Exception {
	/// <summary>Initializes the quit signal.</summary>
	public SDiffQuitException() : base( "merge abandoned" ) { }
}
