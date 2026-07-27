namespace Icod.CoreUtils.Who.Tests;

using System.Net;
using System.Runtime.CompilerServices;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;
using Tool = Icod.CoreUtils.Who.Command;
using Xunit;

public sealed class WhoCommandTests {
	[Fact]
	public async Task DefaultPrintsOnlyUserRecords() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( Array.Empty<string>(), output, new StringWriter(), CreateProvider() ) );
		Assert.Contains( "alice", output.ToString() );
		Assert.Contains( "bob", output.ToString() );
		Assert.DoesNotContain( "system boot", output.ToString() );
		Assert.DoesNotContain( "dead-id", output.ToString() );
	}

	[Fact]
	public async Task AllIncludesAccountingRecordTypes() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( new[] { "--all" }, output, new StringWriter(), CreateProvider() ) );
		Assert.Contains( "system boot", output.ToString() );
		Assert.Contains( "dead-id", output.ToString() );
		Assert.Contains( "run-level", output.ToString() );
	}

	[Fact]
	public async Task CountPrintsNamesAndCount() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( new[] { "-q" }, output, new StringWriter(), CreateProvider() ) );
		Assert.Equal( "alice bob" + Environment.NewLine + "# users=2" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task HeadingAndLongUserFormatAreAvailable() {
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( new[] { "-Huw" }, output, new StringWriter(), CreateProvider() ) );
		Assert.StartsWith( "NAME", output.ToString() );
		Assert.Contains( "PID", output.ToString() );
		Assert.Contains( "alice", output.ToString() );
	}

	[Fact]
	public async Task OneOperandSelectsFile() {
		var provider = CreateProvider();
		Assert.Equal( 0, await RunAsync( new[] { "records.utmp" }, new StringWriter(), new StringWriter(), provider ) );
		Assert.Equal( "records.utmp", provider.LastFileName );
	}

	[Fact]
	public async Task TwoOperandsMeanCurrentTerminal() {
		var provider = CreateProvider();
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( new[] { "am", "i" }, output, new StringWriter(), provider ) );
		Assert.DoesNotContain( "alice", output.ToString() );
		Assert.Contains( "bob", output.ToString() );
		Assert.Null( provider.LastFileName );
	}

	[Fact]
	public async Task ExtraOperandFails() {
		var error = new StringWriter();
		Assert.Equal( 1, await RunAsync( new[] { "one", "two", "three" }, new StringWriter(), error, CreateProvider() ) );
		Assert.Contains( "extra operand", error.ToString() );
	}

	[Fact]
	public async Task UnsupportedPlatformFailsCleanly() {
		var error = new StringWriter();
		Assert.Equal( 1, await RunAsync( Array.Empty<string>(), new StringWriter(), error, new FakeLoginRecordProvider( Array.Empty<LoginRecord>(), false ) ) );
		Assert.Contains( "not supported", error.ToString() );
	}

	[Fact]
	public async Task CancellationReturns130() {
		using var source = new CancellationTokenSource();
		source.Cancel();
		var context = new CommandContext( "who", TextReader.Null, new StringWriter(), new StringWriter(), cancellationToken: source.Token );
		Assert.Equal( 130, await Tool.RunAsync( Array.Empty<string>(), context, CreateProvider() ) );
	}

	private static Task<int> RunAsync( string[] args, StringWriter output, StringWriter error, ILoginRecordProvider provider ) => Tool.RunAsync(
		args,
		new CommandContext( "who", TextReader.Null, output, error ),
		provider
	);

	private static FakeLoginRecordProvider CreateProvider() => new( new[] {
		Record( LoginRecordType.UserProcess, 100, "pts/1", "u1", "alice", "host-a" ),
		Record( LoginRecordType.UserProcess, 101, "pts/2", "u2", "bob", "host-b" ),
		Record( LoginRecordType.BootTime, 0, "", "", "", "" ),
		Record( LoginRecordType.DeadProcess, 102, "pts/3", "dead-id", "", "" ),
		Record( LoginRecordType.RunLevel, ('3' | ('2' << 8)), "", "", "", "" )
	} );

	private static LoginRecord Record( LoginRecordType type, int pid, string line, string id, string user, string host ) => new(
		type, pid, line, id, user, host, 15, 1, 1, new DateTimeOffset( 2026, 1, 2, 3, 4, 0, TimeSpan.Zero ), IPAddress.Loopback
	);

	private sealed class FakeLoginRecordProvider( IReadOnlyList<LoginRecord> records, bool supported = true ) : ILoginRecordProvider {
		public bool IsSupported => supported;
		public string? LastFileName { get; private set; }
		public async IAsyncEnumerable<LoginRecord> ReadAsync( string? fileName = null, [EnumeratorCancellation] CancellationToken cancellationToken = default ) {
			this.LastFileName = fileName;
			foreach ( var record in records ) {
				cancellationToken.ThrowIfCancellationRequested();
				await Task.Yield();
				yield return record;
			}
		}
		public ValueTask<string?> GetStandardInputTerminalLineAsync( CancellationToken cancellationToken = default ) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult<string?>( "pts/2" ); }
	}
}
