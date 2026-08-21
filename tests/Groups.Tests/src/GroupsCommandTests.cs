namespace Icod.CoreUtils.Groups.Tests;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.Platform;
using Tool = Icod.CoreUtils.Groups.Command;
using Xunit;

public sealed class GroupsCommandTests {
	[Fact]
	public async Task CurrentProcessHasNoUserPrefix() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( Array.Empty<string>(), output, new StringWriter() ) );
		Assert.Equal( "staff wheel" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task NamedUsersHavePrefixesAndPrimaryGroupFirst() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( new[] { "alice", "bob" }, output, new StringWriter() ) );
		Assert.Equal( "alice : staff wheel" + Environment.NewLine + "bob : guests" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task UnknownUserDoesNotPreventFollowingUsers() {
		var output = new StringWriter();
		var error = new StringWriter();
		Assert.Equal( 1, await RunAsync( new[] { "missing", "bob" }, output, error ) );
		Assert.Contains( "no such user", error.ToString() );
		Assert.Equal( "bob : guests" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task LargeOperandSetStreamsAllLines() {
		var names = Enumerable.Repeat( "bob", 500 ).ToArray();
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( names, output, new StringWriter() ) );
		Assert.Equal( 500, output.ToString().Split( Environment.NewLine, StringSplitOptions.RemoveEmptyEntries ).Length );
	}

	[Fact]
	public async Task HelpAndVersionWork() {
		Assert.Equal( 0, await RunAsync( new[] { "--help" }, new StringWriter(), new StringWriter() ) );
		Assert.Equal( 0, await RunAsync( new[] { "--version" }, new StringWriter(), new StringWriter() ) );
	}

	[Fact]
	public async Task CancellationReturns130() {
		using var source = new CancellationTokenSource();
		source.Cancel();
		var context = new CommandContext( "groups", TextReader.Null, new StringWriter(), new StringWriter(), cancellationToken: source.Token );
		Assert.Equal( 130, await Tool.RunAsync( Array.Empty<string>(), context, FakeIdentityProvider.Create() ) );
	}

	private static Task<int> RunAsync( string[] args, StringWriter output, StringWriter error ) => Tool.RunAsync(
		args,
		new CommandContext( "groups", TextReader.Null, output, error ),
		FakeIdentityProvider.Create()
	);

	private sealed class FakeIdentityProvider( ProcessIdentity current, IReadOnlyDictionary<string, UserIdentity> users ) : IIdentityProvider {
		public static FakeIdentityProvider Create() {
			var staff = new GroupIdentity( "10", "staff" );
			var wheel = new GroupIdentity( "0", "wheel" );
			var guests = new GroupIdentity( "20", "guests" );
			var alice = new UserIdentity( "1000", "alice", staff, new[] { wheel, staff } );
			var bob = new UserIdentity( "1001", "bob", guests, new[] { guests } );
			var current = new ProcessIdentity( alice, alice, staff, staff, new[] { staff, wheel }, null );
			return new FakeIdentityProvider( current, new Dictionary<string, UserIdentity>( StringComparer.Ordinal ) { ["alice"] = alice, ["bob"] = bob } );
		}
		public ValueTask<ProcessIdentity> GetCurrentAsync( CancellationToken cancellationToken = default ) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult( current ); }
		public ValueTask<UserIdentity?> FindUserAsync( string userName, CancellationToken cancellationToken = default ) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult( users.TryGetValue( userName, out var user ) ? user : null ); }
		public ValueTask<UserIdentity?> FindUserByIdAsync( string userId, CancellationToken cancellationToken = default ) => ValueTask.FromResult<UserIdentity?>( null );

		public ValueTask<string?> GetLoginNameAsync( CancellationToken cancellationToken = default ) => ValueTask.FromResult<string?>( null );
	}
}
