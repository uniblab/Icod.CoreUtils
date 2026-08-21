namespace Icod.CoreUtils.CSplit.Tests;

using System.Text;
using Icod.CommandFramework.Diagnostics;
using CSplitCommand = Icod.CoreUtils.CSplit.Command;
using Xunit;

/// <summary>Exercises GNU-compatible csplit command behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies numeric addresses split before the selected line and report byte counts.</summary>
	[Fact]
	public async Task SplitsAtNumericAddress() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllBytesAsync( input, "1\n2\n3\n4\n"u8.ToArray() );

		var result = await RunAsync( new[] { "-f", prefix, input, "3" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "1\n2\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "00" ) );
		Assert.Equal( "3\n4\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "01" ) );
		Assert.Equal( string.Concat( "4", Environment.NewLine, "4", Environment.NewLine ), result.OutputText );
	}

	/// <summary>Verifies an explicitly signed positive numeric address is accepted.</summary>
	[Fact]
	public async Task AcceptsSignedPositiveNumericAddress() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\nc\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "+2" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Equal( "b\nc\n", await File.ReadAllTextAsync( prefix + "01" ) );
	}

	/// <summary>Verifies multiple numeric addresses produce consecutive sections.</summary>
	[Fact]
	public async Task SplitsAtMultipleNumericAddresses() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "1\n2\n3\n4\n5\n6\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "3", "5" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "1\n2\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Equal( "3\n4\n", await File.ReadAllTextAsync( prefix + "01" ) );
		Assert.Equal( "5\n6\n", await File.ReadAllTextAsync( prefix + "02" ) );
		Assert.Empty( result.Output );
	}

	/// <summary>Verifies numeric repetition uses absolute multiples of the address.</summary>
	[Fact]
	public async Task RepeatsNumericAddress() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "1\n2\n3\n4\n5\n6\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "3", "{1}" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "1\n2\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Equal( "3\n4\n5\n", await File.ReadAllTextAsync( prefix + "01" ) );
		Assert.Equal( "6\n", await File.ReadAllTextAsync( prefix + "02" ) );
	}

	/// <summary>Verifies an explicitly signed positive repeat count is accepted.</summary>
	[Fact]
	public async Task AcceptsSignedPositiveRepeatCount() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "1\n2\n3\n4\n5\n6\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "3", "{+1}" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "1\n2\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Equal( "3\n4\n5\n", await File.ReadAllTextAsync( prefix + "01" ) );
		Assert.Equal( "6\n", await File.ReadAllTextAsync( prefix + "02" ) );
	}

	/// <summary>Verifies GNU basic regular expressions select a line boundary.</summary>
	[Fact]
	public async Task SplitsAtBasicRegularExpression() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\nMARK\nc\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "/^MARK$/" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\nb\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Equal( "MARK\nc\n", await File.ReadAllTextAsync( prefix + "01" ) );
	}

	/// <summary>Verifies positive regex offsets move the split point after the match.</summary>
	[Fact]
	public async Task AppliesPositiveRegularExpressionOffset() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\nMARK\nc\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "/^MARK$/+1" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\nb\nMARK\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Equal( "c\n", await File.ReadAllTextAsync( prefix + "01" ) );
	}

	/// <summary>Verifies negative regex offsets preserve buffered lines for the following section.</summary>
	[Fact]
	public async Task AppliesNegativeRegularExpressionOffset() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\nMARK\nc\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "/^MARK$/-1" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Equal( "b\nMARK\nc\n", await File.ReadAllTextAsync( prefix + "01" ) );
	}

	/// <summary>Verifies percent-delimited regex sections are skipped without consuming a file number.</summary>
	[Fact]
	public async Task SuppressesPercentDelimitedSection() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\nMARK\nc\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "%^MARK$%" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "MARK\nc\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.False( File.Exists( prefix + "01" ) );
	}

	/// <summary>Verifies regex repetition retains each matching line as the next section start.</summary>
	[Fact]
	public async Task RepeatsRegularExpression() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\nMARK\nc\nMARK\nd\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "/^MARK$/", "{1}" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\nb\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Equal( "MARK\nc\n", await File.ReadAllTextAsync( prefix + "01" ) );
		Assert.Equal( "MARK\nd\n", await File.ReadAllTextAsync( prefix + "02" ) );
	}

	/// <summary>Verifies regex star repetition terminates successfully when no later match exists.</summary>
	[Fact]
	public async Task RepeatsRegularExpressionUntilExhausted() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nMARK\nb\nMARK\nc\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "/^MARK$/", "{*}" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Equal( "MARK\nb\n", await File.ReadAllTextAsync( prefix + "01" ) );
		Assert.Equal( "MARK\nc\n", await File.ReadAllTextAsync( prefix + "02" ) );
		Assert.False( File.Exists( prefix + "03" ) );
	}

	/// <summary>Verifies matching lines can be removed from output.</summary>
	[Fact]
	public async Task SuppressesMatchedLine() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\nMARK\nc\n" );

		var result = await RunAsync( new[] { "-q", "--suppress-matched", "-f", prefix, input, "/^MARK$/" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\nb\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Equal( "c\n", await File.ReadAllTextAsync( prefix + "01" ) );
	}

	/// <summary>Verifies newline is excluded from regular-expression matching.</summary>
	[Fact]
	public async Task MatchesLineEndBeforeNewline() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllBytesAsync( input, "x\nEND\ny"u8.ToArray() );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "/END$/" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "x\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "00" ) );
		Assert.Equal( "END\ny"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "01" ) );
	}

	/// <summary>Verifies malformed UTF-8 source bytes remain byte-for-byte intact.</summary>
	[Fact]
	public async Task PreservesInvalidUtf8Bytes() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		var bytes = new byte[] { 0xff, (byte)'\n', (byte)'B', (byte)'\n', 0xfe };
		await File.WriteAllBytesAsync( input, bytes );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "/^B$/" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( new byte[] { 0xff, (byte)'\n' }, await File.ReadAllBytesAsync( prefix + "00" ) );
		Assert.Equal( new byte[] { (byte)'B', (byte)'\n', 0xfe }, await File.ReadAllBytesAsync( prefix + "01" ) );
	}

	/// <summary>Verifies standard input need not be seekable.</summary>
	[Fact]
	public async Task SplitsNonseekableStandardInput() {
		using var directory = new TemporaryDirectory();
		var prefix = directory.File( "part" );
		await using var input = new NonSeekableReadStream( "a\nb\nc\n"u8.ToArray() );

		var result = await RunAsync( new[] { "-q", "-f", prefix, "-", "2" }, input );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "00" ) );
		Assert.Equal( "b\nc\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "01" ) );
	}

	/// <summary>Verifies custom decimal digit width.</summary>
	[Fact]
	public async Task UsesRequestedDigitWidth() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\n" );

		var result = await RunAsync( new[] { "-q", "-n", "4", "-f", prefix, input, "2" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.True( File.Exists( prefix + "0000" ) );
		Assert.True( File.Exists( prefix + "0001" ) );
	}

	/// <summary>Verifies an explicitly signed positive digit width is accepted.</summary>
	[Fact]
	public async Task UsesSignedPositiveDigitWidth() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\n" );

		var result = await RunAsync( new[] { "-q", "-n", "+3", "-f", prefix, input, "2" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.True( File.Exists( prefix + "000" ) );
		Assert.True( File.Exists( prefix + "001" ) );
	}

	/// <summary>Verifies printf-style suffix formats and literal percent signs.</summary>
	[Fact]
	public async Task UsesSuffixFormat() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllTextAsync( input, "a\nb\n" );

		var result = await RunAsync( new[] { "-q", "-b", "pre%03x%%", "-f", prefix, input, "2" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.True( File.Exists( prefix + "pre000%" ) );
		Assert.True( File.Exists( prefix + "pre001%" ) );
	}

	/// <summary>Verifies empty output files can be elided and their number reused.</summary>
	[Fact]
	public async Task ElidesEmptyFiles() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\n" );

		var result = await RunAsync( new[] { "-q", "-z", "-f", prefix, input, "1" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\nb\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.False( File.Exists( prefix + "01" ) );
	}

	/// <summary>Verifies quiet mode suppresses byte counts.</summary>
	[Theory]
	[InlineData( "-q" )]
	[InlineData( "-s" )]
	[InlineData( "--quiet" )]
	[InlineData( "--silent" )]
	public async Task SuppressesByteCounts( string option ) {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\n" );

		var result = await RunAsync( new[] { option, "-f", prefix, input, "2" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Empty( result.Output );
	}

	/// <summary>Verifies default failure cleanup removes all generated files.</summary>
	[Fact]
	public async Task RemovesFilesAfterMatchFailure() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\n" );

		var result = await RunAsync( new[] { "-f", prefix, input, "/missing/" } );

		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Empty( Directory.GetFiles( directory.Path, "part*" ) );
		Assert.Contains( "match not found", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies keep-files retains the partial piece produced before a match failure.</summary>
	[Fact]
	public async Task KeepsFilesAfterMatchFailure() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\n" );

		var result = await RunAsync( new[] { "-k", "-q", "-f", prefix, input, "/missing/" } );

		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Equal( "a\nb\n", await File.ReadAllTextAsync( prefix + "00" ) );
	}

	/// <summary>Verifies keep-files retains bytes consumed before an out-of-range numeric address.</summary>
	[Fact]
	public async Task KeepsPartialNumericPieceOnFailure() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\n" );

		var result = await RunAsync( new[] { "-k", "-q", "-f", prefix, input, "5" } );

		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Equal( "a\nb\n", await File.ReadAllTextAsync( prefix + "00" ) );
	}

	/// <summary>Verifies suppress-matched accepts the boundary immediately after the final input line.</summary>
	[Fact]
	public async Task AcceptsSuppressedAddressImmediatelyAfterInput() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\n" );

		var result = await RunAsync( new[] { "-q", "--suppress-matched", "-f", prefix, input, "3" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\nb\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Empty( await File.ReadAllBytesAsync( prefix + "01" ) );
	}

	/// <summary>Verifies suppress-matched rejects an address more than one line past end of input.</summary>
	[Fact]
	public async Task RejectsDistantSuppressedNumericAddress() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\n" );

		var result = await RunAsync( new[] { "-k", "-q", "--suppress-matched", "-f", prefix, input, "4" } );

		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Equal( "a\nb\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Contains( "line number out of range", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies invalid regular expressions are controlled usage errors.</summary>
	[Fact]
	public async Task RejectsInvalidRegularExpression() {
		var result = await RunAsync( new[] { "-", "/\\(/" } );

		Assert.Equal( CommandExitCodes.UsageError, result.Status );
		Assert.Contains( "invalid regular expression", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies malformed regex offsets are rejected before input is processed.</summary>
	[Fact]
	public async Task RejectsInvalidOffset() {
		var result = await RunAsync( new[] { "-", "/x/+bad" } );

		Assert.Equal( CommandExitCodes.UsageError, result.Status );
		Assert.Contains( "integer expected", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies malformed repeat counts are rejected.</summary>
	[Theory]
	[InlineData( "{x}" )]
	[InlineData( "{1" )]
	public async Task RejectsInvalidRepeatCount( string repeat ) {
		var result = await RunAsync( new[] { "-", "2", repeat } );

		Assert.Equal( CommandExitCodes.UsageError, result.Status );
	}

	/// <summary>Verifies suffix formats require exactly one supported conversion.</summary>
	[Theory]
	[InlineData( "plain" )]
	[InlineData( "%d-%x" )]
	[InlineData( "%s" )]
	public async Task RejectsInvalidSuffixFormat( string format ) {
		var result = await RunAsync( new[] { "-b", format, "-", "1" } );

		Assert.Equal( CommandExitCodes.UsageError, result.Status );
	}

	/// <summary>Verifies decreasing numeric patterns are rejected.</summary>
	[Fact]
	public async Task RejectsDecreasingNumericAddresses() {
		var result = await RunAsync( new[] { "-", "5", "3" } );

		Assert.Equal( CommandExitCodes.UsageError, result.Status );
		Assert.Contains( "smaller than preceding", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies equal numeric addresses produce a warning and an empty section.</summary>
	[Fact]
	public async Task WarnsForEqualNumericAddresses() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "part" );
		await File.WriteAllTextAsync( input, "a\nb\nc\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "2", "2" } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\n", await File.ReadAllTextAsync( prefix + "00" ) );
		Assert.Empty( await File.ReadAllBytesAsync( prefix + "01" ) );
		Assert.Equal( "b\nc\n", await File.ReadAllTextAsync( prefix + "02" ) );
		Assert.Contains( "warning:", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies output cannot overwrite the input operand.</summary>
	[Fact]
	public async Task PreventsInputOverwrite() {
		using var directory = new TemporaryDirectory();
		var prefix = directory.File( "piece" );
		var input = prefix + "00";
		await File.WriteAllTextAsync( input, "a\nb\n" );

		var result = await RunAsync( new[] { "-q", "-f", prefix, input, "2" } );

		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Equal( "a\nb\n", await File.ReadAllTextAsync( input ) );
		Assert.Contains( "overwrite input", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies a missing input operand is reported through the shared usage status.</summary>
	[Fact]
	public async Task RequiresInput() {
		var result = await RunAsync( Array.Empty<string>() );

		Assert.Equal( CommandExitCodes.UsageError, result.Status );
		Assert.Contains( "missing operand", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies a missing pattern operand is reported through the shared usage status.</summary>
	[Fact]
	public async Task RequiresPattern() {
		var result = await RunAsync( new[] { "-" } );

		Assert.Equal( CommandExitCodes.UsageError, result.Status );
		Assert.Contains( "missing operand", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies help and version requests succeed through byte-capable output.</summary>
	[Theory]
	[InlineData( "--help", "Usage: csplit" )]
	[InlineData( "--version", "csplit (Icod.CoreUtils)" )]
	public async Task ReportsHelpAndVersion( string option, string expected ) {
		var result = await RunAsync( new[] { option } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Contains( expected, result.OutputText, StringComparison.Ordinal );
	}

	/// <summary>Verifies cancellation returns the shared canceled status and removes output by default.</summary>
	[Fact]
	public async Task ObservesCancellation() {
		using var directory = new TemporaryDirectory();
		var prefix = directory.File( "part" );
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		var result = await RunAsync(
			new[] { "-f", prefix, "-", "2" },
			new MemoryStream( "a\nb\n"u8.ToArray(), writable: false ),
			cancellation.Token
		);

		Assert.Equal( CommandExitCodes.Canceled, result.Status );
		Assert.Empty( Directory.GetFiles( directory.Path, "part*" ) );
	}

	private static async Task<CommandResult> RunAsync(
		string[] arguments,
		Stream? input = null,
		CancellationToken cancellationToken = default
	) {
		input ??= new MemoryStream( Array.Empty<byte>(), writable: false );
		await using var output = new MemoryStream();
		await using var error = new MemoryStream();
		using var outputText = new StringWriter();
		using var errorText = new StringWriter();
		var status = await CSplitCommand.RunAsync(
			arguments,
			new CommandContext(
				"csplit",
				TextReader.Null,
				outputText,
				errorText,
				input,
				output,
				error,
				cancellationToken
			)
		);
		return new CommandResult(
			status,
			output.ToArray(),
			Encoding.UTF8.GetString( output.ToArray() ),
			errorText.ToString()
		);
	}

	private sealed record CommandResult(
		int Status,
		byte[] Output,
		string OutputText,
		string Error
	);

	private sealed class TemporaryDirectory : IDisposable {
		public string Path { get; }

		public TemporaryDirectory() {
			this.Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				$"icod-csplit-tests-{Guid.NewGuid():N}"
			);
			Directory.CreateDirectory( this.Path );
		}

		public string File( string name ) => System.IO.Path.Combine( this.Path, name );

		public void Dispose() {
			try {
				Directory.Delete( this.Path, recursive: true );
			} catch {
				// Test cleanup must not hide an assertion failure.
			}
		}
	}

	private sealed class NonSeekableReadStream : Stream {
		private readonly MemoryStream inner;

		public NonSeekableReadStream( byte[] data ) {
			this.inner = new MemoryStream( data, writable: false );
		}

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position {
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}
		public override void Flush() {
		}
		public override int Read( byte[] buffer, int offset, int count ) => this.inner.Read( buffer, offset, count );
		public override int Read( Span<byte> buffer ) => this.inner.Read( buffer );
		public override ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) => this.inner.ReadAsync( buffer, cancellationToken );
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
		public override void SetLength( long value ) => throw new NotSupportedException();
		public override void Write( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
		protected override void Dispose( bool disposing ) {
			if ( disposing ) {
				this.inner.Dispose();
			}
			base.Dispose( disposing );
		}
		public override ValueTask DisposeAsync() {
			this.inner.Dispose();
			GC.SuppressFinalize( this );
			return ValueTask.CompletedTask;
		}
	}
}
