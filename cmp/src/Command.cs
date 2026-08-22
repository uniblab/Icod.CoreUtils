// Original behavior/reference: GNU diffutils cmp 3.12
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.DiffUtils.Cmp;

using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Security;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.DiffUtils.Cmp.Numerics;
using Icod.DiffUtils.Shared;

/// <summary>Implements the byte-oriented GNU <c>cmp</c> command.</summary>
public static class Command {
	private const int ComparisonBufferSize = StreamOperations.DefaultBufferSize;
	private const string VersionText = "cmp (Icod.DiffUtils) 1.0";
	private static readonly NumericSuffixTable CmpSuffixes = CreateCmpSuffixes();

	private sealed class CmpUsageException : Exception {
		/// <summary>Initializes a usage exception.</summary>
		public CmpUsageException( string message ) : base( message ) {
		}
	}

	private sealed class CmpOptions {
		/// <summary>Gets or sets whether differing bytes are printed visibly.</summary>
		public bool PrintBytes { get; set; }
		/// <summary>Gets or sets whether every differing byte is listed.</summary>
		public bool Verbose { get; set; }
		/// <summary>Gets or sets whether ordinary output and diagnostics are suppressed.</summary>
		public bool Quiet { get; set; }
		/// <summary>Gets or sets the maximum number of bytes to compare.</summary>
		public long? Limit { get; set; }
		/// <summary>Gets or sets the first input skip.</summary>
		public long FirstSkip { get; set; }
		/// <summary>Gets or sets the second input skip.</summary>
		public long SecondSkip { get; set; }
		/// <summary>Gets or sets the first comparison input.</summary>
		public ComparisonInput First { get; set; }
		/// <summary>Gets or sets the second comparison input.</summary>
		public ComparisonInput Second { get; set; }
	}

	private sealed class ComparisonBuffer : IDisposable {
		private readonly byte[] myBuffer;
		private int myCount;
		private int myIndex;

		/// <summary>Initializes a pooled comparison buffer.</summary>
		public ComparisonBuffer() {
			this.myBuffer = ArrayPool<byte>.Shared.Rent( ComparisonBufferSize );
		}

		/// <summary>Gets the number of unread bytes.</summary>
		public int Available => this.myCount - this.myIndex;
		/// <summary>Gets whether end of input has been observed.</summary>
		public bool EndOfInput { get; private set; }
		/// <summary>Gets an unread byte by relative index.</summary>
		public byte GetByte( int index ) {
			if ( index < 0 || this.Available <= index ) {
				throw new ArgumentOutOfRangeException( nameof( index ) );
			}
			return this.myBuffer[this.myIndex + index];
		}

		/// <summary>Ensures that the buffer contains data or has reached end of input.</summary>
		public async ValueTask FillAsync( Stream source, CancellationToken cancellationToken ) {
			if ( 0 < this.Available || this.EndOfInput ) {
				return;
			}
			this.myIndex = 0;
			this.myCount = await source.ReadAsync(
				this.myBuffer.AsMemory(),
				cancellationToken
			).ConfigureAwait( false );
			this.EndOfInput = 0 == this.myCount;
		}

		/// <summary>Consumes bytes already examined by the comparison loop.</summary>
		public void Consume( int count ) {
			if ( count < 0 || this.Available < count ) {
				throw new ArgumentOutOfRangeException( nameof( count ) );
			}
			this.myIndex += count;
		}

		/// <inheritdoc/>
		public void Dispose() {
			ArrayPool<byte>.Shared.Return( this.myBuffer );
		}
	}

	/// <summary>Runs the command synchronously using supplied text streams.</summary>
	public static int Run(
		IReadOnlyList<string>? arguments,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		return RunAsync( arguments, stdin, stdout, stderr ).GetAwaiter().GetResult();
	}

