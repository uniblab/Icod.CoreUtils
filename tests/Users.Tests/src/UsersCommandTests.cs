namespace Icod.CoreUtils.Users.Tests;

using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CoreUtils.Shared.Platform;
using Tool = Icod.CoreUtils.Users.Command;
using Xunit;

public sealed class UsersCommandTests {
	[Fact]
	public async Task FiltersSortsAndPreservesDuplicateSessions() {
		var provider = new FakeLoginRecordProvider( new[] {
			Record( LoginRecordType.UserProcess, "zoe" ),
			Record( LoginRecordType.DeadProcess, "ignored" ),
			Record( LoginRecordType.UserProcess, "amy" ),
			Record( LoginRecordType.UserProcess, "zoe" )
		} );
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( Array.Empty<string>(), output, new StringWriter(), provider ) );
		Assert.Equal( "amy zoe zoe" + Environment.NewLine, output.ToString() );
	}

	[Fact]
	public async Task FileOperandIsPassedToProvider() {
		var provider = new FakeLoginRecordProvider( Array.Empty<LoginRecord>() );
		Assert.Equal( 0, await RunAsync( new[] { "alternate.utmp" }, new StringWriter(), new StringWriter(), provider ) );
		Assert.Equal( "alternate.utmp", provider.LastFileName );
	}

	[Fact]
	public async Task ExtraOperandFails() {
		var error = new StringWriter();
		Assert.Equal( 1, await RunAsync( new[] { "one", "two" }, new StringWriter(), error, new FakeLoginRecordProvider( Array.Empty<LoginRecord>() ) ) );
		Assert.Contains( "extra operand", error.ToString() );
	}

	[Fact]
	public async Task UnsupportedPlatformFailsCleanly() {
		var error = new StringWriter();
		Assert.Equal( 1, await RunAsync( Array.Empty<string>(), new StringWriter(), error, new FakeLoginRecordProvider( Array.Empty<LoginRecord>(), false ) ) );
		Assert.Contains( "not supported", error.ToString() );
	}

	[Fact]
	public async Task LargeRecordSetIsHandled() {
		var records = Enumerable.Range( 0, 5000 ).Select( index => Record( LoginRecordType.UserProcess, $"u{index:0000}" ) ).ToArray();
		var output = new StringWriter();
		Assert.Equal( 0, await RunAsync( Array.Empty<string>(), output, new StringWriter(), new FakeLoginRecordProvider( records ) ) );
		Assert.Contains( "u0000", output.ToString() );
		Assert.Contains( "u4999", output.ToString() );
	}

	[Fact]
	public async Task LinuxProviderReadsAStreamingUtmpRecord() {
		if ( !OperatingSystem.IsLinux() ) return;
		var path = System.IO.Path.GetTempFileName();
		try {
			var bytes = new byte[ 384 ];
			BitConverter.TryWriteBytes( bytes.AsSpan( 0, 2 ), (short)LoginRecordType.UserProcess );
			BitConverter.TryWriteBytes( bytes.AsSpan( 4, 4 ), 1234 );
			WriteField( bytes, 8, 32, "pts/7" );
			WriteField( bytes, 40, 4, "p7" );
			WriteField( bytes, 44, 32, "alice" );
			WriteField( bytes, 76, 256, "example.test" );
			BitConverter.TryWriteBytes( bytes.AsSpan( 340, 4 ), 1_700_000_000 );
			await File.WriteAllBytesAsync( path, bytes );
			var output = new StringWriter();
			Assert.Equal( 0, await RunAsync( new[] { path }, output, new StringWriter(), SystemLoginRecordProvider.Instance ) );
			Assert.Equal( "alice" + Environment.NewLine, output.ToString() );
		} finally {
			File.Delete( path );
		}
	}

	[Fact]
	public async Task HelpAndVersionWork() {
		var provider = new FakeLoginRecordProvider( Array.Empty<LoginRecord>() );
		Assert.Equal( 0, await RunAsync( new[] { "--help" }, new StringWriter(), new StringWriter(), provider ) );
		Assert.Equal( 0, await RunAsync( new[] { "--version" }, new StringWriter(), new StringWriter(), provider ) );
	}

	[Fact]
	public async Task CancellationReturns130() {
		using var source = new CancellationTokenSource();
		source.Cancel();
		var context = new CommandContext( "users", TextReader.Null, new StringWriter(), new StringWriter(), cancellationToken: source.Token );
		Assert.Equal( 130, await Tool.RunAsync( Array.Empty<string>(), context, new FakeLoginRecordProvider( new[] { Record( LoginRecordType.UserProcess, "amy" ) } ) ) );
	}

	private static void WriteField( byte[] destination, int offset, int length, string value ) {
		var bytes = Encoding.UTF8.GetBytes( value );
		bytes.AsSpan( 0, Math.Min( bytes.Length, length - 1 ) ).CopyTo( destination.AsSpan( offset, length ) );
	}

	private static Task<int> RunAsync( string[] args, StringWriter output, StringWriter error, ILoginRecordProvider provider ) => Tool.RunAsync(
		args,
		new CommandContext( "users", TextReader.Null, output, error ),
		provider
	);

	private static LoginRecord Record( LoginRecordType type, string user ) => new( type, 1, "pts/1", "id", user, "host", 0, 0, 1, DateTimeOffset.UnixEpoch, IPAddress.Loopback );

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
		public ValueTask<string?> GetStandardInputTerminalLineAsync( CancellationToken cancellationToken = default ) => ValueTask.FromResult<string?>( "pts/1" );
	}
}
