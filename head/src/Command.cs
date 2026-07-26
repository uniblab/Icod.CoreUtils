// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Head;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Numerics;

/// <summary>
/// Implements GNU-style <c>head</c>: output the first part of files.
/// </summary>
/// <remarks>
/// Normal executable operation is byte preserving, including line endings and
/// a final unterminated record. Seekable files use direct offsets; forward-only
/// sources use bounded buffering or a temporary spool when an end-relative
/// operation requires it.
/// </remarks>
public static class Command {

	#region fields

	private const int DefaultCount = 10;
	private const int ErrorExitCode = 1;
	private const int MaxBufferedRecords = 65536;
	private const int UsageExitCode = 1;
	private const string VersionText = "Icod.CoreUtils.Head 1.0";

	private static readonly OptionDefinition[] Options = new OptionDefinition[] {
		new(
			"bytes",
			'c',
			new string[] { "bytes" },
			OptionValueArity.Required
		),
		new(
			"lines",
			'n',
			new string[] { "lines" },
			OptionValueArity.Required
		),
		new(
			"quiet",
			'q',
			new string[] { "quiet", "silent" }
		),
		new(
			"verbose",
			'v',
			new string[] { "verbose" }
		),
		new(
			"zero-terminated",
			'z',
			new string[] { "zero-terminated" }
		),
		new(
			"help",
			'?',
			new string[] { "help" }
		),
		new(
			"version",
			longNames: new string[] { "version" }
		)
	};

	#endregion fields

	#region nested types

	private enum CountKind {
		Lines,
		Bytes
	}

	private sealed class Settings {

		public long Count {
			get;
			set;
		} = DefaultCount;

		public CountKind CountKind {
			get;
			set;
		} = CountKind.Lines;

		public bool ExcludeLast {
			get;
			set;
		}

		public bool Quiet {
			get;
			set;
		}

		public bool Verbose {
			get;
			set;
		}

		public bool ZeroTerminated {
			get;
			set;
		}

	}

	#endregion nested types

	#region public methods

