namespace Icod.DiffUtils.Shared.Tests;

using Icod.CommandFramework.Diagnostics;
using Icod.DiffUtils.Shared;
using Xunit;

/// <summary>Tests the suite-wide comparison contracts.</summary>
public sealed class ComparisonContractsTests {
	/// <summary>Verifies the GNU Diffutils status values.</summary>
	[Fact]
	public void StatusValuesMatchDiffutilsContract() {
		Assert.Equal( 0, (int)ComparisonStatus.Equal );
		Assert.Equal( 1, (int)ComparisonStatus.Different );
		Assert.Equal( 2, (int)ComparisonStatus.Trouble );
	}

	/// <summary>Verifies standard-input normalization and display naming.</summary>
	[Theory]
	[InlineData( null )]
	[InlineData( "" )]
	[InlineData( "-" )]
	public void ComparisonInputNormalizesStandardInput( string? value ) {
		var input = ComparisonInput.Create( value );
		Assert.True( input.IsStandardInput );
		Assert.Equal( "-", input.Value );
		Assert.Equal( "standard input", input.DisplayName );
	}

	/// <summary>Verifies that binary standard input is borrowed rather than owned.</summary>
	[Fact]
	public async Task StandardInputSourceLeavesCallerStreamOpen() {
		var bytes = new MemoryStream( new byte[] { 0, 255, 10 } );
		var context = new CommandContext(
			"test",
			TextReader.Null,
			TextWriter.Null,
			TextWriter.Null,
			bytes
		);
		var input = ComparisonInput.Create( "-" );
		await using ( var source = input.OpenBinary( context ) ) {
			Assert.Same( bytes, source.BinaryStream );
			var actual = new byte[3];
			var read = await source.BinaryStream!.ReadAsync( actual );
			Assert.Equal( 3, read );
			Assert.Equal( new byte[] { 0, 255, 10 }, actual );
		}
		Assert.True( bytes.CanRead );
	}
}
