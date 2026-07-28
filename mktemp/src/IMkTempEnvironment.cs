namespace Icod.CoreUtils.MkTemp;

/// <summary>Provides environment and platform defaults used by <c>mktemp</c>.</summary>
public interface IMkTempEnvironment {
	/// <summary>Gets an environment-variable value.</summary>
	/// <param name="name">The variable name.</param>
	/// <returns>The value, or <see langword="null"/> when unset.</returns>
	string? GetEnvironmentVariable( string name );

	/// <summary>Gets the platform's default temporary directory.</summary>
	/// <returns>The default temporary directory.</returns>
	string GetDefaultTemporaryDirectory();
}
