namespace Icod.CoreUtils.Test.Tests;

using System.Globalization;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.Platform;
using Xunit;

/// <summary>Exercises the Batch 37 GNU/POSIX <c>test</c> expression evaluator.</summary>
public sealed class CommandTests {
	/// <summary>Verifies zero- and one-operand truth rules and ordinary option-looking strings.</summary>
	[Fact]
	public async Task AppliesZeroAndOneOperandRules() {
		Assert.Equal( 1, await RunAsync( Array.Empty<string>() ) );
		Assert.Equal( 1, await RunAsync( new[] { string.Empty } ) );
		Assert.Equal( 0, await RunAsync( new[] { "value" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "--help" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "--version" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "[" } ) );
	}

	/// <summary>Verifies the special two-, three-, and four-operand ambiguity rules.</summary>
	[Fact]
	public async Task AppliesOperandCountAmbiguityRules() {
		Assert.Equal( 0, await RunAsync( new[] { "!", string.Empty } ) );
		Assert.Equal( 1, await RunAsync( new[] { "!", "value" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "(", "value", ")" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "!", "(", string.Empty, ")" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "-n", "=", "-n" } ) );
	}

	/// <summary>Verifies GNU's historical <c>-l</c> operand shifting for nonnumeric binary operators.</summary>
	[Fact]
	public async Task PreservesGnuLengthOperandAmbiguities() {
		Assert.Equal( 0, await RunAsync( new[] { "-l", "x", "=", "x" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "x", "!=", "-l", "ignored" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "-1", "!=", "-l", "!=" } ) );
		var result = await RunWithErrorAsync( new[] { "-l", "x", "-ef", "ignored" } );
		Assert.Equal( 2, result.ExitCode );
		Assert.Contains( "does not accept -l", result.StandardError, StringComparison.Ordinal );
	}

	/// <summary>Verifies string unary and binary predicates.</summary>
	[Fact]
	public async Task EvaluatesStringPredicates() {
		Assert.Equal( 0, await RunAsync( new[] { "-n", "abc" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "-z", string.Empty } ) );
		Assert.Equal( 0, await RunAsync( new[] { "abc", "=", "abc" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "abc", "==", "abc" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "abc", "!=", "def" } ) );
		var host = new FakeHost { StringComparison = static ( left, right ) => string.CompareOrdinal( left, right ) };
		Assert.Equal( 0, await RunAsync( new[] { "a", "<", "b" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "b", ">", "a" }, host ) );
	}

	/// <summary>Verifies arbitrary-precision signed integer comparisons and accepted surrounding whitespace.</summary>
	[Fact]
	public async Task EvaluatesArbitraryPrecisionIntegerPredicates() {
		const string huge = "999999999999999999999999999999999999999999999999";
		Assert.Equal( 0, await RunAsync( new[] { huge, "-eq", huge } ) );
		Assert.Equal( 0, await RunAsync( new[] { " -7 ", "-lt", "+2" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "5", "-le", "5" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "8", "-gt", "7" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "8", "-ge", "8" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "8", "-ne", "9" } ) );
	}

	/// <summary>Verifies GNU <c>-l STRING</c> numeric operands use the UTF-8 byte length.</summary>
	[Fact]
	public async Task EvaluatesStringLengthNumericOperands() {
		Assert.Equal( 0, await RunAsync( new[] { "-l", "abcd", "-eq", "4" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "2", "-eq", "-l", "é" } ) );
	}

	/// <summary>Verifies connective precedence, parentheses, repeated negation, and non-short-circuit evaluation.</summary>
	[Fact]
	public async Task EvaluatesConnectivesWithGnuPrecedence() {
		var host = new FakeHost();
		host.SetMetadata( "probe", true, Metadata( "probe", FileSystemEntryKind.File ) );
		Assert.Equal(
			0,
			await RunAsync( new[] { string.Empty, "-a", "-e", "probe", "-o", "fallback" }, host )
		);
		Assert.Equal( 1, host.MetadataRequestCount );
		Assert.Equal( 0, await RunAsync( new[] { "!", "!", "value", "-a", "(", "x", "=", "x", ")" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "(", "x", "=", "x", "-a", "y", ")" } ) );
		Assert.Equal( 0, await RunAsync( new[] { "value", "-o", string.Empty, "-a", string.Empty } ) );
	}

