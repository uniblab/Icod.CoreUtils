namespace Icod.CoreUtils.WhoAmI.Tests;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.Platform;
using Tool = Icod.CoreUtils.WhoAmI.Command;
using Xunit;

public sealed class WhoAmICommandTests {
	[Fact]
	public async Task PrintsEffectiveUserRatherThanRealOrLoginUser() {
		var output = new StringWriter();
		var context = new CommandContext( "whoami", TextReader.Null, output, new StringWriter() );
		Assert.Equal( 0, await Tool.RunAsync( Array.Empty<string>(), context, FakeIdentityProvider.Create() ) );
		Assert.Equal( "effective" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task OperandFails() {
		var error = new StringWriter();
		var context = new CommandContext( "whoami", TextReader.Null, TextWriter.Null, error );
		Assert.Equal( 1, await Tool.RunAsync( new[] { "someone" }, context, FakeIdentityProvider.Create() ) );
		Assert.Contains( "extra operand", error.ToString() );
	}

	[Fact]
	public async Task HelpAndVersionWork() {
		Assert.Equal( 0, await Tool.RunAsync( new[] { "--help" }, new CommandContext( "whoami", TextReader.Null, new StringWriter(), new StringWriter() ), FakeIdentityProvider.Create() ) );
		Assert.Equal( 0, await Tool.RunAsync( new[] { "--version" }, new CommandContext( "whoami", TextReader.Null, new StringWriter(), new StringWriter() ), FakeIdentityProvider.Create() ) );
	}

	[Fact]
	public async Task SystemProviderReturnsAUsableEffectiveIdentity() {
		var identity = await SystemIdentityProvider.Instance.GetCurrentAsync();
		Assert.False( string.IsNullOrWhiteSpace( identity.EffectiveUser.Id ) );
		Assert.False( string.IsNullOrWhiteSpace( identity.EffectiveUser.Name ) );
	}

	[Fact]
	public async Task CancellationReturns130() {
		using var source = new CancellationTokenSource();
		source.Cancel();
		var context = new CommandContext( "whoami", TextReader.Null, new StringWriter(), new StringWriter(), cancellationToken: source.Token );
		Assert.Equal( 130, await Tool.RunAsync( Array.Empty<string>(), context, FakeIdentityProvider.Create() ) );
	}

	private sealed class FakeIdentityProvider( ProcessIdentity current ) : IIdentityProvider {
		public static FakeIdentityProvider Create() {
			var users = new GroupIdentity( "100", "users" );
			var root = new GroupIdentity( "0", "root" );
			var real = new UserIdentity( "1000", "real", users, new[] { users } );
			var effective = new UserIdentity( "0", "effective", root, new[] { root } );
			return new FakeIdentityProvider( new ProcessIdentity( real, effective, users, root, new[] { root }, null ) );
		}
		public ValueTask<ProcessIdentity> GetCurrentAsync( CancellationToken cancellationToken = default ) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult( current ); }
		public ValueTask<UserIdentity?> FindUserAsync( string userName, CancellationToken cancellationToken = default ) => ValueTask.FromResult<UserIdentity?>( null );
		public ValueTask<UserIdentity?> FindUserByIdAsync( string userId, CancellationToken cancellationToken = default ) => ValueTask.FromResult<UserIdentity?>( null );

		public ValueTask<string?> GetLoginNameAsync( CancellationToken cancellationToken = default ) => ValueTask.FromResult<string?>( "login" );
	}
}