	/// <summary>Runs the command asynchronously using supplied streams.</summary>
	public static async Task<int> RunAsync(
		IReadOnlyList<string>? arguments,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default,
		Stream? stdinStream = null
	) {
		stdin ??= TextReader.Null;
		stdout ??= TextWriter.Null;
		stderr ??= TextWriter.Null;
		TextReaderStream? adapter = null;
		if ( null == stdinStream ) {
			adapter = new TextReaderStream( stdin, leaveOpen: true );
			stdinStream = adapter;
		}
		try {
			return await RunAsync(
				arguments,
				new CommandContext(
					"cmp",
					stdin,
					stdout,
					stderr,
					stdinStream,
					cancellationToken: cancellationToken
				)
			).ConfigureAwait( false );
		} finally {
			adapter?.Dispose();
		}
	}

	/// <summary>Runs the command within an existing command context.</summary>
	public static async Task<int> RunAsync(
		IReadOnlyList<string>? arguments,
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull( context );
		CmpOptions? options = null;
		try {
			var parsed = CreateParser().Parse( arguments );
			if ( !parsed.IsSuccess ) {
				foreach ( var error in parsed.Errors ) {
					await context.StandardError.WriteLineAsync(
						OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
						context.CancellationToken
					).ConfigureAwait( false );
				}
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return (int)ComparisonStatus.Trouble;
			}
			if ( parsed.HasOption( "help" ) ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return (int)ComparisonStatus.Equal;
			}
			if ( parsed.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					VersionText.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return (int)ComparisonStatus.Equal;
			}

			options = CreateOptions( parsed );
			return await CompareAsync( options, context ).ConfigureAwait( false );
		} catch ( CmpUsageException exception ) {
			await context.Diagnostics.ErrorAsync(
				exception.Message,
				CancellationToken.None
			).ConfigureAwait( false );
			await WriteTryHelpAsync( context ).ConfigureAwait( false );
			return (int)ComparisonStatus.Trouble;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when ( IsOperationalException( exception ) ) {
			if ( !( options?.Quiet ?? false ) ) {
				await context.Diagnostics.ErrorAsync(
					exception.Message,
				CancellationToken.None
				).ConfigureAwait( false );
			}
			return (int)ComparisonStatus.Trouble;
		}
	}

	/// <summary>Creates the option parser used by <c>cmp</c>.</summary>
	public static OptionParser CreateParser() {
		return new OptionParser(
			new OptionDefinition[] {
				new( "print-bytes", 'b', new[] { "print-bytes" } ),
				new( "ignore-initial", 'i', new[] { "ignore-initial" }, OptionValueArity.Required ),
				new( "verbose", 'l', new[] { "verbose" } ),
				new( "bytes", 'n', new[] { "bytes" }, OptionValueArity.Required ),
				new( "quiet", 's', new[] { "quiet", "silent" } ),
				new( "help", longNames: new[] { "help" }, allowMultiple: false ),
				new( "version", 'v', new[] { "version" }, allowMultiple: false )
			},
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute
			}
		);
	}

