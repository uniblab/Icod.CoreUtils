namespace Icod.CoreUtils.TSort.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Verifies GNU Coreutils 9.11-compatible <c>tsort</c> behavior.</summary>
public sealed class CommandTests {
	private static readonly string Nl = Environment.NewLine;

	/// <summary>Verifies the synchronous compatibility wrapper uses the authoritative asynchronous engine.</summary>
	[Fact]
	public void SynchronousWrapperProducesOrder() {
		using var input = new StringReader( "a b" );
		using var output = new StringWriter();
		using var error = new StringWriter();
		var status = Command.Run( [ ], input, output, error );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( Lines( "a", "b" ), output.ToString() );
		Assert.Empty( error.ToString() );
	}

	/// <summary>Verifies that empty input succeeds without output.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task EmptyInputSucceeds() {
		var result = await RunAsync( " \t\n" );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Empty( result.Output );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies the first POSIX ordering fixture from GNU Coreutils 9.11.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task PosixFixtureOneUsesGnuQueueOrder() {
		var result = await RunAsync( "a b c c d e\ng g\nf g e f\nh h\n" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( Lines( "a", "c", "d", "h", "b", "e", "f", "g" ), result.OutputText );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies the second POSIX ordering fixture from GNU Coreutils 9.11.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task PosixFixtureTwoUsesBytewiseSeedOrder() {
		var result = await RunAsync( "b a\nd c\nz h x h r h\n" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( Lines( "b", "d", "r", "x", "z", "a", "c", "h" ), result.OutputText );
	}

	/// <summary>Verifies GNU's linear relation fixture.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task LinearFixtureProducesChainOrder() {
		var result = await RunAsync( "a b b c c d d e e f f g\n" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( Lines( "a", "b", "c", "d", "e", "f", "g" ), result.OutputText );
	}

	/// <summary>Verifies GNU's first tree fixture ordering.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FirstTreeFixturePreservesQueueOrder() {
		var result = await RunAsync( "a b b c c d d e e f f g\nc x x y y z\n" );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( Lines( "a", "b", "c", "x", "d", "y", "e", "z", "f", "g" ), result.OutputText );
	}

	/// <summary>Verifies GNU's deep tree fixture ordering.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TreeFixturePreservesSuccessorReleaseOrder() {
		var result = await RunAsync( "a b b c c d d e e f f g\nc x x y y z\nf r r s s t\n" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( Lines( "a", "b", "c", "x", "d", "y", "e", "z", "f", "r", "g", "s", "t" ), result.OutputText );
	}

	/// <summary>Verifies that equal pairs declare nodes rather than self-loops.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task EqualPairDeclaresNode() {
		var result = await RunAsync( "alone alone" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( Lines( "alone" ), result.OutputText );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies that repeated relations retain balanced incoming counts.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DuplicateRelationsRemainValid() {
		var result = await RunAsync( "a b a b b c" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( Lines( "a", "b", "c" ), result.OutputText );
	}

	/// <summary>Verifies that a two-node loop is reported, broken, and followed by remaining output.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CycleOneMatchesGnuRecovery() {
		var result = await RunFileAsync( "t b\nt s\ns t\n" );
		Assert.Equal( 1, result.Status );
		Assert.Equal( Lines( "s", "t", "b" ), result.OutputText );
		Assert.Equal(
			string.Concat(
				"tsort: ", result.SourceName, ": input contains a loop:", Nl,
				"tsort: s", Nl,
				"tsort: t", Nl
			),
			result.ErrorText
		);
	}

	/// <summary>Verifies GNU's loop recovery when an acyclic successor follows the cycle.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CycleTwoMatchesGnuRecovery() {
		var result = await RunFileAsync( "t x\nt s\ns t\n" );
		Assert.Equal( 1, result.Status );
		Assert.Equal( Lines( "s", "t", "x" ), result.OutputText );
		Assert.Equal(
			string.Concat(
				"tsort: ", result.SourceName, ": input contains a loop:", Nl,
				"tsort: s", Nl,
				"tsort: t", Nl
			),
			result.ErrorText
		);
	}

	/// <summary>Verifies repeated GNU loop recovery and the ignored <c>-w</c> option.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CycleThreeReportsBothLoops() {
		var result = await RunFileAsync( "a a\na b\na c\nc a\nb a", "-w" );
		Assert.Equal( 1, result.Status );
		Assert.Equal( Lines( "a", "c", "b" ), result.OutputText );
		Assert.Equal(
			string.Concat(
				"tsort: ", result.SourceName, ": input contains a loop:", Nl,
				"tsort: a", Nl,
				"tsort: b", Nl,
				"tsort: ", result.SourceName, ": input contains a loop:", Nl,
				"tsort: a", Nl,
				"tsort: c", Nl
			),
			result.ErrorText
		);
	}

	/// <summary>Verifies the stable odd-token diagnostic.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task OddTokenCountFails() {
		var result = await RunAsync( "a" );
		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
		Assert.Equal( string.Concat( "tsort: -: input contains an odd number of tokens", Nl ), result.ErrorText );
	}

