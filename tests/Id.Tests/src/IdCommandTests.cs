namespace Icod.CoreUtils.Id.Tests;

using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.Platform;
using Tool = Icod.CoreUtils.ID.Command;
using Xunit;

public sealed class IdCommandTests {
	[Fact]
	public async Task DefaultFormatIncludesRealEffectiveGroupsAndContext() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( Array.Empty<string>(), output, new StringWriter() ) );
		Assert.Equal( "uid=1000(alice) gid=10(staff) euid=0(root) egid=0(wheel) groups=0(wheel),10(staff) context=test_u:test_r:test_t:s0" + Environment.NewLine, output.ToString() );
	}

	[Theory]
	[InlineData( "-un", "root" )]
	[InlineData( "-ur", "1000" )]
	[InlineData( "-gnr", "staff" )]
	[InlineData( "-Gn", "staff wheel" )]
	public async Task SelectedFormsWork( string option, string expected ) {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( new[] { option }, output, new StringWriter() ) );
		Assert.Equal( expected + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task ZeroTerminatesWithoutNewline() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( new[] { "-uz" }, output, new StringWriter() ) );
		Assert.Equal( "0\0", output.ToString() );
	}

	[Fact]
	public async Task MultipleNamedUsersAreSupported() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( new[] { "-un", "alice", "bob" }, output, new StringWriter() ) );
		Assert.Equal( "alice" + Environment.NewLine + "bob" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task NumericUserIdOperandsAreResolvedForId() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( new[] { "-un", "1001" }, output, new StringWriter() ) );
		Assert.Equal( "bob" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task SystemProviderAcceptsPlusAndLeadingZerosForNumericUserIds() {
		if ( !(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD()) ) return;
		var current = await SystemIdentityProvider.Instance.GetCurrentAsync();
		var resolved = await SystemIdentityProvider.Instance.FindUserByIdAsync( "+000000" + current.EffectiveUser.Id );
		Assert.NotNull( resolved );
		Assert.Equal( current.EffectiveUser.Id, resolved.Id );
		Assert.Null( await SystemIdentityProvider.Instance.FindUserByIdAsync( "-" + current.EffectiveUser.Id ) );
	}

	[Fact]
	public async Task UnknownUserDoesNotPreventFollowingUser() {
		var output = new StringWriter();
		var error = new StringWriter();
		Assert.Equal( 1, await RunAsync( new[] { "-u", "missing", "bob" }, output, error ) );
		Assert.Contains( "no such user", error.ToString() );
		Assert.Equal( "1001" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task ContextIsAvailableSeparately() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( new[] { "-Z" }, output, new StringWriter() ) );
		Assert.Equal( "test_u:test_r:test_t:s0" + Environment.NewLine, output.ToString() );
	}

	[Theory]
	[InlineData( "-ug", "more than one choice" )]
	[InlineData( "-n", "requires -u, -g, or -G" )]
	[InlineData( "-z", "not permitted" )]
	public async Task InvalidCombinationsFail( string option, string expectedError ) {
		var error = new StringWriter();
		Assert.Equal( 1, await RunAsync( new[] { option }, new StringWriter(), error ) );
		Assert.Contains( expectedError, error.ToString() );
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
		var context = new CommandContext( "id", TextReader.Null, new StringWriter(), new StringWriter(), cancellationToken: source.Token );
		Assert.Equal( 130, await Tool.RunAsync( Array.Empty<string>(), context, FakeIdentityProvider.Create() ) );
	}

	private static Task<int> RunAsync( string[] args, StringWriter output, StringWriter error ) => Tool.RunAsync(
		args,
		new CommandContext( "id", TextReader.Null, output, error ),
		FakeIdentityProvider.Create()
	);

	private sealed class FakeIdentityProvider( ProcessIdentity current, IReadOnlyDictionary<string, UserIdentity> users ) : IIdentityProvider {
		public static FakeIdentityProvider Create() {
			var staff = new GroupIdentity( "10", "staff" );
			var wheel = new GroupIdentity( "0", "wheel" );
			var guests = new GroupIdentity( "20", "guests" );
			var alice = new UserIdentity( "1000", "alice", staff, new[] { staff } );
			var root = new UserIdentity( "0", "root", wheel, new[] { wheel } );
			var bob = new UserIdentity( "1001", "bob", guests, new[] { guests, staff } );
			var current = new ProcessIdentity( alice, root, staff, wheel, new[] { wheel, staff }, "test_u:test_r:test_t:s0" );
			return new FakeIdentityProvider( current, new Dictionary<string, UserIdentity>( StringComparer.Ordinal ) { ["alice"] = alice, ["root"] = root, ["bob"] = bob } );
		}
		public ValueTask<ProcessIdentity> GetCurrentAsync( CancellationToken cancellationToken = default ) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult( current ); }
		public ValueTask<UserIdentity?> FindUserAsync( string userName, CancellationToken cancellationToken = default ) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult( users.TryGetValue( userName, out var user ) ? user : null ); }
		public ValueTask<UserIdentity?> FindUserByIdAsync( string userId, CancellationToken cancellationToken = default ) { cancellationToken.ThrowIfCancellationRequested(); var normalized = userId.TrimStart( '+' ).TrimStart( '0' ); if ( 0 == normalized.Length ) normalized = "0"; return ValueTask.FromResult( users.Values.FirstOrDefault( user => user.Id == normalized ) ); }

		public ValueTask<string?> GetLoginNameAsync( CancellationToken cancellationToken = default ) => ValueTask.FromResult<string?>( "alice" );
	}
}
