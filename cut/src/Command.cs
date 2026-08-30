namespace Icod.CoreUtils.Cut;

using System.Text;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.Ranges;
using Icod.CommandFramework.Text;

/// <summary>Implements GNU <c>cut</c> for .NET.</summary>
/// <remarks>
/// <para>Usage:</para>
/// <code>cut OPTION... [FILE]...</code>
/// <para>One byte, character, or field list is required. Untouched data is retained byte-for-byte.</para>
/// </remarks>
public static class Command {
	private const string BytesKey = "bytes";
	private const string CharactersKey = "characters";
	private const string FieldsKey = "fields";
	private const string FieldsWhitespaceKey = "fields-whitespace";
	private const string DelimiterKey = "delimiter";
	private const string NoPartialKey = "no-partial";
	private const string OutputDelimiterKey = "output-delimiter";
	private const string SuppressKey = "only-delimited";
	private const string WhitespaceKey = "whitespace-delimited";
	private const string ComplementKey = "complement";
	private const string ZeroKey = "zero-terminated";
	private const string HelpKey = "help";
	private const string VersionKey = "version";

	/// <summary>Runs <c>cut</c> synchronously with optional injected text streams.</summary>
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

	/// <summary>Runs <c>cut</c> asynchronously with optional injected text streams.</summary>
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
			new CommandContext( "cut", standardInput, standardOutput, standardError, inputAdapter, null, null, cancellationToken )
		).ConfigureAwait( false );
	}

	/// <summary>Runs <c>cut</c> asynchronously against a command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			var parsed = CreateParser().Parse( RewriteShortWhitespaceClusters( args ) );
			if ( !parsed.IsSuccess ) {
				await WriteOptionErrorAsync( parsed.Errors[0], context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var terminal = parsed.Options.FirstOrDefault( option => option.Definition.Key is HelpKey or VersionKey );
			if ( terminal?.Definition.Key == HelpKey ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( terminal?.Definition.Key == VersionKey ) {
				await WriteVersionAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			var options = await CreateOptionsAsync( parsed, context ).ConfigureAwait( false );
			if ( null == options ) {
				return CommandExitCodes.Failure;
			}
			if ( RequiresStandardInput( options.Operands ) && null == context.StandardInputStream ) {
				await context.Diagnostics.ErrorAsync( "a binary standard-input stream was not supplied", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			await using var output = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
			var success = await new CutProcessor( options ).ProcessAsync( context, output ).ConfigureAwait( false );
			await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return success ? CommandExitCodes.Success : CommandExitCodes.Failure;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( OverflowException ) {
			try { await context.Diagnostics.ErrorAsync( "input position overflow", CancellationToken.None ).ConfigureAwait( false ); } catch { }
			return CommandExitCodes.Failure;
		} catch ( IOException exception ) {
			try { await context.Diagnostics.ErrorAsync( string.Concat( "write failed: ", exception.Message ), CancellationToken.None ).ConfigureAwait( false ); } catch { }
			return CommandExitCodes.Failure;
		}
	}

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( BytesKey, 'b', new[] { "bytes" }, OptionValueArity.Required ),
			new OptionDefinition( CharactersKey, 'c', new[] { "characters" }, OptionValueArity.Required ),
			new OptionDefinition( FieldsKey, 'f', new[] { "fields" }, OptionValueArity.Required ),
			new OptionDefinition( FieldsWhitespaceKey, 'F', Array.Empty<string>(), OptionValueArity.Required ),
			new OptionDefinition( DelimiterKey, 'd', new[] { "delimiter" }, OptionValueArity.Required ),
			new OptionDefinition( NoPartialKey, 'n', new[] { "no-partial" } ),
			new OptionDefinition( OutputDelimiterKey, 'O', new[] { "output-delimiter" }, OptionValueArity.Required ),
			new OptionDefinition( SuppressKey, 's', new[] { "only-delimited" } ),
			new OptionDefinition( WhitespaceKey, 'w', new[] { "whitespace-delimited" }, OptionValueArity.Optional ),
			new OptionDefinition( ComplementKey, null, new[] { "complement" } ),
			new OptionDefinition( ZeroKey, 'z', new[] { "zero-terminated" } ),
			new OptionDefinition( HelpKey, null, new[] { "help" } ),
			new OptionDefinition( VersionKey, null, new[] { "version" } )
		},
		new OptionParserSettings { AllowLongOptionAbbreviations = true, Ordering = OptionOrdering.Permute }
	);

	private static IReadOnlyList<string> RewriteShortWhitespaceClusters( IReadOnlyList<string> args ) {
		var rewritten = new List<string>( args.Count );
		var optionsEnabled = true;
		foreach ( var argument in args ) {
			if ( optionsEnabled && "--" == argument ) {
				optionsEnabled = false;
				rewritten.Add( argument );
				continue;
			}
			if ( optionsEnabled ) {
				RewriteShortWhitespaceCluster( argument, rewritten );
			} else {
				rewritten.Add( argument );
			}
		}
		return rewritten;
	}

	private static void RewriteShortWhitespaceCluster( string argument, ICollection<string> destination ) {
		if ( 2 >= argument.Length || '-' != argument[0] || '-' == argument[1] ) {
			destination.Add( argument );
			return;
		}
		var cluster = argument.AsSpan( 1 );
		for ( var index = 0; index < cluster.Length; index++ ) {
			var option = cluster[index];
			if ( option is 'b' or 'c' or 'd' or 'f' or 'F' or 'O' ) {
				destination.Add( argument );
				return;
			}
			if ( option is not ('n' or 's' or 'z' or 'w') ) {
				destination.Add( argument );
				return;
			}
			if ( 'w' == option && index + 1 < cluster.Length ) {
				destination.Add( string.Concat( "-", cluster[..(index + 1)].ToString() ) );
				RewriteShortWhitespaceCluster( string.Concat( "-", cluster[(index + 1)..].ToString() ), destination );
				return;
			}
		}
		destination.Add( argument );
	}

	private static async Task<CutOptions?> CreateOptionsAsync( OptionParseResult parsed, CommandContext context ) {
		var listOptions = parsed.Options.Where( option => option.Definition.Key is BytesKey or CharactersKey or FieldsKey or FieldsWhitespaceKey ).ToArray();
		if ( 0 == listOptions.Length ) {
			await WriteSemanticErrorAsync( context, "you must specify a list of bytes, characters, or fields" ).ConfigureAwait( false );
			return null;
		}
		if ( 1 != listOptions.Length ) {
			await WriteSemanticErrorAsync( context, "only one list may be specified" ).ConfigureAwait( false );
			return null;
		}
		var listOption = listOptions[0];
		var mode = listOption.Definition.Key switch {
			BytesKey => CutMode.Bytes,
			CharactersKey => CutMode.Characters,
			_ => CutMode.Fields
		};
		var ranges = RangeListParser.Parse(
			listOption.Value ?? string.Empty,
			new RangeListParserOptions { Complement = parsed.HasOption( ComplementKey ) }
		);
		if ( !ranges.IsSuccess ) {
			await WriteSemanticErrorAsync(
				context,
				string.Concat( "invalid byte, character or field list: '", listOption.Value ?? string.Empty, "'" )
			).ConfigureAwait( false );
			return null;
		}

		var delimiterOption = parsed.Options.LastOrDefault( option => option.Definition.Key == DelimiterKey );
		var outputOption = parsed.Options.LastOrDefault( option => option.Definition.Key == OutputDelimiterKey );
		var whitespaceOption = parsed.Options.LastOrDefault( option => option.Definition.Key == WhitespaceKey );
		var whitespaceSpecified = null != whitespaceOption;
		var fieldWhitespace = listOption.Definition.Key == FieldsWhitespaceKey;
		if ( whitespaceSpecified && null != delimiterOption ) {
			await WriteSemanticErrorAsync( context, "the delimiter and whitespace-delimited options are mutually exclusive" ).ConfigureAwait( false );
			return null;
		}
		if ( CutMode.Fields != mode && (null != delimiterOption || whitespaceSpecified || parsed.HasOption( SuppressKey )) ) {
			await WriteSemanticErrorAsync( context, "an input delimiter may be specified only when operating on fields" ).ConfigureAwait( false );
			return null;
		}
		var trimWhitespace = false;
		if ( null != whitespaceOption?.Value ) {
			if ( !string.Equals( whitespaceOption.Value, "trimmed", StringComparison.Ordinal ) ) {
				await WriteSemanticErrorAsync( context, string.Concat( "invalid argument '", whitespaceOption.Value, "' for --whitespace-delimited" ) ).ConfigureAwait( false );
				return null;
			}
			trimWhitespace = true;
		}

		var locale = TextLocaleEnvironment.Resolve();
		var whitespaceDelimited = CutMode.Fields == mode && (whitespaceSpecified || (fieldWhitespace && null == delimiterOption));
		byte[]? fieldDelimiter = null;
		if ( CutMode.Fields == mode && !whitespaceDelimited ) {
			var raw = delimiterOption?.Value ?? "\t";
			if ( !TryParseDelimiter( raw, locale, out fieldDelimiter ) ) {
				await WriteSemanticErrorAsync( context, "the delimiter must be a single character" ).ConfigureAwait( false );
				return null;
			}
		}
		byte[]? outputDelimiter = null;
		if ( null != outputOption ) {
			outputDelimiter = ParseOutputDelimiter( outputOption.Value ?? string.Empty );
		} else if ( CutMode.Fields == mode ) {
			outputDelimiter = fieldWhitespace
				? new[] { (byte)' ' }
				: whitespaceDelimited
					? new[] { (byte)'\t' }
					: fieldDelimiter;
		}
		var expansion = await PathnameOperandExpander.ExpandAsync(
			parsed.Operands,
			cancellationToken: context.CancellationToken
		).ConfigureAwait( false );
		return new CutOptions(
			mode,
			ranges.Value!,
			expansion.Operands,
			locale,
			parsed.HasOption( ZeroKey ) ? (byte)0 : (byte)'\n',
			parsed.HasOption( ZeroKey ) ? new byte[] { 0 } : Encoding.UTF8.GetBytes( Environment.NewLine ),
			fieldDelimiter,
			outputDelimiter,
			parsed.HasOption( SuppressKey ),
			parsed.HasOption( NoPartialKey ),
			whitespaceDelimited,
			trimWhitespace
		);
	}

	private static bool TryParseDelimiter( string value, ITextLocaleProvider locale, out byte[] bytes ) {
		if ( 0 == value.Length ) {
			bytes = [ 0 ];
			return true;
		}
		bytes = Encoding.UTF8.GetBytes( value );
		using var stream = new MemoryStream( bytes, writable: false );
		var reader = new TextUnitReader( stream, locale.DecodingMode, InvalidEncodingPolicy.PreserveBytes );
		var first = reader.Read();
		if ( first is not TextUnit unit || null != reader.Read() ) {
			bytes = [];
			return false;
		}
		bytes = unit.ToByteArray();
		return true;
	}

	private static byte[] ParseOutputDelimiter( string value ) => 0 == value.Length ? [ 0 ] : Encoding.UTF8.GetBytes( value );

	private static bool RequiresStandardInput( IReadOnlyList<string> operands ) => 0 == operands.Count || operands.Any( value => "-" == value );

	private static async Task WriteOptionErrorAsync( OptionParseError error, CommandContext context ) {
		await context.StandardError.WriteLineAsync( OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
		await context.StandardError.WriteLineAsync( "Try 'cut --help' for more information.".AsMemory(), context.CancellationToken ).ConfigureAwait( false );
	}

	private static async Task WriteSemanticErrorAsync( CommandContext context, string message ) {
		await context.Diagnostics.ErrorAsync( message, context.CancellationToken ).ConfigureAwait( false );
		await context.StandardError.WriteLineAsync( "Try 'cut --help' for more information.".AsMemory(), context.CancellationToken ).ConfigureAwait( false );
	}

	private static Task WriteUsageAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "Usage: cut OPTION... [FILE]...".AsMemory(), cancellationToken );
	}

	private static async Task WriteHelpAsync( TextWriter writer, CancellationToken cancellationToken ) {
		await WriteUsageAsync( writer, cancellationToken ).ConfigureAwait( false );
		await writer.WriteAsync(
			string.Concat(
				"Print selected parts of lines from each FILE to standard output.", Environment.NewLine,
				"With no FILE, or when FILE is -, read standard input.", Environment.NewLine, Environment.NewLine,
				"  -b, --bytes=LIST              select only these bytes", Environment.NewLine,
				"  -c, --characters=LIST         select only these characters", Environment.NewLine,
				"  -f, --fields=LIST             select only these fields", Environment.NewLine,
				"  -F LIST                       fields separated by blanks; output uses space", Environment.NewLine,
				"  -d, --delimiter=DELIM         use DELIM instead of TAB for field delimiter", Environment.NewLine,
				"  -n, --no-partial              do not split multibyte characters in byte mode", Environment.NewLine,
				"  -O, --output-delimiter=STRING use STRING between selected ranges or fields", Environment.NewLine,
				"  -s, --only-delimited          suppress records containing no delimiter", Environment.NewLine,
				"  -w, --whitespace-delimited[=trimmed]", Environment.NewLine,
				"                                use runs of locale blanks as delimiters", Environment.NewLine,
				"      --complement              complement the set of selected positions", Environment.NewLine,
				"  -z, --zero-terminated         records end with NUL, not newline", Environment.NewLine,
				"      --help                    display this help and exit", Environment.NewLine,
				"      --version                 output version information and exit", Environment.NewLine,
				Environment.NewLine,
				"LIST uses N, N-, N-M, and -M forms separated by commas or blanks.", Environment.NewLine
			).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	private static Task WriteVersionAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "cut (Icod.CoreUtils) 1.0".AsMemory(), cancellationToken );
	}
}
