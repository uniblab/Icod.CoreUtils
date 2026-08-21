namespace Icod.CoreUtils.UName.Tests;

using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CoreUtils.Shared.Platform;
using Tool = Icod.CoreUtils.UName.Command;
using Xunit;

public sealed class UNameCommandTests {
	private static readonly SystemInformationSnapshot Information = new(
		"Kernel", "node", "1.2.3", "version text", "machine", "processor", "platform", "Operating System"
	);

	[Fact]
	public async Task DefaultPrintsKernelName() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( Array.Empty<string>(), output ) );
		Assert.Equal( "Kernel" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task SelectedFieldsUseDocumentedOrder() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( new[] { "-omns" }, output ) );
		Assert.Equal( "Kernel node machine Operating System" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task AllOmitsUnknownNonportableFields() {
		var output = new StringWriter();
		var provider = new FakeProvider( Information with { Processor = "unknown", HardwarePlatform = "unknown" } );
		Assert.Equal( 0, await RunAsync( new[] { "--all" }, output, provider ) );
		Assert.Equal( "Kernel node 1.2.3 version text machine Operating System" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task ExplicitUnknownFieldIsPrinted() {
		var output = new StringWriter();
		var provider = new FakeProvider( Information with { Processor = "unknown" } );
		Assert.Equal( 0, await RunAsync( new[] { "--processor" }, output, provider ) );
		Assert.Equal( "unknown" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task ExtraOperandFails() {
		var error = new StringWriter();
		var context = new CommandContext( "uname", TextReader.Null, TextWriter.Null, error );
		Assert.Equal( 1, await Tool.RunAsync( new[] { "operand" }, context, new FakeProvider( Information ) ) );
		Assert.Contains( "extra operand", error.ToString() );
	}

	[Fact]
	public async Task HelpAndVersionWork() {
		Assert.Equal( 0, await RunAsync( new[] { "--help" }, new StringWriter() ) );
		Assert.Equal( 0, await RunAsync( new[] { "--version" }, new StringWriter() ) );
	}

	[Fact]
	public async Task SystemProviderReturnsUsablePlatformInformation() {
		var information = await SystemInformationProvider.Instance.GetAsync();
		Assert.False( string.IsNullOrWhiteSpace( information.KernelName ) );
		Assert.False( string.IsNullOrWhiteSpace( information.NodeName ) );
		Assert.False( string.IsNullOrWhiteSpace( information.Machine ) );
	}

	[Fact]
	public async Task CancellationReturns130() {
		using var source = new CancellationTokenSource();
		source.Cancel();
		var context = new CommandContext( "uname", TextReader.Null, new StringWriter(), new StringWriter(), cancellationToken: source.Token );
		Assert.Equal( 130, await Tool.RunAsync( Array.Empty<string>(), context, new FakeProvider( Information ) ) );
	}

	private static Task<int> RunAsync( string[] args, StringWriter output, ISystemInformationProvider? provider = null ) {
		var context = new CommandContext( "uname", TextReader.Null, output, new StringWriter() );
		return Tool.RunAsync( args, context, provider ?? new FakeProvider( Information ) );
	}

	private sealed class FakeProvider( SystemInformationSnapshot information ) : ISystemInformationProvider {
		public ValueTask<SystemInformationSnapshot> GetAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( information );
		}
	}
}
