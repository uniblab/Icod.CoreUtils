namespace Icod.CoreUtils.MkTemp;

/// <summary>
/// Provides process environment values and host temporary-directory defaults for <c>mktemp</c>.
/// </summary>
/// <remarks>
/// The implementation uses conventional <c>/tmp</c> defaults on Unix-like targets and the BCL temporary path elsewhere.
/// </remarks>
public sealed class SystemMkTempEnvironment : IMkTempEnvironment {
	/// <summary>
	/// Gets the shared stateless environment provider.
	/// </summary>
	/// <value>The shared implementation instance.</value>
	public static SystemMkTempEnvironment Instance { get; } = new();

	private SystemMkTempEnvironment() {
	}

	/// <summary>
	/// Gets the current value of a process environment variable.
	/// </summary>
	/// <param name="name">The environment-variable name.</param>
	/// <returns>The variable value, or <see langword="null"/> when it is not defined.</returns>
	/// <exception cref="ArgumentException"><paramref name="name"/> is empty or consists only of white-space characters.</exception>
	public string? GetEnvironmentVariable( string name ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( name );
		return Environment.GetEnvironmentVariable( name );
	}

	/// <summary>
	/// Gets the host default temporary directory using the project portability policy.
	/// </summary>
	/// <returns>The platform default temporary-directory pathname.</returns>
	public string GetDefaultTemporaryDirectory() {
		if ( OperatingSystem.IsWindows() ) {
			return Path.GetTempPath();
		}
		if (
			OperatingSystem.IsLinux()
			|| OperatingSystem.IsMacOS()
			|| OperatingSystem.IsFreeBSD()
		) {
			// FreeBSD follows the conventional /tmp default as best effort.
			return "/tmp";
		}
		return Path.GetTempPath();
	}
}
