namespace Icod.DiffUtils.SDiff;

/// <summary>Represents invalid <c>sdiff</c> command usage.</summary>
internal sealed class SDiffUsageException : Exception {
	/// <summary>Initializes a usage exception.</summary>
	/// <param name="message">The deterministic usage diagnostic.</param>
	public SDiffUsageException( string message ) : base( message ) { }
}
