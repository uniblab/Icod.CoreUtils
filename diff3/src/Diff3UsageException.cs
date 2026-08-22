namespace Icod.DiffUtils.Diff3;

/// <summary>Represents invalid <c>diff3</c> command-line usage.</summary>
internal sealed class Diff3UsageException : Exception {
	/// <summary>Initializes a usage exception.</summary>
	public Diff3UsageException( string message ) : base( message ) {
	}
}
