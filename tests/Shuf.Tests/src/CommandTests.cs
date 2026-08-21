namespace Icod.CoreUtils.Shuf.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Defines a nonparallel collection for tests that inspect the process temporary directory.</summary>
[CollectionDefinition( "shuf temporary-spool tests", DisableParallelization = true )]
public sealed class TemporarySpoolCollection {
	/// <summary>Gets the collection name.</summary>
	public const string Name = "shuf temporary-spool tests";
}

/// <summary>Tests GNU-compatible input modes, external randomization, record preservation, and control paths.</summary>
[Collection( TemporarySpoolCollection.Name )]
public sealed class CommandTests {
	/// <summary>Verifies deterministic permutation, raw-byte preservation, and synthesized final termination.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task PermutesBinaryRecordsAndTerminatesFinalRecord() {
		using var random = new TemporaryFile( CreateRandomBytes( 2, 0 ) );
		var input = new byte[] { (byte)'a', (byte)'\n', 0xff, (byte)'\n', (byte)'c' };
		var result = await RunAsync( [ "--random-source", random.Path ], input );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal(
			new byte[] { (byte)'c', (byte)'\n', 0xff, (byte)'\n', (byte)'a', (byte)'\n' },
			result.Output
		);
	}

	/// <summary>Verifies that head selection is an unbiased without-replacement prefix.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task HeadCountSelectsWithoutReplacement() {
		using var random = new TemporaryFile( CreateRandomBytes( 2, 0 ) );
		var result = await RunAsync(
			[ "-n", "2", "--random-source", random.Path ],
			"a\nb\nc\n"u8.ToArray()
		);
		Assert.Equal( "c\nb\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies command-line echo records and inclusive range records.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsEchoAndInputRangeModes() {
		using var echoRandom = new TemporaryFile( CreateRandomBytes( 0 ) );
		using var rangeRandom = new TemporaryFile( CreateRandomBytes( 2, 0 ) );
		var echo = await RunAsync(
			[ "-e", "alpha", "beta", "--random-source", echoRandom.Path ],
			[]
		);
		var range = await RunAsync(
			[ "-i", "5-7", "-n", "2", "--random-source", rangeRandom.Path ],
			[]
		);
		Assert.Equal( "alpha\nbeta\n"u8.ToArray(), echo.Output );
		Assert.Equal( "7\n6\n"u8.ToArray(), range.Output );
	}

	/// <summary>Verifies finite repeat mode without consuming bytes when only one record exists.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RepeatModeSelectsWithReplacement() {
		using var random = new TemporaryFile( [] );
		var result = await RunAsync(
			[ "-r", "-n", "3", "--random-source", random.Path ],
			"only\n"u8.ToArray()
		);
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "only\nonly\nonly\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies GNU random-source validation for nonzero, zero, and empty output paths.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RandomSourceIsOpenedOnlyWhenTheExecutionRequiresIt() {
		var missingRandom = System.IO.Path.Combine( System.IO.Path.GetTempPath(), Guid.NewGuid().ToString( "N" ) );
		var oneRecord = await RunAsync(
			[ "-e", "only", "--random-source", missingRandom ],
			[]
		);
		var zeroOutput = await RunAsync(
			[ "-n", "0", "-e", "only", "--random-source", missingRandom ],
			[]
		);
		var emptyInput = await RunAsync(
			[ "--random-source", missingRandom ],
			[]
		);
		Assert.Equal( CommandExitCodes.Failure, oneRecord.Status );
		Assert.Contains( missingRandom, oneRecord.Error );
		Assert.Equal( CommandExitCodes.Success, zeroOutput.Status );
		Assert.Equal( CommandExitCodes.Success, emptyInput.Status );
	}

	/// <summary>Verifies that repeating an empty input is diagnosed.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RepeatModeRejectsEmptyInput() {
		var result = await RunAsync( [ "-r", "-n", "1" ], [] );
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Contains( "no lines to repeat", result.Error );
	}

	/// <summary>Verifies GNU output-opening order when repeat mode receives no records.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task EmptyRepeatInputIsDiagnosedAfterOutputIsOpened() {
		using var output = new TemporaryFile( "old"u8.ToArray() );
		var result = await RunAsync(
			[ "-r", "-n", "1", "-o", output.Path ],
			[]
		);
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Contains( "no lines to repeat", result.Error );
		Assert.Empty( await File.ReadAllBytesAsync( output.Path ) );
	}

	/// <summary>Verifies NUL-delimited record input and output.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsNullTerminatedRecords() {
		using var random = new TemporaryFile( CreateRandomBytes( 1, 0 ) );
		var input = new byte[] { (byte)'a', 0, (byte)'b', 0 };
		var result = await RunAsync(
			[ "-z", "--random-source", random.Path ],
			input
		);
		Assert.Equal( new byte[] { (byte)'b', 0, (byte)'a', 0 }, result.Output );
	}

	/// <summary>Verifies safe output when the destination is also the input file.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task OutputMayReplaceTheInputFileSafely() {
		using var input = new TemporaryFile( "a\nb\nc\n"u8.ToArray() );
		using var random = new TemporaryFile( CreateRandomBytes( 2, 0 ) );
		var result = await RunAsync(
			[ "--random-source", random.Path, "-o", input.Path, input.Path ],
			[]
		);
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Empty( result.Output );
		Assert.Equal( "c\nb\na\n"u8.ToArray(), await File.ReadAllBytesAsync( input.Path ) );
	}

	/// <summary>Verifies that a nonrepeat zero head count bypasses missing input and random-source files while creating output.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ZeroHeadCountDoesNotReadInputOrRandomSource() {
		using var output = new TemporaryFile( "old"u8.ToArray() );
		var missingInput = System.IO.Path.Combine( System.IO.Path.GetTempPath(), Guid.NewGuid().ToString( "N" ) );
		var missingRandom = System.IO.Path.Combine( System.IO.Path.GetTempPath(), Guid.NewGuid().ToString( "N" ) );
		var result = await RunAsync(
			[ "-n", "0", "-o", output.Path, "--random-source", missingRandom, missingInput ],
			[]
		);
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Empty( await File.ReadAllBytesAsync( output.Path ) );
	}

	/// <summary>Verifies that repeat mode validates its random source even when the requested count is zero.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ZeroHeadCountRepeatValidatesRandomSourceBeforeOutput() {
		using var output = new TemporaryFile( "preserve"u8.ToArray() );
		var missingRandom = System.IO.Path.Combine( System.IO.Path.GetTempPath(), Guid.NewGuid().ToString( "N" ) );
		var result = await RunAsync(
			[ "-r", "-n", "0", "-o", output.Path, "--random-source", missingRandom ],
			[]
		);
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Contains( missingRandom, result.Error );
		Assert.Equal( "preserve"u8.ToArray(), await File.ReadAllBytesAsync( output.Path ) );
	}

	/// <summary>Verifies that identical random-source bytes produce identical permutations.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RandomSourceMakesSelectionRepeatable() {
		var bytes = CreateRandomBytes( 1, 0 );
		using var firstRandom = new TemporaryFile( bytes );
		using var secondRandom = new TemporaryFile( bytes );
		var first = await RunAsync(
			[ "--random-source", firstRandom.Path ],
			"a\nb\nc\n"u8.ToArray()
		);
		var second = await RunAsync(
			[ "--random-source", secondRandom.Path ],
			"a\nb\nc\n"u8.ToArray()
		);
		Assert.Equal( first.Output, second.Output );
	}

	/// <summary>Verifies controlled failure and destination preservation when a deterministic random source is exhausted.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ExhaustedRandomSourceIsDiagnosedBeforeOutputIsTruncated() {
		using var random = new TemporaryFile( [] );
		using var output = new TemporaryFile( "preserve"u8.ToArray() );
		var result = await RunAsync(
			[ "--random-source", random.Path, "--output", output.Path ],
			"a\nb\n"u8.ToArray()
		);
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Contains( "random source", result.Error );
		Assert.Equal( "preserve"u8.ToArray(), await File.ReadAllBytesAsync( output.Path ) );
	}

	/// <summary>Verifies that a finite shuffle may replace its random-source file after selection completes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task OutputMayReplaceTheRandomSourceAfterFiniteSelection() {
		using var randomAndOutput = new TemporaryFile( CreateRandomBytes( 1 ) );
		var result = await RunAsync(
			[
				"-e", "a", "b",
				"--random-source", randomAndOutput.Path,
				"--output", randomAndOutput.Path
			],
			[]
		);
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "b\na\n"u8.ToArray(), await File.ReadAllBytesAsync( randomAndOutput.Path ) );
	}

	/// <summary>Verifies preservation of a record larger than the shared segment buffer.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task PreservesLargeRecordsAcrossSegments() {
		var record = Enumerable.Repeat( (byte)'x', 200_000 ).ToArray();
		var result = await RunAsync( [], record );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( record.Length + 1, result.Output.Length );
		Assert.Equal( record, result.Output[..record.Length] );
		Assert.Equal( (byte)'\n', result.Output[^1] );
	}

	/// <summary>Verifies conventional help, version, and usage-error behavior.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ControlPathsAndUsageErrorsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], [] );
		var version = await RunAsync( [ "--version" ], [] );
		var conflict = await RunAsync( [ "-e", "-i", "1-2" ], [] );
		var extra = await RunAsync( [ "first", "second" ], [] );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: shuf", help.TextOutput );
		Assert.Equal( CommandExitCodes.Success, version.Status );
		Assert.Contains( "shuf (Icod.CoreUtils)", version.TextOutput );
		Assert.Equal( CommandExitCodes.Failure, conflict.Status );
		Assert.Contains( "cannot combine", conflict.Error );
		Assert.Equal( CommandExitCodes.Failure, extra.Status );
		Assert.Contains( "extra operand", extra.Error );
	}

	/// <summary>Verifies GNU handling of repeated count, range, output, and random-source options.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RepeatedOptionsFollowGnuRules() {
		using var random = new TemporaryFile( CreateRandomBytes( 0 ) );
		using var firstOutput = new TemporaryFile( [] );
		using var secondOutput = new TemporaryFile( [] );
		var minimumCount = await RunAsync(
			[ "-n", "1", "-n", "2", "--random-source", random.Path ],
			"a\nb\n"u8.ToArray()
		);
		var duplicateRange = await RunAsync( [ "-i", "1-2", "-i", "1-2" ], [] );
		var conflictingOutput = await RunAsync(
			[ "-e", "value", "-o", firstOutput.Path, "-o", secondOutput.Path ],
			[]
		);
		using var firstRandomSource = new TemporaryFile( CreateRandomBytes( 0 ) );
		using var secondRandomSource = new TemporaryFile( CreateRandomBytes( 0 ) );
		var conflictingRandomSource = await RunAsync(
			[
				"-e", "first", "second", "-n", "1",
				"--random-source", firstRandomSource.Path,
				"--random-source", secondRandomSource.Path
			],
			[]
		);
		using var matchingOutput = new TemporaryFile( [] );
		using var matchingRandom = new TemporaryFile( CreateRandomBytes( 0 ) );
		var matchingValues = await RunAsync(
			[
				"-e", "first", "second", "-n", "1",
				"-o", matchingOutput.Path,
				"--output", matchingOutput.Path,
				"--random-source", matchingRandom.Path,
				"--random-source", matchingRandom.Path
			],
			[]
		);
		Assert.Single( Encoding.UTF8.GetString( minimumCount.Output ).Split( '\n', StringSplitOptions.RemoveEmptyEntries ) );
		Assert.Equal( CommandExitCodes.Failure, duplicateRange.Status );
		Assert.Contains( "multiple -i", duplicateRange.Error );
		Assert.Equal( CommandExitCodes.Failure, conflictingOutput.Status );
		Assert.Contains( "multiple output", conflictingOutput.Error );
		Assert.Equal( CommandExitCodes.Failure, conflictingRandomSource.Status );
		Assert.Contains( "multiple random", conflictingRandomSource.Error );
		Assert.Equal( CommandExitCodes.Success, matchingValues.Status );
		Assert.Equal( "first\n"u8.ToArray(), await File.ReadAllBytesAsync( matchingOutput.Path ) );
	}

	/// <summary>Verifies GNU-compatible leading whitespace and plus signs in numeric operands.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task NumericOptionsAcceptLeadingWhitespaceAndPlusSigns() {
		using var random = new TemporaryFile( CreateRandomBytes( 0 ) );
		var count = await RunAsync(
			[ "-n", " +1", "--random-source", random.Path ],
			"a\nb\n"u8.ToArray()
		);
		var range = await RunAsync(
			[ "-i", " +5- +5" ],
			[]
		);
		Assert.Equal( "a\n"u8.ToArray(), count.Output );
		Assert.Equal( "5\n"u8.ToArray(), range.Output );
	}

	/// <summary>Verifies rejection sampling when a raw value would bias a bounded selection.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task BoundedSelectionRejectsBiasedRawValues() {
		using var random = new TemporaryFile( CreateRandomBytes( 255, 2, 0 ) );
		var result = await RunAsync(
			[ "--random-source", random.Path ],
			"a\nb\nc\n"u8.ToArray()
		);
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "c\nb\na\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies cleanup of owned temporary spools after successful output.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SuccessfulShuffleCleansTemporarySpools() {
		var before = FindTemporarySpools();
		using var random = new TemporaryFile( CreateRandomBytes( 1 ) );
		var result = await RunAsync(
			[ "--random-source", random.Path ],
			"a\nb\n"u8.ToArray()
		);
		var after = FindTemporarySpools();
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Empty( after.Except( before, StringComparer.Ordinal ) );
	}

	/// <summary>Verifies cancellation of an unbounded repeat operation.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task InfiniteRepeatHonorsCancellation() {
		var before = FindTemporarySpools();
		using var cancellation = new CancellationTokenSource();
		cancellation.CancelAfter( TimeSpan.FromMilliseconds( 25 ) );
		var result = await RunAsync(
			[ "-r" ],
			"value\n"u8.ToArray(),
			cancellation.Token,
			Stream.Null
		);
		var after = FindTemporarySpools();
		Assert.Equal( CommandExitCodes.Canceled, result.Status );
		Assert.Empty( after.Except( before, StringComparer.Ordinal ) );
	}

	/// <summary>Verifies cleanup of owned temporary spools after output failure.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task OutputFailureCleansTemporarySpools() {
		var before = FindTemporarySpools();
		var result = await RunAsync(
			[],
			"a\nb\n"u8.ToArray(),
			default,
			new FailingWriteStream()
		);
		var after = FindTemporarySpools();
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Empty( after.Except( before, StringComparer.Ordinal ) );
	}

	private static byte[] CreateRandomBytes( params byte[] values ) => values;

	private static HashSet<string> FindTemporarySpools() {
		return Directory.EnumerateFiles(
			System.IO.Path.GetTempPath(),
			"icod-coreutils-shuf-*.tmp",
			SearchOption.TopDirectoryOnly
		).ToHashSet( StringComparer.Ordinal );
	}

	private static async Task<(int Status, byte[] Output, string TextOutput, string Error)> RunAsync(
		string[] args,
		byte[] input,
		CancellationToken cancellationToken = default,
		Stream? outputDestination = null
	) {
		using var inputStream = new MemoryStream( input, writable: false );
		using var ownedOutput = null == outputDestination ? new MemoryStream() : null;
		var outputStream = outputDestination ?? ownedOutput!;
		var textOutput = new StringWriter();
		var error = new StringWriter();
		var context = new CommandContext(
			"shuf",
			new StringReader( string.Empty ),
			textOutput,
			error,
			inputStream,
			outputStream,
			null,
			cancellationToken
		);
		var status = await Command.RunAsync( args, context );
		return (
			status,
			null == ownedOutput ? [] : ownedOutput.ToArray(),
			textOutput.ToString(),
			error.ToString()
		);
	}

	private sealed class FailingWriteStream : Stream {
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
		public override void Flush() { }
		/// <inheritdoc/>
		public override int Read( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override void SetLength( long value ) => throw new NotSupportedException();
		/// <inheritdoc/>
		public override void Write( byte[] buffer, int offset, int count ) => throw new IOException( "simulated output failure" );
		/// <inheritdoc/>
		public override ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) {
			return new ValueTask( Task.FromException( new IOException( "simulated output failure" ) ) );
		}
	}

	private sealed class TemporaryFile : IDisposable {
		/// <summary>Creates a uniquely named temporary file containing the supplied bytes.</summary>
		/// <param name="contents">The initial file contents.</param>
		internal TemporaryFile( byte[] contents ) {
			this.Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				string.Concat( "icod-shuf-test-", Guid.NewGuid().ToString( "N" ), ".tmp" )
			);
			File.WriteAllBytes( this.Path, contents );
		}
		/// <summary>Gets the temporary file path.</summary>
		internal string Path { get; }
		/// <inheritdoc/>
		public void Dispose() {
			try {
				File.Delete( this.Path );
			} catch {
				// Test cleanup must not mask the assertion result.
			}
		}
	}
}
