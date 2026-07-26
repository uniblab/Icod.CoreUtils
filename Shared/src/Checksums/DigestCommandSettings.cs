namespace Icod.CoreUtils.Shared.Checksums;

/// <summary>
/// Configures one of the standalone digest commands.
/// </summary>
public sealed class DigestCommandSettings {

	/// <summary>Gets the fixed digest algorithm.</summary>
	public required ChecksumAlgorithmKind Algorithm {
		get;
		init;
	}

	/// <summary>Gets the default digest length in bits.</summary>
	public required int DefaultLengthBits {
		get;
		init;
	}

	/// <summary>Gets the BSD-style algorithm label.</summary>
	public required string DisplayName {
		get;
		init;
	}

	/// <summary>Gets the command name used in diagnostics.</summary>
	public required string ProgramName {
		get;
		init;
	}

	/// <summary>Gets the usage writer.</summary>
	public required Action<TextWriter> PrintUsage {
		get;
		init;
	}

	/// <summary>Gets whether <c>--length</c> is accepted.</summary>
	public bool SupportsLength {
		get;
		init;
	}

	/// <summary>Gets version output.</summary>
	public required string VersionText {
		get;
		init;
	}

}
