namespace Icod.CoreUtils.Shared.Codecs;

/// <summary>
/// Associates a command-line option with an encoding.
/// </summary>
/// <param name="Key">The key value.</param>
/// <param name="LongName">The long name value.</param>
/// <param name="Encoding">The encoding value.</param>
public sealed record BaseEncodingSelection(
	string Key,
	string LongName,
	BaseEncodingKind Encoding
);
