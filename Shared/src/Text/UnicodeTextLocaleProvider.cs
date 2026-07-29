namespace Icod.CoreUtils.Shared.Text;

using System.Globalization;
using System.Text;

/// <summary>
/// Provides deterministic UTF-8 decoding and Unicode blank classification for a named locale profile.
/// </summary>
/// <remarks>
/// Horizontal tab and breakable Unicode space separators are blanks. The nonbreaking spaces U+00A0,
/// U+2007, and U+202F are deliberately excluded. A caller can inject another
/// <see cref="ITextLocaleProvider"/> when a different locale policy is required.
/// </remarks>
public sealed class UnicodeTextLocaleProvider : ITextLocaleProvider {
	/// <summary>Initializes a new instance of the <see cref="UnicodeTextLocaleProvider"/> class.</summary>
	/// <param name="name">The stable human-readable locale-profile name.</param>
	/// <exception cref="ArgumentNullException">The name is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The name is empty or consists only of white-space characters.</exception>
	public UnicodeTextLocaleProvider( string name = "UTF-8" ) {
		ArgumentNullException.ThrowIfNull( name );
		if ( string.IsNullOrWhiteSpace( name ) ) {
			throw new ArgumentException(
				"A locale-profile name is required.",
				nameof( name )
			);
		}
		this.Name = name;
	}

	/// <inheritdoc/>
	public TextDecodingMode DecodingMode => TextDecodingMode.Utf8;

	/// <inheritdoc/>
	public string Name {
		get;
	}

	/// <inheritdoc/>
	public bool IsBlank( TextUnit unit ) {
		if ( unit.Scalar is { } scalar ) {
			return (scalar.Value == 0x09)
				|| (
					Rune.GetUnicodeCategory( scalar ) == UnicodeCategory.SpaceSeparator
					&& (scalar.Value != 0x00A0)
					&& (scalar.Value != 0x2007)
					&& (scalar.Value != 0x202F)
				);
		}
		return (unit.ByteCount == 1)
			&& (unit.GetByte( 0 ) is 0x09 or 0x20);
	}
}
