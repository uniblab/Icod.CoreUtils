namespace Icod.CoreUtils.Shared.Text;

/// <summary>Provides locale-sensitive text classification and the matching decoding mode.</summary>
public interface ITextLocaleProvider {
	/// <summary>Gets the decoding mode associated with the locale profile.</summary>
	TextDecodingMode DecodingMode {
		get;
	}

	/// <summary>Gets a stable human-readable name for the locale profile.</summary>
	string Name {
		get;
	}

	/// <summary>Determines whether a text unit is a locale blank.</summary>
	/// <param name="unit">The text unit to classify.</param>
	/// <returns><see langword="true"/> when the unit is a blank; otherwise, <see langword="false"/>.</returns>
	bool IsBlank( TextUnit unit );
}
