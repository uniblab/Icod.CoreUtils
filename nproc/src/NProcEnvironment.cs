namespace Icod.CoreUtils.NProc;

/// <summary>Supplies environment values used by GNU <c>nproc</c> policy.</summary>
public interface INProcEnvironment {
	/// <summary>Gets one environment variable.</summary>
	/// <param name="name">The variable name.</param>
	/// <returns>The value, or <see langword="null"/> when it is unset.</returns>
	string? GetVariable( string name );
}

/// <summary>Reads <c>nproc</c> policy variables from the current process environment.</summary>
public sealed class SystemNProcEnvironment : INProcEnvironment {
	/// <summary>Gets the process-wide environment reader.</summary>
	public static SystemNProcEnvironment Instance { get; } = new();

	private SystemNProcEnvironment() { }

	/// <inheritdoc />
	public string? GetVariable( string name ) {
		ArgumentNullException.ThrowIfNull( name );
		return Environment.GetEnvironmentVariable( name );
	}
}
