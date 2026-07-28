namespace Icod.CoreUtils.Shared.Tests;

using System.Runtime.InteropServices;
using Icod.CoreUtils.Shared.BinaryFormatting;
using Xunit;

public sealed class BinaryFormattingTests {
	[Fact]
	public void ParserAcceptsConcatenatedFormatsAndTrailers() {
		var success = BinaryFormatParser.TryParse( "x1zod2", out var formats, out var error );
		Assert.True( success, error );
		Assert.Equal( 3, formats.Count );
		Assert.True( formats[ 0 ].AppendPrintableTrailer );
		Assert.Equal( BinaryFormatKind.Octal, formats[ 1 ].Kind );
		Assert.Equal( BinaryFormatKind.SignedDecimal, formats[ 2 ].Kind );
	}

	[Fact]
	public void IntegralAliasesFollowDocumentedHostAbiRules() {
		Assert.True( BinaryFormatParser.TryParse( "dCdSdIdL", out var formats, out var error ), error );
		Assert.Equal( new[] { 1, 2, 4, OperatingSystem.IsWindows() ? 4 : IntPtr.Size }, formats.Select( value => value.Size ) );
	}

	[Fact]
	public void NativeExtendedLongDoubleIsRejectedWhenItCannotBeRepresented() {
		var success = BinaryFormatParser.TryParse( "fL", out _, out _ );
		var representable = OperatingSystem.IsWindows()
			|| (
				OperatingSystem.IsMacOS()
				&& Architecture.Arm64 == RuntimeInformation.ProcessArchitecture
			)
		;
		Assert.Equal( representable, success );
	}

	[Fact]
	public void InvalidFormatCharactersAreRejected() {
		Assert.False( BinaryFormatParser.TryParse( "q1", out _, out var error ) );
		Assert.Contains( "invalid character", error ?? string.Empty );
	}

	[Fact]
	public void LineWidthUsesLeastCommonMultiple() {
		Assert.True( BinaryFormatParser.TryParse( "x2d4", out var formats, out var parseError ), parseError );
		Assert.True( BinaryLineLayout.TryResolveWidth( formats, 12, true, out var width, out var widthMessage ), widthMessage );
		Assert.Equal( 12, width );

		Assert.True( BinaryLineLayout.TryResolveWidth( formats, 10, true, out width, out widthMessage ) );
		Assert.Equal( 4, width );
		Assert.Equal( "warning: invalid width 10; using 4 instead", widthMessage );
	}

	[Fact]
	public void FloatingPointWithoutSizeDefaultsToDouble() {
		Assert.True( BinaryFormatParser.TryParse( "f", out var formats, out var error ), error );
		Assert.Single( formats );
		Assert.Equal( 8, formats[ 0 ].Size );
	}

	[Fact]
	public void PaddingIsDistributedWithoutLosingColumns() {
		var padding = BinaryLineLayout.DistributeLeadingPadding( 4, 7 );
		Assert.Equal( 7, padding.Sum() );
		Assert.All( padding, value => Assert.InRange( value, 1, 2 ) );
	}

	[Fact]
	public void IntegerFormattingHonorsByteOrder() {
		var format = new BinaryFormatSpecification( BinaryFormatKind.Hexadecimal, 2, false, "x2" );
		Assert.Equal( "201", BinaryValueFormatter.Format( format, new byte[] { 1, 2 }, BinaryByteOrder.LittleEndian ) );
		Assert.Equal( "102", BinaryValueFormatter.Format( format, new byte[] { 1, 2 }, BinaryByteOrder.BigEndian ) );
	}

	[Fact]
	public void CharacterFormattingUsesGnuEscapeNames() {
		var named = new BinaryFormatSpecification( BinaryFormatKind.NamedCharacter, 1, false, "a" );
		var escaped = new BinaryFormatSpecification( BinaryFormatKind.Character, 1, false, "c" );
		Assert.Equal( "nl", BinaryValueFormatter.Format( named, new byte[] { 10 }, BinaryByteOrder.Native ) );
		Assert.Equal( " \\n", BinaryValueFormatter.Format( escaped, new byte[] { 10 }, BinaryByteOrder.Native ) );
	}

	[Fact]
	public void BFloat16OneFormatsAsOne() {
		var format = new BinaryFormatSpecification( BinaryFormatKind.FloatingPoint, 2, false, "fB", 'B' );
		Assert.Equal( "1", BinaryValueFormatter.Format( format, new byte[] { 0x80, 0x3f }, BinaryByteOrder.LittleEndian ) );
	}
}