	private static CmpOptions CreateOptions( OptionParseResult parsed ) {
		if ( parsed.HasOption( "verbose" ) && parsed.HasOption( "quiet" ) ) {
			throw new CmpUsageException( "options -l and -s are incompatible" );
		}
		if ( parsed.Operands.Count < 1 ) {
			throw new CmpUsageException( "missing operand after 'cmp'" );
		}
		var options = new CmpOptions {
			PrintBytes = parsed.HasOption( "print-bytes" ),
			Verbose = parsed.HasOption( "verbose" ),
			Quiet = parsed.HasOption( "quiet" ),
			First = ComparisonInput.Create( parsed.Operands[0] ),
			Second = ComparisonInput.Create( 1 < parsed.Operands.Count ? parsed.Operands[1] : "-" )
		};

		var limitText = parsed.GetLastValue( "bytes" );
		if ( null != limitText ) {
			options.Limit = ParseCount( limitText, "bytes" );
		}
		var ignoreText = parsed.GetLastValue( "ignore-initial" );
		if ( null != ignoreText ) {
			var separator = ignoreText.IndexOf( ':' );
			if ( 0 <= separator ) {
				var first = ignoreText[..separator];
				var second = ignoreText[( separator + 1 )..];
				if ( 0 == first.Length ) {
					throw new CmpUsageException( $"invalid --ignore-initial value '{ignoreText}'" );
				}
				if ( 0 == second.Length || 0 <= second.IndexOf( ':' ) ) {
					throw new CmpUsageException( $"invalid --ignore-initial value '{second}'" );
				}
				options.FirstSkip = ParseCount( first, "ignore-initial" );
				options.SecondSkip = ParseCount( second, "ignore-initial" );
			} else {
				options.FirstSkip = ParseCount( ignoreText, "ignore-initial" );
				options.SecondSkip = options.FirstSkip;
			}
		}
		if ( 2 < parsed.Operands.Count ) {
			options.FirstSkip = CheckedAdd(
				options.FirstSkip,
				ParseCount( parsed.Operands[2], "SKIP1" ),
				parsed.Operands[2]
			);
		}
		if ( 3 < parsed.Operands.Count ) {
			options.SecondSkip = CheckedAdd(
				options.SecondSkip,
				ParseCount( parsed.Operands[3], "SKIP2" ),
				parsed.Operands[3]
			);
		}
		if ( 4 < parsed.Operands.Count ) {
			throw new CmpUsageException( $"extra operand '{parsed.Operands[4]}'" );
		}
		return options;
	}

	private static long CheckedAdd( long left, long right, string original ) {
		try {
			return checked( left + right );
		} catch ( OverflowException ) {
			throw new CmpUsageException( $"invalid --ignore-initial value '{original}'" );
		}
	}

	private static long ParseCount( string text, string optionName ) {
		var result = RadixQuantityParser.ParseInt64(
			text,
			CmpSuffixes,
			allowLeadingPlus: true,
			allowLeadingMinus: false
		);
		if ( !result.IsSuccess ) {
			var label = "SKIP1" == optionName || "SKIP2" == optionName
				? "--ignore-initial"
				: string.Concat( "--", optionName );
			throw new CmpUsageException( $"invalid {label} value '{text}'" );
		}
		return result.Value;
	}

	private static NumericSuffixTable CreateCmpSuffixes() {
		var suffixes = new List<NumericSuffix> {
			new( string.Empty, BigInteger.One ),
			new( "k", BigInteger.One << 10 ),
			new( "K", BigInteger.One << 10 ),
			new( "kiB", BigInteger.One << 10 ),
			new( "KiB", BigInteger.One << 10 ),
			new( "kB", BigInteger.Pow( 1000, 1 ) ),
			new( "KB", BigInteger.Pow( 1000, 1 ) )
		};
		var names = new[] { "M", "G", "T", "P", "E", "Z", "Y", "R", "Q" };
		for ( var index = 0; index < names.Length; index++ ) {
			var exponent = index + 2;
			suffixes.Add( new NumericSuffix( names[index], BigInteger.One << ( 10 * exponent ) ) );
			suffixes.Add( new NumericSuffix( string.Concat( names[index], "iB" ), BigInteger.One << ( 10 * exponent ) ) );
			suffixes.Add( new NumericSuffix( string.Concat( names[index], "B" ), BigInteger.Pow( 1000, exponent ) ) );
		}
		return new NumericSuffixTable( suffixes );
	}

	private static async Task<int> CompareAsync( CmpOptions options, CommandContext context ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		if ( options.First.IsStandardInput && options.Second.IsStandardInput ) {
			_ = context.StandardInputStream ?? throw new InvalidOperationException(
				"A binary standard-input stream was not supplied."
			);
			return (int)ComparisonStatus.Equal;
		}

		await using var firstSource = options.First.OpenBinary( context, ComparisonBufferSize );
		await using var secondSource = options.Second.OpenBinary( context, ComparisonBufferSize );
		var first = firstSource.BinaryStream ?? throw new InvalidOperationException( "The first input is not binary." );
		var second = secondSource.BinaryStream ?? throw new InvalidOperationException( "The second input is not binary." );
		await StreamOperations.SkipAsync(
			first,
			options.FirstSkip,
			ComparisonBufferSize,
			context.CancellationToken
		).ConfigureAwait( false );
		await StreamOperations.SkipAsync(
			second,
			options.SecondSkip,
			ComparisonBufferSize,
			context.CancellationToken
		).ConfigureAwait( false );
		return await CompareStreamsAsync(
			first,
			second,
			options,
			context
		).ConfigureAwait( false );
	}