	/// <summary>Verifies every classified file-type predicate through the shared metadata model.</summary>
	[Theory]
	[InlineData( "-b", FileSystemEntryKind.BlockDevice )]
	[InlineData( "-c", FileSystemEntryKind.CharacterDevice )]
	[InlineData( "-d", FileSystemEntryKind.Directory )]
	[InlineData( "-f", FileSystemEntryKind.File )]
	[InlineData( "-p", FileSystemEntryKind.Fifo )]
	[InlineData( "-S", FileSystemEntryKind.Socket )]
	public async Task EvaluatesFileTypePredicates( string predicate, FileSystemEntryKind kind ) {
		var host = new FakeHost();
		host.SetMetadata( "entry", true, Metadata( "entry", kind ) );
		Assert.Equal( 0, await RunAsync( new[] { predicate, "entry" }, host ) );
	}

	/// <summary>Verifies existence, size, special mode bits, and modification-since-read predicates.</summary>
	[Fact]
	public async Task EvaluatesFileCharacteristics() {
		var older = DateTimeOffset.Parse( "2025-01-01T00:00:00Z", CultureInfo.InvariantCulture );
		var newer = older.AddMinutes( 1 );
		var metadata = Metadata(
			"entry",
			FileSystemEntryKind.File,
			size: 12,
			mode: 0x0E00,
			accessTime: older,
			modificationTime: newer
		);
		var host = new FakeHost();
		host.SetMetadata( "entry", true, metadata );
		Assert.Equal( 0, await RunAsync( new[] { "-e", "entry" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "-s", "entry" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "-u", "entry" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "-g", "entry" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "-k", "entry" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "-N", "entry" }, host ) );
	}

	/// <summary>Verifies symbolic links are observed without following only for <c>-h</c> and <c>-L</c>.</summary>
	[Fact]
	public async Task DistinguishesLinkObjectFromFollowedTarget() {
		var host = new FakeHost();
		host.SetMetadata( "link", false, Metadata( "link", FileSystemEntryKind.SymbolicLink, true ) );
		host.SetMetadata( "link", true, Metadata( "target", FileSystemEntryKind.File ) );
		Assert.Equal( 0, await RunAsync( new[] { "-h", "link" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "-L", "link" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "-f", "link" }, host ) );
		Assert.Equal( 1, await RunAsync( new[] { "-d", "link" }, host ) );
	}

	/// <summary>Verifies hard-link identity is compared through the shared E1 identity contract.</summary>
	[Fact]
	public async Task EvaluatesSameFileIdentity() {
		var host = new FakeHost();
		var identity = new FileSystemEntryIdentity( "test", "same-object" );
		host.SetMetadata( "first", true, Metadata( "first", FileSystemEntryKind.File, identity: identity ) );
		host.SetMetadata( "second", true, Metadata( "second", FileSystemEntryKind.File, identity: identity ) );
		host.SetMetadata( "other", true, Metadata( "other", FileSystemEntryKind.File ) );
		Assert.Equal( 0, await RunAsync( new[] { "first", "-ef", "second" }, host ) );
		Assert.Equal( 1, await RunAsync( new[] { "first", "-ef", "other" }, host ) );
	}

	/// <summary>Verifies newer/older comparison and GNU missing-operand ordering semantics.</summary>
	[Fact]
	public async Task EvaluatesModificationTimeOrdering() {
		var host = new FakeHost();
		var oldTime = DateTimeOffset.Parse( "2024-01-01T00:00:00Z", CultureInfo.InvariantCulture );
		var newTime = oldTime.AddDays( 1 );
		host.SetMetadata( "old", true, MetadataWithTime( "old", oldTime ) );
		host.SetMetadata( "new", true, MetadataWithTime( "new", newTime ) );
		Assert.Equal( 0, await RunAsync( new[] { "new", "-nt", "old" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "old", "-ot", "new" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "new", "-nt", "missing" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "missing", "-ot", "new" }, host ) );
		Assert.Equal( 1, await RunAsync( new[] { "missing", "-nt", "new" }, host ) );
	}

