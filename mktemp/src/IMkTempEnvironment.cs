namespace Icod.CoreUtils.MkTemp;

/// <summary>
/// Defines the environment-variable and platform-default inputs used to resolve <c>mktemp</c> directories.
/// </summary>
public interface IMkTempEnvironment {
	/// <summary>
	/// Gets the current value of a process environment variable.
	/// </summary>
	/// <param name="name">The environment-variable name.</param>
	/// <returns>The variable value, or <see langword="null"/> when it is not defined.</returns>
	string? GetEnvironmentVariable( string name );

	/// <summary>
	/// Gets the platform default directory for temporary objects.
	/// </summary>
	/// <returns>The platform default temporary-directory pathname.</returns>
	string GetDefaultTemporaryDirectory();
}
