namespace Icod.CoreUtils.NProc.Tests;

using Icod.CoreUtils.NProc;
using Xunit;

/// <summary>Exercises the public <c>nproc</c> command boundary.</summary>
public sealed class CommandTests {
	/// <summary>Verifies normal command output from an injected provider.</summary>
	[Fact]
	public async Task PrintsResolvedCount() {
		var provider = Provider( ProcessorSnapshotFactory.Create( available: 6 ) );
		var output = new StringWriter();

		var status = await Command.RunAsync(
			[],
			stdout: output,
			provider: provider,
			environment: new TestNProcEnvironment()
		);

		Assert.Equal( 0, status );
		Assert.Equal( string.Concat( "6", Environment.NewLine ), output.ToString() );
	}

	/// <summary>Verifies attached and separate ignore syntax.</summary>
	[Theory]
	[InlineData( "--ignore=2", null )]
	[InlineData( "--ignore", "2" )]
	public async Task AcceptsIgnoreSyntax( string first, string? second ) {
		var output = new StringWriter();
		var args = second is null ? new[] { first } : new[] { first, second };

		var status = await Command.RunAsync(
			args,
			stdout: output,
			provider: Provider( ProcessorSnapshotFactory.Create( available: 5 ) ),
			environment: new TestNProcEnvironment()
		);

		Assert.Equal( 0, status );
		Assert.Equal( string.Concat( "3", Environment.NewLine ), output.ToString() );
	}

	/// <summary>Verifies malformed values and operands are rejected.</summary>
	[Theory]
	[InlineData( "--ignore=-1" )]
	[InlineData( "--ignore=x" )]
	[InlineData( "operand" )]
	public async Task RejectsInvalidUsage( string argument ) {
		var provider = Provider( ProcessorSnapshotFactory.Create( available: 4 ) );
		var error = new StringWriter();

		var status = await Command.RunAsync( [argument], stderr: error, provider: provider );

		Assert.Equal( 1, status );
		Assert.Equal( 0, provider.CallCount );
		Assert.Contains( "nproc:", error.ToString(), StringComparison.Ordinal );
	}

	/// <summary>Verifies informational options do not query processor resources.</summary>
	[Theory]
	[InlineData( "--help" )]
	[InlineData( "--version" )]
	public async Task InformationalOptionsDoNotQueryProvider( string option ) {
		var provider = new TestProcessorResourceProvider(
			_ => throw new InvalidOperationException( "provider should not be called" )
		);
		var output = new StringWriter();

		var status = await Command.RunAsync( [option], stdout: output, provider: provider );

		Assert.Equal( 0, status );
		Assert.Equal( 0, provider.CallCount );
	}

	/// <summary>Verifies provider exceptions become controlled failures.</summary>
	[Fact]
	public async Task ReportsProviderFailure() {
		var provider = new TestProcessorResourceProvider(
			_ => throw new InvalidOperationException( "provider failure" )
		);
		var error = new StringWriter();

		var status = await Command.RunAsync( [], stderr: error, provider: provider );

		Assert.Equal( 1, status );
		Assert.Contains( "provider failure", error.ToString(), StringComparison.Ordinal );
	}

	private static TestProcessorResourceProvider Provider(
		Icod.Host.ProcessorResourceSnapshot snapshot
	) {
		return new TestProcessorResourceProvider( _ => ValueTask.FromResult( snapshot ) );
	}
}
