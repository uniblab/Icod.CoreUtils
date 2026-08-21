namespace Icod.CoreUtils.Shared.Tests.Ordering;

using System.Runtime.CompilerServices;
using System.Text;
using Icod.CoreUtils.Shared.Ordering;
using Icod.CommandFramework.Records;
using Icod.CommandFramework.Temporary;
using Xunit;

/// <summary>Tests the locale, key, stability, spill, merge, workspace, and cleanup contracts introduced by Completion Gate D.</summary>
public sealed class CompletionGateDTests {
	/// <summary>Verifies POSIX locale-variable precedence and C-locale recognition.</summary>
	[Fact]
	public void LocaleResolutionHonorsPosixPrecedence() {
		var culture = CollationEnvironment.Resolve(
			"de_DE.UTF-8",
			"sv_SE.UTF-8",
			"C"
		);
		Assert.True( culture.IsSuccess );
		var cultureProfile = Assert.IsType<CollationProfile>( culture.Profile );
		Assert.Equal( "de-DE", cultureProfile.Name );
		Assert.False( cultureProfile.IsBytewise );
		var bytewise = CollationEnvironment.Resolve(
			null,
			"C.UTF-8",
			"en_US.UTF-8"
		);
		Assert.True( bytewise.IsSuccess );
		var bytewiseProfile = Assert.IsType<CollationProfile>( bytewise.Profile );
		Assert.True( bytewiseProfile.IsBytewise );
		Assert.Equal( "C", bytewiseProfile.Name );
	}

	/// <summary>Verifies unsupported locale names produce a controlled result rather than an arbitrary fallback.</summary>
	[Fact]
	public void LocaleResolutionRejectsUnsupportedLocale() {
		var result = CollationEnvironment.Resolve(
			"!invalid!",
			null,
			null
		);
		Assert.False( result.IsSuccess );
		Assert.Null( result.Profile );
		Assert.Contains(
			"unsupported collation locale",
			result.ErrorMessage ?? string.Empty
		);
	}

	/// <summary>Verifies reusable collation keys have the same order as their provider.</summary>
	[Fact]
	public void CollationKeysMatchProviderOrdering() {
		var provider = new SystemCollationProvider(
			CollationProfile.CreateBytewise()
		);
		var direct = provider.Compare( "alpha", "beta" );
		var keyed = CollationKeyComparer.Instance.Compare(
			provider.CreateKey( "alpha" ),
			provider.CreateKey( "beta" )
		);
		Assert.Equal( Math.Sign( direct ), Math.Sign( keyed ) );
		Assert.Throws<ArgumentException>(
			() => CollationKeyComparer.Instance.Compare(
				provider.CreateKey( "alpha" ),
				new CollationKey( "different", new byte[] { 1 } )
			)
		);
	}

	/// <summary>Verifies GNU field, character, endpoint-blank, and comparison-option syntax.</summary>
	[Fact]
	public void SortKeyParserParsesEndpointsAndOptions() {
		var result = SortKeyParser.Parse( "2.3bdfM,4.0br" );
		Assert.True( result.IsSuccess );
		var definition = Assert.IsType<SortKeyDefinition>( result.Definition );
		Assert.Equal( 2, definition.Start.FieldNumber );
		Assert.Equal( 3, definition.Start.CharacterOffset );
		Assert.True( definition.Start.SkipLeadingBlanks );
		var end = Assert.IsType<SortKeyPosition>( definition.End );
		Assert.Equal( 4, end.FieldNumber );
		Assert.Equal( 0, end.CharacterOffset );
		Assert.True( end.SkipLeadingBlanks );
		Assert.Equal( "dfMr", definition.Options );
	}

	/// <summary>Verifies malformed key syntax reports deterministic codes and source offsets.</summary>
	[Fact]
	public void SortKeyParserReportsDeterministicErrors() {
		var zeroStart = SortKeyParser.Parse( "2.0" );
		Assert.False( zeroStart.IsSuccess );
		Assert.Equal(
			SortKeyParseErrorCode.InvalidStartCharacterOffset,
			zeroStart.ErrorCode
		);
		Assert.Equal( 2, zeroStart.ErrorOffset );
		var unknown = SortKeyParser.Parse( "2z" );
		Assert.False( unknown.IsSuccess );
		Assert.Equal( SortKeyParseErrorCode.UnknownOption, unknown.ErrorCode );
		Assert.Equal( 1, unknown.ErrorOffset );
	}

