// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Join;

using System.Globalization;
using System.Text;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Ordering;
using Icod.CoreUtils.Shared.Records;

/// <summary>Implements GNU-compatible relational joining of two sorted record streams.</summary>
public static class Command {
	private const string VersionText = "join (Icod.CoreUtils) 1.0";
	private static readonly UTF8Encoding Utf8 = new( false, false );

	/// <summary>Runs <c>join</c> synchronously with optional injected text streams.</summary>
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

	/// <summary>Runs <c>join</c> asynchronously with optional injected text streams.</summary>
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
				"join",
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

	/// <summary>Runs <c>join</c> asynchronously against a command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			var parsed = CreateParser().Parse( NormalizeArguments( args ) );
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

	private static string[] NormalizeArguments( IReadOnlyList<string> args ) {
		var normalized = new List<string>( args.Count );
		for ( var index = 0; index < args.Count; index++ ) {
			var argument = args[index];
			normalized.Add( argument );
			string? firstFormat = null;
			if ( argument is "-o" or "--output" ) {
				if ( index + 1 < args.Count ) {
					firstFormat = args[++index];
					normalized.Add( firstFormat );
				}
			} else if ( argument.StartsWith( "--output=", StringComparison.Ordinal ) ) {
				firstFormat = argument["--output=".Length..];
			} else if ( argument.StartsWith( "-o", StringComparison.Ordinal ) && 2 < argument.Length ) {
				firstFormat = argument[2..];
			}
			if ( null == firstFormat || firstFormat == "auto" ) {
				continue;
			}
			while ( index + 1 < args.Count && LooksLikeOutputFormat( args[index + 1] ) ) {
				normalized.Add( string.Concat( "--output=", args[++index] ) );
			}
		}
		return normalized.ToArray();
	}

