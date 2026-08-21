namespace Icod.CoreUtils.Shared.Tests.FileSystem.Usage;

using Icod.CoreUtils.Shared.FileSystem.Usage;
using Icod.CommandFramework.Terminal;
using Xunit;

/// <summary>Verifies GNU block-size resolution and formatting policy.</summary>
public sealed class UsageSizePolicyTests {
	/// <summary>Verifies command-specific environment precedence.</summary>
	[Fact]
	public void ResolvesCommandEnvironmentBeforeGenericValues() {
		var environment = new FakeEnvironmentVariableProvider( new Dictionary<string, string?> {
			[ "DU_BLOCK_SIZE" ] = "2K",
			[ "BLOCK_SIZE" ] = "4K",
			[ "BLOCKSIZE" ] = "8K"
		} );

		var policy = UsageSizePolicy.Resolve( null, "DU_BLOCK_SIZE", environment );

		Assert.Equal( UsageSizeStyle.Blocks, policy.Style );
		Assert.Equal( 2048UL, policy.BlockSize );
		Assert.Equal( "2", policy.Format( 4096 ) );
	}

	/// <summary>Verifies POSIXLY_CORRECT changes the fallback unit to 512 bytes.</summary>
	[Fact]
	public void ResolvesPosixFallback() {
		var environment = new FakeEnvironmentVariableProvider( new Dictionary<string, string?> {
			[ "POSIXLY_CORRECT" ] = "1"
		} );

		var policy = UsageSizePolicy.Resolve( null, "DF_BLOCK_SIZE", environment );

		Assert.Equal( 512UL, policy.BlockSize );
		Assert.Equal( "2", policy.Format( 1024 ) );
	}

	/// <summary>Verifies binary and decimal suffixes remain distinct.</summary>
	[Theory]
	[InlineData( "K", 1024UL )]
	[InlineData( "KiB", 1024UL )]
	[InlineData( "KB", 1000UL )]
	[InlineData( "2M", 2097152UL )]
	public void ParsesBlockSizes( string text, ulong expected ) {
		Assert.Equal( expected, UsageSizePolicy.ParseBlockSize( text ) );
	}

	/// <summary>Verifies human-readable formats use their selected radix.</summary>
	[Fact]
	public void FormatsHumanAndSiValues() {
		Assert.Equal( "1.5K", new UsageSizePolicy( UsageSizeStyle.HumanReadable, 1 ).Format( 1536 ) );
		Assert.Equal( "1.5K", new UsageSizePolicy( UsageSizeStyle.Si, 1 ).Format( 1500 ) );
	}

	private sealed class FakeEnvironmentVariableProvider : IEnvironmentVariableProvider {
		private readonly IReadOnlyDictionary<string, string?> values;

		/// <summary>Initializes the dictionary-backed provider.</summary>
		public FakeEnvironmentVariableProvider( IReadOnlyDictionary<string, string?> values ) {
			this.values = values;
		}

		/// <inheritdoc/>
		public string? GetValue( string name ) => values.TryGetValue( name, out var value ) ? value : null;
	}
}