	/// <summary>Verifies equal extracted keys retain original input order.</summary>
	[Fact]
	public void CompositeComparisonCanBeMadeStable() {
		var keys = new CompositeSortKeyComparer<string>(
			new[] {
				new SortKeyRule<string>(
					value => value[..1],
					StringComparer.Ordinal
				)
			}
		);
		var comparer = new StableComparer<string>( keys );
		var values = new List<StableItem<string>> {
			new( "b-0", 0 ),
			new( "a-1", 1 ),
			new( "b-2", 2 ),
			new( "a-3", 3 )
		};
		values.Sort( comparer );
		Assert.Equal(
			new[] { "a-1", "a-3", "b-0", "b-2" },
			values.Select( value => value.Value )
		);
	}

	/// <summary>Verifies the byte-record run codec retains bytes, termination, and original ordinal.</summary>
	[Fact]
	public async Task ByteRecordCodecRoundTripsStableItems() {
		var codec = new ByteRecordRunCodec();
		using var stream = new MemoryStream();
		await codec.WriteAsync(
			stream,
			new StableItem<ByteRecord>(
				new ByteRecord( new byte[] { 0, 10, 255 }, true ),
				42
			)
		);
		stream.Position = 0;
		var result = await codec.ReadAsync( stream );
		Assert.True( result.HasItem );
		var item = Assert.IsType<StableItem<ByteRecord>>( result.Item );
		Assert.Equal( 42, item.OriginalOrdinal );
		Assert.Equal(
			new byte[] { 0, 10, 255 },
			item.Value.Content.ToArray()
		);
		Assert.True( item.Value.IsTerminated );
		Assert.False( ( await codec.ReadAsync( stream ) ).HasItem );
	}

	/// <summary>Verifies one-item runs, bounded fan-in merge passes, global stability, and cleanup after success.</summary>
	[Fact]
	public async Task ExternalOrderingSpillsMergesStablyAndCleansAfterSuccess() {
		string? workspacePath = null;
		var output = new List<string>();
		var engine = CreateEngine( path => workspacePath = path );
		await engine.OrderAsync(
			CreateRecords( "b-0", "a-1", "b-2", "a-3", "c-4", "a-5" ),
			( record, _ ) => {
				output.Add( Encoding.ASCII.GetString( record.Content.Span ) );
				return ValueTask.CompletedTask;
			}
		);
		Assert.Equal(
			new[] { "a-1", "a-3", "a-5", "b-0", "b-2", "c-4" },
			output
		);
		var completedWorkspacePath = Assert.IsType<string>( workspacePath );
		Assert.False( Directory.Exists( completedWorkspacePath ) );
	}

