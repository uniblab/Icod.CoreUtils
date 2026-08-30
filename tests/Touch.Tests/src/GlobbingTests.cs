namespace Icod.CoreUtils.Touch.Tests;

using Xunit;

/// <summary>Exercises pathname expansion at the <c>touch</c> command boundary.</summary>
public sealed class GlobbingTests {

	/// <summary>Verifies wildcard target operands expand before timestamp mutation.</summary>
	[Fact]
	public async Task ExpandsWildcardTargetsBeforeTimestampMutation() {
		using var temporary = new TemporaryDirectory();
		var first = temporary.CreateFile( "first.dat" );
		var second = temporary.CreateFile( "second.dat" );
		var ignored = temporary.CreateFile( "ignored.bin" );
		var original = DateTimeOffset.FromUnixTimeSeconds( 978307200 ).UtcDateTime;
		File.SetLastWriteTimeUtc( first, original );
		File.SetLastWriteTimeUtc( second, original );
		File.SetLastWriteTimeUtc( ignored, original );

		var exitCode = await Command.RunAsync(
			new[] { "-m", "--date=@946684800", temporary.PathFor( "*.dat" ) },
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		);

		Assert.Equal( 0, exitCode );
		AssertTimestamp( DateTimeOffset.FromUnixTimeSeconds( 946684800 ), first );
		AssertTimestamp( DateTimeOffset.FromUnixTimeSeconds( 946684800 ), second );
		AssertTimestamp( DateTimeOffset.FromUnixTimeSeconds( 978307200 ), ignored );
	}

	/// <summary>Verifies the reference option value remains literal instead of being glob-expanded.</summary>
	[Fact]
	public async Task ReferenceOptionValueIsNotExpanded() {
		using var temporary = new TemporaryDirectory();
		_ = temporary.CreateFile( "reference-one.dat" );
		_ = temporary.CreateFile( "reference-two.dat" );
		var target = temporary.CreateFile( "target.dat" );
		var original = DateTimeOffset.FromUnixTimeSeconds( 978307200 );
		File.SetLastWriteTimeUtc( target, original.UtcDateTime );
		var error = new StringWriter();

		var exitCode = await Command.RunAsync(
			new[] {
				"-m",
				"--reference",
				temporary.PathFor( "reference-*.dat" ),
				target
			},
			TextReader.Null,
			new StringWriter(),
			error
		);

		Assert.Equal( 1, exitCode );
		Assert.Contains( "failed to get attributes", error.ToString(), StringComparison.Ordinal );
		AssertTimestamp( original, target );
	}

	private static void AssertTimestamp( DateTimeOffset expected, string path ) {
		var actual = new DateTimeOffset( File.GetLastWriteTimeUtc( path ) );
		Assert.True(
			( actual - expected ).Duration() <= TimeSpan.FromSeconds( 1 ),
			$"Expected {expected:O}; actual {actual:O}."
		);
	}

	private sealed class TemporaryDirectory : IDisposable {

		public TemporaryDirectory() {
			PathValue = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				string.Concat( "Icod.CoreUtils.Touch.Glob-", Guid.NewGuid().ToString( "N" ) )
			);
			Directory.CreateDirectory( PathValue );
		}

		public string PathValue { get; }

		public string CreateFile( string name ) {
			var path = PathFor( name );
			File.WriteAllText( path, "content" );
			return path;
		}

		public string PathFor( string name ) {
			return System.IO.Path.Combine( PathValue, name );
		}

		public void Dispose() {
			try {
				Directory.Delete( PathValue, recursive: true );
			} catch ( IOException ) {
			} catch ( UnauthorizedAccessException ) {
			}
		}
	}
}
