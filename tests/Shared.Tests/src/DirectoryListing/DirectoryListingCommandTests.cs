namespace Icod.CoreUtils.Shared.Tests.DirectoryListing;

using Icod.CoreUtils.Shared.DirectoryListing;
using Icod.CommandFramework.Terminal;
using Xunit;

/// <summary>Verifies the shared listing engine through its injectable command boundary.</summary>
public sealed class DirectoryListingCommandTests {
	/// <summary>Verifies a redirected single-column listing uses one shared engine for files and directories.</summary>
	[Fact]
	public async Task ListsDirectoryContentsWithClassification() {
		var root = CreateTemporaryDirectory();
		try {
			File.WriteAllText( System.IO.Path.Combine( root, "beta.txt" ), "beta" );
			File.WriteAllText( System.IO.Path.Combine( root, "alpha.txt" ), "alpha" );
			Directory.CreateDirectory( System.IO.Path.Combine( root, "nested" ) );
			var output = new StringWriter();
			var error = new StringWriter();

			var exitCode = await DirectoryListingCommand.RunAsync(
				DirectoryListingProfile.Ls,
				"ls",
				new[] { "-1F", "--quoting-style=literal", root },
				new StringReader( string.Empty ),
				output,
				error,
				presentationProvider: CreatePresentationProvider(),
				environmentProvider: new FakeEnvironmentVariableProvider()
			);

			Assert.Equal( 0, exitCode );
			Assert.Equal( string.Empty, error.ToString() );
			Assert.Equal(
				new[] { "alpha.txt", "beta.txt", "nested/" },
				ReadOutputLines( output )
			);
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies the vdir profile delegates to metadata-backed long output.</summary>
	[Fact]
	public async Task VDirProfileProducesLongMetadataRows() {
		var root = CreateTemporaryDirectory();
		try {
			File.WriteAllText( System.IO.Path.Combine( root, "item.txt" ), "content" );
			var output = new StringWriter();
			var error = new StringWriter();

			var exitCode = await DirectoryListingCommand.RunAsync(
				DirectoryListingProfile.VDir,
				"vdir",
				new[] { "--quoting-style=literal", root },
				new StringReader( string.Empty ),
				output,
				error,
				presentationProvider: CreatePresentationProvider(),
				environmentProvider: new FakeEnvironmentVariableProvider()
			);

			var lines = ReadOutputLines( output );
			Assert.Equal( 0, exitCode );
			Assert.Equal( string.Empty, error.ToString() );
			Assert.Equal( 2, lines.Count );
			Assert.StartsWith( "total ", lines[ 0 ] );
			Assert.EndsWith( " item.txt", lines[ 1 ] );
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies recursive headers retain the complete descended pathname.</summary>
	[Fact]
	public async Task RecursiveHeadersUseDescendedPaths() {
		var root = CreateTemporaryDirectory();
		try {
			var nested = Directory.CreateDirectory( System.IO.Path.Combine( root, "nested" ) ).FullName;
			File.WriteAllText( System.IO.Path.Combine( nested, "leaf.txt" ), "leaf" );
			var output = new StringWriter();
			var error = new StringWriter();

			var exitCode = await DirectoryListingCommand.RunAsync(
				DirectoryListingProfile.Ls,
				"ls",
				new[] { "-R1", "--quoting-style=literal", root },
				new StringReader( string.Empty ),
				output,
				error,
				presentationProvider: CreatePresentationProvider(),
				environmentProvider: new FakeEnvironmentVariableProvider()
			);

			var lines = ReadOutputLines( output );
			Assert.Equal( 0, exitCode );
			Assert.Contains( string.Concat( root, ":" ), lines );
			Assert.Contains( string.Concat( nested, ":" ), lines );
			Assert.Equal( string.Empty, error.ToString() );
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies ANSI color controls do not consume terminal column width.</summary>
	[Fact]
	public async Task ColorControlsDoNotConsumeColumnWidth() {
		var root = CreateTemporaryDirectory();
		try {
			File.WriteAllText( System.IO.Path.Combine( root, "a" ), "a" );
			File.WriteAllText( System.IO.Path.Combine( root, "b" ), "b" );
			var output = new StringWriter();
			var error = new StringWriter();
			var environment = new FakeEnvironmentVariableProvider(
				new Dictionary<string, string?> { [ "LS_COLORS" ] = "fi=31" }
			);

			var exitCode = await DirectoryListingCommand.RunAsync(
				DirectoryListingProfile.Ls,
				"ls",
				new[] { "-C", "--color=always", "--quoting-style=literal", root },
				new StringReader( string.Empty ),
				output,
				error,
				presentationProvider: CreatePresentationProvider( 4 ),
				environmentProvider: environment
			);

			Assert.Equal( 0, exitCode );
			Assert.Single( ReadOutputLines( output ) );
			Assert.Equal( string.Empty, error.ToString() );
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies reverse ordering does not undo directory-first grouping.</summary>
	[Fact]
	public async Task ReverseOrderingPreservesDirectoryFirstGrouping() {
		var root = CreateTemporaryDirectory();
		try {
			Directory.CreateDirectory( System.IO.Path.Combine( root, "nested" ) );
			File.WriteAllText( System.IO.Path.Combine( root, "alpha.txt" ), "alpha" );
			File.WriteAllText( System.IO.Path.Combine( root, "zeta.txt" ), "zeta" );
			var output = new StringWriter();
			var error = new StringWriter();

			var exitCode = await DirectoryListingCommand.RunAsync(
				DirectoryListingProfile.Ls,
				"ls",
				new[] { "-1Fr", "--group-directories-first", "--quoting-style=literal", root },
				new StringReader( string.Empty ),
				output,
				error,
				presentationProvider: CreatePresentationProvider(),
				environmentProvider: new FakeEnvironmentVariableProvider()
			);

			Assert.Equal( 0, exitCode );
			Assert.Equal( new[] { "nested/", "zeta.txt", "alpha.txt" }, ReadOutputLines( output ) );
			Assert.Equal( string.Empty, error.ToString() );
		} finally {
			Directory.Delete( root, true );
		}
	}

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), ".icod-listing-" + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( path );
		return path;
	}

	private static IReadOnlyList<string> ReadOutputLines( StringWriter output ) {
		return output.ToString()
			.Split( new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries );
	}

	private static TerminalPresentationProvider CreatePresentationProvider( int width = 80 ) {
		return new TerminalPresentationProvider(
			new FakeTerminalDeviceProvider(),
			new FakeEnvironmentVariableProvider(),
			new TerminalPresentationOptions { FallbackWidth = width }
		);
	}

	private sealed class FakeTerminalDeviceProvider : ITerminalDeviceProvider {
		/// <inheritdoc/>
		public TerminalDeviceObservation Observe( TerminalStreamKind stream ) {
			return TerminalDeviceObservation.Redirected();
		}
	}

	private sealed class FakeEnvironmentVariableProvider : IEnvironmentVariableProvider {
		private readonly IReadOnlyDictionary<string, string?> values;

		/// <summary>Initializes an optional dictionary-backed environment provider.</summary>
		/// <param name="values">Environment values.</param>
		public FakeEnvironmentVariableProvider( IReadOnlyDictionary<string, string?>? values = null ) {
			this.values = values ?? new Dictionary<string, string?>();
		}

		/// <inheritdoc/>
		public string? GetValue( string name ) {
			return this.values.TryGetValue( name, out var value ) ? value : null;
		}
	}
}