	/// <summary>
	/// Executes <c>head</c> synchronously.
	/// </summary>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		return RunAsync(
			args,
			stdin,
			stdout,
			stderr
		).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Executes <c>head</c> asynchronously using optionally injected standard
	/// streams.
	/// </summary>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		Stream? stdinStream = null,
		Stream? stdoutStream = null,
		CancellationToken cancellationToken = default
	) {
		args ??= Array.Empty<string>();

		var useConsoleInput = null == stdin;
		var useConsoleOutput = null == stdout;
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		if (
			null == stdinStream
			&& useConsoleInput
		) {
			stdinStream = Console.OpenStandardInput();
		}
		if (
			null == stdoutStream
			&& useConsoleOutput
		) {
			stdoutStream = Console.OpenStandardOutput();
		}

		return await RunAsync(
			args,
			new CommandContext(
				"head",
				stdin,
				stdout,
				stderr,
				stdinStream,
				stdoutStream,
				cancellationToken: cancellationToken
			)
		).ConfigureAwait( false );
	}

	/// <summary>
	/// Executes <c>head</c> asynchronously using a shared command context.
	/// </summary>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull(
			context
		);
		args ??= Array.Empty<string>();

		using var output = new ByteOutputStream(
			context.StandardOutput,
			context.StandardOutputStream
		);
		try {
			var parser = CreateOptionParser();
			var parseResult = parser.Parse(
				args
			);
			if ( !parseResult.IsSuccess ) {
				await WriteOptionErrorsAsync(
					parseResult,
					context
				).ConfigureAwait( false );
				return UsageExitCode;
			}

			var settings = new Settings();
			foreach ( var option in parseResult.Options ) {
				switch ( option.Definition.Key ) {
					case "help":
						await PrintUsageAsync(
							context.StandardOutput,
							context.CancellationToken
						).ConfigureAwait( false );
						return 0;
					case "version":
						await context.StandardOutput.WriteLineAsync(
							VersionText.AsMemory(),
							context.CancellationToken
						).ConfigureAwait( false );
						return 0;
					case "quiet":
						settings.Quiet = true;
						settings.Verbose = false;
						break;
					case "verbose":
						settings.Verbose = true;
						settings.Quiet = false;
						break;
					case "zero-terminated":
						settings.ZeroTerminated = true;
						break;
					case "bytes":
						if (
							!TryApplyCount(
								option.Value,
								CountKind.Bytes,
								settings,
								out var countError
							)
						) {
							await context.Diagnostics.ErrorAsync(
								countError,
								context.CancellationToken
							).ConfigureAwait( false );
							return UsageExitCode;
						}
						break;
					case "lines":
						if (
							!TryApplyCount(
								option.Value,
								CountKind.Lines,
								settings,
								out var lineCountError
							)
						) {
							await context.Diagnostics.ErrorAsync(
								lineCountError,
								context.CancellationToken
							).ConfigureAwait( false );
							return UsageExitCode;
						}
						break;
				}
			}

			IReadOnlyList<string> operands = 0 == parseResult.Operands.Count
				? new string[] { "-" }
				: parseResult.Operands
			;
			var showHeaders = settings.Verbose
				|| (
					!settings.Quiet
					&& 1 < operands.Count
				)
			;
			var wroteHeader = false;
			var exitCode = 0;

			foreach ( var value in operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var operand = InputOperand.Create(
					value
				);
				try {
					if ( showHeaders ) {
						await WriteHeaderAsync(
							output,
							operand.DisplayName,
							wroteHeader,
							context.CancellationToken
						).ConfigureAwait( false );
						wroteHeader = true;
					}

					if ( CountKind.Bytes == settings.CountKind ) {
						await ProcessBytesAsync(
							operand,
							settings,
							context,
							output
						).ConfigureAwait( false );
					} else {
						await ProcessRecordsAsync(
							operand,
							settings,
							context,
							output
						).ConfigureAwait( false );
					}
				} catch ( Exception ex ) when (
					ex is not OperationCanceledException
				) {
					await context.Diagnostics.ErrorAsync(
						$"{operand.Value}: {ex.Message}",
						context.CancellationToken
					).ConfigureAwait( false );
					exitCode = ErrorExitCode;
				}
			}

			await output.CompleteAsync(
				context.CancellationToken
			).ConfigureAwait( false );
			return exitCode;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) {
			try {
				await context.Diagnostics.ErrorAsync(
					ex.Message,
					CancellationToken.None
				).ConfigureAwait( false );
			} catch {
				// A diagnostic failure must not replace the command's exit code.
			}
			return ErrorExitCode;
		} finally {
			try {
				await output.CompleteAsync(
					CancellationToken.None
				).ConfigureAwait( false );
			} catch {
				// Completion must not replace the command's primary result.
			}
		}
	}

	#endregion public methods

	#region option methods

	private static OptionParser CreateOptionParser() {
		var settings = new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		};
		settings.TokenRewriteRules.Add(
			new OptionTokenRewriteRule(
				token => {
					if (
						1 < token.Length
						&& '-' == token[ 0 ]
						&& char.IsDigit( token[ 1 ] )
					) {
						return new string[] {
							"-n",
							token.Substring( 1 )
						};
					}
					return null;
				}
			)
		);
		return new OptionParser(
			Options,
			settings
		);
	}

	private static bool TryApplyCount(
		string? value,
		CountKind countKind,
		Settings settings,
		out string error
	) {
		value ??= string.Empty;
		var excludeLast = value.StartsWith(
			"-",
			StringComparison.Ordinal
		);
		var magnitude = (
			excludeLast
			|| value.StartsWith(
				"+",
				StringComparison.Ordinal
			)
		)
			? value.Substring( 1 )
			: value
		;
		var parsed = QuantityParser.ParseInt64(
			magnitude,
			NumericSuffixTable.GnuCounts,
			allowLeadingPlus: false,
			allowLeadingMinus: false,
			overflowBehavior: OverflowBehavior.Clamp
		);
		if ( !parsed.IsSuccess ) {
			error = $"invalid number of {( CountKind.Bytes == countKind ? "bytes" : "lines" )}: '{value}'";
			return false;
		}

		settings.CountKind = countKind;
		settings.Count = parsed.Value;
		settings.ExcludeLast = excludeLast;
		error = string.Empty;
		return true;
	}

	private static async Task WriteOptionErrorsAsync(
		OptionParseResult result,
		CommandContext context
	) {
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format(
					context.ProgramName,
					error
				).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
	}

	#endregion option methods

	#region processing methods

	private static async Task ProcessBytesAsync(
		InputOperand operand,
		Settings settings,
		CommandContext context,
		ByteOutputStream output
	) {
		await using var source = InputSource.OpenBinary(
			operand,
			context
		);
		var stream = source.BinaryStream
			?? throw new InvalidOperationException(
				"A binary input stream was not available."
			)
		;
		if ( !settings.ExcludeLast ) {
			await StreamOperations.CopyCountAsync(
				stream,
				output,
				settings.Count,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			return;
		}

		if ( stream.CanSeek ) {
			var count = Math.Max(
				0,
				stream.Length - settings.Count
			);
			stream.Seek(
				0,
				SeekOrigin.Begin
			);
			await StreamOperations.CopyCountAsync(
				stream,
				output,
				count,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			return;
		}

		await using var spool = TemporarySpool.Create();
		await StreamOperations.CopyAsync(
			stream,
			spool.Stream,
			cancellationToken: context.CancellationToken
		).ConfigureAwait( false );
		await spool.RewindAsync(
			context.CancellationToken
		).ConfigureAwait( false );
		await StreamOperations.CopyCountAsync(
			spool.Stream,
			output,
			Math.Max(
				0,
				spool.Stream.Length - settings.Count
			),
			cancellationToken: context.CancellationToken
		).ConfigureAwait( false );
	}

	private static async Task ProcessRecordsAsync(
		InputOperand operand,
		Settings settings,
		CommandContext context,
		ByteOutputStream output
	) {
		if (
			operand.IsStandardInput
			&& null == context.StandardInputStream
		) {
			await ProcessTextRecordsAsync(
				settings,
				context,
				output
			).ConfigureAwait( false );
			return;
		}

		await using var source = InputSource.OpenBinary(
			operand,
			context
		);
		var stream = source.BinaryStream
			?? throw new InvalidOperationException(
				"A binary input stream was not available."
			)
		;
		var separator = settings.ZeroTerminated
			? (byte)0
			: (byte)'\n'
		;

		if (
			settings.ExcludeLast
			&& stream.CanSeek
		) {
			var offset = await StreamOperations.FindStartOfLastDelimitedRecordsAsync(
				stream,
				separator,
				settings.Count,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			stream.Seek(
				0,
				SeekOrigin.Begin
			);
			await StreamOperations.CopyCountAsync(
				stream,
				output,
				offset,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			return;
		}

		using var reader = new DelimitedByteRecordReader(
			stream,
			separator
		);
		if ( settings.ExcludeLast ) {
			await OutputAllButLastRecordsAsync(
				reader,
				output,
				settings.Count,
				separator,
				context.CancellationToken
			).ConfigureAwait( false );
		} else {
			await OutputFirstRecordsAsync(
				reader,
				output,
				settings.Count,
				context.CancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task ProcessTextRecordsAsync(
		Settings settings,
		CommandContext context,
		ByteOutputStream output
	) {
		var separator = settings.ZeroTerminated
			? '\0'
			: '\n'
		;
		var reader = new DelimitedRecordReader(
			context.StandardInput,
			separator
		);
		if ( !settings.ExcludeLast ) {
			for (
				long index = 0;
				index < settings.Count;
				index++
			) {
				var record = await reader.ReadAsync(
					context.CancellationToken
				).ConfigureAwait( false );
				if ( null == record ) {
					return;
				}
				await WriteTextRecordAsync(
					output,
					record,
					separator,
					context.CancellationToken
				).ConfigureAwait( false );
			}
			return;
		}

		if ( MaxBufferedRecords < settings.Count ) {
			throw new InvalidOperationException(
				"A binary standard-input stream is required for this end-relative count."
			);
		}
		var records = new Queue<string>(
			(int)settings.Count
		);
		while ( true ) {
			var record = await reader.ReadAsync(
				context.CancellationToken
			).ConfigureAwait( false );
			if ( null == record ) {
				return;
			}
			records.Enqueue(
				record
			);
			if ( settings.Count < records.Count ) {
				await WriteTextRecordAsync(
					output,
					records.Dequeue(),
					separator,
					context.CancellationToken
				).ConfigureAwait( false );
			}
		}
	}

	private static async Task OutputFirstRecordsAsync(
		DelimitedByteRecordReader reader,
		ByteOutputStream output,
		long count,
		CancellationToken cancellationToken
	) {
		for (
			long index = 0;
			index < count;
			index++
		) {
			var record = await reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( null == record ) {
				return;
			}
			await output.WriteAsync(
				record.AsMemory(),
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task OutputAllButLastRecordsAsync(
		DelimitedByteRecordReader reader,
		ByteOutputStream output,
		long discard,
		byte separator,
		CancellationToken cancellationToken
	) {
		if ( 0 == discard ) {
			while ( true ) {
				var record = await reader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				if ( null == record ) {
					return;
				}
				await output.WriteAsync(
					record.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
			}
		}
		if ( MaxBufferedRecords < discard ) {
			await OutputAllButLastRecordsSpoolingAsync(
				reader,
				output,
				discard,
				separator,
				cancellationToken
			).ConfigureAwait( false );
			return;
		}

		var records = new Queue<byte[]>(
			(int)discard
		);
		while ( true ) {
			var record = await reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( null == record ) {
				return;
			}
			records.Enqueue(
				record
			);
			if ( discard < records.Count ) {
				var ready = records.Dequeue();
				await output.WriteAsync(
					ready.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
			}
		}
	}

	private static async Task OutputAllButLastRecordsSpoolingAsync(
		DelimitedByteRecordReader reader,
		ByteOutputStream output,
		long discard,
		byte separator,
		CancellationToken cancellationToken
	) {
		await using var spool = TemporarySpool.Create();
		long recordCount = 0;
		while ( true ) {
			var record = await reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( null == record ) {
				break;
			}
			await spool.Stream.WriteAsync(
				record.AsMemory(),
				cancellationToken
			).ConfigureAwait( false );
			recordCount++;
		}

		var outputCount = Math.Max(
			0,
			recordCount - discard
		);
		if ( 0 == outputCount ) {
			return;
		}
		await spool.RewindAsync(
			cancellationToken
		).ConfigureAwait( false );
		using var spoolReader = new DelimitedByteRecordReader(
			spool.Stream,
			separator
		);
		await OutputFirstRecordsAsync(
			spoolReader,
			output,
			outputCount,
			cancellationToken
		).ConfigureAwait( false );
	}

	private static async ValueTask WriteTextRecordAsync(
		ByteOutputStream output,
		string record,
		char separator,
		CancellationToken cancellationToken
	) {
		await output.WriteTextAsync(
			record,
			cancellationToken
		).ConfigureAwait( false );
		var delimiter = new byte[] {
			(byte)separator
		};
		await output.WriteAsync(
			delimiter.AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	#endregion processing methods

	#region output methods

	private static ValueTask WriteHeaderAsync(
		ByteOutputStream output,
		string displayName,
		bool precedingBlankLine,
		CancellationToken cancellationToken
	) {
		return output.WriteTextAsync(
			string.Concat(
				precedingBlankLine
					? "\n"
					: string.Empty,
				"==> ",
				displayName,
				" <==\n"
			),
			cancellationToken
		);
	}

	private static Task PrintUsageAsync(
		TextWriter writer,
		CancellationToken cancellationToken
	) {
		const string usage = """
Usage: head [OPTION]... [FILE]...
Print the first 10 lines of each FILE to standard output.
With more than one FILE, precede each with a header giving the file name.

  -c, --bytes=[-]NUM       print the first NUM bytes of each file;
                             with '-', print all but the last NUM bytes
  -n, --lines=[-]NUM       print the first NUM lines instead of the first 10;
                             with '-', print all but the last NUM lines
  -q, --quiet, --silent    never print headers giving file names
  -v, --verbose            always print headers giving file names
  -z, --zero-terminated    line delimiter is NUL, not newline
  -?, --help               display this help and exit
      --version            output version information and exit

NUM may have a multiplier suffix: b 512, kB 1000, K 1024, MB 1000*1000,
M 1024*1024, and so on for G, T, P, E, Z, Y, R, and Q. Binary prefixes
such as KiB and MiB are accepted too.
""";
		return writer.WriteAsync(
			usage.AsMemory(),
			cancellationToken
		);
	}

	#endregion output methods

}
