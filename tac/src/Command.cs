// Original behavior/reference: GNU coreutils tac
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Tac;

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CommandFramework.RegularExpressions;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>Implements GNU-compatible byte-preserving reverse-record output.</summary>
public static class Command {
	private const string VersionText = "tac (Icod.CoreUtils) 1.0";
	private const int BufferSize = 64 * 1024;
	private const int IndexRecordSize = sizeof( long ) * 2;
	private const int RegexInitialWindowSize = 64 * 1024;

	private sealed class TacOptions {
		public bool Before { get; set; }
		public bool RegularExpression { get; set; }
		public string Separator { get; set; } = "\n";
		public List<string> Inputs { get; } = new();
	}

	private sealed record SeparatorMatch( long Start, long End );

	/// <summary>Runs <c>tac</c> synchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <returns>The process exit status.</returns>
	public static int Run(
		string[] args,
		TextReader? standardInput = null,
		TextWriter? standardOutput = null,
		TextWriter? standardError = null
	) => RunAsync( args, standardInput, standardOutput, standardError ).GetAwaiter().GetResult();

	/// <summary>Runs <c>tac</c> asynchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="standardInputStream">The binary standard-input stream, or <see langword="null"/> to derive one from <paramref name="standardInput"/>.</param>
	/// <param name="standardOutputStream">The binary standard-output stream, or <see langword="null"/> to use <paramref name="standardOutput"/> through the byte-output adapter.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? standardInput = null,
		TextWriter? standardOutput = null,
		TextWriter? standardError = null,
		CancellationToken cancellationToken = default,
		Stream? standardInputStream = null,
		Stream? standardOutputStream = null
	) {
		standardInput ??= Console.In;
		standardOutput ??= Console.Out;
		standardError ??= Console.Error;
		TextReaderStream? inputAdapter = null;
		if ( null == standardInputStream ) {
			inputAdapter = new TextReaderStream( standardInput, leaveOpen: true );
			standardInputStream = inputAdapter;
		}
		try {
			return await RunAsync(
				args,
				new CommandContext(
					"tac",
					standardInput,
					standardOutput,
					standardError,
					standardInputStream,
					standardOutputStream,
					null,
					cancellationToken
				)
			).ConfigureAwait( false );
		} finally {
			inputAdapter?.Dispose();
		}
	}

	/// <summary>Runs <c>tac</c> asynchronously against a byte-capable command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		TextReaderStream? inputAdapter = null;
		try {
			var parsed = CreateParser().Parse( args );
			if ( !parsed.IsSuccess ) {
				await context.StandardError.WriteLineAsync(
					OptionDiagnosticFormatter.Format( context.ProgramName, parsed.Errors[0] ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.UsageError;
			}
			if ( parsed.HasOption( "help" ) ) {
				await WriteHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.HasOption( "version" ) ) {
				await WriteStandardOutputTextAsync(
					context,
					string.Concat( VersionText, Environment.NewLine )
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			var options = CreateOptions( parsed );
			var expansion = await PathnameOperandExpander.ExpandAsync(
				options.Inputs,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			options.Inputs.Clear();
			options.Inputs.AddRange(
				expansion.Operands
			);
			var standardInput = context.StandardInputStream;
			if ( null == standardInput ) {
				inputAdapter = new TextReaderStream( context.StandardInput, leaveOpen: true );
				standardInput = inputAdapter;
			}
			await using var output = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
			var status = await ExecuteAsync( options, standardInput, output, context ).ConfigureAwait( false );
			await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return status;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or InvalidOperationException
			or ArgumentException
			or NotSupportedException
			or OverflowException
			or System.Security.SecurityException
		) {
			try {
				await context.Diagnostics.ErrorAsync( exception.Message, CancellationToken.None ).ConfigureAwait( false );
			} catch {
				// A diagnostic failure must not replace the command failure status.
			}
			return CommandExitCodes.Failure;
		} finally {
			inputAdapter?.Dispose();
		}
	}

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( "before", 'b', new[] { "before" } ),
			new OptionDefinition( "regex", 'r', new[] { "regex" } ),
			new OptionDefinition( "separator", 's', new[] { "separator" }, OptionValueArity.Required ),
			new OptionDefinition( "help", null, new[] { "help" } ),
			new OptionDefinition( "version", null, new[] { "version" } )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	private static TacOptions CreateOptions( OptionParseResult parsed ) {
		var options = new TacOptions {
			Before = parsed.HasOption( "before" ),
			RegularExpression = parsed.HasOption( "regex" ),
			Separator = parsed.GetLastValue( "separator" ) ?? "\n"
		};
		options.Inputs.AddRange( parsed.Operands );
		if ( 0 == options.Inputs.Count ) {
			options.Inputs.Add( "-" );
		}
		return options;
	}

	private static async Task<int> ExecuteAsync(
		TacOptions options,
		Stream standardInput,
		ByteOutputStream output,
		CommandContext context
	) {
		ICompiledRegularExpression? expression = null;
		byte[]? separator = null;
		if ( options.RegularExpression ) {
			if ( 0 == options.Separator.Length ) {
				await context.Diagnostics.ErrorAsync(
					"separator cannot be empty",
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.UsageError;
			}
			var compiled = new GnuBasicRegularExpressionProvider().Compile(
				options.Separator,
				RegularExpressionOptions.GnuEmacsCompatibility,
				context.CancellationToken
			);
			if ( !compiled.IsSuccess ) {
				await context.Diagnostics.ErrorAsync(
					compiled.Diagnostic?.Message ?? "invalid regular expression",
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.UsageError;
			}
			expression = compiled.Expression;
		} else {
			separator = 0 == options.Separator.Length
				? new byte[] { 0 }
				: Encoding.UTF8.GetBytes( options.Separator );
			if ( 0 == separator.Length ) {
				separator = new byte[] { 0 };
			}
		}

		var status = CommandExitCodes.Success;
		foreach ( var operand in options.Inputs ) {
			try {
				await ProcessInputAsync(
					operand,
					standardInput,
					output,
					options,
					separator,
					expression,
					context.CancellationToken
				).ConfigureAwait( false );
			} catch ( OperationCanceledException ) {
				throw;
			} catch ( Exception exception ) when (
				exception is IOException
				or UnauthorizedAccessException
				or InvalidOperationException
				or ArgumentException
				or NotSupportedException
				or OverflowException
				or System.Security.SecurityException
			) {
				await context.Diagnostics.ErrorAsync(
					$"{operand}: {exception.Message}",
					context.CancellationToken
				).ConfigureAwait( false );
				status = CommandExitCodes.Failure;
			}
		}
		return status;
	}

	private static async Task ProcessInputAsync(
		string operand,
		Stream standardInput,
		ByteOutputStream output,
		TacOptions options,
		byte[]? separator,
		ICompiledRegularExpression? expression,
		CancellationToken cancellationToken
	) {
		Stream input;
		var disposeInput = false;
		if ( "-" == operand ) {
			input = standardInput;
		} else {
			input = new FileStream(
				operand,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read,
				BufferSize,
				FileOptions.Asynchronous | FileOptions.RandomAccess
			);
			disposeInput = true;
		}
		var openedInput = input;
		TemporarySpool? inputSpool = null;
		try {
			if ( !input.CanSeek ) {
				inputSpool = TemporarySpool.Create();
				await input.CopyToAsync( inputSpool.Stream, BufferSize, cancellationToken ).ConfigureAwait( false );
				await inputSpool.RewindAsync( cancellationToken ).ConfigureAwait( false );
				input = inputSpool.Stream;
			}
			var start = input.Position;
			await using var index = TemporarySpool.Create();
			if ( null != expression ) {
				await BuildRegexReverseIndexAsync(
					input,
					start,
					index.Stream,
					expression,
					options.Before,
					cancellationToken
				).ConfigureAwait( false );
				await WriteForwardAsync( input, index.Stream, output, cancellationToken ).ConfigureAwait( false );
			} else {
				await BuildFixedIndexAsync(
					input,
					start,
					index.Stream,
					separator!,
					options.Before,
					cancellationToken
				).ConfigureAwait( false );
				await WriteReverseAsync( input, index.Stream, output, cancellationToken ).ConfigureAwait( false );
			}
		} finally {
			if ( null != inputSpool ) {
				await inputSpool.DisposeAsync().ConfigureAwait( false );
			}
			if ( disposeInput ) {
				await openedInput.DisposeAsync().ConfigureAwait( false );
			}
		}
	}

	private static async Task BuildFixedIndexAsync(
		Stream input,
		long start,
		Stream index,
		byte[] separator,
		bool before,
		CancellationToken cancellationToken
	) {
		input.Position = start;
		var failure = BuildFailureTable( separator );
		var buffer = new byte[BufferSize];
		var matched = 0;
		long absolute = start;
		long recordStart = start;
		while ( true ) {
			var read = await input.ReadAsync( buffer.AsMemory(), cancellationToken ).ConfigureAwait( false );
			if ( 0 == read ) {
				break;
			}
			for ( var indexInBuffer = 0; indexInBuffer < read; indexInBuffer++, absolute++ ) {
				var value = buffer[indexInBuffer];
				while ( 0 < matched && separator[matched] != value ) {
					matched = failure[matched - 1];
				}
				if ( separator[matched] == value ) {
					matched++;
				}
				if ( matched == separator.Length ) {
					var separatorEnd = absolute + 1;
					var separatorStart = separatorEnd - separator.Length;
					if ( before ) {
						await WriteIndexRecordAsync( index, recordStart, separatorStart - recordStart, cancellationToken ).ConfigureAwait( false );
						recordStart = separatorStart;
					} else {
						await WriteIndexRecordAsync( index, recordStart, separatorEnd - recordStart, cancellationToken ).ConfigureAwait( false );
						recordStart = separatorEnd;
					}
					matched = 0;
				}
			}
		}
		var end = absolute;
		if ( recordStart < end ) {
			await WriteIndexRecordAsync( index, recordStart, end - recordStart, cancellationToken ).ConfigureAwait( false );
		}
	}

	private static async Task BuildRegexReverseIndexAsync(
		Stream input,
		long start,
		Stream index,
		ICompiledRegularExpression expression,
		bool before,
		CancellationToken cancellationToken
	) {
		var searchEnd = input.Length;
		var pastEnd = searchEnd;
		var firstMatch = true;
		while ( start < searchEnd ) {
			var match = await FindPreviousRegexSeparatorAsync(
				input,
				start,
				searchEnd,
				expression,
				cancellationToken
			).ConfigureAwait( false );
			if ( null == match ) {
				break;
			}
			if ( before ) {
				await WriteNonemptyIndexRecordAsync(
					index,
					match.Start,
					pastEnd - match.Start,
					cancellationToken
				).ConfigureAwait( false );
				pastEnd = match.Start;
			} else {
				if ( !firstMatch || match.End != pastEnd ) {
					await WriteNonemptyIndexRecordAsync(
						index,
						match.End,
						pastEnd - match.End,
						cancellationToken
					).ConfigureAwait( false );
				}
				pastEnd = match.End;
				firstMatch = false;
			}
			searchEnd = match.Start;
		}
		await WriteNonemptyIndexRecordAsync(
			index,
			start,
			pastEnd - start,
			cancellationToken
		).ConfigureAwait( false );
	}

	private static async Task<SeparatorMatch?> FindPreviousRegexSeparatorAsync(
		Stream input,
		long start,
		long searchEnd,
		ICompiledRegularExpression expression,
		CancellationToken cancellationToken
	) {
		long span = Math.Min( RegexInitialWindowSize, searchEnd - start );
		while ( 0 < span ) {
			var tentativeStart = searchEnd - span;
			var windowStart = await AlignUtf8WindowStartAsync(
				input,
				start,
				tentativeStart,
				searchEnd,
				cancellationToken
			).ConfigureAwait( false );
			var windowLength = searchEnd - windowStart;
			if ( int.MaxValue < windowLength ) {
				throw new IOException( "record too large" );
			}
			var buffer = new byte[(int)windowLength];
			input.Position = windowStart;
			await ReadExactlyAsync( input, buffer, cancellationToken ).ConfigureAwait( false );
			var match = FindLastRegexMatch(
				buffer,
				windowStart,
				start,
				expression,
				cancellationToken
			);
			if ( null != match ) {
				return match;
			}
			if ( windowStart == start ) {
				return null;
			}
			span = Math.Min( searchEnd - start, checked( span * 2 ) );
		}
		return null;
	}

	private static SeparatorMatch? FindLastRegexMatch(
		ReadOnlyMemory<byte> input,
		long windowStart,
		long inputStart,
		ICompiledRegularExpression expression,
		CancellationToken cancellationToken
	) {
		var inputOptions = new RegularExpressionInputOptions();
		SeparatorMatch? lastMatch = null;
		var searchOffset = 0;
		while ( searchOffset < input.Length ) {
			var result = expression.Match(
				input,
				inputOptions,
				new RegularExpressionByteMatchOptions { StartByteOffset = searchOffset },
				cancellationToken
			);
			if ( !result.IsSuccess ) {
				throw new InvalidOperationException(
					result.Diagnostic?.Message ?? "regular-expression matching failed"
				);
			}
			if ( null == result.Match || result.Match.ByteIndex >= input.Length ) {
				break;
			}
			var localStart = result.Match.ByteIndex;
			if ( 0 != localStart || windowStart == inputStart ) {
				lastMatch = new SeparatorMatch(
					windowStart + localStart,
					windowStart + localStart + result.Match.ByteLength
				);
			}
			searchOffset = GetNextUtf8Boundary( input.Span, localStart );
		}
		return lastMatch;
	}

	private static async Task<long> AlignUtf8WindowStartAsync(
		Stream input,
		long inputStart,
		long tentativeStart,
		long searchEnd,
		CancellationToken cancellationToken
	) {
		if ( tentativeStart <= inputStart ) {
			return inputStart;
		}
		var probeStart = Math.Max( inputStart, tentativeStart - 3 );
		var probeEnd = Math.Min( searchEnd, tentativeStart + 4 );
		var probeLength = checked( (int)( probeEnd - probeStart ) );
		var probe = new byte[probeLength];
		input.Position = probeStart;
		await ReadExactlyAsync( input, probe, cancellationToken ).ConfigureAwait( false );
		var target = checked( (int)( tentativeStart - probeStart ) );
		if ( !IsUtf8ContinuationByte( probe[target] ) ) {
			return tentativeStart;
		}
		for ( var candidate = target - 1; 0 <= candidate; candidate-- ) {
			var status = Rune.DecodeFromUtf8(
				probe.AsSpan( candidate ),
				out _,
				out var consumed
			);
			if ( OperationStatus.Done == status && candidate + consumed > target ) {
				return probeStart + candidate;
			}
			if ( !IsUtf8ContinuationByte( probe[candidate] ) ) {
				break;
			}
		}
		return tentativeStart;
	}

	private static int GetNextUtf8Boundary( ReadOnlySpan<byte> input, int start ) {
		var status = Rune.DecodeFromUtf8( input[start..], out _, out var consumed );
		return start + ( OperationStatus.Done == status ? consumed : 1 );
	}

	private static bool IsUtf8ContinuationByte( byte value ) => 0x80 == ( value & 0xC0 );

	private static int[] BuildFailureTable( byte[] pattern ) {
		var failure = new int[pattern.Length];
		var matched = 0;
		for ( var index = 1; index < pattern.Length; index++ ) {
			while ( 0 < matched && pattern[matched] != pattern[index] ) {
				matched = failure[matched - 1];
			}
			if ( pattern[matched] == pattern[index] ) {
				matched++;
			}
			failure[index] = matched;
		}
		return failure;
	}

	private static Task WriteNonemptyIndexRecordAsync(
		Stream index,
		long start,
		long length,
		CancellationToken cancellationToken
	) => 0 == length
		? Task.CompletedTask
		: WriteIndexRecordAsync( index, start, length, cancellationToken );

	private static async Task WriteIndexRecordAsync(
		Stream index,
		long start,
		long length,
		CancellationToken cancellationToken
	) {
		if ( length < 0 ) {
			throw new InvalidOperationException( "invalid record boundary" );
		}
		var buffer = new byte[IndexRecordSize];
		BinaryPrimitives.WriteInt64LittleEndian( buffer.AsSpan( 0, sizeof( long ) ), start );
		BinaryPrimitives.WriteInt64LittleEndian( buffer.AsSpan( sizeof( long ), sizeof( long ) ), length );
		await index.WriteAsync( buffer, cancellationToken ).ConfigureAwait( false );
	}

	private static async Task WriteForwardAsync(
		Stream input,
		Stream index,
		ByteOutputStream output,
		CancellationToken cancellationToken
	) {
		await index.FlushAsync( cancellationToken ).ConfigureAwait( false );
		var metadata = new byte[IndexRecordSize];
		for ( long position = 0; position < index.Length; position += IndexRecordSize ) {
			index.Position = position;
			await ReadExactlyAsync( index, metadata, cancellationToken ).ConfigureAwait( false );
			var start = BinaryPrimitives.ReadInt64LittleEndian( metadata.AsSpan( 0, sizeof( long ) ) );
			var length = BinaryPrimitives.ReadInt64LittleEndian( metadata.AsSpan( sizeof( long ), sizeof( long ) ) );
			input.Position = start;
			await CopyExactlyAsync( input, output, length, cancellationToken ).ConfigureAwait( false );
		}
	}

	private static async Task WriteReverseAsync(
		Stream input,
		Stream index,
		ByteOutputStream output,
		CancellationToken cancellationToken
	) {
		await index.FlushAsync( cancellationToken ).ConfigureAwait( false );
		var metadata = new byte[IndexRecordSize];
		for ( long position = index.Length - IndexRecordSize; position >= 0; position -= IndexRecordSize ) {
			index.Position = position;
			await ReadExactlyAsync( index, metadata, cancellationToken ).ConfigureAwait( false );
			var start = BinaryPrimitives.ReadInt64LittleEndian( metadata.AsSpan( 0, sizeof( long ) ) );
			var length = BinaryPrimitives.ReadInt64LittleEndian( metadata.AsSpan( sizeof( long ), sizeof( long ) ) );
			input.Position = start;
			await CopyExactlyAsync( input, output, length, cancellationToken ).ConfigureAwait( false );
		}
	}

	private static async Task ReadExactlyAsync(
		Stream input,
		Memory<byte> destination,
		CancellationToken cancellationToken
	) {
		var offset = 0;
		while ( offset < destination.Length ) {
			var read = await input.ReadAsync( destination[offset..], cancellationToken ).ConfigureAwait( false );
			if ( 0 == read ) {
				throw new EndOfStreamException();
			}
			offset += read;
		}
	}

	private static async Task CopyExactlyAsync(
		Stream input,
		Stream output,
		long count,
		CancellationToken cancellationToken
	) {
		var buffer = new byte[BufferSize];
		var remaining = count;
		while ( 0 < remaining ) {
			var read = await input.ReadAsync(
				buffer.AsMemory( 0, (int)Math.Min( buffer.Length, remaining ) ),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				throw new EndOfStreamException();
			}
			await output.WriteAsync( buffer.AsMemory( 0, read ), cancellationToken ).ConfigureAwait( false );
			remaining -= read;
		}
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string help = """
Usage: tac [OPTION]... [FILE]...
Write each FILE to standard output, last record first.

  -b, --before           attach the separator before instead of after each record
  -r, --regex            interpret the separator as a regular expression
  -s, --separator=STRING use STRING instead of newline as the record separator;
                         an empty STRING specifies NUL
      --help             display this help and exit
      --version          output version information and exit
""";
		await WriteStandardOutputTextAsync(
			context,
			string.Concat(
				help.ReplaceLineEndings( Environment.NewLine ),
				Environment.NewLine
			)
		).ConfigureAwait( false );
	}

	private static async Task WriteStandardOutputTextAsync(
		CommandContext context,
		string value
	) {
		await using var output = new ByteOutputStream(
			context.StandardOutput,
			context.StandardOutputStream
		);
		await output.WriteTextAsync(
			value,
			context.CancellationToken
		).ConfigureAwait( false );
		await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
	}
}
