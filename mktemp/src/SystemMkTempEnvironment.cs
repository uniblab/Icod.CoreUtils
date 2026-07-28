namespace Icod.CoreUtils.MkTemp;

/// <summary>Provides process environment values and host temporary-directory defaults.</summary>
public sealed class SystemMkTempEnvironment : IMkTempEnvironment {
	/// <summary>Gets the shared system environment provider.</summary>
	public static SystemMkTempEnvironment Instance { get; } = new();

	private SystemMkTempEnvironment() {
	}

	/// <inheritdoc/>
	public string? GetEnvironmentVariable( string name ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( name );
		return Environment.GetEnvironmentVariable( name );
	}

	/// <inheritdoc/>
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
