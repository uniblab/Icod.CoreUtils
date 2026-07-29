namespace Icod.CoreUtils.Shared.Text;

/// <summary>Provides byte-oriented blank classification for the POSIX C locale.</summary>
public sealed class PosixCLocaleProvider : ITextLocaleProvider {
	private PosixCLocaleProvider() {
	}

	/// <summary>Gets the shared POSIX C-locale provider.</summary>
	public static PosixCLocaleProvider Instance {
		get;
	} = new();

	/// <inheritdoc/>
	public TextDecodingMode DecodingMode => TextDecodingMode.Bytes;

	/// <inheritdoc/>
	public string Name => "C";

	/// <inheritdoc/>
	public bool IsBlank( TextUnit unit ) => (unit.ByteCount == 1)
		&& (unit.GetByte( 0 ) is 0x09 or 0x20);
}
