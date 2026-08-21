namespace Icod.CoreUtils.HostId.Tests;

using Icod.CoreUtils.HostId;
using Icod.CommandFramework.Host;
using Xunit;

/// <summary>Exercises the public <c>hostid</c> command boundary.</summary>
public sealed class CommandTests {
	/// <summary>Verifies fixed-width lowercase hexadecimal presentation.</summary>
	[Fact]
	public async Task PrintsNormalizedIdentifier() {
		var provider = AvailableProvider( 0x00ABCDEFu );
		var output = new StringWriter();
		var error = new StringWriter();

		var status = await Command.RunAsync(
			[],
			stdout: output,
			stderr: error,
			provider: provider
		);

		Assert.Equal( 0, status );
		Assert.Equal( string.Concat( "00abcdef", Environment.NewLine ), output.ToString() );
		Assert.Equal( string.Empty, error.ToString() );
	}

	/// <summary>Verifies controlled diagnostics when the fact is unavailable.</summary>
	[Fact]
	public async Task ReportsUnavailableIdentifier() {
		var provider = new TestHostIdentifierProvider(
			_ => ValueTask.FromResult(
				HostResourceValue<HostIdentifier>.Unavailable( "no stable identifier" )
			)
		);
		var error = new StringWriter();

		var status = await Command.RunAsync( [], stderr: error, provider: provider );

		Assert.Equal( 1, status );
		Assert.Contains( "no stable identifier", error.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies that informational options do not query the host.</summary>
	[Theory]
	[InlineData( "--help" )]
	[InlineData( "--version" )]
	public async Task InformationalOptionsDoNotQueryProvider( string option ) {
		var provider = new TestHostIdentifierProvider(
			_ => throw new InvalidOperationException( "provider should not be called" )
		);
		var output = new StringWriter();

		var status = await Command.RunAsync( [option], stdout: output, provider: provider );

		Assert.Equal( 0, status );
		Assert.Equal( 0, provider.CallCount );
		Assert.NotEmpty( output.ToString() );
	}

	/// <summary>Verifies that operands are rejected before provider access.</summary>
	[Fact]
	public async Task RejectsOperands() {
		var provider = AvailableProvider( 1 );
		var error = new StringWriter();

		var status = await Command.RunAsync( ["unexpected"], stderr: error, provider: provider );

		Assert.Equal( 1, status );
		Assert.Equal( 0, provider.CallCount );
		Assert.Contains( "extra operand", error.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies provider exceptions become controlled failures.</summary>
	[Fact]
	public async Task ReportsProviderFailure() {
		var provider = new TestHostIdentifierProvider(
			_ => throw new InvalidOperationException( "provider failure" )
		);
		var error = new StringWriter();

		var status = await Command.RunAsync( [], stderr: error, provider: provider );

		Assert.Equal( 1, status );
		Assert.Contains( "provider failure", error.ToString(), StringComparison.Ordinal );
	}

	private static TestHostIdentifierProvider AvailableProvider( uint value ) {
		return new TestHostIdentifierProvider(
			_ => ValueTask.FromResult(
				HostResourceValue<HostIdentifier>.Available(
					new HostIdentifier( value, "test" ),
					HostResourceProvenance.Derived
				)
			)
		);
	}
}