	/// <summary>Verifies that only one input operand is accepted.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ExtraOperandFailsWithTryHelp() {
		var result = await RunAsync( string.Empty, "f", "g" );
		Assert.Equal( 1, result.Status );
		Assert.Equal(
			string.Concat(
				"tsort: extra operand 'g'", Nl,
				"Try 'tsort --help' for more information.", Nl
			),
			result.ErrorText
		);
	}

	/// <summary>Verifies that an option may follow the operand under GNU permutation rules.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CompatibilityOptionMayFollowFileOperand() {
		var result = await RunFileAsync( "a b", "-w", optionAfterFile: true );
		Assert.Equal( 0, result.Status );
		Assert.Equal( Lines( "a", "b" ), result.OutputText );
	}

	/// <summary>Verifies that an explicit dash selects the injected standard input.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ExplicitDashReadsStandardInput() {
		var result = await RunAsync( "a b", "-" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( Lines( "a", "b" ), result.OutputText );
	}

	/// <summary>Verifies that <c>--</c> ends option recognition.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DoubleDashTreatsFollowingTextAsOperand() {
		var result = await RunAsync( string.Empty, "--", "-w" );
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Contains( "-w: No such file or directory", result.ErrorText );
	}

	/// <summary>Verifies unique GNU long-option abbreviation.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task UniqueLongOptionAbbreviationIsAccepted() {
		var result = await RunAsync( string.Empty, "--ver" );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( string.Concat( "tsort (Icod.CoreUtils) 1.0", Nl ), result.OutputText );
	}

	/// <summary>Verifies that carriage return is token data rather than a separator.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CarriageReturnRemainsInsideToken() {
		var result = await RunAsync( "a\rb a\rb" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( string.Concat( "a\rb", Nl ), result.OutputText );
	}

	/// <summary>Verifies GNU C-string canonicalization when a token contains NUL.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task EmbeddedNulMatchesGnuNodeIdentity() {
		var result = await RunAsync( "a\0x a\0x" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( Lines( "a" ), result.OutputText );
	}

	/// <summary>Verifies unsigned bytewise ordering and preservation of non-UTF-8 node bytes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task OrdersAndPreservesHighBitBytes() {
		var result = await RunBytesAsync(
			[ 0xff, (byte)' ', 0xff, (byte)' ', 0x80, (byte)' ', 0x80 ]
		);
		var newline = Encoding.UTF8.GetBytes( Nl );
		var expected = new byte[ 2 + ( 2 * newline.Length ) ];
		expected[ 0 ] = 0x80;
		newline.CopyTo( expected, 1 );
		expected[ 1 + newline.Length ] = 0xff;
		newline.CopyTo( expected, 2 + newline.Length );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( expected, result.Output );
	}

	/// <summary>Verifies help output without consuming standard input.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task HelpSucceeds() {
		var result = await RunAsync( "unused", "--help" );
		Assert.Equal( 0, result.Status );
		Assert.StartsWith( string.Concat( "Usage: tsort [OPTION] [FILE]", Nl ), result.OutputText );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies version output.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task VersionSucceeds() {
		var result = await RunAsync( string.Empty, "--version" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( string.Concat( "tsort (Icod.CoreUtils) 1.0", Nl ), result.OutputText );
	}

	/// <summary>Verifies that an unknown option follows the conventional diagnostic path.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task UnknownOptionFailsWithTryHelp() {
		var result = await RunAsync( string.Empty, "--definitely-unknown" );
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Contains( "option", result.ErrorText.ToLowerInvariant() );
		Assert.Contains( "Try 'tsort --help'", result.ErrorText );
	}

	/// <summary>Verifies the controlled missing-file diagnostic.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task MissingFileFailsCleanly() {
		var path = Path.Combine( Path.GetTempPath(), string.Concat( "tsort-missing-", Guid.NewGuid().ToString( "N" ) ) );
		var result = await RunAsync( string.Empty, path );
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Contains( path, result.ErrorText );
		Assert.Contains( "No such file or directory", result.ErrorText );
	}

	/// <summary>Verifies that an unusable binary output becomes a controlled failure.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task OutputConstructionFailureIsControlled() {
		using var input = new MemoryStream( Encoding.UTF8.GetBytes( "a b" ), writable: false );
		var output = new MemoryStream();
		output.Dispose();
		using var error = new MemoryStream();
		var status = await Command.RunAsync(
			Array.Empty<string>(),
			new CommandContext(
				"tsort",
				TextReader.Null,
				TextWriter.Null,
				TextWriter.Null,
				input,
				output,
				error
			)
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains(
			"writable",
			Encoding.UTF8.GetString( error.ToArray() ).ToLowerInvariant()
		);
	}

	/// <summary>Verifies input failures become controlled source diagnostics.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReadFailureIsControlled() {
		using var input = new ThrowingReadStream();
		using var output = new MemoryStream();
		using var error = new MemoryStream();
		var status = await Command.RunAsync(
			Array.Empty<string>(),
			new CommandContext(
				"tsort",
				TextReader.Null,
				TextWriter.Null,
				TextWriter.Null,
				input,
				output,
				error
			)
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "-: read error: simulated read failure", Encoding.UTF8.GetString( error.ToArray() ) );
		Assert.True( input.CanRead );
	}

	/// <summary>Verifies output write failures become controlled command failures.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task WriteFailureIsControlled() {
		using var input = new MemoryStream( Encoding.UTF8.GetBytes( "a b" ), writable: false );
		using var output = new ThrowingWriteStream();
		using var error = new MemoryStream();
		var status = await Command.RunAsync(
			Array.Empty<string>(),
			new CommandContext(
				"tsort",
				TextReader.Null,
				TextWriter.Null,
				TextWriter.Null,
				input,
				output,
				error
			)
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "simulated output failure", Encoding.UTF8.GetString( error.ToArray() ) );
		Assert.True( output.CanWrite );
	}

	/// <summary>Verifies successful execution preserves every injected standard stream.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SuccessLeavesInjectedStreamsOpen() {
		var input = new MemoryStream( Encoding.UTF8.GetBytes( "a b" ), writable: false );
		var output = new MemoryStream();
		var error = new MemoryStream();
		var status = await Command.RunAsync(
			Array.Empty<string>(),
			new CommandContext(
				"tsort",
				TextReader.Null,
				TextWriter.Null,
				TextWriter.Null,
				input,
				output,
				error
			)
		);
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.True( input.CanRead );
		Assert.True( output.CanWrite );
		Assert.True( error.CanWrite );
		input.Dispose();
		output.Dispose();
		error.Dispose();
	}

	/// <summary>Verifies a deep graph is processed without recursive traversal.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DeepChainDoesNotRequireRecursion() {
		const int edgeCount = 5_000;
		var input = new StringBuilder();
		for ( var index = 0; index < edgeCount; index++ ) {
			input.Append( 'n' ).Append( index ).Append( ' ' ).Append( 'n' ).Append( index + 1 ).Append( '\n' );
		}
		var result = await RunAsync( input.ToString() );
		var lines = result.OutputText.Split( Nl, StringSplitOptions.RemoveEmptyEntries );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( edgeCount + 1, lines.Length );
		Assert.Equal( "n0", lines[ 0 ] );
		Assert.Equal( string.Concat( "n", edgeCount ), lines[ ^1 ] );
	}

	/// <summary>Verifies cancellation status and injected-stream ownership.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CancellationLeavesInjectedStreamsOpen() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var input = new MemoryStream( Encoding.UTF8.GetBytes( "a b" ), writable: false );
		var output = new MemoryStream();
		var error = new MemoryStream();
		var status = await Command.RunAsync(
			Array.Empty<string>(),
			new CommandContext(
				"tsort",
				TextReader.Null,
				TextWriter.Null,
				TextWriter.Null,
				input,
				output,
				error,
				cancellation.Token
			)
		);
		Assert.Equal( CommandExitCodes.Canceled, status );
		Assert.True( input.CanRead );
		Assert.True( output.CanWrite );
		Assert.True( error.CanWrite );
		input.Dispose();
		output.Dispose();
		error.Dispose();
	}

	private static string Lines( params string[] values ) => string.Concat(
		string.Join( Nl, values ),
		Nl
	);

	private static string FormatSourceName( string sourceName ) {
		foreach ( var character in sourceName ) {
			if (
				!char.IsLetterOrDigit( character )
				&& '/' != character
				&& '\\' != character
				&& '.' != character
				&& '_' != character
				&& '-' != character
				&& ':' != character
			) {
				return string.Concat(
					"'",
					sourceName.Replace( "'", "'\\''", StringComparison.Ordinal ),
					"'"
				);
			}
		}
		return sourceName;
	}

	private static Task<RunResult> RunAsync( string inputText, params string[] args ) => RunBytesAsync(
		Encoding.UTF8.GetBytes( inputText ),
		args
	);

	private static async Task<RunResult> RunBytesAsync( byte[] inputBytes, params string[] args ) {
		var input = new MemoryStream( inputBytes, writable: false );
		var output = new MemoryStream();
		var error = new MemoryStream();
		var status = await Command.RunAsync(
			args,
			new CommandContext(
				"tsort",
				TextReader.Null,
				TextWriter.Null,
				TextWriter.Null,
				input,
				output,
				error
			)
		);
		var result = new RunResult( status, output.ToArray(), error.ToArray(), "-" );
		input.Dispose();
		output.Dispose();
		error.Dispose();
		return result;
	}

	private static async Task<RunResult> RunFileAsync(
		string inputText,
		string? option = null,
		bool optionAfterFile = false
	) {
		var path = Path.Combine( Path.GetTempPath(), string.Concat( "tsort-", Guid.NewGuid().ToString( "N" ), ".txt" ) );
		await File.WriteAllTextAsync( path, inputText, new UTF8Encoding( false ) );
		try {
			var args = null == option
				? new[] { path }
				: optionAfterFile ? new[] { path, option! } : new[] { option!, path };
			var standardInput = new MemoryStream();
			var output = new MemoryStream();
			var error = new MemoryStream();
			var status = await Command.RunAsync(
				args,
				new CommandContext(
					"tsort",
					TextReader.Null,
					TextWriter.Null,
					TextWriter.Null,
					standardInput,
					output,
					error
				)
			);
			var result = new RunResult( status, output.ToArray(), error.ToArray(), FormatSourceName( path ) );
			standardInput.Dispose();
			output.Dispose();
			error.Dispose();
			return result;
		} finally {
			File.Delete( path );
		}
	}

	private sealed class ThrowingReadStream : Stream {
		/// <inheritdoc/>
		public override bool CanRead => true;

		/// <inheritdoc/>
		public override bool CanSeek => false;

		/// <inheritdoc/>
		public override bool CanWrite => false;

		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Position {
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override void Flush() {
		}

		/// <inheritdoc/>
		public override int Read( byte[] buffer, int offset, int count ) => throw new IOException( "simulated read failure" );

		/// <inheritdoc/>
		public override ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) => throw new IOException( "simulated read failure" );

		/// <inheritdoc/>
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void SetLength( long value ) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void Write( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
	}

	private sealed class ThrowingWriteStream : Stream {
		/// <inheritdoc/>
		public override bool CanRead => false;

		/// <inheritdoc/>
		public override bool CanSeek => false;

		/// <inheritdoc/>
		public override bool CanWrite => true;

		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Position {
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override void Flush() {
		}

		/// <inheritdoc/>
		public override int Read( byte[] buffer, int offset, int count ) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void SetLength( long value ) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void Write( byte[] buffer, int offset, int count ) => throw new IOException( "simulated output failure" );

		/// <inheritdoc/>
		public override ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) => throw new IOException( "simulated output failure" );
	}

	private sealed record RunResult( int Status, byte[] Output, byte[] Error, string SourceName ) {
		/// <summary>Gets standard error decoded as UTF-8 for textual assertions.</summary>
		internal string ErrorText => Encoding.UTF8.GetString( this.Error );

		/// <summary>Gets standard output decoded as UTF-8 for textual assertions.</summary>
		internal string OutputText => Encoding.UTF8.GetString( this.Output );
	}
}
