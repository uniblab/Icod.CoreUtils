namespace Icod.CoreUtils.Shred.Tests;

using Icod.CoreUtils.Shred;
using Xunit;

/// <summary>Verifies the GNU-compatible <c>shred</c> option model.</summary>
public sealed class ShredOptionsTests {
	/// <summary>Verifies the default pass count and preservation policy.</summary>
	[Fact]
	public void UsesDocumentedDefaults() {
		var options = ShredOptions.Parse( [ "payload.bin" ] );

		Assert.Equal( 3, options.Iterations );
		Assert.Equal( ShredRemovalMode.None, options.RemovalMode );
		Assert.False( options.Exact );
		Assert.False( options.Zero );
		Assert.Equal( new[] { "payload.bin" }, options.Targets );
	}

	/// <summary>Verifies long options and their values.</summary>
	[Fact]
	public void ParsesLongOptions() {
		var options = ShredOptions.Parse( [
			"--force",
			"--iterations=7",
			"--random-source", "random.bin",
			"--size=4KiB",
			"--remove=wipe",
			"--verbose",
			"--exact",
			"--zero",
			"payload.bin"
		] );

		Assert.True( options.Force );
		Assert.Equal( 7, options.Iterations );
		Assert.Equal( "random.bin", options.RandomSourcePath );
		Assert.Equal( 4096UL, options.Size );
		Assert.Equal( ShredRemovalMode.Wipe, options.RemovalMode );
		Assert.True( options.Verbose );
		Assert.True( options.Exact );
		Assert.True( options.Zero );
	}

	/// <summary>Verifies combined flags and attached values.</summary>
	[Fact]
	public void ParsesCombinedShortOptions() {
		var options = ShredOptions.Parse( [ "-fvzxn2", "-s16", "payload.bin" ] );

		Assert.True( options.Force );
		Assert.True( options.Verbose );
		Assert.True( options.Zero );
		Assert.True( options.Exact );
		Assert.Equal( 2, options.Iterations );
		Assert.Equal( 16UL, options.Size );
	}

	/// <summary>Verifies GNU size suffixes.</summary>
	/// <param name="text">The size operand.</param>
	/// <param name="expected">The expected byte count.</param>
	[Theory]
	[InlineData( "1", 1UL )]
	[InlineData( "2b", 1024UL )]
	[InlineData( "3K", 3072UL )]
	[InlineData( "4KB", 4000UL )]
	[InlineData( "5KiB", 5120UL )]
	[InlineData( "6M", 6291456UL )]
	public void ParsesSizeSuffixes( string text, ulong expected ) {
		Assert.Equal( expected, ShredOptions.ParseSize( text ) );
	}

	/// <summary>Verifies controlled rejection of invalid removal methods.</summary>
	[Theory]
	[InlineData( "--remove=erase" )]
	[InlineData( "--remove=" )]
	public void RejectsInvalidRemovalMethod( string argument ) {
		Assert.Throws<ShredUsageException>( () => ShredOptions.Parse( [ argument, "payload.bin" ] ) );
	}

	/// <summary>Verifies that standard output cannot be combined with removal.</summary>
	[Fact]
	public void RejectsRemovingStandardOutput() {
		Assert.Throws<ShredUsageException>( () => ShredOptions.Parse( [ "-u", "-" ] ) );
	}

	/// <summary>Verifies that a target operand is mandatory for normal execution.</summary>
	[Fact]
	public void RejectsMissingOperand() {
		Assert.Throws<ShredUsageException>( () => ShredOptions.Parse( [] ) );
	}
}
