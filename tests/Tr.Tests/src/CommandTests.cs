namespace Icod.CoreUtils.Tr.Tests;

using Icod.CommandFramework.Diagnostics;
using Xunit;

/// <summary>Tests byte translation, set grammar, deletion, squeezing, diagnostics, and control paths.</summary>
public sealed class CommandTests {
	/// <summary>Verifies inclusive byte ranges and byte-preserving delimiter handling.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TranslatesRangesWithoutLineDecoding() {
		var result = await RunAsync( [ "a-z", "A-Z" ], "alpha\nbeta\r\n"u8.ToArray() );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "ALPHA\nBETA\r\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies named and octal escapes can transform NUL and record-delimiter bytes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TranslatesEscapedDelimiterBytes() {
		var result = await RunAsync(
			[ "\\000\\n\\r", "XYZ" ],
			new byte[] { 0, (byte)'\n', (byte)'\r', (byte)'a' }
		);
		Assert.Equal( new byte[] { (byte)'X', (byte)'Y', (byte)'Z', (byte)'a' }, result.Output );
	}

	/// <summary>Verifies complemented character classes and squeezing form word delimiters.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ComplementsClassesAndSqueezesTranslatedBytes() {
		var result = await RunAsync(
			[ "-cs", "[:alpha:]", "\\n" ],
			"one  2two\tthree\n"u8.ToArray()
		);
		Assert.Equal( "one\ntwo\nthree\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies every POSIX class uses the C-locale byte membership required by <c>-A</c>.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RecognizesEveryPosixCharacterClass() {
		var input = new byte[] { 0, 9, 10, 31, 32, (byte)'!', (byte)'0', (byte)'9', (byte)'A', (byte)'F', (byte)'G', (byte)'_', (byte)'a', (byte)'f', (byte)'g', 127 };
		var alnum = await RunAsync( [ "-Ad", "[:alnum:]" ], input );
		var alpha = await RunAsync( [ "-Ad", "[:alpha:]" ], input );
		var blank = await RunAsync( [ "-Ad", "[:blank:]" ], input );
		var cntrl = await RunAsync( [ "-Ad", "[:cntrl:]" ], input );
		var digit = await RunAsync( [ "-Ad", "[:digit:]" ], input );
		var graph = await RunAsync( [ "-Ad", "[:graph:]" ], input );
		var lower = await RunAsync( [ "-Ad", "[:lower:]" ], input );
		var print = await RunAsync( [ "-Ad", "[:print:]" ], input );
		var punct = await RunAsync( [ "-Ad", "[:punct:]" ], input );
		var space = await RunAsync( [ "-Ad", "[:space:]" ], input );
		var upper = await RunAsync( [ "-Ad", "[:upper:]" ], input );
		var xdigit = await RunAsync( [ "-Ad", "[:xdigit:]" ], input );
		Assert.Equal( new byte[] { 0, 9, 10, 31, 32, (byte)'!', (byte)'_', 127 }, alnum.Output );
		Assert.Equal( new byte[] { 0, 9, 10, 31, 32, (byte)'!', (byte)'0', (byte)'9', (byte)'_', 127 }, alpha.Output );
		Assert.Equal( new byte[] { 0, 10, 31, (byte)'!', (byte)'0', (byte)'9', (byte)'A', (byte)'F', (byte)'G', (byte)'_', (byte)'a', (byte)'f', (byte)'g', 127 }, blank.Output );
		Assert.Equal( new byte[] { 32, (byte)'!', (byte)'0', (byte)'9', (byte)'A', (byte)'F', (byte)'G', (byte)'_', (byte)'a', (byte)'f', (byte)'g' }, cntrl.Output );
		Assert.Equal( new byte[] { 0, 9, 10, 31, 32, (byte)'!', (byte)'A', (byte)'F', (byte)'G', (byte)'_', (byte)'a', (byte)'f', (byte)'g', 127 }, digit.Output );
		Assert.Equal( new byte[] { 0, 9, 10, 31, 32, 127 }, graph.Output );
		Assert.Equal( new byte[] { 0, 9, 10, 31, 32, (byte)'!', (byte)'0', (byte)'9', (byte)'A', (byte)'F', (byte)'G', (byte)'_', 127 }, lower.Output );
		Assert.Equal( new byte[] { 0, 9, 10, 31, 127 }, print.Output );
		Assert.Equal( new byte[] { 0, 9, 10, 31, 32, (byte)'0', (byte)'9', (byte)'A', (byte)'F', (byte)'G', (byte)'a', (byte)'f', (byte)'g', 127 }, punct.Output );
		Assert.Equal( new byte[] { 0, 31, (byte)'!', (byte)'0', (byte)'9', (byte)'A', (byte)'F', (byte)'G', (byte)'_', (byte)'a', (byte)'f', (byte)'g', 127 }, space.Output );
		Assert.Equal( new byte[] { 0, 9, 10, 31, 32, (byte)'!', (byte)'0', (byte)'9', (byte)'_', (byte)'a', (byte)'f', (byte)'g', 127 }, upper.Output );
		Assert.Equal( new byte[] { 0, 9, 10, 31, 32, (byte)'!', (byte)'G', (byte)'_', (byte)'g', 127 }, xdigit.Output );
	}

	/// <summary>Verifies deletion and squeezing are applied in their documented order.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DeletesBeforeSqueezing() {
		var result = await RunAsync( [ "-ds", "x", " " ], "a  x  b"u8.ToArray() );
		Assert.Equal( "a b"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies squeeze-only and translate-plus-squeeze modes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsBothSqueezeModes() {
		var only = await RunAsync( [ "-s", " " ], "a   b  c"u8.ToArray() );
		var translated = await RunAsync( [ "-s", "a-z", "A-Z" ], "aaabbbccc"u8.ToArray() );
		Assert.Equal( "a b c"u8.ToArray(), only.Output );
		Assert.Equal( "ABC"u8.ToArray(), translated.Output );
	}

	/// <summary>Verifies default target padding and System V-compatible truncation.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task PadsOrTruncatesTheSecondArray() {
		var padded = await RunAsync( [ "abc", "X" ], "abc cab"u8.ToArray() );
		var truncated = await RunAsync( [ "-t", "abc", "X" ], "abc cab"u8.ToArray() );
		Assert.Equal( "XXX XXX"u8.ToArray(), padded.Output );
		Assert.Equal( "Xbc cXb"u8.ToArray(), truncated.Output );
	}

	/// <summary>Verifies the final duplicate source occurrence determines translation.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task LastDuplicateSourceMappingWins() {
		var result = await RunAsync( [ "aba", "XYZ" ], "abba"u8.ToArray() );
		Assert.Equal( "ZYYZ"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies indefinite, decimal, and octal repeated-byte constructs.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ExpandsRepeatedByteConstructs() {
		var indefinite = await RunAsync( [ "a-d", "[x*]" ], "abcd"u8.ToArray() );
		var explicitCount = await RunAsync( [ "a-d", "[x*2]yz" ], "abcd"u8.ToArray() );
		var octalCount = await RunAsync( [ "a-h", "[q*010]" ], "abcdefgh"u8.ToArray() );
		Assert.Equal( "xxxx"u8.ToArray(), indefinite.Output );
		Assert.Equal( "xxyz"u8.ToArray(), explicitCount.Output );
		Assert.Equal( "qqqqqqqq"u8.ToArray(), octalCount.Output );
	}

	/// <summary>Verifies incomplete class-like prefixes retain GNU repeated-byte fallback behavior.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FallsBackToRepeatGrammarForIncompleteClassPrefixes() {
		var colon = await RunAsync( [ "[:*2]", "ab" ], ":"u8.ToArray() );
		var equals = await RunAsync( [ "[=*2]", "ab" ], "="u8.ToArray() );
		Assert.Equal( "b"u8.ToArray(), colon.Output );
		Assert.Equal( "b"u8.ToArray(), equals.Output );
	}

	/// <summary>Verifies invalid class-shaped prefixes can fall back to a valid repeat construct.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FallsBackFromDelimitedClassSyntaxToRepeatGrammar() {
		var colon = await RunAsync( [ "[:*2]:]", "abcd" ], ":]"u8.ToArray() );
		var equals = await RunAsync( [ "[=*2]=]", "abcd" ], "=]"u8.ToArray() );
		Assert.Equal( "cd"u8.ToArray(), colon.Output );
		Assert.Equal( "cd"u8.ToArray(), equals.Output );
	}

	/// <summary>Verifies escaped ordinary bytes remain valid inside a character-class name.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task AcceptsEscapedBytesInsideCharacterClassNames() {
		var result = await RunAsync( [ "-d", "[:d\\igit:]" ], "a1b2"u8.ToArray() );
		Assert.Equal( "ab"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies escaped repeat-count bytes prevent recognition of the bracketed repeat construct.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TreatsEscapedRepeatCountsAsLiteralSetBytes() {
		var result = await RunAsync( [ "abc", "[x*\\3]" ], "abc"u8.ToArray() );
		Assert.Equal( "[x*"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies paired lower and upper classes perform case conversion.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ConvertsCaseWithPairedClasses() {
		var upper = await RunAsync( [ "-A", "[:lower:]", "[:upper:]" ], "Az09z"u8.ToArray() );
		var lower = await RunAsync( [ "-A", "[:upper:]", "[:lower:]" ], "Az09Z"u8.ToArray() );
		Assert.Equal( "AZ09Z"u8.ToArray(), upper.Output );
		Assert.Equal( "az09z"u8.ToArray(), lower.Output );
	}

	/// <summary>Verifies a target case class cannot begin after the source sequence has ended.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RejectsCaseClassBeginningAfterSourceExhaustion() {
		var result = await RunAsync( [ "a", "a[:lower:]" ], [] );
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Contains( "misaligned", result.Error );
	}

	/// <summary>Verifies GNU equivalence classes contain their single specified byte.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsEquivalenceClasses() {
		var result = await RunAsync( [ "-d", "[=a=]" ], "banana"u8.ToArray() );
		Assert.Equal( "bnn"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies command operands are interpreted as UTF-8 bytes rather than UTF-16 characters.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TreatsMultibyteOperandsAsByteSequences() {
		var result = await RunAsync( [ "é", "X" ], "é"u8.ToArray() );
		Assert.Equal( "XX"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies escaped syntax remains literal and does not form ranges or classes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task EscapedGrammarCharactersRemainLiteral() {
		var result = await RunAsync( [ "a\\-c\\[", "WXYZ" ], "a-c["u8.ToArray() );
		Assert.Equal( "WXYZ"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies a trailing backslash remains usable and reports the Shared parser warning.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsPortableEscapeWarningsWithoutFailing() {
		var result = await RunAsync( [ "\\", "X" ], new byte[] { (byte)'\\' } );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( new byte[] { (byte)'X' }, result.Output );
		Assert.Contains( "warning:", result.Error );
		Assert.Contains( "trailing backslash", result.Error.ToLowerInvariant() );
	}

	/// <summary>Verifies invalid grammar and invalid option composition receive controlled diagnostics.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RejectsInvalidSetGrammar() {
		var reverse = await RunAsync( [ "z-a", "x" ], [] );
		var reverseBeforeClass = await RunAsync( [ "a-[:digit:]", "x" ], [] );
		var invalidClass = await RunAsync( [ "[:unknown:]", "x" ], [] );
		var repeatInFirst = await RunAsync( [ "[x*]", "y" ], [] );
		var repeatedFill = await RunAsync( [ "ab", "[x*][y*]" ], [] );
		var targetClass = await RunAsync( [ "a", "[:digit:]" ], [] );
		var targetEquivalence = await RunAsync( [ "a", "[=x=]" ], [] );
		Assert.Equal( CommandExitCodes.Failure, reverse.Status );
		Assert.Contains( "reverse", reverse.Error );
		Assert.Equal( CommandExitCodes.Failure, reverseBeforeClass.Status );
		Assert.Contains( "reverse", reverseBeforeClass.Error );
		Assert.Equal( CommandExitCodes.Failure, invalidClass.Status );
		Assert.Contains( "invalid character class", invalidClass.Error );
		Assert.Equal( CommandExitCodes.Failure, repeatInFirst.Status );
		Assert.Contains( "string1", repeatInFirst.Error );
		Assert.Equal( CommandExitCodes.Failure, repeatedFill.Status );
		Assert.Contains( "only one", repeatedFill.Error );
		Assert.Equal( CommandExitCodes.Failure, targetClass.Status );
		Assert.Contains( "only character classes", targetClass.Error );
		Assert.Equal( CommandExitCodes.Failure, targetEquivalence.Status );
		Assert.Contains( "may not appear", targetEquivalence.Error );
	}

	/// <summary>Verifies operand counts, unknown options, and require-order parsing.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DiagnosesInvalidControlSyntax() {
		var missing = await RunAsync( [], [] );
		var deleteExtra = await RunAsync( [ "-d", "a", "b" ], [] );
		var deleteSqueezeMissing = await RunAsync( [ "-ds", "a" ], [] );
		var unknown = await RunAsync( [ "--definitely-unknown", "a", "b" ], [] );
		var optionAfterOperand = await RunAsync( [ "a", "-d" ], [] );
		Assert.Equal( CommandExitCodes.Failure, missing.Status );
		Assert.Contains( "missing operand", missing.Error );
		Assert.Equal( CommandExitCodes.Failure, deleteExtra.Status );
		Assert.Contains( "extra operand", deleteExtra.Error );
		Assert.Equal( CommandExitCodes.Failure, deleteSqueezeMissing.Status );
		Assert.Contains( "two strings", deleteSqueezeMissing.Error );
		Assert.Equal( CommandExitCodes.Failure, unknown.Status );
		Assert.Contains( "option", unknown.Error );
		Assert.Equal( CommandExitCodes.Success, optionAfterOperand.Status );
	}

	/// <summary>Verifies help and version use textual standard output while transformed data remains binary.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], [] );
		var version = await RunAsync( [ "--version" ], [] );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: tr", help.TextOutput );
		Assert.Equal( CommandExitCodes.Success, version.Status );
		Assert.Contains( "tr (Icod.CoreUtils)", version.TextOutput );
	}

	/// <summary>Verifies squeezing retains state when a repeated run crosses an internal buffer boundary.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SqueezesAcrossReadBuffers() {
		var input = Enumerable.Repeat( (byte)'x', 70 * 1024 ).ToArray();
		var result = await RunAsync( [ "-s", "x" ], input );
		Assert.Equal( new byte[] { (byte)'x' }, result.Output );
	}

	/// <summary>Verifies the complete byte domain can be selected with escaped range endpoints.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SelectsEveryPossibleByte() {
		var input = Enumerable.Range( 0, byte.MaxValue + 1 ).Select( value => (byte)value ).ToArray();
		var result = await RunAsync( [ "-d", "\\000-\\377" ], input );
		Assert.Empty( result.Output );
	}

	/// <summary>Verifies cancellation returns the conventional canceled status.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task HonorsCancellation() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var result = await RunAsync( [ "a", "b" ], "a"u8.ToArray(), cancellation.Token );
		Assert.Equal( CommandExitCodes.Canceled, result.Status );
	}

	/// <summary>Verifies output failures become controlled command failures.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsOutputFailures() {
		using var input = new MemoryStream( "a"u8.ToArray(), writable: false );
		using var output = new MemoryStream();
		output.Dispose();
		var textOutput = new StringWriter();
		var error = new StringWriter();
		var context = new CommandContext(
			"tr",
			new StringReader( string.Empty ),
			textOutput,
			error,
			input,
			output
		);
		var status = await Command.RunAsync( [ "a", "b" ], context );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "writable", error.ToString() );
	}

	private static async Task<(int Status, byte[] Output, string TextOutput, string Error)> RunAsync(
		string[] args,
		byte[] input,
		CancellationToken cancellationToken = default
	) {
		using var inputStream = new MemoryStream( input, writable: false );
		using var outputStream = new MemoryStream();
		var textOutput = new StringWriter();
		var error = new StringWriter();
		var context = new CommandContext(
			"tr",
			new StringReader( string.Empty ),
			textOutput,
			error,
			inputStream,
			outputStream,
			null,
			cancellationToken
		);
		var status = await Command.RunAsync( args, context );
		return ( status, outputStream.ToArray(), textOutput.ToString(), error.ToString() );
	}
}
