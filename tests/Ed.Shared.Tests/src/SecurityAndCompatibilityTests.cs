namespace Icod.LineEditor.Ed.Shared.Tests;

using System.Text;
using Icod.CoreUtils.Shared.RegularExpressions;
using Icod.LineEditor.Ed;

public sealed class SecurityAndCompatibilityTests {
	[Fact]
	public async Task RestrictedPolicyRejectsShellBeforeInvokingCapabilityAndPreservesState() {
		var process = new MemoryProcessAccess();
		var engine = new EditorEngine(
			EditorSecurityPolicy.Restricted( Directory.GetCurrentDirectory() ),
			new MemoryFileAccess(),
			process,
			GnuBasicRegularExpressionProvider.Default
		);
		engine.Load( Lines( "one", "two" ) );
		var identities = engine.Buffer.GetLines().Select( line => line.Id ).ToArray();

		var result = await engine.ExecuteScriptAsync(
			StreamOf( "1,2!cat\n" ),
			new MemoryStream(),
			new MemoryStream()
		);

		Assert.Equal( EditorExitStatus.Error, result.ExitStatus );
		Assert.Equal( EditorDiagnosticCode.RestrictedOperation, result.Diagnostic?.Code );
		Assert.Equal( 0, process.CallCount );
		Assert.Equal( identities, engine.Buffer.GetLines().Select( line => line.Id ) );
		Assert.False( engine.IsModified );
	}

	[Theory]
	[InlineData( "../outside" )]
	[InlineData( "/absolute" )]
	[InlineData( "C:\\outside" )]
	[InlineData( "dir/file" )]
	[InlineData( "stream:name" )]
	public async Task RestrictedPolicyRejectsPathBearingFileCommands(
		string path
	) {
		var files = new MemoryFileAccess();
		var engine = new EditorEngine(
			EditorSecurityPolicy.Restricted( Directory.GetCurrentDirectory() ),
			files,
			new DeniedEditorProcessAccess(),
			GnuBasicRegularExpressionProvider.Default
		);
		engine.Load( Lines( "one" ) );

		var result = await engine.ExecuteScriptAsync(
			StreamOf( string.Concat( "w ", path, "\n" ) ),
			new MemoryStream(),
			new MemoryStream()
		);

		Assert.Equal( EditorExitStatus.Error, result.ExitStatus );
		Assert.Equal( EditorDiagnosticCode.RestrictedOperation, result.Diagnostic?.Code );
		Assert.Empty( files.WrittenPaths );
		Assert.Equal( "one", engine.Buffer.GetLine( 1 ).GetText() );
	}

	[Fact]
	public async Task RestrictedEngineAllowsSimpleNamesAndReusesTheRememberedLogicalName() {
		var files = new MemoryFileAccess();
		files.Files[ "input.txt" ] = new EditorFileReadResult( Lines( "value" ), true, 6 );
		var engine = new EditorEngine(
			EditorSecurityPolicy.Restricted( Directory.GetCurrentDirectory() ),
			files,
			new DeniedEditorProcessAccess(),
			GnuBasicRegularExpressionProvider.Default
		);

		var result = await engine.ExecuteScriptAsync(
			StreamOf( "e input.txt\nw\n" ),
			new MemoryStream(),
			new MemoryStream()
		);

		Assert.True( result.IsSuccess, result.Diagnostic?.Message );
		Assert.Equal( "input.txt", engine.RememberedFileName );
		Assert.Equal( new[] { "input.txt" }, files.ReadPaths );
		Assert.Equal( new[] { "input.txt" }, files.WrittenPaths );
	}

	[Fact]
	public async Task RestrictedFactoryConstrainsInjectedFileCapabilityToCapturedDirectory() {
		var files = new MemoryFileAccess();
		var directory = Path.GetFullPath( Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( directory );
		try {
			var expected = Path.Combine( directory, "input.txt" );
			files.Files[ expected ] = new EditorFileReadResult( Lines( "value" ), true, 6 );
			var engine = EditorEngine.CreateRestricted( directory, files );

			var result = await engine.ExecuteScriptAsync(
				StreamOf( "e input.txt\n" ),
				new MemoryStream(),
				new MemoryStream()
			);

			Assert.True( result.IsSuccess, result.Diagnostic?.Message );
			Assert.Equal( expected, Assert.Single( files.ReadPaths ) );
			Assert.Equal( "input.txt", engine.RememberedFileName );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	[Fact]
	public async Task RestrictedFileCapabilityMapsSimpleNamesIntoCapturedDirectory() {
		var inner = new MemoryFileAccess();
		var directory = Path.GetFullPath( Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( directory );
		try {
			var expected = Path.Combine( directory, "file.txt" );
			inner.Files[ expected ] = new EditorFileReadResult( Lines( "value" ), true, 6 );
			var restricted = new RestrictedEditorFileAccess( directory, inner );

			var result = await restricted.ReadAsync( "file.txt" );

			Assert.Equal( expected, Assert.Single( inner.ReadPaths ) );
			Assert.Equal( "value", Encoding.UTF8.GetString( Assert.Single( result.Lines ).Span ) );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	[Theory]
	[InlineData( "gnu-diffutils" )]
	[InlineData( "icod-diffutils" )]
	public async Task AppliesDiffutilsEdScriptCompatibilityFixture(
		string fixtureName
	) {
		var root = Path.Combine( AppContext.BaseDirectory, "fixtures", fixtureName );
		var original = await ReadLfLinesAsync( Path.Combine( root, "original.txt" ) );
		var expected = await ReadLfLinesAsync( Path.Combine( root, "expected.txt" ) );
		await using var script = File.OpenRead( Path.Combine( root, "change.ed" ) );
		var engine = new EditorEngine(
			EditorSecurityPolicy.Standard,
			new MemoryFileAccess(),
			new MemoryProcessAccess(),
			GnuBasicRegularExpressionProvider.Default
		);
		engine.Load( original );

		var result = await engine.ExecuteScriptAsync(
			script,
			new MemoryStream(),
			new MemoryStream(),
			Path.Combine( fixtureName, "change.ed" )
		);

		Assert.True( result.IsSuccess, result.Diagnostic?.Message );
		Assert.Equal(
			expected.Select( line => Encoding.UTF8.GetString( line.Span ) ),
			engine.Buffer.GetLines().Select( line => line.GetText() )
		);
	}

	private static async Task<IReadOnlyList<ReadOnlyMemory<byte>>> ReadLfLinesAsync(
		string path
	) {
		var text = await File.ReadAllTextAsync( path );
		return text.Split( '\n', StringSplitOptions.RemoveEmptyEntries )
			.Select( value => Encoding.UTF8.GetBytes( value ).AsMemory() )
			.ToArray();
	}

	private static IReadOnlyList<ReadOnlyMemory<byte>> Lines(
		params string[] values
	) => values.Select( value => Encoding.UTF8.GetBytes( value ).AsMemory() ).ToArray();

	private static MemoryStream StreamOf(
		string value
	) => new( Encoding.UTF8.GetBytes( value ), writable: false );
}
