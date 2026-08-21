namespace Icod.CoreUtils.Sort.Tests;

using System.Globalization;
using System.Text;
using Icod.CommandFramework.Diagnostics;
using Xunit;

/// <summary>Tests ordering families, key semantics, external runs, check mode, merge mode, and control paths.</summary>
public sealed class CommandTests {
	private const int SortFailure = 2;
	/// <summary>Verifies default lexical ordering and synthesized termination of an unterminated final record.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SortsRecordsLexically() {
		var result = await RunAsync( [], Bytes( "pear\napple\nbanana" ) );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( Bytes( "apple\nbanana\npear\n" ), result.Output );
	}

	/// <summary>Verifies exact numeric comparison does not round very large integer prefixes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SortsExactNumericPrefixesWithoutFloatingPointRounding() {
		var result = await RunAsync(
			[ "-n" ],
			Bytes( "9007199254740993\n10\n9007199254740992\n" )
		);
		Assert.Equal( Bytes( "10\n9007199254740992\n9007199254740993\n" ), result.Output );
	}

	/// <summary>Verifies the C/POSIX numeric category does not treat commas as thousands separators.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CLocaleNumericParsingStopsAtComma() {
		var result = await WithEnvironmentAsync(
			new Dictionary<string, string?> {
				[ "LC_ALL" ] = "C"
			},
			() => RunAsync( [ "-n" ], Bytes( "2\n1,5\n" ) )
		);
		Assert.Equal( Bytes( "1,5\n2\n" ), result.Output );
	}

	/// <summary>Verifies general numeric ordering accepts prefixes, special values, exponents, and hexadecimal forms.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SortsGeneralNumericPrefixesAndSpecialValues() {
		var result = await RunAsync(
			[ "-g" ],
			Bytes( "inf\n10tail\nfoo\n-inf\nNaN\n1e2tail\n2\n0x10\n" )
		);
		Assert.Equal(
			Bytes( "foo\nNaN\n-inf\n2\n10tail\n0x10\n1e2tail\ninf\n" ),
			result.Output
		);
	}

	/// <summary>Verifies a numeric key restricted to one delimiter-separated field.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SortsByExplicitNumericField() {
		var result = await RunAsync(
			[ "-t", ":", "-k", "2,2n" ],
			Bytes( "b:10\na:2\nc:1\n" )
		);
		Assert.Equal( Bytes( "c:1\na:2\nb:10\n" ), result.Output );
	}

	/// <summary>Verifies a multibyte locale character may delimit fields without corrupting record bytes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsMultibyteFieldSeparators() {
		var result = await RunAsync(
			[ "-t", "☃", "-k", "2,2n" ],
			Bytes( "b☃10\na☃2\nc☃1\n" )
		);
		Assert.Equal( Bytes( "c☃1\na☃2\nb☃10\n" ), result.Output );
	}

	/// <summary>Verifies stable mode preserves input order when explicit keys compare equal.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task StableModePreservesEqualKeyOrder() {
		var input = Bytes( "z:1\na:1\nm:1\n" );
		var unstable = await RunAsync( [ "-t", ":", "-k", "2,2n" ], input );
		var stable = await RunAsync( [ "-s", "-t", ":", "-k", "2,2n" ], input );
		Assert.Equal( Bytes( "a:1\nm:1\nz:1\n" ), unstable.Output );
		Assert.Equal( input, stable.Output );
	}

	/// <summary>Verifies a key-local blank modifier prevents inheritance of a global comparison family.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task KeyLocalModifierDisablesGlobalOrderingInheritance() {
		var result = await RunAsync(
			[ "-s", "-n", "-k", "2b" ],
			Bytes( "x 2\nx 10\n" )
		);
		Assert.Equal( Bytes( "x 10\nx 2\n" ), result.Output );
	}

	/// <summary>Verifies unique mode removes equal keys rather than only byte-identical records.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task UniqueModeUsesKeyEquivalence() {
		var result = await RunAsync(
			[ "-u", "-s", "-t", ":", "-k", "2,2n" ],
			Bytes( "first:1\nsecond:1\nthird:2\n" )
		);
		Assert.Equal( Bytes( "first:1\nthird:2\n" ), result.Output );
	}

	/// <summary>Verifies human-readable quantities compare by scaled exact value.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SortsHumanReadableNumbers() {
		var result = await RunAsync( [ "-h" ], Bytes( "2K\n100\n1M\n900K\n" ) );
		Assert.Equal( Bytes( "100\n2K\n900K\n1M\n" ), result.Output );
	}

	/// <summary>Verifies month names use the active culture's month table.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SortsMonthNames() {
		var oldCulture = CultureInfo.CurrentCulture;
		try {
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo( "en-US" );
			var result = await RunAsync( [ "-M" ], Bytes( "DEC\nFEB\nJAN\nunknown\n" ) );
			Assert.Equal( Bytes( "unknown\nJAN\nFEB\nDEC\n" ), result.Output );
		} finally {
			CultureInfo.CurrentCulture = oldCulture;
		}
	}

	/// <summary>Verifies a UTF-8 C locale retains bytewise collation while enabling Unicode character folding.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task Utf8CLocaleUsesUnicodeCharacterClassification() {
		var result = await WithEnvironmentAsync(
			new Dictionary<string, string?> {
				[ "LC_ALL" ] = "C.UTF-8"
			},
			() => RunAsync( [ "-s", "-f" ], Bytes( "ä\nÄ\n" ) )
		);
		Assert.Equal( Bytes( "ä\nÄ\n" ), result.Output );
	}

	/// <summary>Verifies <c>LC_NUMERIC</c> is resolved independently from bytewise collation.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task NumericLocaleIsIndependentOfCollationLocale() {
		var result = await WithEnvironmentAsync(
			new Dictionary<string, string?> {
				[ "LC_ALL" ] = null,
				[ "LC_COLLATE" ] = "C",
				[ "LC_NUMERIC" ] = "fr_FR.UTF-8",
				[ "LANG" ] = "C"
			},
			() => RunAsync( [ "-n" ], Bytes( "-1,10\n-1,2\n" ) )
		);
		Assert.Equal( Bytes( "-1,2\n-1,10\n" ), result.Output );
	}

	/// <summary>Verifies <c>LC_TIME</c> supplies month names independently from collation.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TimeLocaleIsIndependentOfCollationLocale() {
		var culture = CultureInfo.GetCultureInfo( "fr-FR" );
		var january = culture.DateTimeFormat.GetAbbreviatedMonthName( 1 );
		var february = culture.DateTimeFormat.GetAbbreviatedMonthName( 2 );
		var december = culture.DateTimeFormat.GetAbbreviatedMonthName( 12 );
		var result = await WithEnvironmentAsync(
			new Dictionary<string, string?> {
				[ "LC_ALL" ] = null,
				[ "LC_COLLATE" ] = "C",
				[ "LC_TIME" ] = "fr_FR.UTF-8",
				[ "LANG" ] = "C"
			},
			() => RunAsync(
				[ "-M" ],
				Bytes( string.Concat( december, "\n", february, "\n", january, "\nunknown\n" ) )
			)
		);
		Assert.Equal(
			Bytes( string.Concat( "unknown\n", january, "\n", february, "\n", december, "\n" ) ),
			result.Output
		);
	}

	/// <summary>Verifies <c>LC_CTYPE</c> controls case folding independently from bytewise collation.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CharacterLocaleIsIndependentOfCollationLocale() {
		var result = await WithEnvironmentAsync(
			new Dictionary<string, string?> {
				[ "LC_ALL" ] = null,
				[ "LC_COLLATE" ] = "C",
				[ "LC_CTYPE" ] = "tr_TR.UTF-8",
				[ "LANG" ] = "C"
			},
			() => RunAsync( [ "-s", "-f" ], Bytes( "i\nI\nı\n" ) )
		);
		Assert.Equal( Bytes( "I\nı\ni\n" ), result.Output );
	}

	/// <summary>Verifies digit runs receive natural version ordering.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SortsVersionsNaturally() {
		var result = await RunAsync( [ "-V" ], Bytes( "v10\nv2\nv1\nv1.9\n" ) );
		Assert.Equal( Bytes( "v1\nv1.9\nv2\nv10\n" ), result.Output );
	}

	/// <summary>Verifies version ordering handles tildes, leading zeros, letters, and punctuation like GNU file-version comparison.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SortsVersionPunctuationAndLeadingZeros() {
		var oldLocale = Environment.GetEnvironmentVariable( "LC_ALL" );
		try {
			Environment.SetEnvironmentVariable( "LC_ALL", "C" );
			var result = await RunAsync(
				[ "-V" ],
				Bytes( "a1\na01\na001\na~\na\na-\na.\na_1\naA\naa\na+\n" )
			);
			Assert.Equal(
				Bytes( "a~\na\na001\na01\na1\naA\naa\na+\na-\na.\na_1\n" ),
				result.Output
			);
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", oldLocale );
		}
	}

	/// <summary>Verifies GNU file-version ordering for empty, dot-prefixed, tilde, and removable-suffix names.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SortsVersionSpecialNamesAndSuffixes() {
		var result = await WithEnvironmentAsync(
			new Dictionary<string, string?> {
				[ "LC_ALL" ] = "C"
			},
			() => RunAsync(
				[ "-V" ],
				Bytes( "foo-1.0.1\nfoo-1.0.tar.gz\na\n~\n.a\n..\n.\n\nfoo-1.0\nfoo-1.0~rc1.tar.gz\n" )
			)
		);
		Assert.Equal(
			Bytes( "\n.\n..\n.a\n~\na\nfoo-1.0~rc1.tar.gz\nfoo-1.0\nfoo-1.0.tar.gz\nfoo-1.0.1\n" ),
			result.Output
		);
	}

	/// <summary>Verifies stable and unique version modes treat leading-zero variants as one key-equivalence class.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task StableAndUniqueVersionModesUseVersionEquivalence() {
		var input = Bytes( "a1\na01\na001\n" );
		var stable = await RunAsync( [ "-s", "-V" ], input );
		var unique = await RunAsync( [ "-u", "-V" ], input );
		Assert.Equal( input, stable.Output );
		Assert.Equal( Bytes( "a1\n" ), unique.Output );
	}

	/// <summary>Verifies reverse ordering applies to comparison results.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReversesOrdering() {
		var result = await RunAsync( [ "-nr" ], Bytes( "1\n3\n2\n" ) );
		Assert.Equal( Bytes( "3\n2\n1\n" ), result.Output );
	}

	/// <summary>Verifies check mode returns one for disorder and quiet mode suppresses diagnostics.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CheckModesUseExactStatuses() {
		var sorted = await RunAsync( [ "-c" ], Bytes( "a\nb\n" ) );
		var disorder = await RunAsync( [ "-c" ], Bytes( "b\na\n" ) );
		var quiet = await RunAsync( [ "-C" ], Bytes( "b\na\n" ) );
		Assert.Equal( CommandExitCodes.Success, sorted.Status );
		Assert.Equal( CommandExitCodes.Failure, disorder.Status );
		Assert.Contains( "disorder", disorder.Error );
		Assert.Equal( CommandExitCodes.Failure, quiet.Status );
		Assert.Equal( string.Empty, quiet.Error );
	}

	/// <summary>Verifies merge mode accepts multiple already-sorted operands and produces one ordered stream.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task MergesSortedInputs() {
		var first = System.IO.Path.GetTempFileName();
		var second = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( first, Bytes( "a\nc\n" ) );
			await File.WriteAllBytesAsync( second, Bytes( "b\nd\n" ) );
			var result = await RunAsync( [ "-m", first, second ], [] );
			Assert.Equal( Bytes( "a\nb\nc\nd\n" ), result.Output );
		} finally {
			File.Delete( first );
			File.Delete( second );
		}
	}

	/// <summary>Verifies merge mode reduces more inputs than the configured fan-in through stable intermediate passes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task MergeModeUsesBoundedMultiPassFanIn() {
		var files = Enumerable.Range( 0, 5 ).Select( _ => System.IO.Path.GetTempFileName() ).ToArray();
		try {
			await File.WriteAllBytesAsync( files[ 0 ], Bytes( "a\nf\n" ) );
			await File.WriteAllBytesAsync( files[ 1 ], Bytes( "b\ng\n" ) );
			await File.WriteAllBytesAsync( files[ 2 ], Bytes( "c\nh\n" ) );
			await File.WriteAllBytesAsync( files[ 3 ], Bytes( "d\ni\n" ) );
			await File.WriteAllBytesAsync( files[ 4 ], Bytes( "e\nj\n" ) );
			var result = await RunAsync( [ "-m", "--batch-size=2", .. files ], [] );
			Assert.Equal( CommandExitCodes.Success, result.Status );
			Assert.Equal( Bytes( "a\nb\nc\nd\ne\nf\ng\nh\ni\nj\n" ), result.Output );
		} finally {
			foreach ( var file in files ) {
				File.Delete( file );
			}
		}
	}

	/// <summary>Verifies NUL-delimited input and output preserve embedded newlines.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsNullTerminatedRecords() {
		var result = await RunAsync(
			[ "-z" ],
			[ (byte)'b', (byte)'\n', 0, (byte)'a', 0 ]
		);
		Assert.Equal( new byte[] { (byte)'a', 0, (byte)'b', (byte)'\n', 0 }, result.Output );
	}

	/// <summary>Verifies a tiny buffer budget forces spill runs without changing stable output.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TinyMemoryBudgetUsesExternalRuns() {
		var result = await RunAsync(
			[ "-S", "1", "-s", "-t", ":", "-k", "2,2n" ],
			Bytes( "z:2\na:1\nb:1\ny:2\n" )
		);
		Assert.Equal( Bytes( "a:1\nb:1\nz:2\ny:2\n" ), result.Output );
	}

	/// <summary>Verifies binary, decimal, and block-size buffer suffixes are accepted.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task AcceptsDocumentedBufferSizeSuffixes() {
		var binary = await RunAsync( [ "-S", "1KiB" ], Bytes( "b\na\n" ) );
		var @decimal = await RunAsync( [ "-S", "1KB" ], Bytes( "b\na\n" ) );
		var block = await RunAsync( [ "-S", "1b" ], Bytes( "b\na\n" ) );
		Assert.Equal( Bytes( "a\nb\n" ), binary.Output );
		Assert.Equal( Bytes( "a\nb\n" ), @decimal.Output );
		Assert.Equal( Bytes( "a\nb\n" ), block.Output );
	}

	/// <summary>Verifies external run workspaces are removed after successful bounded-memory sorting.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ExternalRunWorkspaceIsCleanedAfterSuccess() {
		var directory = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "sort-tests-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( directory );
		try {
			var result = await RunAsync(
				[ "-T", directory, "-S", "1" ],
				Bytes( "d\nc\nb\na\n" )
			);
			Assert.Equal( CommandExitCodes.Success, result.Status );
			Assert.Empty( Directory.EnumerateFileSystemEntries( directory ) );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies output spooling permits the output pathname to equal an input pathname.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SortsSafelyInPlace() {
		var file = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( file, Bytes( "b\na\n" ) );
			var result = await RunAsync( [ "-o", file, file ], [] );
			Assert.Equal( CommandExitCodes.Success, result.Status );
			Assert.Equal( Bytes( "a\nb\n" ), await File.ReadAllBytesAsync( file ) );
		} finally {
			File.Delete( file );
		}
	}

	/// <summary>Verifies NUL-delimited file-name operands are accepted.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReadsFileNamesFromNullDelimitedList() {
		var input = System.IO.Path.GetTempFileName();
		var list = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( input, Bytes( "b\na\n" ) );
			await File.WriteAllBytesAsync( list, [ .. Encoding.UTF8.GetBytes( input ), 0 ] );
			var result = await RunAsync( [ "--files0-from", list ], [] );
			Assert.Equal( Bytes( "a\nb\n" ), result.Output );
		} finally {
			File.Delete( input );
			File.Delete( list );
		}
	}

	/// <summary>Verifies standard input may provide file names without also becoming sortable record input.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReadsFileNamesFromStandardInput() {
		var input = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( input, Bytes( "b\na\n" ) );
			byte[] names = [ .. Encoding.UTF8.GetBytes( input ), 0 ];
			var result = await RunAsync( [ "--files0-from=-" ], names );
			Assert.Equal( CommandExitCodes.Success, result.Status );
			Assert.Equal( Bytes( "a\nb\n" ), result.Output );
		} finally {
			File.Delete( input );
		}
	}

	/// <summary>Verifies an empty file-name list means there are no input records.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task EmptyFileNameListDoesNotFallBackToStandardInput() {
		var list = System.IO.Path.GetTempFileName();
		try {
			var result = await RunAsync( [ "--files0-from", list ], Bytes( "must-not-be-read\n" ) );
			Assert.Equal( CommandExitCodes.Success, result.Status );
			Assert.Empty( result.Output );
		} finally {
			File.Delete( list );
		}
	}

	/// <summary>Verifies a fixed random source produces repeatable key-group ordering while preserving equal-key input order.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RandomOrderingIsRepeatableAndGroupsEqualKeys() {
		var seed = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( seed, Bytes( "fixed random seed" ) );
			var arguments = new[] { "-s", "-R", "-t", ":", "-k", "1,1", "--random-source", seed };
			var first = await RunAsync( arguments, Bytes( "a:1\nb:1\na:2\nb:2\n" ) );
			var second = await RunAsync( arguments, Bytes( "a:1\nb:1\na:2\nb:2\n" ) );
			Assert.Equal( first.Output, second.Output );
			Assert.True(
				first.Output.AsSpan().SequenceEqual( Bytes( "a:1\na:2\nb:1\nb:2\n" ) )
				|| first.Output.AsSpan().SequenceEqual( Bytes( "b:1\nb:2\na:1\na:2\n" ) )
			);
		} finally {
			File.Delete( seed );
		}
	}

	/// <summary>Verifies a random source is ignored when no random comparison is active.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task IgnoresRandomSourceWithoutRandomOrdering() {
		var missing = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "missing-seed-", Guid.NewGuid().ToString( "N" ) ) );
		var result = await RunAsync( [ "--random-source", missing ], Bytes( "b\na\n" ) );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( Bytes( "a\nb\n" ), result.Output );
	}

	/// <summary>Verifies the long quiet check form returns disorder without a diagnostic.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task LongQuietCheckFormSuppressesDiagnostics() {
		var result = await RunAsync( [ "--check=quiet" ], Bytes( "b\na\n" ) );
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Equal( string.Empty, result.Error );
	}

	/// <summary>Verifies mutually exclusive ordering, check, and separator selections fail as usage errors.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RejectsIncompatibleOptions() {
		var modes = await RunAsync( [ "-n", "-M" ], [] );
		var keyModes = await RunAsync( [ "-k", "1,1nM" ], [] );
		var characterNumeric = await RunAsync( [ "-d", "-n" ], [] );
		var keyCharacterNumeric = await RunAsync( [ "-k", "1,1dn" ], [] );
		var checks = await RunAsync( [ "-c", "-C" ], [] );
		var separators = await RunAsync( [ "-t", ":", "-t", "," ], [] );
		var temporaryDirectories = await RunAsync( [ "-T", System.IO.Path.GetTempPath(), "-T", System.IO.Path.GetTempPath() ], [] );
		Assert.Equal( CommandExitCodes.UsageError, modes.Status );
		Assert.Equal( CommandExitCodes.UsageError, keyModes.Status );
		Assert.Equal( CommandExitCodes.UsageError, characterNumeric.Status );
		Assert.Equal( CommandExitCodes.UsageError, keyCharacterNumeric.Status );
		Assert.Equal( CommandExitCodes.UsageError, checks.Status );
		Assert.Equal( CommandExitCodes.UsageError, separators.Status );
		Assert.Equal( CommandExitCodes.UsageError, temporaryDirectories.Status );
	}

	/// <summary>Verifies merge is accepted as an optimization hint while checking one input.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CheckModeMayBeCombinedWithMerge() {
		var result = await RunAsync( [ "-m", "-c" ], Bytes( "a\nb\n" ) );
		Assert.Equal( CommandExitCodes.Success, result.Status );
	}

	/// <summary>Verifies key character offsets count input bytes, including bytes inside a multibyte UTF-8 character.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task KeyOffsetsCountInputBytes() {
		var input = Bytes( "x😀b\nx😀a\n" );
		var insideCharacter = await RunAsync( [ "-s", "-k", "1.3,1.3" ], input );
		var followingByte = await RunAsync( [ "-s", "-k", "1.6,1.6" ], input );
		Assert.Equal( input, insideCharacter.Output );
		Assert.Equal( Bytes( "x😀a\nx😀b\n" ), followingByte.Output );
	}

	/// <summary>Verifies C-locale collation preserves bytewise ordering for invalid UTF-8 input bytes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CLocaleOrdersRawBytes() {
		var oldLocale = Environment.GetEnvironmentVariable( "LC_ALL" );
		try {
			Environment.SetEnvironmentVariable( "LC_ALL", "C" );
			var result = await RunAsync( [], [ 0xff, (byte)'\n', 0xfe, (byte)'\n' ] );
			Assert.Equal( new byte[] { 0xfe, (byte)'\n', 0xff, (byte)'\n' }, result.Output );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", oldLocale );
		}
	}

	/// <summary>Verifies cancellation and output failures return exact nonzero statuses without escaping exceptions.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CancellationAndWriteFailureUseExactStatuses() {
		using var canceledInput = new MemoryStream( Bytes( "b\na\n" ), writable: false );
		using var canceledOutput = new MemoryStream();
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var canceledError = new StringWriter();
		var canceledContext = new CommandContext(
			"sort",
			new StringReader( string.Empty ),
			new StringWriter(),
			canceledError,
			canceledInput,
			canceledOutput,
			cancellationToken: cancellation.Token
		);
		var canceledStatus = await Command.RunAsync( [], canceledContext );

		using var failingInput = new MemoryStream( Bytes( "a\n" ), writable: false );
		using var failingOutput = new FailingWriteStream();
		var failingError = new StringWriter();
		var failingContext = new CommandContext(
			"sort",
			new StringReader( string.Empty ),
			new StringWriter(),
			failingError,
			failingInput,
			failingOutput
		);
		var failingStatus = await Command.RunAsync( [], failingContext );

		Assert.Equal( CommandExitCodes.Canceled, canceledStatus );
		Assert.Equal( SortFailure, failingStatus );
		Assert.Contains( "simulated output failure", failingError.ToString() );
	}

	/// <summary>Verifies help, version, malformed keys, and operational failures use conventional statuses.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], [] );
		var version = await RunAsync( [ "--version" ], [] );
		var invalid = await RunAsync( [ "-k", "0" ], [] );
		var missing = await RunAsync( [ "does-not-exist" ], [] );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: sort", help.TextOutput );
		Assert.Equal( CommandExitCodes.Success, version.Status );
		Assert.Contains( "sort (Icod.CoreUtils)", version.TextOutput );
		Assert.Equal( CommandExitCodes.UsageError, invalid.Status );
		Assert.Contains( "field", invalid.Error );
		Assert.Equal( CommandExitCodes.UsageError, missing.Status );
	}

	private sealed class FailingWriteStream : MemoryStream {
		/// <inheritdoc/>
		public override ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			return ValueTask.FromException( new IOException( "simulated output failure" ) );
		}
	}

	private static async Task<T> WithEnvironmentAsync<T>(
		IReadOnlyDictionary<string, string?> values,
		Func<Task<T>> action
	) {
		var previous = values.Keys.ToDictionary(
			key => key,
			Environment.GetEnvironmentVariable
		);
		try {
			foreach ( var pair in values ) {
				Environment.SetEnvironmentVariable( pair.Key, pair.Value );
			}
			return await action();
		} finally {
			foreach ( var pair in previous ) {
				Environment.SetEnvironmentVariable( pair.Key, pair.Value );
			}
		}
	}

	private static byte[] Bytes( string value ) => Encoding.UTF8.GetBytes( value );

	private static async Task<(int Status, byte[] Output, string TextOutput, string Error)> RunAsync(
		string[] args,
		byte[] input
	) {
		using var inputStream = new MemoryStream( input, writable: false );
		using var outputStream = new MemoryStream();
		var textOutput = new StringWriter();
		var error = new StringWriter();
		var context = new CommandContext(
			"sort",
			new StringReader( string.Empty ),
			textOutput,
			error,
			inputStream,
			outputStream
		);
		var status = await Command.RunAsync( args, context );
		return ( status, outputStream.ToArray(), textOutput.ToString(), error.ToString() );
	}
}
