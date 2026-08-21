namespace Icod.CoreUtils.LogName.Tests;

using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.Platform;
using Tool = Icod.CoreUtils.LogName.Command;
using Xunit;

public sealed class LogNameCommandTests {
	[Fact]
	public async Task PrintsLoginSessionName() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( Array.Empty<string>(), output, "session-user" ) );
		Assert.Equal( "session-user" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task MissingLoginNameFails() {
		var error = new StringWriter();
		var context = new CommandContext( "logname", TextReader.Null, TextWriter.Null, error );
		Assert.Equal( 1, await Tool.RunAsync( Array.Empty<string>(), context, new FakeIdentityProvider( null ) ) );
		Assert.Contains( "no login name", error.ToString() );
	}

	[Fact]
	public async Task ShortHelpOptionIsNotInvented() {
		var error = new StringWriter();
		var context = new CommandContext( "logname", TextReader.Null, TextWriter.Null, error );
		Assert.Equal( 1, await Tool.RunAsync( new[] { "-h" }, context, new FakeIdentityProvider( "login" ) ) );
		Assert.Contains( "invalid option", error.ToString().ToLowerInvariant() );
	}

	[Fact]
	public async Task HelpAndVersionWork() {
		Assert.Equal( 0, await RunAsync( new[] { "--help" }, new StringWriter(), "login" ) );
		Assert.Equal( 0, await RunAsync( new[] { "--version" }, new StringWriter(), "login" ) );
	}

	[Fact]
	public async Task CancellationReturns130() {
		using var source = new CancellationTokenSource();
		source.Cancel();
		var context = new CommandContext( "logname", TextReader.Null, new StringWriter(), new StringWriter(), cancellationToken: source.Token );
		Assert.Equal( 130, await Tool.RunAsync( Array.Empty<string>(), context, new FakeIdentityProvider( "login" ) ) );
	}

	private static Task<int> RunAsync( string[] args, StringWriter output, string? loginName ) => Tool.RunAsync(
		args,
		new CommandContext( "logname", TextReader.Null, output, new StringWriter() ),
		new FakeIdentityProvider( loginName )
	);

	private sealed class FakeIdentityProvider( string? loginName ) : IIdentityProvider {
		public ValueTask<ProcessIdentity> GetCurrentAsync( CancellationToken cancellationToken = default ) => throw new NotSupportedException();
		public ValueTask<UserIdentity?> FindUserAsync( string userName, CancellationToken cancellationToken = default ) => throw new NotSupportedException();
		public ValueTask<UserIdentity?> FindUserByIdAsync( string userId, CancellationToken cancellationToken = default ) => throw new NotSupportedException();

		public ValueTask<string?> GetLoginNameAsync( CancellationToken cancellationToken = default ) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult( loginName ); }
	}
}
