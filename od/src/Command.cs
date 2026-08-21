namespace Icod.CoreUtils.Od;

using System.Globalization;
using System.Numerics;
using System.Text;
using Icod.CoreUtils.Shared.BinaryFormatting;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;

/// <summary>
/// Implements <c>od [OPTION]... [FILE]...</c> and the traditional offset forms.
/// </summary>
public static class Command {
	private const string VersionText = "od (Icod.CoreUtils) 1.0";
	private enum AddressRadix {
		None,
		Decimal,
		Octal,
		Hexadecimal
	}
	private sealed record ParsedOptions(
		AddressRadix AddressRadix,
		BinaryByteOrder ByteOrder,
		ulong SkipBytes,
		ulong? ReadBytes,
		int? MinimumStringLength,
		IReadOnlyList<BinaryFormatSpecification> Formats,
		int Width,
		bool OutputDuplicates,
		IReadOnlyList<string> Files,
		ulong? PseudoAddress,
		string? Warning
	);

	/// <summary>
	/// Executes <c>od</c> synchronously with optional standard-stream substitution.
	/// </summary>
	/// <remarks>
	/// This compatibility entry point blocks on the TAP implementation. A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream; caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		return RunAsync(
			args,
			new CommandContext(
				"od",
				stdin,
				stdout,
				stderr,
				Console.OpenStandardInput(),
				Console.OpenStandardOutput(),
				Console.OpenStandardError()
			)
		).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Executes <c>od</c> asynchronously using caller-supplied standard streams.
	/// </summary>
	/// <remarks>
	/// The supplied standard streams are required for this overload and remain caller-owned. Cancellation is reported through the command status policy rather than by disposing those streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="standardInput">The caller-owned standard-input source used for command data.</param>
	/// <param name="standardOutput">The caller-owned writer used for standard output.</param>
	/// <param name="standardError">The caller-owned writer used for diagnostics.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static Task<int> RunAsync(
		string[] args,
		Stream standardInput,
		TextWriter standardOutput,
		TextWriter standardError,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( standardInput );
		return RunAsync(
			args,
			new CommandContext(
				"od",
				TextReader.Null,
				standardOutput,
				standardError,
				standardInputStream: standardInput,
				cancellationToken: cancellationToken
			)
		);
	}

	/// <summary>
	/// Executes <c>od</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			var normalizedArguments = NormalizeOptionalArguments( args );
			var parser = CreateParser();
			var parseResult = parser.Parse( normalizedArguments );
			if ( !parseResult.IsSuccess ) {
				await WriteParseErrorsAsync( parseResult, context ).ConfigureAwait( false );
				return CommandExitCodes.UsageError;
			}
			if ( parseResult.HasOption( "help" ) ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parseResult.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					VersionText.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( !TryBuildOptions( parseResult, out var options, out var optionError ) ) {
				await context.Diagnostics.ErrorAsync(
					optionError ?? "invalid arguments",
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.UsageError;
			}
			if ( null != options.Warning ) {
				await context.StandardError.WriteLineAsync(
					string.Concat( context.ProgramName, ": ", options.Warning ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
			}

			await using var input = new ConcatenatedInput(
				options.Files,
				context
			);
			if ( !await SkipAsync( input, options.SkipBytes, context ).ConfigureAwait( false ) ) {
				return CommandExitCodes.Failure;
			}
			var status = options.MinimumStringLength.HasValue
				? await DumpStringsAsync( input, options, context ).ConfigureAwait( false )
				: await DumpValuesAsync( input, options, context ).ConfigureAwait( false )
			;
			await context.StandardOutput.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
			return input.HadError ? CommandExitCodes.Failure : status;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		} catch ( IOException ex ) {
			await WriteDiagnosticWithoutCancellationAsync( context, ex.Message ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		} catch ( UnauthorizedAccessException ex ) {
			await WriteDiagnosticWithoutCancellationAsync( context, ex.Message ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static OptionParser CreateParser() {
		return new OptionParser(
			new OptionDefinition[] {
				new( "address-radix", 'A', new[] { "address-radix" }, OptionValueArity.Required ),
				new( "endian", null, new[] { "endian" }, OptionValueArity.Required ),
				new( "skip", 'j', new[] { "skip-bytes" }, OptionValueArity.Required ),
				new( "read", 'N', new[] { "read-bytes" }, OptionValueArity.Required ),
				new( "strings", 'S', new[] { "strings" }, OptionValueArity.Required ),
				new( "format", 't', new[] { "format" }, OptionValueArity.Required ),
				new( "duplicates", 'v', new[] { "output-duplicates" } ),
				new( "width", 'w', new[] { "width" }, OptionValueArity.Required ),
				new( "traditional", null, new[] { "traditional" } ),
				new( "named", 'a' ),
				new( "byte-octal", 'b' ),
				new( "character", 'c' ),
				new( "unsigned-short", 'd' ),
				new( "single", 'f' ),
				new( "signed-int", 'i' ),
				new( "signed-long", 'l' ),
				new( "word-octal", 'o' ),
				new( "signed-short", 's' ),
				new( "word-hex", 'x' ),
				new( "help", null, new[] { "help" }, allowMultiple: false ),
				new( "version", null, new[] { "version" }, allowMultiple: false )
			},
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute
			}
		);
	}

	private static string[] NormalizeOptionalArguments(
		IReadOnlyList<string> args
	) {
		var output = new string[ args.Count ];
		for ( var index = 0; index < args.Count; index++ ) {
			output[ index ] = args[ index ] switch {
				"--strings" => "--strings=3",
				"--width" => "--width=32",
				"-w" => "-w32",
				_ => args[ index ]
			};
		}
		return output;
	}

	private static bool TryBuildOptions(
		OptionParseResult parseResult,
		out ParsedOptions options,
		out string? error
	) {
		error = null;
		var addressRadix = AddressRadix.Octal;
		var byteOrder = BinaryByteOrder.Native;
		ulong skip = 0;
		ulong? read = null;
		int? minimumStringLength = null;
		int? requestedWidth = null;
		var formats = new List<BinaryFormatSpecification>();
		var explicitFormat = false;

		foreach ( var occurrence in parseResult.Options ) {
			var key = occurrence.Definition.Key;
			switch ( key ) {
				case "address-radix":
					if ( !TryParseAddressRadix( occurrence.Value, out addressRadix ) ) {
						error = string.Concat( "invalid address radix '", occurrence.Value, "'" );
						options = null!;
						return false;
					}
					break;
				case "endian":
					if ( !TryParseByteOrder( occurrence.Value, out byteOrder ) ) {
						error = string.Concat( "invalid byte order '", occurrence.Value, "'" );
						options = null!;
						return false;
					}
					break;
				case "skip":
					if ( !TryParseByteCount( occurrence.Value, out skip ) ) {
						error = string.Concat( "invalid number of bytes to skip: '", occurrence.Value, "'" );
						options = null!;
						return false;
					}
					break;
				case "read":
					if ( !TryParseByteCount( occurrence.Value, out var parsedRead ) ) {
						error = string.Concat( "invalid maximum number of bytes: '", occurrence.Value, "'" );
						options = null!;
						return false;
					}
					read = parsedRead;
					break;
				case "strings":
					if (
						!TryParseByteCount( occurrence.Value, out var parsedMinimum )
						|| 0 == parsedMinimum
						|| int.MaxValue < parsedMinimum
					) {
						error = string.Concat( "invalid minimum string length: '", occurrence.Value, "'" );
						options = null!;
						return false;
					}
					minimumStringLength = ( int )parsedMinimum;
					break;
				case "width":
					if (
						!int.TryParse( occurrence.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedWidth )
						|| 0 >= parsedWidth
					) {
						error = string.Concat( "invalid output width: '", occurrence.Value, "'" );
						options = null!;
						return false;
					}
					requestedWidth = parsedWidth;
					break;
				case "format":
					explicitFormat = true;
					if ( !AppendFormats( occurrence.Value, formats, out error ) ) {
						options = null!;
						return false;
					}
					break;
				case "named":
					explicitFormat = true;
					AppendFormats( "a", formats, out _ );
					break;
				case "byte-octal":
					explicitFormat = true;
					AppendFormats( "o1", formats, out _ );
					break;
				case "character":
					explicitFormat = true;
					AppendFormats( "c", formats, out _ );
					break;
				case "unsigned-short":
					explicitFormat = true;
					AppendFormats( "u2", formats, out _ );
					break;
				case "single":
					explicitFormat = true;
					AppendFormats( "fF", formats, out _ );
					break;
				case "signed-int":
					explicitFormat = true;
					AppendFormats( "dI", formats, out _ );
					break;
				case "signed-long":
					explicitFormat = true;
					AppendFormats( "dL", formats, out _ );
					break;
				case "word-octal":
					explicitFormat = true;
					AppendFormats( "o2", formats, out _ );
					break;
				case "signed-short":
					explicitFormat = true;
					AppendFormats( "d2", formats, out _ );
					break;
				case "word-hex":
					explicitFormat = true;
					AppendFormats( "x2", formats, out _ );
					break;
			}
		}

		if ( minimumStringLength.HasValue && explicitFormat ) {
			error = "no type may be specified when dumping strings";
			options = null!;
			return false;
		}
		if ( 0 == formats.Count && !minimumStringLength.HasValue ) {
			AppendFormats( "oS", formats, out _ );
		}
		if (
			!BinaryLineLayout.TryResolveWidth(
				formats,
				requestedWidth,
				parseResult.HasOption( "width" ),
				out var width,
				out var widthMessage
			)
		) {
			error = widthMessage;
			options = null!;
			return false;
		}

		var operands = parseResult.Operands.ToList();
		ulong? pseudoAddress = null;
		if (
			!TryApplyTraditionalOperands(
				parseResult,
				operands,
				ref skip,
				out pseudoAddress,
				out error
			)
		) {
			options = null!;
			return false;
		}
		var files = PathnameExpander.Expand(
			operands,
			new PathnameExpansionOptions {
				IncludeFiles = true,
				IncludeDirectories = false,
				PreserveUnmatchedPatterns = true
			}
		);
		options = new ParsedOptions(
			addressRadix,
			byteOrder,
			skip,
			read,
			minimumStringLength,
			formats.AsReadOnly(),
			width,
			parseResult.HasOption( "duplicates" ),
			files,
			pseudoAddress,
			widthMessage
		);
		return true;
	}

	private static bool AppendFormats(
		string? value,
		ICollection<BinaryFormatSpecification> output,
		out string? error
	) {
		if ( !BinaryFormatParser.TryParse( value ?? string.Empty, out var parsed, out error ) ) {
			return false;
		}
		foreach ( var format in parsed ) {
			output.Add( format );
		}
		return true;
	}

	private static bool TryApplyTraditionalOperands(
		OptionParseResult parseResult,
		List<string> operands,
		ref ulong skip,
		out ulong? pseudoAddress,
		out string? error
	) {
		pseudoAddress = null;
		error = null;
		var explicitTraditional = parseResult.HasOption( "traditional" );
		var implicitTraditional = !explicitTraditional
			&& CanInferTraditional( parseResult )
			&& (
				0 < operands.Count
				&& (
					operands[ ^1 ].StartsWith( "+", StringComparison.Ordinal )
					|| ( 2 == operands.Count && LooksLikeTraditionalNumber( operands[ 1 ] ) )
				)
			)
		;
		if ( !explicitTraditional && !implicitTraditional ) {
			return true;
		}
		if ( 3 < operands.Count ) {
			error = "too many operands for traditional format";
			return false;
		}
		if ( 0 == operands.Count ) {
			return true;
		}

		string? offsetText = null;
		string? labelText = null;
		if ( 1 == operands.Count ) {
			if ( LooksLikeTraditionalNumber( operands[ 0 ] ) ) {
				offsetText = operands[ 0 ];
				operands.Clear();
			}
		} else if ( 2 == operands.Count ) {
			if ( LooksLikeTraditionalNumber( operands[ 0 ] ) ) {
				offsetText = operands[ 0 ];
				labelText = operands[ 1 ];
				operands.Clear();
			} else {
				offsetText = operands[ 1 ];
				operands.RemoveAt( 1 );
			}
		} else {
			offsetText = operands[ 1 ];
			labelText = operands[ 2 ];
			operands.RemoveRange( 1, 2 );
		}

		if ( null != offsetText ) {
			if ( parseResult.HasOption( "skip" ) ) {
				error = "a traditional offset cannot be combined with --skip-bytes";
				return false;
			}
			if ( !TryParseTraditionalNumber( offsetText, out skip ) ) {
				error = string.Concat( "invalid offset '", offsetText, "'" );
				return false;
			}
		}
		if ( null != labelText ) {
			if ( !TryParseTraditionalNumber( labelText, out var label ) ) {
				error = string.Concat( "invalid label '", labelText, "'" );
				return false;
			}
			pseudoAddress = label;
		}
		return true;
	}

	private static bool CanInferTraditional(
		OptionParseResult parseResult
	) {
		var allowed = new HashSet<string>(
			new[] {
				"endian", "named", "byte-octal", "character", "unsigned-short",
				"single", "signed-int", "signed-long", "word-octal", "signed-short", "word-hex"
			},
			StringComparer.Ordinal
		);
		return parseResult.Options.All(
			occurrence => allowed.Contains( occurrence.Definition.Key )
		);
	}

	private static bool LooksLikeTraditionalNumber(
		string value
	) {
		if ( string.IsNullOrEmpty( value ) ) {
			return false;
		}
		var span = value.AsSpan();
		if ( '+' == span[ 0 ] ) {
			span = span.Slice( 1 );
		}
		return 0 < span.Length && ( char.IsDigit( span[ 0 ] ) );
	}

	private static bool TryParseTraditionalNumber(
		string value,
		out ulong result
	) {
		result = 0;
		var text = value.Trim();
		if ( text.StartsWith( "+", StringComparison.Ordinal ) ) {
			text = text.Substring( 1 );
		}
		var multiplyByBlock = text.EndsWith( "b", StringComparison.Ordinal );
		if ( multiplyByBlock ) {
			text = text.Substring( 0, text.Length - 1 );
		}
		var decimalNotation = text.EndsWith( ".", StringComparison.Ordinal );
		if ( decimalNotation ) {
			text = text.Substring( 0, text.Length - 1 );
		}
		var radix = decimalNotation
			? 10
			: text.StartsWith( "0x", StringComparison.OrdinalIgnoreCase ) ? 16 : 8
		;
		if ( 16 == radix ) {
			text = text.Substring( 2 );
		}
		if ( !TryParseUnsignedRadix( text, radix, out result ) ) {
			return false;
		}
		try {
			result = multiplyByBlock ? checked( result * 512UL ) : result;
			return true;
		} catch ( OverflowException ) {
			return false;
		}
	}

	private static bool TryParseAddressRadix(
		string? value,
		out AddressRadix radix
	) {
		radix = value switch {
			"d" => AddressRadix.Decimal,
			"o" => AddressRadix.Octal,
			"x" => AddressRadix.Hexadecimal,
			"n" => AddressRadix.None,
			_ => AddressRadix.Octal
		};
		return value is "d" or "o" or "x" or "n";
	}

	private static bool TryParseByteOrder(
		string? value,
		out BinaryByteOrder byteOrder
	) {
		byteOrder = value switch {
			"little" => BinaryByteOrder.LittleEndian,
			"big" => BinaryByteOrder.BigEndian,
			_ => BinaryByteOrder.Native
		};
		return value is "little" or "big";
	}

	private static bool TryParseByteCount(
		string? value,
		out ulong result
	) {
		result = 0;
		if ( string.IsNullOrEmpty( value ) ) {
			return false;
		}
		var text = value;
		var multiplier = BigInteger.One;
		if ( text.EndsWith( "b", StringComparison.Ordinal ) ) {
			text = text.Substring( 0, text.Length - 1 );
			multiplier = new BigInteger( 512 );
		} else {
			var suffixLetters = "KMGTPEZYRQ";
			for ( var power = suffixLetters.Length; 1 <= power; power-- ) {
				var letter = suffixLetters[ power - 1 ];
				var binaryLong = string.Concat( letter, "iB" );
				var decimalLong = string.Concat( letter, "B" );
				if ( text.EndsWith( binaryLong, StringComparison.Ordinal ) ) {
					text = text.Substring( 0, text.Length - binaryLong.Length );
					multiplier = BigInteger.Pow( new BigInteger( 1024 ), power );
					break;
				}
				if ( text.EndsWith( decimalLong, StringComparison.Ordinal ) ) {
					text = text.Substring( 0, text.Length - decimalLong.Length );
					multiplier = BigInteger.Pow( new BigInteger( 1000 ), power );
					break;
				}
				if ( text.EndsWith( letter.ToString(), StringComparison.Ordinal ) ) {
					text = text.Substring( 0, text.Length - 1 );
					multiplier = BigInteger.Pow( new BigInteger( 1024 ), power );
					break;
				}
			}
		}
		if ( !TryParseNonnegativeInteger( text, out var number ) ) {
			return false;
		}
		var product = number * multiplier;
		if ( product < BigInteger.Zero || product > ulong.MaxValue ) {
			return false;
		}
		result = ( ulong )product;
		return true;
	}

	private static bool TryParseNonnegativeInteger(
		string text,
		out BigInteger value
	) {
		value = BigInteger.Zero;
		if ( string.IsNullOrEmpty( text ) ) {
			return false;
		}
		var radix = 10;
		if ( text.StartsWith( "0x", StringComparison.OrdinalIgnoreCase ) ) {
			radix = 16;
			text = text.Substring( 2 );
		}
		if ( string.IsNullOrEmpty( text ) ) {
			return false;
		}
		foreach ( var character in text ) {
			var digit = character switch {
				>= '0' and <= '9' => character - '0',
				>= 'a' and <= 'f' => character - 'a' + 10,
				>= 'A' and <= 'F' => character - 'A' + 10,
				_ => -1
			};
			if ( 0 > digit || radix <= digit ) {
				return false;
			}
			value = value * radix + digit;
		}
		return true;
	}

	private static bool TryParseUnsignedRadix(
		string text,
		int radix,
		out ulong value
	) {
		value = 0;
		if ( string.IsNullOrEmpty( text ) ) {
			return false;
		}
		try {
			foreach ( var character in text ) {
				var digit = character switch {
					>= '0' and <= '9' => character - '0',
					>= 'a' and <= 'f' => character - 'a' + 10,
					>= 'A' and <= 'F' => character - 'A' + 10,
					_ => -1
				};
				if ( 0 > digit || radix <= digit ) {
					return false;
				}
				value = checked( value * ( ulong )radix + ( ulong )digit );
			}
			return true;
		} catch ( OverflowException ) {
			return false;
		}
	}

	private static async Task<bool> SkipAsync(
		ConcatenatedInput input,
		ulong count,
		CommandContext context
	) {
		var buffer = new byte[ 8192 ];
		var remaining = count;
		while ( 0 < remaining ) {
			var requested = ( int )Math.Min( ( ulong )buffer.Length, remaining );
			var read = await input.ReadAsync(
				buffer.AsMemory( 0, requested ),
				context.CancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				await context.Diagnostics.ErrorAsync(
					"cannot skip past end of combined input",
					context.CancellationToken
				).ConfigureAwait( false );
				return false;
			}
			remaining = remaining - ( ulong )read;
		}
		return true;
	}

	private static async Task<int> DumpValuesAsync(
		ConcatenatedInput input,
		ParsedOptions options,
		CommandContext context
	) {
		var buffer = new byte[ options.Width ];
		byte[]? previousFull = null;
		var duplicateMarkerWritten = false;
		var consumed = 0UL;
		var remaining = options.ReadBytes;
		while ( !remaining.HasValue || 0 < remaining.Value ) {
			var requested = remaining.HasValue
				? ( int )Math.Min( ( ulong )buffer.Length, remaining.Value )
				: buffer.Length
			;
			var count = await ReadBlockAsync(
				input,
				buffer.AsMemory( 0, requested ),
				context.CancellationToken
			).ConfigureAwait( false );
			if ( 0 == count ) {
				break;
			}
			var isFull = count == options.Width;
			var duplicate = isFull
				&& null != previousFull
				&& buffer.AsSpan( 0, count ).SequenceEqual( previousFull )
			;
			if ( duplicate && !options.OutputDuplicates ) {
				if ( !duplicateMarkerWritten ) {
					await context.StandardOutput.WriteLineAsync(
						"*".AsMemory(),
						context.CancellationToken
					).ConfigureAwait( false );
					duplicateMarkerWritten = true;
				}
			} else {
				await WriteValueBlockAsync(
					buffer.AsMemory( 0, count ),
					checked( options.SkipBytes + consumed ),
					options,
					context
				).ConfigureAwait( false );
				duplicateMarkerWritten = false;
				if ( isFull ) {
					previousFull ??= new byte[ options.Width ];
					buffer.AsSpan( 0, count ).CopyTo( previousFull );
				} else {
					previousFull = null;
				}
			}
			consumed += ( ulong )count;
			if ( remaining.HasValue ) {
				remaining = remaining.Value - ( ulong )count;
			}
		}
		if ( AddressRadix.None != options.AddressRadix || options.PseudoAddress.HasValue ) {
			await context.StandardOutput.WriteLineAsync(
				FormatAddressPrefix(
					checked( options.SkipBytes + consumed ),
					options.PseudoAddress.HasValue
						? checked( options.PseudoAddress.Value + consumed )
						: null,
					options.AddressRadix
				).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return CommandExitCodes.Success;
	}

	private static async Task WriteValueBlockAsync(
		ReadOnlyMemory<byte> block,
		ulong address,
		ParsedOptions options,
		CommandContext context
	) {
		var prefix = FormatAddressPrefix(
			address,
			options.PseudoAddress.HasValue
				? checked( options.PseudoAddress.Value + address - options.SkipBytes )
				: null,
			options.AddressRadix
		);
		var maxColumns = options.Formats.Max(
			format => GetNaturalValueColumns( format, options.Width )
		);
		for ( var formatIndex = 0; formatIndex < options.Formats.Count; formatIndex++ ) {
			var format = options.Formats[ formatIndex ];
			var builder = new StringBuilder();
			if ( 0 == formatIndex ) {
				builder.Append( prefix );
			} else if ( 0 < prefix.Length ) {
				builder.Append( ' ', prefix.Length );
			}
			var unitCount = ( block.Length + format.Size - 1 ) / format.Size;
			var fullUnitCount = options.Width / format.Size;
			var fieldWidth = BinaryValueFormatter.GetFieldWidth( format );
			var naturalColumns = GetNaturalValueColumns( format, options.Width );
			var padding = BinaryLineLayout.DistributeLeadingPadding(
				fullUnitCount,
				maxColumns - naturalColumns
			);
			for ( var unitIndex = 0; unitIndex < unitCount; unitIndex++ ) {
				builder.Append( ' ', 1 + padding[ unitIndex ] );
				var offset = unitIndex * format.Size;
				var count = Math.Min( format.Size, block.Length - offset );
				var formatted = BinaryValueFormatter.Format(
					format,
					block.Span.Slice( offset, count ),
					options.ByteOrder
				);
				builder.Append( formatted.PadLeft( fieldWidth ) );
			}
			if ( format.AppendPrintableTrailer ) {
				builder.Append( "  >" );
				foreach ( var value in block.Span ) {
					builder.Append( 0x20 <= value && 0x7e >= value ? ( char )value : '.' );
				}
				builder.Append( '<' );
			}
			await context.StandardOutput.WriteLineAsync(
				builder.ToString().AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
	}

	private static int GetNaturalValueColumns(
		BinaryFormatSpecification format,
		int width
	) {
		var count = width / format.Size;
		return count * ( BinaryValueFormatter.GetFieldWidth( format ) + 1 );
	}

	private static async Task<int> DumpStringsAsync(
		ConcatenatedInput input,
		ParsedOptions options,
		CommandContext context
	) {
		var minimum = options.MinimumStringLength!.Value;
		var buffer = new byte[ 8192 ];
		var current = new List<byte>();
		var consumed = 0UL;
		var start = 0UL;
		var remaining = options.ReadBytes;
		var reachedReadLimit = false;
		while ( !remaining.HasValue || 0 < remaining.Value ) {
			var requested = remaining.HasValue
				? ( int )Math.Min( ( ulong )buffer.Length, remaining.Value )
				: buffer.Length
			;
			var read = await input.ReadAsync(
				buffer.AsMemory( 0, requested ),
				context.CancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				break;
			}
			for ( var index = 0; index < read; index++ ) {
				var value = buffer[ index ];
				var absolute = checked( options.SkipBytes + consumed );
				if ( 0x20 <= value && 0x7e >= value ) {
					if ( 0 == current.Count ) {
						start = absolute;
					}
					current.Add( value );
				} else {
					if ( 0 == value && minimum <= current.Count ) {
						await WriteStringAsync( current, start, options, context ).ConfigureAwait( false );
					}
					current.Clear();
				}
				consumed++;
			}
			if ( remaining.HasValue ) {
				remaining = remaining.Value - ( ulong )read;
				reachedReadLimit = 0 == remaining.Value;
			}
		}
		if ( reachedReadLimit && minimum <= current.Count ) {
			await WriteStringAsync( current, start, options, context ).ConfigureAwait( false );
		}
		return CommandExitCodes.Success;
	}

	private static async Task WriteStringAsync(
		IReadOnlyCollection<byte> value,
		ulong address,
		ParsedOptions options,
		CommandContext context
	) {
		var prefix = FormatAddressPrefix(
			address,
			options.PseudoAddress.HasValue
				? checked( options.PseudoAddress.Value + address - options.SkipBytes )
				: null,
			options.AddressRadix
		);
		var text = Encoding.ASCII.GetString( value.ToArray() );
		await context.StandardOutput.WriteLineAsync(
			string.Concat( prefix, 0 < prefix.Length ? " " : string.Empty, text ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static async Task<int> ReadBlockAsync(
		ConcatenatedInput input,
		Memory<byte> buffer,
		CancellationToken cancellationToken
	) {
		var total = 0;
		while ( total < buffer.Length ) {
			var read = await input.ReadAsync(
				buffer.Slice( total ),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				break;
			}
			total += read;
		}
		return total;
	}

	private static string FormatAddressPrefix(
		ulong address,
		ulong? pseudoAddress,
		AddressRadix radix
	) {
		if ( AddressRadix.None == radix ) {
			return pseudoAddress.HasValue
				? string.Concat( "(", FormatAddress( pseudoAddress.Value, AddressRadix.Octal ), ")" )
				: string.Empty
			;
		}
		var primary = FormatAddress( address, radix );
		return pseudoAddress.HasValue
			? string.Concat( primary, " (", FormatAddress( pseudoAddress.Value, radix ), ")" )
			: primary
		;
	}

	private static string FormatAddress(
		ulong value,
		AddressRadix radix
	) {
		var text = radix switch {
			AddressRadix.Decimal => value.ToString( CultureInfo.InvariantCulture ),
			AddressRadix.Octal => FormatUnsignedBase( value, 8 ),
			AddressRadix.Hexadecimal => value.ToString( "x", CultureInfo.InvariantCulture ),
			_ => string.Empty
		};
		return text.PadLeft( AddressRadix.Hexadecimal == radix ? 6 : 7, '0' );
	}

	private static string FormatUnsignedBase(
		ulong value,
		int radix
	) {
		Span<char> characters = stackalloc char[ 64 ];
		var index = characters.Length;
		do {
			characters[ --index ] = ( char )( '0' + value % ( ulong )radix );
			value /= ( ulong )radix;
		} while ( 0 != value );
		return new string( characters.Slice( index ) );
	}

	private static async Task WriteParseErrorsAsync(
		OptionParseResult result,
		CommandContext context
	) {
		foreach ( var parseError in result.Errors ) {
			var message = parseError.Kind switch {
				OptionParseErrorKind.MissingOptionValue => string.Concat( "option requires an argument -- '", parseError.OptionName, "'" ),
				OptionParseErrorKind.UnexpectedOptionValue => string.Concat( "option does not allow an argument -- '", parseError.OptionName, "'" ),
				OptionParseErrorKind.AmbiguousLongOption => string.Concat( "option '", parseError.OptionName, "' is ambiguous" ),
				_ => string.Concat( "unrecognized option '", parseError.Token, "'" )
			};
			await context.Diagnostics.ErrorAsync(
				message,
				context.CancellationToken
			).ConfigureAwait( false );
		}
	}

	private static Task WriteHelpAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		const string help = """
Usage: od [OPTION]... [FILE]...
  or:  od [-abcdfilosx]... [FILE] [[+]OFFSET[.][b]]
  or:  od --traditional [OPTION]... [FILE] [[+]OFFSET[.][b] [[+]LABEL[.][b]]]
Write an unambiguous representation, octal bytes by default, of FILE to standard output.

  -A, --address-radix=RADIX   output format for file offsets: d, o, x, or n
      --endian={big|little}   swap input bytes according to the specified order
  -j, --skip-bytes=BYTES     skip BYTES input bytes first
  -N, --read-bytes=BYTES     limit dump to BYTES input bytes
  -S BYTES, --strings[=BYTES] output strings of at least BYTES graphic characters
  -t, --format=TYPE          select output formats
  -v, --output-duplicates    do not use * to mark repeated output lines
  -w[BYTES], --width[=BYTES] output BYTES bytes per output line
      --traditional          accept the traditional offset and label syntax
      --help                 display this help and exit
      --version              output version information and exit

Traditional shorthands: -a -b -c -d -f -i -l -o -s -x.
TYPE is one or more of a, c, d, f, o, u, or x, followed where applicable by a byte count or C/S/I/L alias; append z for a printable-character trailer.
BSD behavior is best effort; the tested platforms are Windows, Ubuntu, and macOS.
""";
		return output.WriteAsync(
			help.AsMemory(),
			cancellationToken
		);
	}

	private static async Task WriteDiagnosticWithoutCancellationAsync(
		CommandContext context,
		string message
	) {
		try {
			await context.Diagnostics.ErrorAsync( message, CancellationToken.None ).ConfigureAwait( false );
		} catch ( IOException ) {
			// Nothing further can be reported when the diagnostic stream itself has failed.
		}
	}

	private sealed class ConcatenatedInput : IAsyncDisposable {
		private readonly CommandContext myContext;
		private readonly IReadOnlyList<string> myFiles;
		private Stream? myCurrent;
		private bool myCurrentOwned;
		private int myIndex;
		public bool HadError {
			get;
			private set;
		}

		public ConcatenatedInput(
			IReadOnlyList<string> files,
			CommandContext context
		) {
			this.myFiles = 0 == files.Count ? new[] { "-" } : files;
			this.myContext = context;
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken
		) {
			while ( true ) {
				if ( null == this.myCurrent ) {
					if ( this.myIndex >= this.myFiles.Count ) {
						return 0;
					}
					var path = this.myFiles[ this.myIndex++ ];
					if ( "-" == path ) {
						this.myCurrent = this.myContext.StandardInputStream;
						this.myCurrentOwned = false;
						if ( null == this.myCurrent ) {
							await this.ReportAsync( "standard input is not available as a binary stream" ).ConfigureAwait( false );
							continue;
						}
					} else {
						try {
							this.myCurrent = new FileStream(
								path,
								FileMode.Open,
								FileAccess.Read,
								FileShare.ReadWrite | FileShare.Delete,
								bufferSize: 8192,
								FileOptions.Asynchronous | FileOptions.SequentialScan
							);
							this.myCurrentOwned = true;
						} catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) {
							await this.ReportAsync(
								string.Concat( "cannot open '", path, "': ", ex.Message )
							).ConfigureAwait( false );
							continue;
						}
					}
				}

				try {
					var read = await this.myCurrent.ReadAsync( buffer, cancellationToken ).ConfigureAwait( false );
					if ( 0 < read ) {
						return read;
					}
				} catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) {
					await this.ReportAsync( string.Concat( "read error: ", ex.Message ) ).ConfigureAwait( false );
				}
				await this.CloseCurrentAsync().ConfigureAwait( false );
			}
		}

		public async ValueTask DisposeAsync() {
			await this.CloseCurrentAsync().ConfigureAwait( false );
		}

		private async ValueTask CloseCurrentAsync() {
			if ( this.myCurrentOwned && null != this.myCurrent ) {
				try {
					await this.myCurrent.DisposeAsync().ConfigureAwait( false );
				} catch ( IOException ex ) {
					await this.ReportAsync( string.Concat( "closing input: ", ex.Message ) ).ConfigureAwait( false );
				}
			}
			this.myCurrent = null;
			this.myCurrentOwned = false;
		}

		private async ValueTask ReportAsync(
			string message
		) {
			this.HadError = true;
			await this.myContext.Diagnostics.ErrorAsync(
				message,
				this.myContext.CancellationToken
			).ConfigureAwait( false );
		}
	}
}
