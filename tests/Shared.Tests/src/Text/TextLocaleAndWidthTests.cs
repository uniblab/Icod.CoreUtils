namespace Icod.CoreUtils.Shared.Tests.Text;

using System.Text;
using Icod.CoreUtils.Shared.Text;
using Xunit;

/// <summary>Tests locale classification and deterministic display-width providers.</summary>
public sealed class TextLocaleAndWidthTests {
	/// <summary>Verifies POSIX C-locale decoding and blank classification.</summary>
	[Fact]
	public void PosixLocaleRecognizesOnlyTabAndSpaceBytes() {
		var provider = PosixCLocaleProvider.Instance;
		Assert.Equal( "C", provider.Name );
		Assert.Equal( TextDecodingMode.Bytes, provider.DecodingMode );
		Assert.True( provider.IsBlank( ReadByteUnit( 0x09 ) ) );
		Assert.True( provider.IsBlank( ReadByteUnit( 0x20 ) ) );
		Assert.False( provider.IsBlank( ReadByteUnit( 0xA0 ) ) );
	}

	/// <summary>Verifies Unicode blank classification for breakable spaces and nonbreaking exclusions.</summary>
	[Fact]
	public void UnicodeLocaleRecognizesBreakableHorizontalSpaces() {
		var provider = new UnicodeTextLocaleProvider( "test UTF-8" );
		Assert.Equal( "test UTF-8", provider.Name );
		Assert.Equal( TextDecodingMode.Utf8, provider.DecodingMode );
		Assert.True( provider.IsBlank( ReadScalarUnit( "\t" ) ) );
		Assert.True( provider.IsBlank( ReadScalarUnit( "\u1680" ) ) );
		Assert.True( provider.IsBlank( ReadScalarUnit( "\u2003" ) ) );
		Assert.True( provider.IsBlank( ReadScalarUnit( "\u3000" ) ) );
		Assert.False( provider.IsBlank( ReadScalarUnit( "\u00A0" ) ) );
		Assert.False( provider.IsBlank( ReadScalarUnit( "\u2007" ) ) );
		Assert.False( provider.IsBlank( ReadScalarUnit( "\u202F" ) ) );
		Assert.False( provider.IsBlank( ReadScalarUnit( "\n" ) ) );
		Assert.False( provider.IsBlank( ReadScalarUnit( "A" ) ) );
	}

	/// <summary>Verifies that a Unicode locale profile requires a stable name.</summary>
	[Fact]
	public void UnicodeLocaleRejectsEmptyName() {
		Assert.Throws<ArgumentNullException>(
			() => new UnicodeTextLocaleProvider( null! )
		);
		Assert.Throws<ArgumentException>(
			() => new UnicodeTextLocaleProvider( " " )
		);
	}

	/// <summary>Verifies representative zero-, one-, two-, and indeterminate-width scalars.</summary>
	[Fact]
	public void UnicodeWidthProviderMeasuresRepresentativeScalars() {
		var provider = UnicodeDisplayWidthProvider.Instance;
		Assert.Equal( "16.0.0", UnicodeDisplayWidthProvider.UnicodeVersion );
		Assert.Equal( 0, provider.GetWidth( new Rune( 0 ) ) );
		Assert.Equal( 0, provider.GetWidth( new Rune( 0x0301 ) ) );
		Assert.Equal( 0, provider.GetWidth( new Rune( 0x200D ) ) );
		Assert.Equal( 1, provider.GetWidth( new Rune( 'A' ) ) );
		Assert.Equal( 1, provider.GetWidth( new Rune( 0x00A1 ) ) );
		Assert.Equal( 2, provider.GetWidth( new Rune( 0x2630 ) ) );
		Assert.Equal( 2, provider.GetWidth( new Rune( 0x754C ) ) );
		Assert.Equal( 2, provider.GetWidth( new Rune( 0x1F600 ) ) );
		Assert.Equal( -1, provider.GetWidth( new Rune( '\t' ) ) );
		Assert.Equal( -1, provider.GetWidth( new Rune( 0x0378 ) ) );
	}

	/// <summary>Verifies that opaque units use the caller-selected width.</summary>
	[Fact]
	public void TextUnitWidthUsesOpaqueByteFallback() {
		var provider = new ConstantWidthProvider( 7 );
		Assert.Equal(
			3,
			TextUnitDisplayWidth.GetWidth(
				ReadByteUnit( 0xFF ),
				provider,
				3
			)
		);
		Assert.Equal(
			7,
			TextUnitDisplayWidth.GetWidth(
				ReadScalarUnit( "A" ),
				provider,
				3
			)
		);
	}

	/// <summary>Verifies validation of opaque-byte width and provider inputs.</summary>
	[Fact]
	public void TextUnitWidthValidatesInputs() {
		var unit = ReadByteUnit( 0xFF );
		Assert.Throws<ArgumentNullException>(
			() => TextUnitDisplayWidth.GetWidth( unit, null! )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TextUnitDisplayWidth.GetWidth(
				unit,
				UnicodeDisplayWidthProvider.Instance,
				-1
			)
		);
	}

	private static TextUnit ReadByteUnit( byte value ) {
		using var input = new MemoryStream(
			new[] { value },
			writable: false
		);
		var unit = new TextUnitReader(
			input,
			TextDecodingMode.Bytes
		).Read();
		return unit ?? throw new InvalidOperationException(
			"The test input did not produce a text unit."
		);
	}

	private static TextUnit ReadScalarUnit( string value ) {
		using var input = new MemoryStream(
			Encoding.UTF8.GetBytes( value ),
			writable: false
		);
		var unit = new TextUnitReader( input ).Read();
		return unit ?? throw new InvalidOperationException(
			"The test input did not produce a text unit."
		);
	}

	private sealed class ConstantWidthProvider : IDisplayWidthProvider {
		private readonly int myWidth;

		/// <summary>Initializes a constant-width provider.</summary>
		/// <param name="width">The returned width.</param>
		public ConstantWidthProvider( int width ) {
			this.myWidth = width;
		}

		/// <inheritdoc/>
		public int GetWidth( Rune scalar ) => this.myWidth;
	}
}
