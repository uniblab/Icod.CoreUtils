namespace Icod.CoreUtils.Shared.Codecs;

/// <summary>
/// Associates a command-line option with an encoding.
/// </summary>
public sealed record BaseEncodingSelection(
	string Key,
	string LongName,
	BaseEncodingKind Encoding
);
