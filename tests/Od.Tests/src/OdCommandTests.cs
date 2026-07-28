namespace Icod.CoreUtils.Od.Tests;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

public sealed class OdCommandTests {
	[Fact]
	public async Task DefaultOutputUsesOctalWordsAndFinalAddress() {
		var result = await RunAsync( new byte[] { 1, 0, 2, 0 } );
		Assert.Equal( 0, result.Status );
		Assert.Contains( "0000000", result.Output );
		Assert.Contains( "000001", result.Output );
		Assert.Contains( "000002", result.Output );
		Assert.EndsWith( result.Output, string.Concat( "0000004", Environment.NewLine ), StringComparison.Ordinal );
	}

	[Fact]
	public async Task HexByteFormatCanSuppressAddresses() {
		var result = await RunAsync( new byte[] { 1, 2, 255 }, "-A", "n", "-t", "x1" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( string.Concat( " 01 02 ff", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task HexadecimalAddressesUseSixColumns() {
		var result = await RunAsync( new byte[] { 1 }, "-A", "x", "-t", "x1" );
		Assert.StartsWith( result.Output, "000000 01", StringComparison.Ordinal );
		Assert.EndsWith( result.Output, string.Concat( "000001", Environment.NewLine ), StringComparison.Ordinal );
	}

	[Fact]
	public async Task BigEndianAndLittleEndianAreDistinct() {
		var little = await RunAsync( new byte[] { 1, 2 }, "-A", "n", "--endian=little", "-t", "x2" );
		var big = await RunAsync( new byte[] { 1, 2 }, "-A", "n", "--endian=big", "-t", "x2" );
		Assert.Contains( "0201", little.Output );
		Assert.Contains( "0102", big.Output );
	}

	[Fact]
	public async Task MultipleFormatsAccumulateInEncounterOrder() {
		var result = await RunAsync( new byte[] { 65, 66 }, "-A", "n", "-t", "x1", "-c" );
		var lines = result.Output.Split( Environment.NewLine, StringSplitOptions.RemoveEmptyEntries );
		Assert.Equal( 2, lines.Length );
		Assert.Contains( "41", lines[ 0 ] );
		Assert.Contains( "A", lines[ 1 ] );
	}

	[Fact]
	public async Task RepeatedFullBlocksAreAbbreviated() {
		var input = Enumerable.Repeat( ( byte )7, 8 ).ToArray();
		var result = await RunAsync( input, "-w4", "-t", "x1" );
		Assert.Contains( string.Concat( Environment.NewLine, "*", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task OutputDuplicatesDisablesAbbreviation() {
		var input = Enumerable.Repeat( ( byte )7, 8 ).ToArray();
		var result = await RunAsync( input, "-w4", "-t", "x1", "-v" );
		Assert.DoesNotContain( string.Concat( Environment.NewLine, "*", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task PartialFinalBlockIsNotAbbreviated() {
		var input = Enumerable.Repeat( ( byte )7, 10 ).ToArray();
		var result = await RunAsync( input, "-w4", "-t", "x1" );
		Assert.Contains( "0000010", result.Output );
	}

	[Fact]
	public async Task SkipAndReadLimitApplyAcrossInput() {
		var result = await RunAsync(
			Enumerable.Range( 0, 16 ).Select( value => ( byte )value ).ToArray(),
			"-A", "n", "-j", "4", "-N", "3", "-t", "x1"
		);
		Assert.Contains( "04 05 06", result.Output );
		Assert.DoesNotContain( "07", result.Output );
	}

	[Fact]
	public async Task BinarySuffixesAreAccepted() {
		var input = new byte[ 1026 ];
		input[ 1024 ] = 0xaa;
		var result = await RunAsync( input, "-A", "n", "-j", "1K", "-N", "1", "-t", "x1" );
		Assert.Contains( "aa", result.Output );
	}

	[Fact]
	public async Task StringModeOutputsNulTerminatedPrintableStrings() {
		var result = await RunAsync(
			Encoding.ASCII.GetBytes( "xx\0hello\0no\0" ),
			"-A", "n", "--strings=3"
		);
		Assert.Equal( string.Concat( "hello", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task ReadLimitTerminatesAnOtherwiseUnterminatedString() {
		var result = await RunAsync(
			Encoding.ASCII.GetBytes( "helloWORLD" ),
			"-A", "n", "--strings=3", "-N", "5"
		);
		Assert.Equal( string.Concat( "hello", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task StringModeRejectsAnExplicitType() {
		var result = await RunAsync( Array.Empty<byte>(), "--strings=3", "-t", "x1" );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "no type may be specified", result.Error );
	}

	[Fact]
	public async Task PrintableTrailerIsAppended() {
		var result = await RunAsync( new byte[] { 65, 0, 66 }, "-A", "n", "-t", "x1z" );
		Assert.Contains( ">A.B<", result.Output );
	}

	[Fact]
	public async Task TraditionalOffsetSkipsInput() {
		var result = await RunAsync( new byte[] { 0, 1, 2, 3 }, "--traditional", "-A", "n", "-t", "x1", "+2" );
		Assert.Contains( "02 03", result.Output );
	}

	[Fact]
	public async Task TraditionalDecimalOffsetUsesDotSuffix() {
		var input = Enumerable.Range( 0, 12 ).Select( value => ( byte )value ).ToArray();
		var result = await RunAsync( input, "--traditional", "-A", "n", "-t", "x1", "+10." );
		Assert.Contains( "0a 0b", result.Output );
	}

	[Fact]
	public async Task TraditionalLabelProducesPseudoAddress() {
		var result = await RunAsync( new byte[] { 1, 2 }, "--traditional", "-t", "x1", "+0", "+16." );
		Assert.Contains( "0000000 (0000020)", result.Output );
	}

	[Fact]
	public async Task WidthWithoutValueUsesThirtyTwoBytes() {
		var result = await RunAsync( Enumerable.Range( 0, 33 ).Select( value => ( byte )value ).ToArray(), "-w", "-t", "x1" );
		var dataLines = result.Output.Split( Environment.NewLine, StringSplitOptions.RemoveEmptyEntries );
		Assert.Equal( 3, dataLines.Length );
		Assert.StartsWith( dataLines[ 1 ], "0000040", StringComparison.Ordinal );
	}

	[Fact]
	public async Task InvalidWidthWarnsAndFallsBackToLeastCommonMultiple() {
		var result = await RunAsync( new byte[] { 1, 2 }, "-w3", "-t", "x2" );
		Assert.Equal( 0, result.Status );
		Assert.Contains( "warning: invalid width 3; using 2 instead", result.Error );
		Assert.Contains( "0201", result.Output );
	}

	[Fact]
	public async Task PseudoAddressRemainsVisibleWhenNormalAddressesAreSuppressed() {
		var result = await RunAsync(
			new byte[] { 1, 2 },
			"--traditional", "-A", "n", "-t", "x1", "+0", "+16."
		);
		Assert.Contains( "(0000020)", result.Output );
		Assert.EndsWith( result.Output, string.Concat( "(0000022)", Environment.NewLine ), StringComparison.Ordinal );
	}

	[Fact]
	public async Task NamedCharacterAndCharacterShorthandsWork() {
		var named = await RunAsync( new byte[] { 10 }, "-A", "n", "-a" );
		var character = await RunAsync( new byte[] { 10 }, "-A", "n", "-c" );
		Assert.Contains( "nl", named.Output );
		Assert.Contains( "\\n", character.Output );
	}

	[Fact]
	public async Task FilesAreConcatenatedWithoutResettingAddress() {
		var first = Path.GetTempFileName();
		var second = Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( first, new byte[] { 1, 2 } );
			await File.WriteAllBytesAsync( second, new byte[] { 3, 4 } );
			var result = await RunAsync( Array.Empty<byte>(), "-A", "n", "-t", "x1", first, second );
			Assert.Contains( "01 02 03 04", result.Output );
		} finally {
			File.Delete( first );
			File.Delete( second );
		}
	}

	[Fact]
	public async Task PathnameGlobsAreExpandedInOrdinalOrder() {
		var directory = Directory.CreateTempSubdirectory( "od-tests-" );
		try {
			await File.WriteAllBytesAsync( Path.Combine( directory.FullName, "b.bin" ), new byte[] { 2 } );
			await File.WriteAllBytesAsync( Path.Combine( directory.FullName, "a.bin" ), new byte[] { 1 } );
			var pattern = Path.Combine( directory.FullName, "*.bin" );
			var result = await RunAsync( Array.Empty<byte>(), "-A", "n", "-t", "x1", pattern );
			Assert.Contains( "01 02", result.Output );
		} finally {
			directory.Delete( true );
		}
	}

	[Fact]
	public async Task MissingFilesProduceFailureButLaterFilesAreProcessed() {
		var existing = Path.GetTempFileName();
		try {
			await File.WriteAllBytesAsync( existing, new byte[] { 0xaa } );
			var result = await RunAsync( Array.Empty<byte>(), "-A", "n", "-t", "x1", existing + ".missing", existing );
			Assert.Equal( 1, result.Status );
			Assert.Contains( "aa", result.Output );
			Assert.Contains( "cannot open", result.Error );
		} finally {
			File.Delete( existing );
		}
	}

	[Fact]
	public async Task SkipPastEndIsAnOperationalFailure() {
		var result = await RunAsync( new byte[] { 1 }, "-j", "2" );
		Assert.Equal( 1, result.Status );
		Assert.Contains( "cannot skip past end", result.Error );
	}

	[Fact]
	public async Task HelpDocumentsBestEffortBsdSupport() {
		var result = await RunAsync( Array.Empty<byte>(), "--help" );
		Assert.Equal( 0, result.Status );
		Assert.Contains( "BSD behavior is best effort", result.Output );
	}

	[Fact]
	public async Task VersionIsReported() {
		var result = await RunAsync( Array.Empty<byte>(), "--version" );
		Assert.Equal( 0, result.Status );
		Assert.Contains( "od (Icod.CoreUtils) 1.0", result.Output );
	}

	[Fact]
	public async Task UnknownOptionsReturnUsageError() {
		var result = await RunAsync( Array.Empty<byte>(), "--definitely-unknown" );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "unrecognized option", result.Error );
	}

	[Fact]
	public async Task CancellationReturnsConventionalStatus() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var result = await RunAsync( new byte[] { 1 }, cancellation.Token, "-t", "x1" );
		Assert.Equal( CommandExitCodes.Canceled, result.Status );
	}

	[Fact]
	public async Task SuppliedInputStreamIsNotDisposed() {
		await using var input = new TrackingMemoryStream( new byte[] { 1 } );
		var output = new StringWriter( CultureInfo.InvariantCulture );
		var error = new StringWriter( CultureInfo.InvariantCulture );
		var status = await Command.RunAsync( Array.Empty<string>(), input, output, error );
		Assert.Equal( 0, status );
		Assert.False( input.WasDisposed );
	}

	[Fact]
	public async Task OutputFailureIsControlled() {
		var input = new MemoryStream( new byte[] { 1 } );
		var error = new StringWriter( CultureInfo.InvariantCulture );
		var status = await Command.RunAsync(
			Array.Empty<string>(),
			input,
			new ThrowingTextWriter(),
			error
		);
		Assert.Equal( 1, status );
	}

	[Fact]
	public async Task UnsupportedNativeLongDoubleIsRejectedWhenHostAbiUsesExtendedEncoding() {
		if (
			OperatingSystem.IsWindows()
			|| (
				OperatingSystem.IsMacOS()
				&& Architecture.Arm64 == RuntimeInformation.ProcessArchitecture
			)
		) {
			return;
		}
		var result = await RunAsync( Array.Empty<byte>(), "-t", "fL" );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "unsupported size", result.Error );
	}

	private static Task<CommandResult> RunAsync(
		byte[] input,
		params string[] args
	) {
		return RunAsync( input, CancellationToken.None, args );
	}

	private static async Task<CommandResult> RunAsync(
		byte[] input,
		CancellationToken cancellationToken,
		params string[] args
	) {
		await using var stream = new MemoryStream( input, writable: false );
		var output = new StringWriter( CultureInfo.InvariantCulture );
		var error = new StringWriter( CultureInfo.InvariantCulture );
		var status = await Command.RunAsync(
			args,
			stream,
			output,
			error,
			cancellationToken
		);
		return new CommandResult( status, output.ToString(), error.ToString() );
	}

	private sealed record CommandResult(
		int Status,
		string Output,
		string Error
	);

	private sealed class TrackingMemoryStream : MemoryStream {
		public bool WasDisposed {
			get;
			private set;
		}
		public TrackingMemoryStream(
			byte[] value
		) : base( value, writable: false ) {
		}
		protected override void Dispose(
			bool disposing
		) {
			this.WasDisposed = true;
			base.Dispose( disposing );
		}
	}

	private sealed class ThrowingTextWriter : TextWriter {
		public override Encoding Encoding {
			get {
				return Encoding.UTF8;
			}
		}
		public override Task WriteAsync(
			ReadOnlyMemory<char> buffer,
			CancellationToken cancellationToken = default
		) {
			return Task.FromException( new IOException( "output failed" ) );
		}
		public override Task WriteLineAsync(
			ReadOnlyMemory<char> buffer,
			CancellationToken cancellationToken = default
		) {
			return Task.FromException( new IOException( "output failed" ) );
		}
	}
}
