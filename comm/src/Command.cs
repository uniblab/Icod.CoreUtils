// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Comm;

using System.Globalization;
using System.Text;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CoreUtils.Shared.Ordering;
using Icod.CommandFramework.Records;

/// <summary>Implements GNU-compatible comparison of two sorted record streams.</summary>
public static class Command {
	private const string VersionText = "comm (Icod.CoreUtils) 1.0";
	private static readonly UTF8Encoding Utf8 = new( false, false );

	/// <summary>Runs <c>comm</c> synchronously with optional injected text streams.</summary>
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

	/// <summary>Runs <c>comm</c> asynchronously with optional injected text streams.</summary>
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
				"comm",
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

	/// <summary>Runs <c>comm</c> asynchronously against a command context.</summary>
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
			or InvalidDataException
			or UnauthorizedAccessException
			or InvalidOperationException
			or ArgumentException
			or NotSupportedException
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
			new OptionDefinition( "suppress-one", '1' ),
			new OptionDefinition( "suppress-two", '2' ),
			new OptionDefinition( "suppress-three", '3' ),
			new OptionDefinition( "check-order", null, new[] { "check-order" } ),
			new OptionDefinition( "nocheck-order", null, new[] { "nocheck-order" } ),
			new OptionDefinition( "output-delimiter", null, new[] { "output-delimiter" }, OptionValueArity.Required ),
			new OptionDefinition( "total", null, new[] { "total" } ),
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
		out CommOptions? options,
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
		var delimiterValues = parsed.GetOccurrences( "output-delimiter" )
			.Select( occurrence => occurrence.Value ?? string.Empty )
			.ToArray();
		if ( 1 < delimiterValues.Length && delimiterValues.Skip( 1 ).Any( value => value != delimiterValues[0] ) ) {
			error = "multiple output delimiters specified";
			return false;
		}
		var delimiter = 0 == delimiterValues.Length
			? new byte[] { (byte)'\t' }
			: string.IsNullOrEmpty( delimiterValues[^1] )
				? new byte[] { 0 }
				: Utf8.GetBytes( delimiterValues[^1] );
		options = new CommOptions(
			parsed.Operands[0],
			parsed.Operands[1],
			!parsed.HasOption( "suppress-one" ),
			!parsed.HasOption( "suppress-two" ),
			!parsed.HasOption( "suppress-three" ),
			parsed.HasOption( "nocheck-order" ) ? OrderCheckMode.Never : parsed.HasOption( "check-order" ) ? OrderCheckMode.Always : OrderCheckMode.Default,
			delimiter,
			parsed.HasOption( "total" ),
			parsed.HasOption( "zero-terminated" ) ? RecordSeparator.Null : RecordSeparator.LineFeed
		);
		return true;
	}

	private static async Task<int> ExecuteAsync( CommOptions options, CommandContext context ) {
		var resolution = CollationEnvironment.ResolveCurrent();
		if ( !resolution.IsSuccess ) {
			throw new NotSupportedException( resolution.ErrorMessage );
		}
		var firstPath = await Icod.CoreUtils.Shared.FileSystem.Traversal.PathnameOperandExpander.ExpandSingularAsync(
			options.FirstPath,
			cancellationToken: context.CancellationToken
		).ConfigureAwait( false );
		var secondPath = await Icod.CoreUtils.Shared.FileSystem.Traversal.PathnameOperandExpander.ExpandSingularAsync(
			options.SecondPath,
			cancellationToken: context.CancellationToken
		).ConfigureAwait( false );
		var comparer = new ByteCollationComparer( new SystemCollationProvider( resolution.Profile! ) );
		await using var firstSource = InputSource.OpenBinary( InputOperand.Create( firstPath ), context );
		await using var secondSource = InputSource.OpenBinary( InputOperand.Create( secondPath ), context );
		using var firstReader = new ByteRecordReader( firstSource.BinaryStream!, options.RecordSeparator );
		using var secondReader = new ByteRecordReader( secondSource.BinaryStream!, options.RecordSeparator );
		await using var output = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
		var writer = new DelimitedByteRecordWriter( output, options.RecordSeparator );
		var firstState = new InputState( firstSource.DisplayName );
		var secondState = new InputState( secondSource.DisplayName );
		var first = await ReadAsync( firstReader, firstState, comparer, options.CheckMode, context ).ConfigureAwait( false );
		var second = await ReadAsync( secondReader, secondState, comparer, options.CheckMode, context ).ConfigureAwait( false );
		ulong firstOnly = 0;
		ulong secondOnly = 0;
		ulong common = 0;
		while ( null != first || null != second ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			if ( null == second || ( null != first && comparer.Compare( first.Content, second.Content ) < 0 ) ) {
				firstOnly++;
				if ( options.ShowFirst ) {
					await WriteColumnAsync( writer, options, 1, first!.Content, context.CancellationToken ).ConfigureAwait( false );
				}
				first = await ReadAsync( firstReader, firstState, comparer, options.CheckMode, context ).ConfigureAwait( false );
			} else if ( null == first || comparer.Compare( first.Content, second.Content ) > 0 ) {
				secondOnly++;
				if ( options.ShowSecond ) {
					await WriteColumnAsync( writer, options, 2, second!.Content, context.CancellationToken ).ConfigureAwait( false );
				}
				second = await ReadAsync( secondReader, secondState, comparer, options.CheckMode, context ).ConfigureAwait( false );
			} else {
				common++;
				if ( options.ShowCommon ) {
					await WriteColumnAsync( writer, options, 3, first.Content, context.CancellationToken ).ConfigureAwait( false );
				}
				first = await ReadAsync( firstReader, firstState, comparer, options.CheckMode, context ).ConfigureAwait( false );
				second = await ReadAsync( secondReader, secondState, comparer, options.CheckMode, context ).ConfigureAwait( false );
			}
		}
		if ( options.ShowTotal ) {
			await WriteTotalAsync( writer, options, firstOnly, secondOnly, common, context.CancellationToken ).ConfigureAwait( false );
		}
		await writer.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
		await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
		if (
			options.CheckMode == OrderCheckMode.Default
			&& 0 < firstOnly + secondOnly
			&& ( firstState.IsDisordered || secondState.IsDisordered )
		) {
			throw new InvalidDataException( "input is not in sorted order" );
		}
		return CommandExitCodes.Success;
	}

	private static async ValueTask<ByteRecord?> ReadAsync(
		ByteRecordReader reader,
		InputState state,
		ByteCollationComparer comparer,
		OrderCheckMode checkMode,
		CommandContext context
	) {
		var record = await reader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
		if ( null == record ) {
			return null;
		}
		state.LineNumber++;
		if ( checkMode != OrderCheckMode.Never && null != state.Previous && 0 < comparer.Compare( state.Previous.Content, record.Content ) ) {
			if ( checkMode == OrderCheckMode.Always ) {
				throw new InvalidDataException(
					string.Concat( state.DisplayName, ": is not in sorted order at record ", state.LineNumber.ToString( CultureInfo.InvariantCulture ) )
				);
			}
			state.IsDisordered = true;
		}
		state.Previous = record;
		return record;
	}

	private static async ValueTask WriteColumnAsync(
		DelimitedByteRecordWriter writer,
		CommOptions options,
		int column,
		ReadOnlyMemory<byte> content,
		CancellationToken cancellationToken
	) {
		var preceding = column switch {
			1 => 0,
			2 => options.ShowFirst ? 1 : 0,
			3 => ( options.ShowFirst ? 1 : 0 ) + ( options.ShowSecond ? 1 : 0 ),
			_ => throw new ArgumentOutOfRangeException( nameof( column ) )
		};
		for ( var index = 0; index < preceding; index++ ) {
			await writer.WriteContentAsync( options.OutputDelimiter, cancellationToken ).ConfigureAwait( false );
		}
		await writer.WriteRecordAsync( content, terminate: true, cancellationToken ).ConfigureAwait( false );
	}

	private static async ValueTask WriteTotalAsync(
		DelimitedByteRecordWriter writer,
		CommOptions options,
		ulong firstOnly,
		ulong secondOnly,
		ulong common,
		CancellationToken cancellationToken
	) {
		await writer.WriteContentAsync( Utf8.GetBytes( firstOnly.ToString( CultureInfo.InvariantCulture ) ), cancellationToken ).ConfigureAwait( false );
		await writer.WriteContentAsync( options.OutputDelimiter, cancellationToken ).ConfigureAwait( false );
		await writer.WriteContentAsync( Utf8.GetBytes( secondOnly.ToString( CultureInfo.InvariantCulture ) ), cancellationToken ).ConfigureAwait( false );
		await writer.WriteContentAsync( options.OutputDelimiter, cancellationToken ).ConfigureAwait( false );
		await writer.WriteContentAsync( Utf8.GetBytes( common.ToString( CultureInfo.InvariantCulture ) ), cancellationToken ).ConfigureAwait( false );
		await writer.WriteContentAsync( options.OutputDelimiter, cancellationToken ).ConfigureAwait( false );
		await writer.WriteRecordAsync( "total"u8.ToArray(), terminate: true, cancellationToken ).ConfigureAwait( false );
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string help = """
Usage: comm [OPTION]... FILE1 FILE2
Compare sorted files FILE1 and FILE2 record by record.

  -1                      suppress column 1 (records unique to FILE1)
  -2                      suppress column 2 (records unique to FILE2)
  -3                      suppress column 3 (records common to both files)
      --check-order       check that input is correctly sorted
      --nocheck-order     do not check that input is correctly sorted
      --output-delimiter=STR  separate columns with STR
      --total             output a summary
  -z, --zero-terminated  end records with NUL instead of newline
      --help              display this help and exit
      --version           output version information and exit
""";
		await context.StandardOutput.WriteAsync( help.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
	}

	private sealed class CommOptions {
		/// <summary>Initializes the immutable command options.</summary>
		/// <param name="firstPath">The first input path.</param>
		/// <param name="secondPath">The second input path.</param>
		/// <param name="showFirst">Whether to emit records unique to the first input.</param>
		/// <param name="showSecond">Whether to emit records unique to the second input.</param>
		/// <param name="showCommon">Whether to emit records common to both inputs.</param>
		/// <param name="checkMode">The sorted-input checking mode.</param>
		/// <param name="outputDelimiter">The delimiter inserted between output columns.</param>
		/// <param name="showTotal">Whether to emit the totals record.</param>
		/// <param name="recordSeparator">The input and output record separator.</param>
		public CommOptions(
			string firstPath,
			string secondPath,
			bool showFirst,
			bool showSecond,
			bool showCommon,
			OrderCheckMode checkMode,
			ReadOnlyMemory<byte> outputDelimiter,
			bool showTotal,
			RecordSeparator recordSeparator
		) {
			this.FirstPath = firstPath;
			this.SecondPath = secondPath;
			this.ShowFirst = showFirst;
			this.ShowSecond = showSecond;
			this.ShowCommon = showCommon;
			this.CheckMode = checkMode;
			this.OutputDelimiter = outputDelimiter;
			this.ShowTotal = showTotal;
			this.RecordSeparator = recordSeparator;
		}

		/// <summary>Gets the first input path.</summary>
		public string FirstPath { get; }
		/// <summary>Gets the second input path.</summary>
		public string SecondPath { get; }
		/// <summary>Gets whether records unique to the first input are emitted.</summary>
		public bool ShowFirst { get; }
		/// <summary>Gets whether records unique to the second input are emitted.</summary>
		public bool ShowSecond { get; }
		/// <summary>Gets whether records common to both inputs are emitted.</summary>
		public bool ShowCommon { get; }
		/// <summary>Gets the sorted-input checking mode.</summary>
		public OrderCheckMode CheckMode { get; }
		/// <summary>Gets the delimiter inserted between output columns.</summary>
		public ReadOnlyMemory<byte> OutputDelimiter { get; }
		/// <summary>Gets whether the totals record is emitted.</summary>
		public bool ShowTotal { get; }
		/// <summary>Gets the input and output record separator.</summary>
		public RecordSeparator RecordSeparator { get; }
	}

	private sealed class InputState {
		/// <summary>Initializes state for one input stream.</summary>
		/// <param name="displayName">The input name used in diagnostics.</param>
		public InputState( string displayName ) {
			this.DisplayName = displayName;
		}

		/// <summary>Gets the input name used in diagnostics.</summary>
		public string DisplayName { get; }
		/// <summary>Gets or sets whether a descending adjacent record pair was observed.</summary>
		public bool IsDisordered { get; set; }
		/// <summary>Gets or sets the one-based number of the most recently read record.</summary>
		public long LineNumber { get; set; }
		/// <summary>Gets or sets the previously read record used for order checking.</summary>
		public ByteRecord? Previous { get; set; }
	}

	private enum OrderCheckMode {
		Default,
		Always,
		Never
	}
}