	/// <summary>Verifies effective owner/group, access-mode, and terminal predicates.</summary>
	[Fact]
	public async Task EvaluatesIdentityAccessAndTerminalPredicates() {
		var host = new FakeHost {
			Identity = Identity( "1000", "100" )
		};
		var metadata = Metadata(
			"owned",
			FileSystemEntryKind.File,
			userId: 1000,
			groupId: 100
		);
		host.SetMetadata( "owned", true, metadata );
		host.SetAccess( "owned", TestAccessMode.Read, true );
		host.SetAccess( "owned", TestAccessMode.Write, false );
		host.Terminals.Add( 7 );
		Assert.Equal( 0, await RunAsync( new[] { "-O", "owned" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "-G", "owned" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "-r", "owned" }, host ) );
		Assert.Equal( 1, await RunAsync( new[] { "-w", "owned" }, host ) );
		Assert.Equal( 0, await RunAsync( new[] { "-t", "7" }, host ) );
		Assert.Equal( 1, await RunAsync( new[] { "-t", "8" }, host ) );
	}

	/// <summary>Verifies invalid integers and malformed expressions produce status 2 diagnostics.</summary>
	[Fact]
	public async Task ReportsSyntaxErrors() {
		var result = await RunWithErrorAsync( new[] { "abc", "-eq", "1" } );
		Assert.Equal( 2, result.ExitCode );
		Assert.Contains( "invalid integer", result.StandardError, StringComparison.Ordinal );
		result = await RunWithErrorAsync( new[] { "left", "unknown", "right" } );
		Assert.Equal( 2, result.ExitCode );
		Assert.Contains( "binary operator expected", result.StandardError, StringComparison.Ordinal );
		result = await RunWithErrorAsync( new[] { "left", "right" } );
		Assert.Equal( 2, result.ExitCode );
		Assert.Contains( "missing argument", result.StandardError, StringComparison.Ordinal );
		result = await RunWithErrorAsync( new[] { "(", "value", "-a", "other" } );
		Assert.Equal( 2, result.ExitCode );
		Assert.Contains( "')' expected", result.StandardError, StringComparison.Ordinal );
		result = await RunWithErrorAsync( new[] { "-ne", "-a", "-a" } );
		Assert.Equal( 2, result.ExitCode );
		Assert.Contains( "unary operator expected", result.StandardError, StringComparison.Ordinal );
		result = await RunWithErrorAsync( new[] { "0", "-o", "-l" } );
		Assert.Equal( 2, result.ExitCode );
		Assert.Contains( "unary operator expected", result.StandardError, StringComparison.Ordinal );
	}

