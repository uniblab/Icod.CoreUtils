// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Uniq;

using System.Buffers;
using System.Globalization;
using System.Text;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Ordering;
using Icod.CoreUtils.Shared.Records;
using Icod.CoreUtils.Shared.Temporary;

/// <summary>Implements GNU-compatible adjacent-record filtering and grouping.</summary>
public static class Command {
	private const string VersionText = "uniq (Icod.CoreUtils) 1.0";
	private static readonly UTF8Encoding Utf8 = new( false, false );
	private static readonly UTF8Encoding StrictUtf8 = new( false, true );

	/// <summary>Runs <c>uniq</c> synchronously with optional injected text streams.</summary>
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

	/// <summary>Runs <c>uniq</c> asynchronously with optional injected text streams.</summary>
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
				"uniq",
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

	/// <summary>Runs <c>uniq</c> asynchronously against a command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			var parsed = CreateParser().Parse( args );
			if ( !parsed.IsSuccess ) {
				await context.StandardError.WriteLineAsync(
					OptionDiagnosticFormatter.Format( context.ProgramName, parsed.Errors[0] ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( parsed.HasOption( "help" ) ) {
				await WriteHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync( VersionText.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( !TryCreateOptions( parsed, out var options, out var error ) ) {
				await context.Diagnostics.ErrorAsync( error!, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			return await ExecuteAsync( options!, context ).ConfigureAwait( false );
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or InvalidOperationException
			or ArgumentException
			or NotSupportedException
			or OverflowException
		) {
			try {
				await context.Diagnostics.ErrorAsync( exception.Message, CancellationToken.None ).ConfigureAwait( false );
			} catch {
				// A diagnostic failure must not replace the conventional command status.
			}
			return CommandExitCodes.Failure;
		}
	}

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( "count", 'c', new[] { "count" } ),
			new OptionDefinition( "repeated", 'd', new[] { "repeated" } ),
			new OptionDefinition( "all-repeated", 'D', new[] { "all-repeated" }, OptionValueArity.Optional ),
			new OptionDefinition( "skip-fields", 'f', new[] { "skip-fields" }, OptionValueArity.Required ),
			new OptionDefinition( "group", null, new[] { "group" }, OptionValueArity.Optional ),
			new OptionDefinition( "ignore-case", 'i', new[] { "ignore-case" } ),
			new OptionDefinition( "skip-chars", 's', new[] { "skip-chars" }, OptionValueArity.Required ),
			new OptionDefinition( "unique", 'u', new[] { "unique" } ),
			new OptionDefinition( "check-chars", 'w', new[] { "check-chars" }, OptionValueArity.Required ),
			new OptionDefinition( "zero-terminated", 'z', new[] { "zero-terminated" } ),
			new OptionDefinition( "help", null, new[] { "help" } ),
			new OptionDefinition( "version", null, new[] { "version" } )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	private static bool TryCreateOptions(
		OptionParseResult parsed,
		out UniqOptions? options,
		out string? error
	) {
		options = null;
		error = null;
		if ( 2 < parsed.Operands.Count ) {
			error = string.Concat( "extra operand '", parsed.Operands[2], "'" );
			return false;
		}
		if ( !TryGetRepeatedNonnegativeValue( parsed, "skip-fields", out var skipFields, out error )
			|| !TryGetRepeatedNonnegativeValue( parsed, "skip-chars", out var skipChars, out error )
			|| !TryGetOptionalNonnegativeValue( parsed, "check-chars", out var checkChars, out error ) ) {
			return false;
		}
		var count = false;
		var outputUnique = true;
		var outputFirstRepeated = true;
		var outputLaterRepeated = false;
		var repeatedMethod = DelimiterMethod.None;
		var group = false;
		var groupMethod = DelimiterMethod.Separate;
		var hasGroupConflict = false;
		foreach ( var occurrence in parsed.Options ) {
			switch ( occurrence.Definition.Key ) {
				case "count":
					count = true;
					hasGroupConflict = true;
					break;
				case "repeated":
					outputUnique = false;
					hasGroupConflict = true;
					break;
				case "all-repeated":
					outputUnique = false;
					outputLaterRepeated = true;
					hasGroupConflict = true;
					if ( !TryParseDelimiterMethod(
						occurrence.Value,
						allowAppendAndBoth: false,
						out repeatedMethod,
						out error
					) ) {
						return false;
					}
					break;
				case "unique":
					outputFirstRepeated = false;
					hasGroupConflict = true;
					break;
				case "group":
					group = true;
					if ( !TryParseDelimiterMethod(
						occurrence.Value ?? "separate",
						allowAppendAndBoth: true,
						out groupMethod,
						out error
					) ) {
						return false;
					}
					break;
			}
		}
		if ( group && hasGroupConflict ) {
			error = "--group is incompatible with --count, --repeated, --all-repeated, and --unique";
			return false;
		}
		if ( count && outputLaterRepeated ) {
			error = "printing all duplicated records and repeat counts is meaningless";
			return false;
		}
		options = new UniqOptions {
			InputPath = 0 == parsed.Operands.Count ? "-" : parsed.Operands[0],
			OutputPath = 2 > parsed.Operands.Count ? null : parsed.Operands[1],
			Count = count,
			OutputUnique = outputUnique,
			OutputFirstRepeated = outputFirstRepeated,
			OutputLaterRepeated = outputLaterRepeated,
			RepeatedMethod = repeatedMethod,
			Group = group,
			GroupMethod = groupMethod,
			IgnoreCase = parsed.HasOption( "ignore-case" ),
			SkipFields = skipFields,
			SkipChars = skipChars,
			CheckChars = checkChars,
			RecordSeparator = parsed.HasOption( "zero-terminated" ) ? RecordSeparator.Null : RecordSeparator.LineFeed
		};
		return true;
	}

	private static async Task<int> ExecuteAsync( UniqOptions options, CommandContext context ) {
		ResolveCharacterLocale(
			out var bytewiseCharacters,
			out var characterCulture
		);
		options.BytewiseCharacters = bytewiseCharacters;
		options.CharacterCulture = characterCulture;
		var outputIsStandard = string.IsNullOrEmpty( options.OutputPath ) || options.OutputPath == "-";
		var aliasesInput = !outputIsStandard
			&& options.InputPath != "-"
			&& string.Equals(
				Path.GetFullPath( options.InputPath ),
				Path.GetFullPath( options.OutputPath! ),
				OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
			);
		if ( aliasesInput ) {
			await using var workspace = TemporaryWorkspace.Create( cancellationToken: context.CancellationToken );
			var spoolPath = workspace.CreateFile( "uniq-XXXXXXXX.tmp", context.CancellationToken );
			await using ( var source = InputSource.OpenBinary( InputOperand.Create( options.InputPath ), context ) ) {
				await ProcessToFileAsync( options, context, source.BinaryStream!, spoolPath ).ConfigureAwait( false );
			}
			File.Copy( spoolPath, options.OutputPath!, overwrite: true );
			return CommandExitCodes.Success;
		}
		await using var input = InputSource.OpenBinary( InputOperand.Create( options.InputPath ), context );
		if ( outputIsStandard ) {
			await using var destination = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
			await ProcessAsync( options, context, input.BinaryStream!, destination ).ConfigureAwait( false );
			await destination.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		}
		await ProcessToFileAsync(
			options,
			context,
			input.BinaryStream!,
			options.OutputPath!
		).ConfigureAwait( false );
		return CommandExitCodes.Success;
	}

	private static async Task ProcessToFileAsync(
		UniqOptions options,
		CommandContext context,
		Stream source,
		string outputPath
	) {
		await using var destination = new FileStream(
			outputPath,
			FileMode.Create,
			FileAccess.Write,
			FileShare.Read,
			StreamOperations.DefaultBufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
		await ProcessAsync( options, context, source, destination ).ConfigureAwait( false );
		await destination.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
	}

	private static async Task ProcessAsync(
		UniqOptions options,
		CommandContext context,
		Stream source,
		Stream destination
	) {
		using var reader = new ByteRecordReader( source, options.RecordSeparator );
		var writer = new DelimitedByteRecordWriter( destination, options.RecordSeparator );
		if ( options.Group ) {
			await ProcessGroupsAsync( reader, writer, options, context ).ConfigureAwait( false );
		} else if ( options.OutputLaterRepeated ) {
			await ProcessRepeatedRecordsAsync( reader, writer, options, context ).ConfigureAwait( false );
		} else {
			await ProcessSummariesAsync( reader, writer, options, context ).ConfigureAwait( false );
		}
		await writer.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
	}

	private static async Task ProcessSummariesAsync(
		ByteRecordReader reader,
		DelimitedByteRecordWriter writer,
		UniqOptions options,
		CommandContext context
	) {
		var first = await reader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
		if ( null == first ) {
			return;
		}
		var representative = first;
		ulong count = 1;
		while ( true ) {
			var next = await reader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
			if ( null != next && AreEqual( representative, next, options ) ) {
				count++;
				continue;
			}
			if ( ( 1 == count && options.OutputUnique ) || ( 1 < count && options.OutputFirstRepeated ) ) {
				await WriteSelectedAsync( writer, representative, count, options.Count, context.CancellationToken ).ConfigureAwait( false );
			}
			if ( null == next ) {
				break;
			}
			representative = next;
			count = 1;
		}
	}

	private static async Task ProcessRepeatedRecordsAsync(
		ByteRecordReader reader,
		DelimitedByteRecordWriter writer,
		UniqOptions options,
		CommandContext context
	) {
		var current = await reader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
		if ( null == current ) {
			return;
		}
		var duplicateGroup = false;
		var wroteDuplicateGroup = false;
		while ( true ) {
			var next = await reader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
			if ( null != next && AreEqual( current, next, options ) ) {
				if ( !duplicateGroup ) {
					if (
						options.RepeatedMethod == DelimiterMethod.Prepend
						|| ( options.RepeatedMethod == DelimiterMethod.Separate && wroteDuplicateGroup )
					) {
						await writer.WriteSeparatorAsync( context.CancellationToken ).ConfigureAwait( false );
					}
					duplicateGroup = true;
				}
				await writer.WriteRecordAsync( current.Content, terminate: true, context.CancellationToken ).ConfigureAwait( false );
				current = next;
				continue;
			}
			if ( duplicateGroup ) {
				if ( options.OutputFirstRepeated ) {
					await writer.WriteRecordAsync( current.Content, terminate: true, context.CancellationToken ).ConfigureAwait( false );
				}
				wroteDuplicateGroup = true;
			}
			if ( null == next ) {
				break;
			}
			current = next;
			duplicateGroup = false;
		}
	}

	private static async Task ProcessGroupsAsync(
		ByteRecordReader reader,
		DelimitedByteRecordWriter writer,
		UniqOptions options,
		CommandContext context
	) {
		var current = await reader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
		if ( null == current ) {
			return;
		}
		await WriteGroupStartAsync( writer, options.GroupMethod, firstGroup: true, context.CancellationToken ).ConfigureAwait( false );
		await writer.WriteRecordAsync( current.Content, terminate: true, context.CancellationToken ).ConfigureAwait( false );
		while ( true ) {
			var next = await reader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
			if ( null == next ) {
				await WriteGroupEndAsync( writer, options.GroupMethod, context.CancellationToken ).ConfigureAwait( false );
				break;
			}
			if ( !AreEqual( current, next, options ) ) {
				await WriteGroupBoundaryAsync( writer, options.GroupMethod, context.CancellationToken ).ConfigureAwait( false );
			}
			await writer.WriteRecordAsync( next.Content, terminate: true, context.CancellationToken ).ConfigureAwait( false );
			current = next;
		}
	}

	private static async ValueTask WriteSelectedAsync(
		DelimitedByteRecordWriter writer,
		ByteRecord record,
		ulong count,
		bool includeCount,
		CancellationToken cancellationToken
	) {
		if ( includeCount ) {
			var prefix = string.Concat( count.ToString( CultureInfo.InvariantCulture ).PadLeft( 7 ), " " );
			await writer.WriteContentAsync( Utf8.GetBytes( prefix ), cancellationToken ).ConfigureAwait( false );
		}
		await writer.WriteRecordAsync( record.Content, terminate: true, cancellationToken ).ConfigureAwait( false );
	}

	private static bool AreEqual(
		ByteRecord first,
		ByteRecord second,
		UniqOptions options
	) {
		var left = ComparisonSlice( first.Content, options ).Span;
		var right = ComparisonSlice( second.Content, options ).Span;
		if ( left.Length != right.Length ) {
			return false;
		}
		if ( !options.IgnoreCase ) {
			return left.SequenceEqual( right );
		}
		if ( options.BytewiseCharacters ) {
			return EqualsAsciiFolded( left, right );
		}
		try {
			var leftText = StrictUtf8.GetString( left );
			var rightText = StrictUtf8.GetString( right );
			return 0 == ( options.CharacterCulture ?? CultureInfo.CurrentCulture ).CompareInfo.Compare(
				leftText,
				rightText,
				CompareOptions.IgnoreCase
			);
		} catch ( DecoderFallbackException ) {
			return EqualsAsciiFolded( left, right );
		}
	}

	private static bool EqualsAsciiFolded(
		ReadOnlySpan<byte> left,
		ReadOnlySpan<byte> right
	) {
		for ( var index = 0; index < left.Length; index++ ) {
			if ( FoldAscii( left[index] ) != FoldAscii( right[index] ) ) {
				return false;
			}
		}
		return true;
	}

	private static byte FoldAscii( byte value ) {
		return value is >= (byte)'A' and <= (byte)'Z'
			? (byte)( value + ( (byte)'a' - (byte)'A' ) )
			: value;
	}

	private static ReadOnlyMemory<byte> ComparisonSlice(
		ReadOnlyMemory<byte> content,
		UniqOptions options
	) {
		var bytes = content.Span;
		var index = 0;
		var bytewiseCharacters = options.BytewiseCharacters;
		for ( long field = 0; field < options.SkipFields && index < bytes.Length; field++ ) {
			while ( index < bytes.Length && IsBlankAt( bytes, index, bytewiseCharacters, out var consumed ) ) {
				index += consumed;
			}
			while ( index < bytes.Length && !IsBlankAt( bytes, index, bytewiseCharacters, out var consumed ) ) {
				index += consumed;
			}
		}
		index = AdvanceCharacters( bytes, index, options.SkipChars, bytewiseCharacters );
		var end = options.CheckChars.HasValue
			? AdvanceCharacters( bytes, index, options.CheckChars.Value, bytewiseCharacters )
			: bytes.Length;
		return content.Slice( index, end - index );
	}

	private static int AdvanceCharacters(
		ReadOnlySpan<byte> bytes,
		int index,
		long count,
		bool bytewise
	) {
		if ( bytewise ) {
			return (int)Math.Min( bytes.Length, (long)index + count );
		}
		while ( 0 < count && index < bytes.Length ) {
			_ = Rune.DecodeFromUtf8( bytes[index..], out _, out var consumed );
			index += 0 < consumed ? consumed : 1;
			count--;
		}
		return index;
	}

	private static bool IsBlankAt(
		ReadOnlySpan<byte> bytes,
		int index,
		bool bytewise,
		out int consumed
	) {
		if ( bytewise ) {
			consumed = 1;
			return bytes[index] is (byte)' ' or (byte)'\t';
		}
		var status = Rune.DecodeFromUtf8( bytes[index..], out var rune, out consumed );
		if ( status != OperationStatus.Done ) {
			consumed = 1;
			return bytes[index] is (byte)' ' or (byte)'\t';
		}
		return Rune.IsWhiteSpace( rune );
	}

	private static async ValueTask WriteGroupStartAsync(
		DelimitedByteRecordWriter writer,
		DelimiterMethod method,
		bool firstGroup,
		CancellationToken cancellationToken
	) {
		if ( method is DelimiterMethod.Prepend or DelimiterMethod.Both ) {
			await writer.WriteSeparatorAsync( cancellationToken ).ConfigureAwait( false );
		} else if ( !firstGroup && method == DelimiterMethod.Separate ) {
			await writer.WriteSeparatorAsync( cancellationToken ).ConfigureAwait( false );
		}
	}

	private static async ValueTask WriteGroupBoundaryAsync(
		DelimitedByteRecordWriter writer,
		DelimiterMethod method,
		CancellationToken cancellationToken
	) {
		if ( method != DelimiterMethod.None ) {
			await writer.WriteSeparatorAsync( cancellationToken ).ConfigureAwait( false );
		}
	}

	private static async ValueTask WriteGroupEndAsync(
		DelimitedByteRecordWriter writer,
		DelimiterMethod method,
		CancellationToken cancellationToken
	) {
		if ( method is DelimiterMethod.Append or DelimiterMethod.Both ) {
			await writer.WriteSeparatorAsync( cancellationToken ).ConfigureAwait( false );
		}
	}

	private static void ResolveCharacterLocale(
		out bool bytewiseCharacters,
		out CultureInfo? characterCulture
	) {
		var selected = FirstNonempty(
			Environment.GetEnvironmentVariable( "LC_ALL" ),
			Environment.GetEnvironmentVariable( "LC_CTYPE" ),
			Environment.GetEnvironmentVariable( "LANG" )
		);
		if ( null == selected ) {
			bytewiseCharacters = false;
			characterCulture = CultureInfo.CurrentCulture;
			return;
		}
		if ( string.Equals( selected, "C", StringComparison.OrdinalIgnoreCase )
			|| string.Equals( selected, "POSIX", StringComparison.OrdinalIgnoreCase ) ) {
			bytewiseCharacters = true;
			characterCulture = null;
			return;
		}
		if ( selected.StartsWith( "C.", StringComparison.OrdinalIgnoreCase )
			|| selected.StartsWith( "C@", StringComparison.OrdinalIgnoreCase ) ) {
			bytewiseCharacters = false;
			characterCulture = CultureInfo.InvariantCulture;
			return;
		}
		var normalized = NormalizeCultureName( selected );
		try {
			bytewiseCharacters = false;
			characterCulture = CultureInfo.GetCultureInfo( normalized );
		} catch ( CultureNotFoundException exception ) {
			throw new NotSupportedException(
				string.Concat( "unsupported character locale: ", selected ),
				exception
			);
		}
	}

	private static string? FirstNonempty( params string?[] values ) {
		foreach ( var value in values ) {
			if ( !string.IsNullOrWhiteSpace( value ) ) {
				return value.Trim();
			}
		}
		return null;
	}

	private static string NormalizeCultureName( string value ) {
		var end = value.Length;
		var encoding = value.IndexOf( '.' );
		if ( 0 <= encoding ) {
			end = encoding;
		}
		var modifier = value.IndexOf( '@' );
		if ( 0 <= modifier && modifier < end ) {
			end = modifier;
		}
		return value[..end].Replace( '_', '-' );
	}

	private static bool TryGetRepeatedNonnegativeValue(
		OptionParseResult parsed,
		string key,
		out long value,
		out string? error
	) {
		value = 0;
		error = null;
		var values = parsed.GetOccurrences( key ).Select( occurrence => occurrence.Value ?? string.Empty ).ToArray();
		foreach ( var text in values ) {
			if ( !long.TryParse( text, NumberStyles.None, CultureInfo.InvariantCulture, out var current ) || 0 > current ) {
				error = string.Concat( "invalid number of fields or characters: '", text, "'" );
				return false;
			}
			value = current;
		}
		return true;
	}

	private static bool TryGetOptionalNonnegativeValue(
		OptionParseResult parsed,
		string key,
		out long? value,
		out string? error
	) {
		value = null;
		error = null;
		var values = parsed.GetOccurrences( key ).Select( occurrence => occurrence.Value ?? string.Empty ).ToArray();
		foreach ( var text in values ) {
			if ( !long.TryParse( text, NumberStyles.None, CultureInfo.InvariantCulture, out var current ) || 0 > current ) {
				error = string.Concat( "invalid number of fields or characters: '", text, "'" );
				return false;
			}
			value = current;
		}
		return true;
	}

	private static bool TryParseDelimiterMethod(
		string? value,
		bool allowAppendAndBoth,
		out DelimiterMethod method,
		out string? error
	) {
		error = null;
		if ( null == value || ( !allowAppendAndBoth && value == "none" ) ) {
			method = DelimiterMethod.None;
			return true;
		}
		if ( value == "prepend" ) {
			method = DelimiterMethod.Prepend;
			return true;
		}
		if ( value == "separate" ) {
			method = DelimiterMethod.Separate;
			return true;
		}
		if ( allowAppendAndBoth && value == "append" ) {
			method = DelimiterMethod.Append;
			return true;
		}
		if ( allowAppendAndBoth && value == "both" ) {
			method = DelimiterMethod.Both;
			return true;
		}
		method = DelimiterMethod.None;
		error = string.Concat( "invalid delimiter method: '", value, "'" );
		return false;
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string help = """
Usage: uniq [OPTION]... [INPUT [OUTPUT]]
Filter adjacent matching records from INPUT, writing to OUTPUT.

  -c, --count             prefix records by the number of occurrences
  -d, --repeated          only print duplicate groups
  -D, --all-repeated[=METHOD]  print every record in duplicate groups
  -f, --skip-fields=N     avoid comparing the first N fields
      --group[=METHOD]    show all records, separating groups
  -i, --ignore-case       ignore differences in case
  -s, --skip-chars=N      avoid comparing the first N characters
  -u, --unique            only print unique groups
  -w, --check-chars=N     compare no more than N characters
  -z, --zero-terminated   end records with NUL instead of newline
      --help              display this help and exit
      --version           output version information and exit
""";
		await context.StandardOutput.WriteAsync( help.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
	}

	private sealed class UniqOptions {
		private string InputPath { get; init; } = string.Empty;
		private string? OutputPath { get; init; }
		private bool Count { get; init; }
		private bool OutputUnique { get; init; }
		private bool OutputFirstRepeated { get; init; }
		private bool OutputLaterRepeated { get; init; }
		private DelimiterMethod RepeatedMethod { get; init; }
		private bool Group { get; init; }
		private DelimiterMethod GroupMethod { get; init; }
		private bool IgnoreCase { get; init; }
		private bool BytewiseCharacters { get; set; }
		private CultureInfo? CharacterCulture { get; set; }
		private long SkipFields { get; init; }
		private long SkipChars { get; init; }
		private long? CheckChars { get; init; }
		private RecordSeparator RecordSeparator { get; init; }
	}

	private enum DelimiterMethod {
		None,
		Prepend,
		Append,
		Separate,
		Both
	}

}