	private static bool LooksLikeOutputFormat( string value ) {
		if ( string.IsNullOrWhiteSpace( value ) ) {
			return false;
		}
		var tokens = value.Split( new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries );
		return 0 < tokens.Length && tokens.All( token => {
			if ( token == "0" ) {
				return true;
			}
			return 2 < token.Length
				&& token[0] is '1' or '2'
				&& token[1] == '.'
				&& int.TryParse( token.AsSpan( 2 ), NumberStyles.None, CultureInfo.InvariantCulture, out var field )
				&& 0 < field;
		} );
	}

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( "also-unpaired", 'a', valueArity: OptionValueArity.Required ),
			new OptionDefinition( "empty", 'e', valueArity: OptionValueArity.Required ),
			new OptionDefinition( "ignore-case", 'i', new[] { "ignore-case" } ),
			new OptionDefinition( "join-field", 'j', valueArity: OptionValueArity.Required ),
			new OptionDefinition( "output", 'o', new[] { "output" }, OptionValueArity.Required ),
			new OptionDefinition( "field-separator", 't', valueArity: OptionValueArity.Required ),
			new OptionDefinition( "unpaired-only", 'v', valueArity: OptionValueArity.Required ),
			new OptionDefinition( "file-one-field", '1', valueArity: OptionValueArity.Required ),
			new OptionDefinition( "file-two-field", '2', valueArity: OptionValueArity.Required ),
			new OptionDefinition( "check-order", null, new[] { "check-order" } ),
			new OptionDefinition( "nocheck-order", null, new[] { "nocheck-order" } ),
			new OptionDefinition( "header", null, new[] { "header" } ),
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
		out JoinOptions? options,
		out string? error
	) {
		options = null;
		error = null;
		if ( 2 != parsed.Operands.Count ) {
			error = 2 > parsed.Operands.Count ? "missing operand" : string.Concat( "extra operand '", parsed.Operands[2], "'" );
			return false;
		}
		if ( parsed.Operands[0] == "-" && parsed.Operands[1] == "-" ) {
			error = "both files cannot be standard input";
			return false;
		}
		if ( parsed.HasOption( "check-order" ) && parsed.HasOption( "nocheck-order" ) ) {
			error = "options --check-order and --nocheck-order are mutually exclusive";
			return false;
		}
		int? firstJoinField = null;
		int? secondJoinField = null;
		var includeFirstUnpaired = false;
		var includeSecondUnpaired = false;
		var outputPairable = true;
		var outputFields = new List<OutputField>();
		var outputAuto = false;
		foreach ( var occurrence in parsed.Options ) {
			switch ( occurrence.Definition.Key ) {
				case "join-field":
					if ( !TryParseFieldNumber( occurrence.Value, out var commonField ) ) {
						error = string.Concat( "invalid field number: '", occurrence.Value, "'" );
						return false;
					}
					if ( !TrySetJoinField( ref firstJoinField, commonField, out error )
						|| !TrySetJoinField( ref secondJoinField, commonField, out error ) ) {
						return false;
					}
					break;
				case "file-one-field":
					if ( !TryParseFieldNumber( occurrence.Value, out var firstField ) ) {
						error = string.Concat( "invalid field number: '", occurrence.Value, "'" );
						return false;
					}
					if ( !TrySetJoinField( ref firstJoinField, firstField, out error ) ) {
						return false;
					}
					break;
				case "file-two-field":
					if ( !TryParseFieldNumber( occurrence.Value, out var secondField ) ) {
						error = string.Concat( "invalid field number: '", occurrence.Value, "'" );
						return false;
					}
					if ( !TrySetJoinField( ref secondJoinField, secondField, out error ) ) {
						return false;
					}
					break;
				case "also-unpaired":
				case "unpaired-only":
					if ( !TryParseFileNumber( occurrence.Value, out var fileNumber ) ) {
						error = string.Concat( "invalid file number: '", occurrence.Value, "'" );
						return false;
					}
					if ( 1 == fileNumber ) {
						includeFirstUnpaired = true;
					} else {
						includeSecondUnpaired = true;
					}
					if ( occurrence.Definition.Key == "unpaired-only" ) {
						outputPairable = false;
					}
					break;
				case "output":
					if ( string.Equals( occurrence.Value, "auto", StringComparison.Ordinal ) ) {
						if ( 0 < outputFields.Count ) {
							error = "cannot combine -o auto with an explicit output format";
							return false;
						}
						outputAuto = true;
					} else {
						if ( outputAuto ) {
							error = "cannot combine -o auto with an explicit output format";
							return false;
						}
						if ( !TryParseOutputFields( occurrence.Value ?? string.Empty, outputFields, out error ) ) {
							return false;
						}
					}
					break;
			}
		}
		var emptyValues = parsed.GetOccurrences( "empty" ).Select( value => value.Value ?? string.Empty ).ToArray();
		if ( 1 < emptyValues.Length && emptyValues.Skip( 1 ).Any( value => value != emptyValues[0] ) ) {
			error = "multiple empty-field replacements specified";
			return false;
		}
		var separatorValues = parsed.GetOccurrences( "field-separator" ).Select( value => value.Value ?? string.Empty ).ToArray();
		if ( 1 < separatorValues.Length && separatorValues.Skip( 1 ).Any( value => value != separatorValues[0] ) ) {
			error = "multiple field separators specified";
			return false;
		}
		byte[]? fieldSeparator = null;
		ReadOnlyMemory<byte> outputSeparator = new byte[] { (byte)' ' };
		if ( 0 < separatorValues.Length
			&& !TryParseFieldSeparator( separatorValues[^1], out fieldSeparator, out outputSeparator, out error ) ) {
			return false;
		}
		options = new JoinOptions {
			FirstPath = parsed.Operands[0],
			SecondPath = parsed.Operands[1],
			FirstJoinField = firstJoinField ?? 1,
			SecondJoinField = secondJoinField ?? 1,
			IncludeFirstUnpaired = includeFirstUnpaired,
			IncludeSecondUnpaired = includeSecondUnpaired,
			OutputPairable = outputPairable,
			IgnoreCase = parsed.HasOption( "ignore-case" ),
			CheckMode = parsed.HasOption( "nocheck-order" ) ? OrderCheckMode.Never : parsed.HasOption( "check-order" ) ? OrderCheckMode.Always : OrderCheckMode.Default,
			Header = parsed.HasOption( "header" ),
			RecordSeparator = parsed.HasOption( "zero-terminated" ) ? RecordSeparator.Null : RecordSeparator.LineFeed,
			FieldSeparator = fieldSeparator,
			OutputSeparator = outputSeparator,
			MissingReplacement = 0 == emptyValues.Length ? ReadOnlyMemory<byte>.Empty : Utf8.GetBytes( emptyValues[^1] ),
			OutputFields = outputFields,
			OutputAuto = outputAuto
		};
		return true;
	}

	private static async Task<int> ExecuteAsync( JoinOptions options, CommandContext context ) {
		var resolution = CollationEnvironment.ResolveCurrent();
		if ( !resolution.IsSuccess ) {
			throw new NotSupportedException( resolution.ErrorMessage );
		}
		var comparer = new JoinKeyComparer(
			new ByteCollationComparer(
				new SystemCollationProvider( resolution.Profile! )
			),
			options.IgnoreCase
		);
		await using var firstSource = InputSource.OpenBinary( InputOperand.Create( options.FirstPath ), context );
		await using var secondSource = InputSource.OpenBinary( InputOperand.Create( options.SecondPath ), context );
		using var firstReader = new ByteRecordReader( firstSource.BinaryStream!, options.RecordSeparator );
		using var secondReader = new ByteRecordReader( secondSource.BinaryStream!, options.RecordSeparator );
		var first = new JoinCursor( firstReader, firstSource.DisplayName, options.FirstJoinField, options.FieldSeparator, comparer, options.CheckMode );
		var second = new JoinCursor( secondReader, secondSource.DisplayName, options.SecondJoinField, options.FieldSeparator, comparer, options.CheckMode );
		await first.AdvanceAsync( context, checkOrder: !options.Header ).ConfigureAwait( false );
		await second.AdvanceAsync( context, checkOrder: !options.Header ).ConfigureAwait( false );
		if ( options.OutputAuto ) {
			options.OutputFields = CreateAutomaticFormat( first.Current, second.Current, options );
		}
		await using var output = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
		var writer = new JoinOutputWriter( output, options );
		var sawUnpaired = false;
		if ( options.Header && ( null != first.Current || null != second.Current ) ) {
			await writer.WriteAsync( first.Current, second.Current, context.CancellationToken ).ConfigureAwait( false );
			await first.AdvanceAsync( context, checkOrder: true ).ConfigureAwait( false );
			await second.AdvanceAsync( context, checkOrder: true ).ConfigureAwait( false );
		}
		while ( null != first.Current && null != second.Current ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			var firstRecord = first.Current!;
			var secondRecord = second.Current!;
			var comparison = comparer.Compare( firstRecord.Key, secondRecord.Key );
			if ( comparison < 0 ) {
				sawUnpaired = true;
				if ( options.IncludeFirstUnpaired ) {
					await writer.WriteAsync( firstRecord, null, context.CancellationToken ).ConfigureAwait( false );
				}
				await first.AdvanceAsync( context, checkOrder: true ).ConfigureAwait( false );
			} else if ( 0 < comparison ) {
				sawUnpaired = true;
				if ( options.IncludeSecondUnpaired ) {
					await writer.WriteAsync( null, secondRecord, context.CancellationToken ).ConfigureAwait( false );
				}
				await second.AdvanceAsync( context, checkOrder: true ).ConfigureAwait( false );
			} else {
				var key = firstRecord.Key.ToArray();
				var firstGroup = await ReadGroupAsync( first, key, comparer, context ).ConfigureAwait( false );
				var secondGroup = await ReadGroupAsync( second, key, comparer, context ).ConfigureAwait( false );
				if ( options.OutputPairable ) {
					foreach ( var firstRecord in firstGroup ) {
						foreach ( var secondRecord in secondGroup ) {
							await writer.WriteAsync( firstRecord, secondRecord, context.CancellationToken ).ConfigureAwait( false );
						}
					}
				}
			}
		}
		while ( first.Current is JoinRecord firstRemaining ) {
			sawUnpaired = true;
			if ( options.IncludeFirstUnpaired ) {
				await writer.WriteAsync( firstRemaining, null, context.CancellationToken ).ConfigureAwait( false );
			}
			await first.AdvanceAsync( context, checkOrder: true ).ConfigureAwait( false );
		}
		while ( second.Current is JoinRecord secondRemaining ) {
			sawUnpaired = true;
			if ( options.IncludeSecondUnpaired ) {
				await writer.WriteAsync( null, secondRemaining, context.CancellationToken ).ConfigureAwait( false );
			}
			await second.AdvanceAsync( context, checkOrder: true ).ConfigureAwait( false );
		}
		await writer.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
		await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
		if (
			options.CheckMode == OrderCheckMode.Default
			&& sawUnpaired
			&& ( first.IsDisordered || second.IsDisordered )
		) {
			throw new InvalidDataException( "input is not in sorted order" );
		}
		return CommandExitCodes.Success;
	}

	private static async Task<List<JoinRecord>> ReadGroupAsync(
		JoinCursor cursor,
		ReadOnlyMemory<byte> key,
		JoinKeyComparer comparer,
		CommandContext context
	) {
		var records = new List<JoinRecord>();
		while ( cursor.Current is JoinRecord current && 0 == comparer.Compare( current.Key, key ) ) {
			records.Add( current );
			await cursor.AdvanceAsync( context, checkOrder: true ).ConfigureAwait( false );
		}
		return records;
	}

	private static List<OutputField> CreateAutomaticFormat(
		JoinRecord? first,
		JoinRecord? second,
		JoinOptions options
	) {
		var result = new List<OutputField> { OutputField.JoinKey };
		for ( var field = 1; field <= ( first?.Fields.Count ?? 0 ); field++ ) {
			if ( field != options.FirstJoinField ) {
				result.Add( new OutputField( 1, field ) );
			}
		}
		for ( var field = 1; field <= ( second?.Fields.Count ?? 0 ); field++ ) {
			if ( field != options.SecondJoinField ) {
				result.Add( new OutputField( 2, field ) );
			}
		}
		return result;
	}

	private static bool TrySetJoinField(
		ref int? destination,
		int value,
		out string? error
	) {
		if ( destination.HasValue && destination.Value != value ) {
			error = string.Concat(
				"incompatible join fields ",
				destination.Value.ToString( CultureInfo.InvariantCulture ),
				", ",
				value.ToString( CultureInfo.InvariantCulture )
			);
			return false;
		}
		destination = value;
		error = null;
		return true;
	}

	private static bool TryParseFieldSeparator(
		string value,
		out byte[]? inputSeparator,
		out ReadOnlyMemory<byte> outputSeparator,
		out string? error
	) {
		error = null;
		if ( 0 == value.Length ) {
			inputSeparator = Array.Empty<byte>();
			outputSeparator = new byte[] { (byte)' ' };
			return true;
		}
		if ( value == "\\0" ) {
			inputSeparator = new byte[] { 0 };
			outputSeparator = inputSeparator;
			return true;
		}
		var runes = value.EnumerateRunes().ToArray();
		if ( 1 != runes.Length ) {
			inputSeparator = null;
			outputSeparator = ReadOnlyMemory<byte>.Empty;
			error = string.Concat( "multi-character field separator: '", value, "'" );
			return false;
		}
		inputSeparator = Utf8.GetBytes( value );
		outputSeparator = inputSeparator;
		return true;
	}

	private static bool TryParseOutputFields(
		string value,
		List<OutputField> destination,
		out string? error
	) {
		error = null;
		var tokens = value.Split( new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries );
		if ( 0 == tokens.Length ) {
			error = "empty output format";
			return false;
		}
		foreach ( var token in tokens ) {
			if ( token == "0" ) {
				destination.Add( OutputField.JoinKey );
				continue;
			}
			var dot = token.IndexOf( '.' );
			if ( 1 != dot || token[0] is not ( '1' or '2' ) || !TryParseFieldNumber( token[( dot + 1 )..], out var field ) ) {
				error = string.Concat( "invalid field specification: '", token, "'" );
				return false;
			}
			destination.Add( new OutputField( token[0] - '0', field ) );
		}
		return true;
	}

	private static bool TryParseFieldNumber( string? value, out int field ) {
		return int.TryParse( value, NumberStyles.None, CultureInfo.InvariantCulture, out field ) && 0 < field;
	}

	private static bool TryParseFileNumber( string? value, out int file ) {
		return int.TryParse( value, NumberStyles.None, CultureInfo.InvariantCulture, out file ) && file is 1 or 2;
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string help = """
Usage: join [OPTION]... FILE1 FILE2
Join records of two sorted files on a common field.

  -a FILENUM             also print unpairable records from file FILENUM
  -e STRING              replace missing input fields with STRING
  -i, --ignore-case      ignore differences in case when comparing fields
  -j FIELD               equivalent to '-1 FIELD -2 FIELD'
  -o FORMAT              obey FORMAT while constructing output records
  -t CHAR                use CHAR as the input and output field separator
  -v FILENUM             print only unpairable records from file FILENUM
  -1 FIELD               join on this FIELD of file 1
  -2 FIELD               join on this FIELD of file 2
      --check-order      check that input is correctly sorted
      --nocheck-order    do not check that input is correctly sorted
      --header           treat the first record in each file as field headers
  -z, --zero-terminated  end records with NUL instead of newline
      --help             display this help and exit
      --version          output version information and exit
""";
		await context.StandardOutput.WriteAsync( help.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
	}

	private sealed class JoinOptions {
		private string FirstPath { get; init; } = string.Empty;
		private string SecondPath { get; init; } = string.Empty;
		private int FirstJoinField { get; init; }
		private int SecondJoinField { get; init; }
		private bool IncludeFirstUnpaired { get; init; }
		private bool IncludeSecondUnpaired { get; init; }
		private bool OutputPairable { get; init; }
		private bool IgnoreCase { get; init; }
		private OrderCheckMode CheckMode { get; init; }
		private bool Header { get; init; }
		private RecordSeparator RecordSeparator { get; init; }
		private byte[]? FieldSeparator { get; init; }
		private ReadOnlyMemory<byte> OutputSeparator { get; init; }
		private ReadOnlyMemory<byte> MissingReplacement { get; init; }
		private List<OutputField> OutputFields { get; set; } = new();
		private bool OutputAuto { get; init; }
	}

	private sealed class JoinCursor {
		private readonly JoinKeyComparer myComparer;
		private readonly OrderCheckMode myCheckMode;
		private readonly string myDisplayName;
		private readonly byte[]? myFieldSeparator;
		private readonly int myJoinField;
		private readonly ByteRecordReader myReader;
		private ReadOnlyMemory<byte>? myPreviousKey;
		private long myRecordNumber;

		private JoinCursor(
			ByteRecordReader reader,
			string displayName,
			int joinField,
			byte[]? fieldSeparator,
			JoinKeyComparer comparer,
			OrderCheckMode checkMode
		) {
			this.myReader = reader;
			this.myDisplayName = displayName;
			this.myJoinField = joinField;
			this.myFieldSeparator = fieldSeparator;
			this.myComparer = comparer;
			this.myCheckMode = checkMode;
		}

		private JoinRecord? Current { get; private set; }
		private bool IsDisordered { get; private set; }

		private async ValueTask AdvanceAsync( CommandContext context, bool checkOrder ) {
			var record = await this.myReader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
			if ( null == record ) {
				this.Current = null;
				return;
			}
			this.myRecordNumber++;
			var current = JoinRecord.Create( record, this.myJoinField, this.myFieldSeparator );
			if (
				checkOrder
				&& this.myCheckMode != OrderCheckMode.Never
				&& this.myPreviousKey.HasValue
				&& 0 < this.myComparer.Compare( this.myPreviousKey.Value, current.Key )
			) {
				if ( this.myCheckMode == OrderCheckMode.Always ) {
					throw new InvalidDataException(
						string.Concat( this.myDisplayName, ": is not in sorted order at record ", this.myRecordNumber.ToString( CultureInfo.InvariantCulture ) )
					);
				}
				this.IsDisordered = true;
			}
			if ( checkOrder ) {
				this.myPreviousKey = current.Key;
			}
			this.Current = current;
		}
	}

	private sealed class JoinRecord {
		private JoinRecord( ByteRecord record, List<FieldSlice> fields, int joinField ) {
			this.Record = record;
			this.Fields = fields;
			this.Key = GetField( fields, record.Content, joinField );
		}

		private ByteRecord Record { get; }
		private List<FieldSlice> Fields { get; }
		private ReadOnlyMemory<byte> Key { get; }

		private ReadOnlyMemory<byte> Field( int number ) {
			return GetField( this.Fields, this.Record.Content, number );
		}

		private static JoinRecord Create( ByteRecord record, int joinField, byte[]? separator ) {
			return new JoinRecord( record, SplitFields( record.Content, separator ), joinField );
		}

		private static ReadOnlyMemory<byte> GetField(
			IReadOnlyList<FieldSlice> fields,
			ReadOnlyMemory<byte> content,
			int number
		) {
			if ( number < 1 || fields.Count < number ) {
				return ReadOnlyMemory<byte>.Empty;
			}
			var slice = fields[number - 1];
			return content.Slice( slice.Start, slice.Length );
		}
	}

	private sealed class JoinOutputWriter {
		private readonly JoinOptions myOptions;
		private readonly DelimitedByteRecordWriter myWriter;
		private readonly ReadOnlyMemory<byte> myOutputSeparator;

		private JoinOutputWriter( Stream stream, JoinOptions options ) {
			this.myOptions = options;
			this.myWriter = new DelimitedByteRecordWriter( stream, options.RecordSeparator );
			this.myOutputSeparator = options.OutputSeparator;
		}

		private async ValueTask WriteAsync(
			JoinRecord? first,
			JoinRecord? second,
			CancellationToken cancellationToken
		) {
			var fields = 0 < this.myOptions.OutputFields.Count
				? this.myOptions.OutputFields
				: CreateDefaultFormat( first, second, this.myOptions );
			for ( var index = 0; index < fields.Count; index++ ) {
				if ( 0 < index ) {
					await this.myWriter.WriteContentAsync( this.myOutputSeparator, cancellationToken ).ConfigureAwait( false );
				}
				var value = ResolveField( fields[index], first, second, this.myOptions );
				await this.myWriter.WriteContentAsync( value, cancellationToken ).ConfigureAwait( false );
			}
			await this.myWriter.WriteSeparatorAsync( cancellationToken ).ConfigureAwait( false );
		}

		private ValueTask FlushAsync( CancellationToken cancellationToken ) {
			return this.myWriter.FlushAsync( cancellationToken );
		}

		private static List<OutputField> CreateDefaultFormat(
			JoinRecord? first,
			JoinRecord? second,
			JoinOptions options
		) {
			var fields = new List<OutputField> { OutputField.JoinKey };
			for ( var field = 1; field <= ( first?.Fields.Count ?? 0 ); field++ ) {
				if ( field != options.FirstJoinField ) {
					fields.Add( new OutputField( 1, field ) );
				}
			}
			for ( var field = 1; field <= ( second?.Fields.Count ?? 0 ); field++ ) {
				if ( field != options.SecondJoinField ) {
					fields.Add( new OutputField( 2, field ) );
				}
			}
			return fields;
		}

		private static ReadOnlyMemory<byte> ResolveField(
			OutputField field,
			JoinRecord? first,
			JoinRecord? second,
			JoinOptions options
		) {
			ReadOnlyMemory<byte> value;
			if ( 0 == field.FileNumber ) {
				value = first?.Key ?? second?.Key ?? ReadOnlyMemory<byte>.Empty;
			} else {
				var source = 1 == field.FileNumber ? first : second;
				if ( null == source || source.Fields.Count < field.FieldNumber ) {
					return options.MissingReplacement;
				}
				value = source.Field( field.FieldNumber );
			}
			return value.IsEmpty ? options.MissingReplacement : value;
		}
	}

	private readonly record struct OutputField( int FileNumber, int FieldNumber ) {
		private static OutputField JoinKey { get; } = new( 0, 0 );
	}

	private readonly record struct FieldSlice( int Start, int Length );

	private static List<FieldSlice> SplitFields( ReadOnlyMemory<byte> content, byte[]? separator ) {
		var result = new List<FieldSlice>();
		var bytes = content.Span;
		if ( null != separator && 0 == separator.Length ) {
			result.Add( new FieldSlice( 0, bytes.Length ) );
			return result;
		}
		if ( null == separator ) {
			var index = 0;
			while ( index < bytes.Length ) {
				while ( index < bytes.Length && IsBlank( bytes[index] ) ) {
					index++;
				}
				if ( bytes.Length <= index ) {
					break;
				}
				var start = index;
				while ( index < bytes.Length && !IsBlank( bytes[index] ) ) {
					index++;
				}
				result.Add( new FieldSlice( start, index - start ) );
			}
			return result;
		}
		var position = 0;
		while ( true ) {
			var relative = bytes[position..].IndexOf( separator );
			if ( 0 > relative ) {
				result.Add( new FieldSlice( position, bytes.Length - position ) );
				break;
			}
			result.Add( new FieldSlice( position, relative ) );
			position += relative + separator.Length;
			if ( position == bytes.Length ) {
				result.Add( new FieldSlice( position, 0 ) );
				break;
			}
		}
		return result;
	}

	private static bool IsBlank( byte value ) => value is (byte)' ' or (byte)'\t';

	private sealed class JoinKeyComparer : IComparer<ReadOnlyMemory<byte>> {
		private readonly ByteCollationComparer myCollation;
		private readonly bool myIgnoreCase;

		private JoinKeyComparer(
			ByteCollationComparer collation,
			bool ignoreCase
		) {
			this.myCollation = collation;
			this.myIgnoreCase = ignoreCase;
		}

		private int Compare(
			ReadOnlyMemory<byte> left,
			ReadOnlyMemory<byte> right
		) {
			if ( left.IsEmpty ) {
				return right.IsEmpty ? 0 : -1;
			}
			if ( right.IsEmpty ) {
				return 1;
			}
			if ( !this.myIgnoreCase ) {
				return this.myCollation.Compare( left, right );
			}
			var leftSpan = left.Span;
			var rightSpan = right.Span;
			var count = Math.Min( leftSpan.Length, rightSpan.Length );
			for ( var index = 0; index < count; index++ ) {
				var leftByte = FoldAscii( leftSpan[index] );
				var rightByte = FoldAscii( rightSpan[index] );
				if ( leftByte != rightByte ) {
					return leftByte < rightByte ? -1 : 1;
				}
			}
			return leftSpan.Length.CompareTo( rightSpan.Length );
		}

		int IComparer<ReadOnlyMemory<byte>>.Compare(
			ReadOnlyMemory<byte> left,
			ReadOnlyMemory<byte> right
		) => this.Compare( left, right );

		private static byte FoldAscii( byte value ) {
			return value is >= (byte)'A' and <= (byte)'Z'
				? (byte)( value + ( (byte)'a' - (byte)'A' ) )
				: value;
		}
	}

	private enum OrderCheckMode {
		Default,
		Always,
		Never
	}
}
