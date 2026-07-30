namespace Icod.CoreUtils.NL;

using System.Text;
using Icod.CoreUtils.Shared.Text;

/// <summary>Represents the logical-page delimiters recognized by GNU <c>nl</c>.</summary>
internal sealed class NlSectionDelimiter {
	private NlSectionDelimiter(
		bool isEnabled,
		byte[] header,
		byte[] body,
		byte[] footer,
		string optionValue
	) {
		this.IsEnabled = isEnabled;
		this.HeaderBytes = header;
		this.BodyBytes = body;
		this.FooterBytes = footer;
		this.OptionValue = optionValue;
	}

	/// <summary>Gets the body-section delimiter bytes.</summary>
	internal byte[] BodyBytes { get; }

	/// <summary>Gets the footer-section delimiter bytes.</summary>
	internal byte[] FooterBytes { get; }

	/// <summary>Gets the header-section delimiter bytes.</summary>
	internal byte[] HeaderBytes { get; }

	/// <summary>Gets whether logical-page delimiter recognition is enabled.</summary>
	internal bool IsEnabled { get; }

	/// <summary>Gets the original delimiter option value.</summary>
	internal string OptionValue { get; }

	/// <summary>Gets the default backslash-colon delimiter.</summary>
	internal static NlSectionDelimiter Default { get; } = Parse( "\\:", TextDecodingMode.Utf8 );

	/// <summary>Parses a delimiter option value under the active text-decoding profile.</summary>
	/// <param name="value">The option value.</param>
	/// <param name="decodingMode">The active locale decoding mode.</param>
	/// <returns>The delimiter model.</returns>
	internal static NlSectionDelimiter Parse( string value, TextDecodingMode decodingMode ) {
		ArgumentNullException.ThrowIfNull( value );
		if ( 0 == value.Length ) {
			return new NlSectionDelimiter( false, [ ], [ ], [ ], value );
		}
		var encodedValue = Encoding.UTF8.GetBytes( value );
		var characterCount = TextDecodingMode.Bytes == decodingMode
			? encodedValue.Length
			: value.EnumerateRunes().Take( 2 ).Count();
		var baseBytes = 1 == characterCount
			? Encoding.UTF8.GetBytes( string.Concat( value, ":" ) )
			: encodedValue;
		return new NlSectionDelimiter(
			true,
			Repeat( baseBytes, 3 ),
			Repeat( baseBytes, 2 ),
			baseBytes,
			value
		);
	}

	/// <summary>Determines whether content bytes form a logical-page delimiter.</summary>
	/// <param name="content">The line content excluding its line feed.</param>
	/// <param name="section">The selected section when a delimiter matches.</param>
	/// <returns><see langword="true"/> when the line is a delimiter.</returns>
	internal bool TryClassify( ReadOnlySpan<byte> content, out NlSection section ) {
		if ( this.IsEnabled && content.SequenceEqual( this.HeaderBytes ) ) {
			section = NlSection.Header;
			return true;
		}
		if ( this.IsEnabled && content.SequenceEqual( this.BodyBytes ) ) {
			section = NlSection.Body;
			return true;
		}
		if ( this.IsEnabled && content.SequenceEqual( this.FooterBytes ) ) {
			section = NlSection.Footer;
			return true;
		}
		section = default;
		return false;
	}

	private static byte[] Repeat( byte[] value, int count ) {
		var result = new byte[checked(value.Length * count)];
		for ( var index = 0; index < count; index++ ) {
			value.CopyTo( result, index * value.Length );
		}
		return result;
	}
}
