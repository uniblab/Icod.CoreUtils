// Original behavior/reference: GNU coreutils 9.11
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Ptx;

using System.Text;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;

/// <summary>Implements GNU-compatible permuted-index generation.</summary>
public static class Command {
	private const string VersionText = "ptx (Icod.CoreUtils) 1.0";

	/// <summary>Runs <c>ptx</c> synchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <returns>The command exit status.</returns>
	public static int Run(
		string[] args,
		TextReader? standardInput = null,
		TextWriter? standardOutput = null,
		TextWriter? standardError = null
	) => RunAsync( args, standardInput, standardOutput, standardError ).GetAwaiter().GetResult();

	/// <summary>Runs <c>ptx</c> asynchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? standardInput = null,
		TextWriter? standardOutput = null,
		TextWriter? standardError = null,
		CancellationToken cancellationToken = default
	) {
		standardInput ??= Console.In;
		standardOutput ??= Console.Out;
		standardError ??= Console.Error;
		using var inputAdapter = new TextReaderStream( standardInput, leaveOpen: true );
		return await RunAsync(
			args,
			new CommandContext(
				"ptx",
				standardInput,
				standardOutput,
				standardError,
				inputAdapter,
				null,
				null,
				cancellationToken
			)
		).ConfigureAwait( false );
	}

	/// <summary>Runs <c>ptx</c> asynchronously against a byte-capable command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		ByteOutputStream? standardOutput = null;
		ByteOutputStream? error = null;
		Stream? destination = null;
		var ownsDestination = false;
		var status = CommandExitCodes.Failure;
		try {
			error = new ByteOutputStream( context.StandardError, context.StandardErrorStream );
			var parsed = CreateParser().Parse( args );
			if ( !parsed.IsSuccess ) {
				await WriteParseFailureAsync( error, context, parsed ).ConfigureAwait( false );
				return await FinishAsync( CommandExitCodes.Failure, null, error ).ConfigureAwait( false );
			}
			if ( parsed.HasOption( "help" ) ) {
				standardOutput = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
				await WriteHelpAsync( standardOutput, context.CancellationToken ).ConfigureAwait( false );
				return await FinishAsync( CommandExitCodes.Success, standardOutput, error ).ConfigureAwait( false );
			}
			if ( parsed.HasOption( "version" ) ) {
				standardOutput = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
				await standardOutput.WriteTextAsync(
					string.Concat( VersionText, Environment.NewLine ),
					context.CancellationToken
				).ConfigureAwait( false );
				return await FinishAsync( CommandExitCodes.Success, standardOutput, error ).ConfigureAwait( false );
			}
			if ( !TryCreateSettings( parsed, out var settings, out var settingsError ) ) {
				await WriteDiagnosticAsync(
					error,
					context.ProgramName,
					settingsError!,
					context.CancellationToken
				).ConfigureAwait( false );
				await WriteTryHelpAsync( error, context.ProgramName, context.CancellationToken ).ConfigureAwait( false );
				return await FinishAsync( CommandExitCodes.Failure, null, error ).ConfigureAwait( false );
			}
			if ( settings!.GnuExtensions ) {
				var inputFiles = await Icod.CoreUtils.Shared.FileSystem.Traversal.PathnameOperandExpander.ExpandPatternsPreservingLiteralsAsync(
					settings.InputFiles,
					cancellationToken: context.CancellationToken
				).ConfigureAwait( false );
				settings.InputFiles.Clear();
				settings.InputFiles.AddRange(
					inputFiles
				);
			} else {
				settings.InputFiles[ 0 ] = await Icod.CoreUtils.Shared.FileSystem.Traversal.PathnameOperandExpander.ExpandSingularAsync(
					settings.InputFiles[ 0 ],
					cancellationToken: context.CancellationToken
				).ConfigureAwait( false );
			}
			if ( null == settings.OutputFile ) {
				standardOutput = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
				destination = standardOutput;
			} else {
				destination = new FileStream(
					settings.OutputFile,
					FileMode.Create,
					FileAccess.Write,
					FileShare.Read,
					65_536,
					FileOptions.Asynchronous | FileOptions.SequentialScan
				);
				ownsDestination = true;
			}
			await PtxEngine.RunAsync( settings, context, destination ).ConfigureAwait( false );
			await destination.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
			status = CommandExitCodes.Success;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			status = CommandExitCodes.Canceled;
		} catch ( Exception exception ) when ( IsExpectedCommandException( exception ) ) {
			await TryWriteFailureAsync( error, context, exception.Message ).ConfigureAwait( false );
			status = CommandExitCodes.Failure;
		} finally {
			if ( ownsDestination && null != destination ) {
				try {
					await destination.DisposeAsync().ConfigureAwait( false );
				} catch ( Exception exception ) when ( IsExpectedCommandException( exception ) ) {
					status = CommandExitCodes.Failure;
					await TryWriteFailureAsync( error, context, exception.Message ).ConfigureAwait( false );
				}
			}
		}
		return await FinishAsync( status, standardOutput, error ).ConfigureAwait( false );
	}

	private static OptionParser CreateParser() => new(
		[
			new OptionDefinition( "auto-reference", 'A', [ "auto-reference" ] ),
			new OptionDefinition( "break-file", 'b', [ "break-file" ], OptionValueArity.Required ),
			new OptionDefinition( "flag-truncation", 'F', [ "flag-truncation" ], OptionValueArity.Required ),
			new OptionDefinition( "ignore-case", 'f', [ "ignore-case" ] ),
			new OptionDefinition( "gap-size", 'g', [ "gap-size" ], OptionValueArity.Required ),
			new OptionDefinition( "ignore-file", 'i', [ "ignore-file" ], OptionValueArity.Required ),
			new OptionDefinition( "macro-name", 'M', [ "macro-name" ], OptionValueArity.Required ),
			new OptionDefinition( "only-file", 'o', [ "only-file" ], OptionValueArity.Required ),
			new OptionDefinition( "references", 'r', [ "references" ] ),
			new OptionDefinition( "right-side-refs", 'R', [ "right-side-refs" ] ),
			new OptionDefinition( "format", longNames: [ "format" ], valueArity: OptionValueArity.Required ),
			new OptionDefinition( "roff", 'O' ),
			new OptionDefinition( "sentence-regexp", 'S', [ "sentence-regexp" ], OptionValueArity.Required ),
			new OptionDefinition( "traditional", 'G', [ "traditional" ] ),
			new OptionDefinition( "tex", 'T' ),
			new OptionDefinition( "typeset-mode", 't', [ "typeset-mode" ] ),
			new OptionDefinition( "width", 'w', [ "width" ], OptionValueArity.Required ),
			new OptionDefinition( "word-regexp", 'W', [ "word-regexp" ], OptionValueArity.Required ),
			new OptionDefinition( "help", longNames: [ "help" ], allowMultiple: false ),
			new OptionDefinition( "version", longNames: [ "version" ], allowMultiple: false )
		],
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	private static bool TryCreateSettings(
		OptionParseResult parsed,
		out PtxSettings? settings,
		out string? error
	) {
		settings = new PtxSettings();
		error = null;
		int? lineWidth = null;
		PtxOutputFormat? selectedFormat = null;
		foreach ( var option in parsed.Options ) {
			switch ( option.Definition.Key ) {
				case "auto-reference":
					settings.AutoReference = true;
					break;
				case "break-file":
					settings.BreakFile = option.Value;
					break;
				case "flag-truncation":
					settings.TruncationString = UnescapeBytes( option.Value! );
					break;
				case "ignore-case":
					settings.IgnoreCase = true;
					break;
				case "gap-size":
					if ( !TryParsePositiveInteger( option.Value, out var gapSize ) ) {
						error = string.Concat( "invalid gap width: '", option.Value, "'" );
						return false;
					}
					settings.GapSize = gapSize;
					break;
				case "ignore-file":
					settings.IgnoreFile = option.Value;
					break;
				case "macro-name":
					settings.MacroName = option.Value!;
					break;
				case "only-file":
					settings.OnlyFile = option.Value;
					break;
				case "references":
					settings.InputReference = true;
					break;
				case "right-side-refs":
					settings.RightReference = true;
					break;
				case "format":
					if ( !TryParseFormat( option.Value, out var format ) ) {
						error = string.Concat( "invalid argument '", option.Value, "' for '--format'" );
						return false;
					}
					selectedFormat = format;
					break;
				case "roff":
					selectedFormat = PtxOutputFormat.Roff;
					break;
				case "sentence-regexp":
					settings.HasSentencePattern = true;
					settings.SentencePattern = Encoding.Latin1.GetString( UnescapeBytes( option.Value! ) );
					break;
				case "traditional":
					settings.GnuExtensions = false;
					break;
				case "tex":
					selectedFormat = PtxOutputFormat.Tex;
					break;
				case "typeset-mode":
					lineWidth ??= 100;
					break;
				case "width":
					if ( !TryParsePositiveInteger( option.Value, out var width ) ) {
						error = string.Concat( "invalid line width: '", option.Value, "'" );
						return false;
					}
					lineWidth = width;
					break;
				case "word-regexp":
					settings.WordPattern = Encoding.Latin1.GetString( UnescapeBytes( option.Value! ) );
					break;
			}
		}
		settings.LineWidth = lineWidth ?? 72;
		settings.OutputFormat = selectedFormat
			?? ( settings.GnuExtensions ? PtxOutputFormat.Dumb : PtxOutputFormat.Roff );
		if ( settings.GnuExtensions ) {
			if ( 0 == parsed.Operands.Count ) {
				settings.InputFiles.Add( "-" );
			} else {
				settings.InputFiles.AddRange( parsed.Operands );
			}
		} else {
			if ( 2 < parsed.Operands.Count ) {
				error = string.Concat( "extra operand '", parsed.Operands[ 2 ], "'" );
				return false;
			}
			settings.InputFiles.Add( 0 == parsed.Operands.Count ? "-" : parsed.Operands[ 0 ] );
			if ( 2 == parsed.Operands.Count ) {
				settings.OutputFile = parsed.Operands[ 1 ];
			}
		}
		return true;
	}

	private static bool TryParseFormat( string? value, out PtxOutputFormat format ) {
		format = PtxOutputFormat.Dumb;
		if ( !string.IsNullOrEmpty( value ) && "roff".StartsWith( value, StringComparison.Ordinal ) ) {
			format = PtxOutputFormat.Roff;
			return true;
		}
		if ( !string.IsNullOrEmpty( value ) && "tex".StartsWith( value, StringComparison.Ordinal ) ) {
			format = PtxOutputFormat.Tex;
			return true;
		}
		return false;
	}

	private static bool TryParsePositiveInteger( string? value, out int result ) {
		result = 0;
		if ( string.IsNullOrEmpty( value ) ) {
			return false;
		}
		var index = 0;
		if ( '+' == value[ index ] ) {
			index++;
			if ( index == value.Length ) {
				return false;
			}
		} else if ( '-' == value[ index ] ) {
			return false;
		}
		var numberBase = 10;
		if ( index + 1 < value.Length && '0' == value[ index ] ) {
			if ( 'x' == value[ index + 1 ] || 'X' == value[ index + 1 ] ) {
				numberBase = 16;
				index += 2;
				if ( index == value.Length ) {
					return false;
				}
			} else {
				numberBase = 8;
				index++;
			}
		}
		var parsed = 0;
		for ( ; index < value.Length; index++ ) {
			var digit = DigitValue( value[ index ] );
			if ( digit < 0 || digit >= numberBase ) {
				return false;
			}
			if ( parsed > ( int.MaxValue - digit ) / numberBase ) {
				return false;
			}
			parsed = ( parsed * numberBase ) + digit;
		}
		result = parsed;
		return 0 < result;
	}

	private static int DigitValue( char value ) => value switch {
		>= '0' and <= '9' => value - '0',
		>= 'A' and <= 'F' => value - 'A' + 10,
		>= 'a' and <= 'f' => value - 'a' + 10,
		_ => -1
	};

	private static byte[] UnescapeBytes( string value ) {
		var source = Encoding.UTF8.GetBytes( value );
		var result = new List<byte>( source.Length );
		for ( var index = 0; index < source.Length; index++ ) {
			var current = source[ index ];
			if ( (byte)'\\' != current ) {
				if ( 0 == current ) {
					break;
				}
				result.Add( current );
				continue;
			}
			index++;
			if ( index >= source.Length ) {
				break;
			}
			current = source[ index ];
			switch ( current ) {
				case (byte)'x': {
					var parsed = 0;
					var digits = 0;
					while ( index + 1 < source.Length && 3 > digits && IsHex( source[ index + 1 ] ) ) {
						index++;
						parsed = ( parsed * 16 ) + HexValue( source[ index ] );
						digits++;
					}
					if ( 0 == digits ) {
						result.Add( (byte)'\\' );
						result.Add( (byte)'x' );
					} else if ( !AppendCStringByte( result, (byte)parsed ) ) {
						return [ .. result ];
					}
					break;
				}
				case (byte)'0': {
					var parsed = 0;
					var digits = 0;
					while (
						index + 1 < source.Length
						&& 3 > digits
						&& source[ index + 1 ] is >= (byte)'0' and <= (byte)'7'
					) {
						index++;
						parsed = ( parsed * 8 ) + ( source[ index ] - (byte)'0' );
						digits++;
					}
					if ( !AppendCStringByte( result, (byte)parsed ) ) {
						return [ .. result ];
					}
					break;
				}
				case (byte)'a': result.Add( (byte)'\a' ); break;
				case (byte)'b': result.Add( (byte)'\b' ); break;
				case (byte)'c': return [ .. result ];
				case (byte)'f': result.Add( (byte)'\f' ); break;
				case (byte)'n': result.Add( (byte)'\n' ); break;
				case (byte)'r': result.Add( (byte)'\r' ); break;
				case (byte)'t': result.Add( (byte)'\t' ); break;
				case (byte)'v': result.Add( (byte)'\v' ); break;
				default:
					result.Add( (byte)'\\' );
					if ( !AppendCStringByte( result, current ) ) {
						return [ .. result ];
					}
					break;
			}
		}
		return [ .. result ];
	}

	private static bool AppendCStringByte( List<byte> destination, byte value ) {
		if ( 0 == value ) {
			return false;
		}
		destination.Add( value );
		return true;
	}

	private static bool IsHex( byte value ) => value is >= (byte)'0' and <= (byte)'9'
		or >= (byte)'A' and <= (byte)'F'
		or >= (byte)'a' and <= (byte)'f';

	private static int HexValue( byte value ) => value switch {
		>= (byte)'0' and <= (byte)'9' => value - (byte)'0',
		>= (byte)'A' and <= (byte)'F' => value - (byte)'A' + 10,
		_ => value - (byte)'a' + 10
	};

	private static async Task WriteHelpAsync(
		ByteOutputStream output,
		CancellationToken cancellationToken
	) {
		await output.WriteTextAsync(
			string.Concat(
				"Usage: ptx [OPTION]... [INPUT]...   (without -G)", Environment.NewLine,
				"  or:  ptx -G [OPTION]... [INPUT [OUTPUT]]", Environment.NewLine,
				"Output a permuted index, including context, of the words in the input files.", Environment.NewLine,
				Environment.NewLine,
				"  -A, --auto-reference           output automatically generated references", Environment.NewLine,
				"  -G, --traditional              behave more like System V 'ptx'", Environment.NewLine,
				"  -F, --flag-truncation=STRING   use STRING to flag truncations (default '/')", Environment.NewLine,
				"  -M, --macro-name=STRING        macro name to use instead of 'xx'", Environment.NewLine,
				"  -O, --format=roff              generate roff directives", Environment.NewLine,
				"  -R, --right-side-refs          put references at right, not counted in -w", Environment.NewLine,
				"  -S, --sentence-regexp=REGEXP   regexp for ends of lines or sentences", Environment.NewLine,
				"  -T, --format=tex               generate TeX directives", Environment.NewLine,
				"  -W, --word-regexp=REGEXP       use REGEXP to match each keyword", Environment.NewLine,
				"  -b, --break-file=FILE          read word-break characters from FILE", Environment.NewLine,
				"  -f, --ignore-case              fold lower case to upper case for sorting", Environment.NewLine,
				"  -g, --gap-size=NUMBER          gap size between output fields", Environment.NewLine,
				"  -i, --ignore-file=FILE         read ignored words from FILE", Environment.NewLine,
				"  -o, --only-file=FILE           read accepted words from FILE", Environment.NewLine,
				"  -r, --references               use the first field of each line as a reference", Environment.NewLine,
				"  -t, --typeset-mode             use a default width of 100", Environment.NewLine,
				"  -w, --width=NUMBER             output width in columns", Environment.NewLine,
				"      --help                     display this help and exit", Environment.NewLine,
				"      --version                  output version information and exit", Environment.NewLine
			),
			cancellationToken
		).ConfigureAwait( false );
	}

	private static async Task WriteParseFailureAsync(
		ByteOutputStream error,
		CommandContext context,
		OptionParseResult parsed
	) {
		await error.WriteTextAsync(
			string.Concat(
				OptionDiagnosticFormatter.Format( context.ProgramName, parsed.Errors[ 0 ] ),
				Environment.NewLine
			),
			context.CancellationToken
		).ConfigureAwait( false );
		await WriteTryHelpAsync( error, context.ProgramName, context.CancellationToken ).ConfigureAwait( false );
	}

	private static Task WriteTryHelpAsync(
		ByteOutputStream error,
		string programName,
		CancellationToken cancellationToken
	) => error.WriteTextAsync(
		string.Concat( "Try '", programName, " --help' for more information.", Environment.NewLine ),
		cancellationToken
	).AsTask();

	private static Task WriteDiagnosticAsync(
		ByteOutputStream error,
		string programName,
		string message,
		CancellationToken cancellationToken
	) => error.WriteTextAsync(
		string.Concat( programName, ": ", message, Environment.NewLine ),
		cancellationToken
	).AsTask();

	private static async Task TryWriteFailureAsync(
		ByteOutputStream? error,
		CommandContext context,
		string message
	) {
		if ( null == error ) {
			return;
		}
		try {
			await WriteDiagnosticAsync(
				error,
				context.ProgramName,
				message,
				CancellationToken.None
			).ConfigureAwait( false );
		} catch ( Exception exception ) when ( IsExpectedCommandException( exception ) ) {
			// A secondary diagnostic failure cannot be recovered from.
		}
	}

	private static async Task<int> FinishAsync(
		int status,
		ByteOutputStream? output,
		ByteOutputStream? error
	) {
		if ( null != output ) {
			try {
				await output.CompleteAsync( CancellationToken.None ).ConfigureAwait( false );
			} catch ( Exception exception ) when ( IsExpectedCommandException( exception ) ) {
				status = CommandExitCodes.Failure;
			}
			await output.DisposeAsync().ConfigureAwait( false );
		}
		if ( null != error ) {
			try {
				await error.CompleteAsync( CancellationToken.None ).ConfigureAwait( false );
			} catch ( Exception exception ) when ( IsExpectedCommandException( exception ) ) {
				status = CommandExitCodes.Failure;
			}
			await error.DisposeAsync().ConfigureAwait( false );
		}
		return status;
	}

	private static bool IsExpectedCommandException( Exception exception ) => exception is
		IOException
		or UnauthorizedAccessException
		or InvalidDataException
		or InvalidOperationException
		or NotSupportedException
		or ArgumentException
		or OverflowException
		or AggregateException;
}
