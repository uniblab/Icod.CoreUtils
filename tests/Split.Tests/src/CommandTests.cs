namespace Icod.CoreUtils.Split.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using SplitCommand = Icod.CoreUtils.Split.Command;
using Xunit;

/// <summary>Exercises GNU-compatible split command behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies line-mode output rotation and unterminated final records.</summary>
	[Fact]
	public async Task SplitsLinesAndRotatesOutputs() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, "a\nb\nc"u8.ToArray() );

		var result = await RunAsync( new[] { "-l", "2", input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\nb\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "aa" ) );
		Assert.Equal( "c"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "ab" ) );
	}

	/// <summary>Verifies byte splitting does not require seeking standard input.</summary>
	[Fact]
	public async Task SplitsNonseekableInputByBytes() {
		using var directory = new TemporaryDirectory();
		var prefix = directory.File( "byte" );
		await using var input = new NonSeekableReadStream( "abcdef"u8.ToArray() );

		var result = await RunAsync( new[] { "-b", "2", "-", prefix }, input );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "ab"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "aa" ) );
		Assert.Equal( "cd"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "ab" ) );
		Assert.Equal( "ef"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "ac" ) );
	}

	/// <summary>Verifies line-byte mode keeps ordinary records intact and splits oversized records.</summary>
	[Fact]
	public async Task HonorsLineByteBoundaries() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, "abcdef\nX\n"u8.ToArray() );

		var result = await RunAsync( new[] { "-C", "4", input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "abcd"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "aa" ) );
		Assert.Equal( "ef\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "ab" ) );
		Assert.Equal( "X\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "ac" ) );
	}

	/// <summary>Verifies numeric suffix starts and additional suffixes.</summary>
	[Fact]
	public async Task GeneratesNumericAndAdditionalSuffixes() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, "a\nb\nc\n"u8.ToArray() );

		var result = await RunAsync(
			new[] { "-l", "1", "--numeric-suffixes=8", "--additional-suffix=.part", input, prefix }
		);

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.True( File.Exists( prefix + "08.part" ) );
		Assert.True( File.Exists( prefix + "09.part" ) );
		Assert.True( File.Exists( prefix + "10.part" ) );
	}

	/// <summary>Verifies hexadecimal suffix starts.</summary>
	[Fact]
	public async Task GeneratesHexadecimalSuffixes() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, "a\nb\nc\n"u8.ToArray() );

		var result = await RunAsync( new[] { "-l1", "--hex-suffixes=14", input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.True( File.Exists( prefix + "0e" ) );
		Assert.True( File.Exists( prefix + "0f" ) );
		Assert.True( File.Exists( prefix + "10" ) );
	}

	/// <summary>Verifies size-balanced chunk generation.</summary>
	[Fact]
	public async Task BalancesByteChunks() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, "abcdefg"u8.ToArray() );

		var result = await RunAsync( new[] { "-n", "3", input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "abc"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "aa" ) );
		Assert.Equal( "de"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "ab" ) );
		Assert.Equal( "fg"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "ac" ) );
	}

	/// <summary>Verifies selected size-balanced chunks are written to standard output.</summary>
	[Fact]
	public async Task WritesSelectedByteChunkToStandardOutput() {
		var result = await RunAsync( new[] { "-n", "2/3" }, new MemoryStream( "abcdefg"u8.ToArray(), writable: false ) );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "de"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies line-balanced chunks never split records.</summary>
	[Fact]
	public async Task BalancesWholeRecords() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllTextAsync( input, string.Concat( Enumerable.Range( 1, 10 ).Select( value => $"{value}\n" ) ) );

		var result = await RunAsync( new[] { "-n", "l/3", input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( 4, File.ReadLines( prefix + "aa" ).Count() );
		Assert.Equal( 3, File.ReadLines( prefix + "ab" ).Count() );
		Assert.Equal( 3, File.ReadLines( prefix + "ac" ).Count() );
	}

	/// <summary>Verifies round-robin distribution with a NUL record separator.</summary>
	[Fact]
	public async Task DistributesRecordsRoundRobinWithNulSeparator() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, new byte[] { (byte)'a', 0, (byte)'b', 0, (byte)'c', 0, (byte)'d', 0, (byte)'e' } );

		var result = await RunAsync( new[] { "-t", "\\0", "-n", "r/2", input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( new byte[] { (byte)'a', 0, (byte)'c', 0, (byte)'e' }, await File.ReadAllBytesAsync( prefix + "aa" ) );
		Assert.Equal( new byte[] { (byte)'b', 0, (byte)'d', 0 }, await File.ReadAllBytesAsync( prefix + "ab" ) );
	}

	/// <summary>Verifies empty outputs from number mode can be omitted.</summary>
	[Fact]
	public async Task ElidesEmptyNumberOutputs() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, new byte[] { 1, 2 } );

		var result = await RunAsync( new[] { "-e", "-n", "4", input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.True( File.Exists( prefix + "aa" ) );
		Assert.True( File.Exists( prefix + "ab" ) );
		Assert.False( File.Exists( prefix + "ac" ) );
		Assert.False( File.Exists( prefix + "ad" ) );
	}

	/// <summary>Verifies a short oversized-record remainder can share a later complete record.</summary>
	[Fact]
	public async Task CombinesLineByteRemainderWithFollowingRecord() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, "abcde\nX\n"u8.ToArray() );

		var result = await RunAsync( new[] { "-C4", input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "abcd"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "aa" ) );
		Assert.Equal( "e\nX\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "ab" ) );
		Assert.False( File.Exists( prefix + "ac" ) );
	}

	/// <summary>Verifies number mode selects a fixed suffix width from its output count.</summary>
	[Fact]
	public async Task SizesNumberModeSuffixesBeforeWriting() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, Array.Empty<byte>() );

		var result = await RunAsync( new[] { "-d", "-n101", input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( 101, Directory.GetFiles( directory.Path, "piece*" ).Length );
		Assert.True( File.Exists( prefix + "000" ) );
		Assert.True( File.Exists( prefix + "100" ) );
	}

	/// <summary>Verifies an explicit suffix start disables automatic suffix expansion.</summary>
	[Fact]
	public async Task ExplicitNumericStartUsesFixedDefaultWidth() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, "abcdef"u8.ToArray() );

		var result = await RunAsync( new[] { "-b1", "--numeric-suffixes=95", input, prefix } );

		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Equal( 5, Directory.GetFiles( directory.Path, "piece*" ).Length );
		Assert.True( File.Exists( prefix + "95" ) );
		Assert.True( File.Exists( prefix + "99" ) );
	}

	/// <summary>Verifies suffix length zero restores automatic suffix expansion.</summary>
	[Fact]
	public async Task ZeroSuffixLengthRestoresAutomaticExpansion() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, Enumerable.Repeat( (byte)'x', 91 ).ToArray() );

		var result = await RunAsync( new[] { "-a0", "-d", "-b1", input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.True( File.Exists( prefix + "00" ) );
		Assert.True( File.Exists( prefix + "9000" ) );
	}

	/// <summary>Verifies line-balanced mode assigns records by byte-balanced source regions.</summary>
	[Fact]
	public async Task LineBalancedModeCanCreateAnEmptyMiddleChunk() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, "abcdefghij\nx\n"u8.ToArray() );

		var result = await RunAsync( new[] { "-n", "l/3", input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "abcdefghij\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "aa" ) );
		Assert.Empty( await File.ReadAllBytesAsync( prefix + "ab" ) );
		Assert.Equal( "x\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "ac" ) );
	}

	/// <summary>Verifies mutually exclusive split methods are rejected even when repeated.</summary>
	[Fact]
	public async Task RejectsMultipleSplitMethods() {
		var result = await RunAsync(
			new[] { "-l1", "-b1" },
			new MemoryStream( "abc"u8.ToArray(), writable: false )
		);

		Assert.Equal( CommandExitCodes.UsageError, result.Status );
		Assert.Contains( "more than one way", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies mode-specific flags are harmless when their mode is not selected.</summary>
	[Fact]
	public async Task AcceptsIrrelevantElideAndUnbufferedOptions() {
		using var directory = new TemporaryDirectory();
		var prefix = directory.File( "piece" );

		var result = await RunAsync(
			new[] { "-e", "-u", "-l1", "-", prefix },
			new MemoryStream( "a\n"u8.ToArray(), writable: false )
		);

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "aa" ) );
	}

	/// <summary>Verifies an empty record separator is rejected rather than treated as NUL.</summary>
	[Fact]
	public async Task RejectsEmptySeparator() {
		var result = await RunAsync( new[] { "--separator=" } );

		Assert.Equal( CommandExitCodes.UsageError, result.Status );
		Assert.Contains( "separator", result.Error, StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Verifies an output piece cannot overwrite its input pathname.</summary>
	[Fact]
	public async Task PreventsOutputFromOverwritingInput() {
		using var directory = new TemporaryDirectory();
		var prefix = directory.File( "piece" );
		var input = prefix + "aa";
		await File.WriteAllBytesAsync( input, "original"u8.ToArray() );

		var result = await RunAsync( new[] { "-b1", input, prefix } );

		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Equal( "original"u8.ToArray(), await File.ReadAllBytesAsync( input ) );
		Assert.Contains( "overwrite input", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies the obsolete numeric line-count syntax is routed through the shared parser.</summary>
	[Fact]
	public async Task SupportsLegacyNumericLineCount() {
		using var directory = new TemporaryDirectory();
		var prefix = directory.File( "piece" );

		var result = await RunAsync(
			new[] { "-2", "-", prefix },
			new MemoryStream( "a\nb\nc\n"u8.ToArray(), writable: false )
		);

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\nb\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "aa" ) );
		Assert.Equal( "c\n"u8.ToArray(), await File.ReadAllBytesAsync( prefix + "ab" ) );
	}

	/// <summary>Verifies suffix exhaustion preserves pieces already created.</summary>
	[Fact]
	public async Task PreservesCreatedFilesWhenSuffixesAreExhausted() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, Enumerable.Range( 0, 27 ).Select( value => (byte)value ).ToArray() );

		var result = await RunAsync( new[] { "-a", "1", "-b", "1", input, prefix } );

		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Equal( 26, Directory.GetFiles( directory.Path, "piece*" ).Length );
		Assert.Equal( new byte[] { 0 }, await File.ReadAllBytesAsync( prefix + "a" ) );
		Assert.Equal( new byte[] { 25 }, await File.ReadAllBytesAsync( prefix + "z" ) );
	}

	/// <summary>Verifies filter failures preserve output and propagate the filter status.</summary>
	[Fact]
	public async Task PreservesFilterOutputAfterFilterFailure() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, "abc\n"u8.ToArray() );
		var filter = OperatingSystem.IsWindows()
			? "more > \"%FILE%\" & exit /b 7"
			: "cat > \"$FILE\"; exit 7";

		var result = await RunAsync( new[] { "-l", "1", $"--filter={filter}", input, prefix } );

		Assert.Equal( 7, result.Status );
		Assert.Single( Directory.GetFiles( directory.Path, "piece*" ) );
		Assert.True( File.Exists( prefix + "aa" ) );
		Assert.Contains( "exit 7", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies empty input does not create a default output piece.</summary>
	[Fact]
	public async Task DoesNotCreateOutputForEmptyInput() {
		using var directory = new TemporaryDirectory();
		var input = directory.File( "input" );
		var prefix = directory.File( "piece" );
		await File.WriteAllBytesAsync( input, Array.Empty<byte>() );

		var result = await RunAsync( new[] { input, prefix } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Empty( Directory.GetFiles( directory.Path, "piece*" ) );
	}

	/// <summary>Verifies help and version requests succeed.</summary>
	[Theory]
	[InlineData( "--help", "Usage: split" )]
	[InlineData( "--version", "split (Icod.CoreUtils)" )]
	public async Task ReportsHelpAndVersion( string option, string expected ) {
		var result = await RunAsync( new[] { option } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Contains( expected, Encoding.UTF8.GetString( result.Output ), StringComparison.Ordinal );
	}

	/// <summary>Verifies cancellation returns the shared canceled status.</summary>
	[Fact]
	public async Task ObservesCancellation() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		var result = await RunAsync(
			new[] { "-b1" },
			new MemoryStream( new byte[] { 1 }, writable: false ),
			cancellation.Token
		);

		Assert.Equal( CommandExitCodes.Canceled, result.Status );
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
		var status = await SplitCommand.RunAsync(
			arguments,
			new CommandContext(
				"split",
				TextReader.Null,
				outputText,
				errorText,
				input,
				output,
				error,
				cancellationToken
			)
		);
		return new CommandResult( status, output.ToArray(), errorText.ToString() );
	}

	private sealed record CommandResult( int Status, byte[] Output, string Error );

	private sealed class TemporaryDirectory : IDisposable {
		public string Path { get; }

		public TemporaryDirectory() {
			this.Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				$"icod-split-tests-{Guid.NewGuid():N}"
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
		public override ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) => this.inner.ReadAsync( buffer, cancellationToken );
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