	/// <summary>Verifies output failure is preserved and the secure workspace is still removed.</summary>
	[Fact]
	public async Task ExternalOrderingCleansAfterFailure() {
		string? workspacePath = null;
		var engine = CreateEngine( path => workspacePath = path );
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => engine.OrderAsync(
				CreateRecords( "b-0", "a-1", "c-2" ),
				( _, _ ) => throw new InvalidOperationException(
					"expected output failure"
				)
			)
		);
		Assert.Equal( "expected output failure", exception.Message );
		var failedWorkspacePath = Assert.IsType<string>( workspacePath );
		Assert.False( Directory.Exists( failedWorkspacePath ) );
	}

	/// <summary>Verifies operation and cleanup failures are both preserved and every removable object is still deleted.</summary>
	[Fact]
	public async Task ExternalOrderingPreservesOperationAndCleanupFailures() {
		string? workspacePath = null;
		var creator = new SecureTemporaryObjectCreator(
			new DirectoryDeleteFailureFileSystem(),
			CryptographicRandomSource.Instance
		);
		var engine = CreateEngine(
			path => workspacePath = path,
			creator
		);
		try {
			var exception = await Assert.ThrowsAsync<AggregateException>(
				() => engine.OrderAsync(
					CreateRecords( "b-0", "a-1", "c-2" ),
					( _, _ ) => throw new InvalidOperationException(
						"expected output failure"
					)
				)
			);
			Assert.Collection(
				exception.InnerExceptions,
				value => Assert.IsType<InvalidOperationException>( value ),
				value => Assert.IsType<IOException>( value )
			);
			var failedWorkspacePath = Assert.IsType<string>( workspacePath );
			Assert.True( Directory.Exists( failedWorkspacePath ) );
			Assert.Empty( Directory.EnumerateFileSystemEntries( failedWorkspacePath ) );
		} finally {
			if ( ( null != workspacePath ) && Directory.Exists( workspacePath ) ) {
				Directory.Delete( workspacePath, recursive: true );
			}
		}
	}

	/// <summary>Verifies cancellation is preserved while cleanup runs independently of the canceled token.</summary>
	[Fact]
	public async Task ExternalOrderingCleansAfterCancellation() {
		string? workspacePath = null;
		using var cancellation = new CancellationTokenSource();
		var writes = 0;
		var engine = CreateEngine( path => workspacePath = path );
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => engine.OrderAsync(
				CreateRecords( "c-0", "b-1", "a-2" ),
				( _, _ ) => {
					writes++;
					cancellation.Cancel();
					return ValueTask.CompletedTask;
				},
				cancellation.Token
			)
		);
		Assert.Equal( 1, writes );
		var canceledWorkspacePath = Assert.IsType<string>( workspacePath );
		Assert.False( Directory.Exists( canceledWorkspacePath ) );
	}

	/// <summary>Verifies workspace-owned files can be released early and leaf-name boundaries are enforced.</summary>
	[Fact]
	public void TemporaryWorkspaceOwnsOnlySecureLeafFiles() {
		var workspace = TemporaryWorkspace.Create(
			directoryTemplate: "gate-d-tests-XXXXXXXX"
		);
		var root = workspace.RootPath;
		try {
			var path = workspace.CreateFile( "record-XXXXXXXX.bin" );
			Assert.True( File.Exists( path ) );
			workspace.DeleteFile( path );
			Assert.False( File.Exists( path ) );
			Assert.Throws<ArgumentException>(
				() => workspace.CreateFile(
					System.IO.Path.Combine( "nested", "record-XXXXXXXX.bin" )
				)
			);
			Assert.Throws<ArgumentException>(
				() => workspace.CreateFile( "nested\\record-XXXXXXXX.bin" )
			);
			Assert.Throws<ArgumentException>(
				() => workspace.CreateFile( "nested/record-XXXXXXXX.bin" )
			);
		} finally {
			workspace.Dispose();
		}
		Assert.False( Directory.Exists( root ) );
	}

	private static ExternalOrderingEngine<ByteRecord> CreateEngine(
		Action<string> captureWorkspace,
		SecureTemporaryObjectCreator? creator = null
	) {
		return new ExternalOrderingEngine<ByteRecord>(
			new FirstByteComparer(),
			new ByteRecordRunCodec(),
			new ExternalOrderingOptions<ByteRecord>(
				memoryLimitBytes: 1,
				sizeEstimator: record => record.Content.Length,
				perItemOverheadBytes: 0,
				mergeFanIn: 2,
				fileBufferSize: 128,
				runFileTemplate: "gate-d-run-XXXXXXXX.bin"
			),
			cancellationToken => {
				var workspace = TemporaryWorkspace.Create(
					directoryTemplate: "gate-d-work-XXXXXXXX",
					creator: creator,
					cancellationToken: cancellationToken
				);
				captureWorkspace( workspace.RootPath );
				return workspace;
			}
		);
	}

	private static async IAsyncEnumerable<ByteRecord> CreateRecords(
		string first,
		string second,
		string third,
		string? fourth = null,
		string? fifth = null,
		string? sixth = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	) {
		var values = new[] { first, second, third, fourth, fifth, sixth };
		foreach ( var value in values ) {
			if ( null == value ) {
				continue;
			}
			cancellationToken.ThrowIfCancellationRequested();
			await Task.Yield();
			yield return new ByteRecord( Encoding.ASCII.GetBytes( value ), true );
		}
	}

	private sealed class DirectoryDeleteFailureFileSystem : ITemporaryObjectFileSystem {
		TemporaryObjectAttemptResult ITemporaryObjectFileSystem.TryCreate(
			string path,
			TemporaryObjectKind kind
		) {
			return SystemTemporaryObjectFileSystem.Instance.TryCreate( path, kind );
		}

		bool ITemporaryObjectFileSystem.TryDelete(
			string path,
			TemporaryObjectKind kind,
			out string? errorMessage
		) {
			if ( TemporaryObjectKind.Directory == kind ) {
				errorMessage = "expected directory cleanup failure";
				return false;
			}
			return SystemTemporaryObjectFileSystem.Instance.TryDelete(
				path,
				kind,
				out errorMessage
			);
		}
	}

	private sealed class FirstByteComparer : IComparer<ByteRecord> {
		int IComparer<ByteRecord>.Compare( ByteRecord? x, ByteRecord? y ) {
			if ( ReferenceEquals( x, y ) ) {
				return 0;
			}
			if ( null == x ) {
				return -1;
			}
			if ( null == y ) {
				return 1;
			}
			return x.Content.Span[0].CompareTo( y.Content.Span[0] );
		}
	}
}