	private static async Task<int> CompareStreamsAsync(
		Stream first,
		Stream second,
		CmpOptions options,
		CommandContext context
	) {
		using var firstBuffer = new ComparisonBuffer();
		using var secondBuffer = new ComparisonBuffer();
		long byteNumber = 0;
		long lineNumber = 1;
		long lastComparedLineNumber = 1;
		var lastComparedByteWasNewline = false;
		var different = false;
		var remaining = options.Limit ?? long.MaxValue;
		while ( 0 < remaining ) {
			await firstBuffer.FillAsync( first, context.CancellationToken ).ConfigureAwait( false );
			await secondBuffer.FillAsync( second, context.CancellationToken ).ConfigureAwait( false );
			if ( 0 == firstBuffer.Available || 0 == secondBuffer.Available ) {
				if ( 0 == firstBuffer.Available && 0 == secondBuffer.Available ) {
					return (int)( different ? ComparisonStatus.Different : ComparisonStatus.Equal );
				}
				if ( !options.Quiet ) {
					await WriteEofAsync(
						0 == firstBuffer.Available ? options.First.DisplayName : options.Second.DisplayName,
						byteNumber,
						lastComparedLineNumber,
						lastComparedByteWasNewline,
						options.Verbose,
						context
					).ConfigureAwait( false );
				}
				return (int)ComparisonStatus.Different;
			}

			var count = (int)Math.Min(
				Math.Min( firstBuffer.Available, secondBuffer.Available ),
				remaining
			);
			for ( var index = 0; index < count; index++ ) {
				var firstByte = firstBuffer.GetByte( index );
				var secondByte = secondBuffer.GetByte( index );
				lastComparedLineNumber = lineNumber;
				lastComparedByteWasNewline = (byte)'\n' == firstByte;
				if ( firstByte != secondByte ) {
					different = true;
					if ( options.Quiet ) {
						return (int)ComparisonStatus.Different;
					}
					if ( options.Verbose ) {
						await WriteVerboseDifferenceAsync(
							byteNumber + index + 1,
							firstByte,
							secondByte,
							options.PrintBytes,
							context
						).ConfigureAwait( false );
					} else {
						await WriteFirstDifferenceAsync(
							options,
							byteNumber + index + 1,
							lineNumber,
							firstByte,
							secondByte,
							context
						).ConfigureAwait( false );
						return (int)ComparisonStatus.Different;
					}
				}
				if ( (byte)'\n' == firstByte ) {
					lineNumber++;
				}
			}
			firstBuffer.Consume( count );
			secondBuffer.Consume( count );
			byteNumber += count;
			remaining -= count;
		}
		return (int)( different ? ComparisonStatus.Different : ComparisonStatus.Equal );
	}