	/// <summary>Verifies ordinary host files and directories are evaluated by the system provider.</summary>
	[Fact]
	public async Task EvaluatesHostFileAndDirectory() {
		var root = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "test-batch37-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( root );
		var file = System.IO.Path.Combine( root, "item.txt" );
		try {
			await File.WriteAllTextAsync( file, "content" );
			Assert.Equal( 0, await RunAsync( new[] { "-d", root } ) );
			Assert.Equal( 0, await RunAsync( new[] { "-f", file } ) );
			Assert.Equal( 0, await RunAsync( new[] { "-s", file } ) );
			Assert.Equal( 1, await RunAsync( new[] { "-e", System.IO.Path.Combine( root, "missing" ) } ) );
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies cancellation returns the shared cancellation status without writing a syntax diagnostic.</summary>
	[Fact]
	public async Task HonorsCancellation() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var standardError = new StringWriter();
		var context = new CommandContext(
			"test",
			TextReader.Null,
			new StringWriter(),
			standardError,
			cancellationToken: cancellation.Token
		);
		Assert.Equal( CommandExitCodes.Canceled, await Command.RunAsync( new[] { "value" }, context, new FakeHost() ) );
		Assert.Equal( string.Empty, standardError.ToString() );
	}

	private static async Task<int> RunAsync( string[] arguments, ITestEvaluationHost? host = null ) {
		var context = new CommandContext( "test", TextReader.Null, new StringWriter(), new StringWriter() );
		return null == host
			? await Command.RunAsync( arguments, context )
			: await Command.RunAsync( arguments, context, host );
	}

	private static async Task<(int ExitCode, string StandardError)> RunWithErrorAsync( string[] arguments ) {
		var standardError = new StringWriter();
		var context = new CommandContext( "test", TextReader.Null, new StringWriter(), standardError );
		var exitCode = await Command.RunAsync( arguments, context, new FakeHost() );
		return (exitCode, standardError.ToString());
	}

	private static FileSystemMetadata Metadata(
		string path,
		FileSystemEntryKind kind,
		bool isSymbolicLink = false,
		FileSystemEntryIdentity? identity = null,
		ulong? size = null,
		uint? mode = null,
		uint? userId = null,
		uint? groupId = null,
		DateTimeOffset? accessTime = null,
		DateTimeOffset? modificationTime = null
	) => new(
		path,
		kind,
		isSymbolicLink,
		false,
		identity ?? new FileSystemEntryIdentity( "test", path ),
		new FileSystemIdentity( "test", "filesystem" )
	) {
		Size = size.HasValue
			? FileSystemMetadataValue<ulong>.Available( size.Value )
			: default,
		Mode = mode.HasValue
			? FileSystemMetadataValue<uint>.Available( mode.Value )
			: default,
		UserId = userId.HasValue
			? FileSystemMetadataValue<uint>.Available( userId.Value )
			: default,
		GroupId = groupId.HasValue
			? FileSystemMetadataValue<uint>.Available( groupId.Value )
			: default,
		AccessTime = accessTime.HasValue
			? FileSystemMetadataValue<DateTimeOffset>.Available( accessTime.Value )
			: default,
		ModificationTime = modificationTime.HasValue
			? FileSystemMetadataValue<DateTimeOffset>.Available( modificationTime.Value )
			: default
	};

	private static FileSystemMetadata MetadataWithTime( string path, DateTimeOffset modificationTime ) => Metadata(
		path,
		FileSystemEntryKind.File,
		modificationTime: modificationTime
	);

	private static ProcessIdentity Identity( string userId, string groupId ) {
		var group = new GroupIdentity( groupId, string.Concat( "group-", groupId ) );
		var user = new UserIdentity( userId, string.Concat( "user-", userId ), group, new[] { group } );
		return new ProcessIdentity( user, user, group, group, new[] { group }, null );
	}

	private sealed class FakeHost : ITestEvaluationHost {
		private readonly Dictionary<(string Path, bool Follow), FileSystemMetadata?> myMetadata = new();
		private readonly Dictionary<(string Path, TestAccessMode Mode), bool> myAccess = new();

		/// <summary>Initializes an empty fake evaluation host.</summary>
		public FakeHost() { }

		/// <summary>Gets or sets the process identity returned by the host.</summary>
		public ProcessIdentity Identity { get; set; } = CommandTests.Identity( "1000", "100" );

		/// <summary>Gets the file descriptors reported as terminals.</summary>
		public HashSet<int> Terminals { get; } = new();

		/// <summary>Gets or sets the string comparison function.</summary>
		public Func<string, string, int> StringComparison { get; set; } = static ( left, right ) => string.CompareOrdinal( left, right );

		/// <summary>Gets the number of metadata observations requested.</summary>
		public int MetadataRequestCount { get; private set; }

		/// <summary>Registers one metadata response.</summary>
		/// <param name="path">The operand pathname.</param>
		/// <param name="follow">Whether the request follows terminal indirection.</param>
		/// <param name="metadata">The response.</param>
		public void SetMetadata( string path, bool follow, FileSystemMetadata? metadata ) {
			myMetadata[(path, follow)] = metadata;
		}

		/// <summary>Registers one access-test response.</summary>
		/// <param name="path">The operand pathname.</param>
		/// <param name="mode">The requested mode.</param>
		/// <param name="value">The response.</param>
		public void SetAccess( string path, TestAccessMode mode, bool value ) {
			myAccess[(path, mode)] = value;
		}

		/// <inheritdoc/>
		public ValueTask<FileSystemMetadata?> GetMetadataAsync(
			string path,
			bool followPathIndirection,
			CancellationToken cancellationToken
		) {
			cancellationToken.ThrowIfCancellationRequested();
			MetadataRequestCount++;
			myMetadata.TryGetValue( (path, followPathIndirection), out var metadata );
			return ValueTask.FromResult( metadata );
		}

		/// <inheritdoc/>
		public ValueTask<ProcessIdentity> GetProcessIdentityAsync( CancellationToken cancellationToken ) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( Identity );
		}

		/// <inheritdoc/>
		public ValueTask<bool> CanAccessAsync(
			string path,
			TestAccessMode mode,
			CancellationToken cancellationToken
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( myAccess.TryGetValue( (path, mode), out var value ) && value );
		}

		/// <inheritdoc/>
		public bool IsTerminal( int fileDescriptor, CommandContext context ) => Terminals.Contains( fileDescriptor );

		/// <inheritdoc/>
		public int CompareStrings( string left, string right ) => StringComparison( left, right );
	}
}
