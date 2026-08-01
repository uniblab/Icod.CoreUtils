namespace Icod.DiffUtils.Diff;

/// <summary>Represents invalid command-line usage.</summary>
internal sealed class DiffUsageException : Exception {
	/// <summary>Initializes a usage exception.</summary>
	public DiffUsageException( string message ) : base( message ) {
	}
}