	private static async Task WriteFirstDifferenceAsync(
		CmpOptions options,
		long byteNumber,
		long lineNumber,
		byte firstByte,
		byte secondByte,
		CommandContext context
	) {
		var message = string.Concat(
			options.First.Value,
			" ",
			options.Second.Value,
			" differ: ",
			options.PrintBytes ? "byte " : "char ",
			byteNumber.ToString( CultureInfo.InvariantCulture ),
			", line ",
			lineNumber.ToString( CultureInfo.InvariantCulture )
		);
		if ( options.PrintBytes ) {
			message = string.Concat(
				message,
				" is ",
				ToOctal( firstByte ).PadLeft( 3 ),
				" ",
				ToVisible( firstByte ),
				" ",
				ToOctal( secondByte ).PadLeft( 3 ),
				" ",
				ToVisible( secondByte )
			);
		}
		await context.StandardOutput.WriteLineAsync(
			message.AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static async Task WriteVerboseDifferenceAsync(
		long byteNumber,
		byte firstByte,
		byte secondByte,
		bool printBytes,
		CommandContext context
	) {
		var message = string.Concat(
			byteNumber.ToString( CultureInfo.InvariantCulture ),
			" ",
			ToOctal( firstByte ).PadLeft( 3 ),
			printBytes ? string.Concat( " ", ToVisible( firstByte ).PadRight( 4 ) ) : string.Empty,
			" ",
			ToOctal( secondByte ).PadLeft( 3 ),
			printBytes ? string.Concat( " ", ToVisible( secondByte ) ) : string.Empty
		);
		await context.StandardOutput.WriteLineAsync(
			message.AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static async Task WriteEofAsync(
		string displayName,
		long byteNumber,
		long lineNumber,
		bool endedWithNewline,
		bool verbose,
		CommandContext context
	) {
		string message;
		if ( 0 == byteNumber ) {
			message = string.Concat( "EOF on ", displayName, " which is empty" );
		} else {
			message = string.Concat(
				"EOF on ",
				displayName,
				" after byte ",
				byteNumber.ToString( CultureInfo.InvariantCulture ),
				verbose
					? string.Empty
					: string.Concat(
						endedWithNewline ? ", line " : ", in line ",
						lineNumber.ToString( CultureInfo.InvariantCulture )
					)
			);
		}
		await context.Diagnostics.ErrorAsync(
			message,
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static string ToOctal( byte value ) {
		return Convert.ToString( value, 8 );
	}

	private static string ToVisible( byte value ) {
		if ( 128 <= value ) {
			return string.Concat( "M-", ToVisible( (byte)( value & 0x7f ) ) );
		}
		if ( 127 == value ) {
			return "^?";
		}
		if ( value < 32 ) {
			return string.Concat( "^", (char)( value + 64 ) );
		}
		return ((char)value).ToString();
	}

	private static bool IsOperationalException( Exception exception ) {
		return exception is IOException
			or UnauthorizedAccessException
			or ArgumentException
			or InvalidOperationException
			or NotSupportedException
			or SecurityException;
	}

	private static async Task WriteTryHelpAsync( CommandContext context ) {
		await context.StandardError.WriteLineAsync(
			string.Concat( context.ProgramName, ": Try '", context.ProgramName, " --help' for more information." ).AsMemory(),
			CancellationToken.None
		).ConfigureAwait( false );
	}

	private static async Task WriteHelpAsync( TextWriter output, CancellationToken cancellationToken ) {
		var text = string.Join(
			Environment.NewLine,
			new[] {
				"Usage: cmp [OPTION]... FILE1 [FILE2 [SKIP1 [SKIP2]]]",
				"Compare two files byte by byte.",
				string.Empty,
				"  -b, --print-bytes          print differing bytes",
				"  -i, --ignore-initial=SKIP  skip first SKIP bytes of both inputs",
				"  -i, --ignore-initial=SKIP1:SKIP2",
				"                             skip first SKIP1 and SKIP2 bytes",
				"  -l, --verbose              output byte numbers and differing byte values",
				"  -n, --bytes=LIMIT          compare at most LIMIT bytes",
				"  -s, --quiet, --silent      suppress all normal output",
				"      --help                 display this help and exit",
				"  -v, --version              output version information and exit",
				string.Empty,
				"SKIP and LIMIT may be hexadecimal with a 0x prefix, octal with a leading 0,",
				"or decimal, followed by a GNU multiplier such as kB, K, KiB, MB, M, or MiB.",
				"If FILE2 is omitted or is -, compare FILE1 with standard input.",
				"Exit status is 0 if inputs are equal, 1 if different, and 2 on trouble."
			}
		);
		await output.WriteLineAsync( text.AsMemory(), cancellationToken ).ConfigureAwait( false );
	}
}
