namespace Icod.CoreUtils.Shared.Codecs;

/// <summary>
/// Configures the common base-encoding command runner.
/// </summary>
public sealed class BaseEncodingCommandSettings {

	/// <summary>Gets the fixed encoding, or <see langword="null"/> when an option selects it.</summary>
	public BaseEncodingKind? FixedEncoding {
		get;
		init;
	}

	/// <summary>Gets the encoding-selection options accepted by the command.</summary>
	public IReadOnlyList<BaseEncodingSelection> EncodingSelections {
		get;
		init;
	} = Array.Empty<BaseEncodingSelection>();

	/// <summary>Gets the command name used in diagnostics.</summary>
	public required string ProgramName {
		get;
		init;
	}

	/// <summary>Gets the usage printer.</summary>
	public required Action<TextWriter> PrintUsage {
		get;
		init;
	}

	/// <summary>Gets the version text.</summary>
	public required string VersionText {
		get;
		init;
	}

}
