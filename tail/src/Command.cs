// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Tail;

using System.Diagnostics;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.Numerics;

/// <summary>
/// Implements GNU-style <c>tail</c>: output the last part of files and,
/// optionally, follow files as they grow.
/// </summary>
/// <remarks>
/// Normal executable operation is byte preserving. Seekable files are scanned
/// backward to locate the requested final records. Forward-only sources use
/// bounded buffering or a temporary spool. Follow mode uses cancellation-aware
/// asynchronous polling and supports descriptor and name semantics.
/// </remarks>
public static class Command {

	#region fields

	private const int DefaultCount = 10;
	private const int DefaultMaxUnchangedStats = 5;
	private const int ErrorExitCode = 1;
	private const int MaxBufferedRecords = 65536;
	private const int UsageExitCode = 1;
	private const string VersionText = "Icod.CoreUtils.Tail 1.0";

	private static readonly OptionDefinition[] Options = new OptionDefinition[] {
		new(
			"bytes",
			'c',
			new string[] { "bytes" },
			OptionValueArity.Required
		),
		new(
			"debug",
			longNames: new string[] { "debug" }
		),
		new(
			"follow",
			'f',
			new string[] { "follow" },
			OptionValueArity.Optional
		),
		new(
			"follow-retry",
			'F'
		),
		new(
			"lines",
			'n',
			new string[] { "lines" },
			OptionValueArity.Required
		),
		new(
			"max-unchanged-stats",
			longNames: new string[] { "max-unchanged-stats" },
			valueArity: OptionValueArity.Required
		),
		new(
			"pid",
			longNames: new string[] { "pid" },
			valueArity: OptionValueArity.Required
		),
		new(
			"quiet",
			'q',
			new string[] { "quiet", "silent" }
		),
		new(
			"retry",
			longNames: new string[] { "retry" }
		),
		new(
			"sleep-interval",
			's',
			new string[] { "sleep-interval" },
			OptionValueArity.Required
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

	private enum FollowMode {
		None,
		Descriptor,
		Name
	}

	private sealed class FollowState : IDisposable {

		public bool Active {
			get;
			set;
		} = true;

		public DateTime CreationTimeUtc {
			get;
			set;
		}

		public FileStream? Descriptor {
			get;
			set;
		}

		public bool MissingReported {
			get;
			set;
		}

		public string Path {
			get;
		}

		public long Position {
			get;
			set;
		}

		public int UnchangedIterations {
			get;
			set;
		}

		public FollowState(
			string path
		) {
			this.Path = path;
		}

		public void Dispose() {
			this.Descriptor?.Dispose();
			this.Descriptor = null;
		}

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

		public bool Debug {
			get;
			set;
		}

		public FollowMode FollowMode {
			get;
			set;
		}

		public int MaxUnchangedStats {
			get;
			set;
		} = DefaultMaxUnchangedStats;

		public List<int> ProcessIds {
			get;
		} = new List<int>();

		public bool Quiet {
			get;
			set;
		}

		public bool Retry {
			get;
			set;
		}

		public double SleepInterval {
			get;
			set;
		} = 1.0;

		public bool StartAt {
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
	/// Executes <c>tail</c> synchronously with optional standard-stream substitution.
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
		return RunAsync(
			args,
			stdin,
			stdout,
			stderr
		).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Executes <c>tail</c> asynchronously with optional injected standard streams.
	/// </summary>
	/// <remarks>
	/// Binary streams are used for byte-preserving command data when supplied; text streams remain available for diagnostics and textual fallbacks. Caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <param name="stdinStream">The binary standard-input stream, or <see langword="null"/> to derive one from the selected text input.</param>
	/// <param name="stdoutStream">The binary standard-output stream, or <see langword="null"/> to derive one from the selected text output.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
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
				"tail",
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
	/// Executes <c>tail</c> asynchronously using a complete shared command context.
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
		ArgumentNullException.ThrowIfNull(
			context
		);
		args ??= Array.Empty<string>();

		using var output = new ByteOutputStream(
			context.StandardOutput,
			context.StandardOutputStream
		);
		try {
			return await RunCoreAsync(
				args,
				context,
				output
			).ConfigureAwait( false );
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

	#region command methods

	private static async Task<int> RunCoreAsync(
		string[] args,
		CommandContext context,
		ByteOutputStream output
	) {
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
				case "debug":
					settings.Debug = true;
					break;
				case "follow":
					if (
						!TryApplyFollowMode(
							option.Value,
							settings,
							out var followError
						)
					) {
						await context.Diagnostics.ErrorAsync(
							followError,
							context.CancellationToken
						).ConfigureAwait( false );
						return UsageExitCode;
					}
					break;
				case "follow-retry":
					settings.FollowMode = FollowMode.Name;
					settings.Retry = true;
					break;
				case "quiet":
					settings.Quiet = true;
					settings.Verbose = false;
					break;
				case "verbose":
					settings.Verbose = true;
					settings.Quiet = false;
					break;
				case "retry":
					settings.Retry = true;
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
							out var byteCountError
						)
					) {
						await context.Diagnostics.ErrorAsync(
							byteCountError,
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
				case "max-unchanged-stats":
					if (
						!TryApplyPositiveInteger(
							option.Value,
							"maximum unchanged statistics",
							out var maximumStats,
							out var maximumStatsError
						)
					) {
						await context.Diagnostics.ErrorAsync(
							maximumStatsError,
							context.CancellationToken
						).ConfigureAwait( false );
						return UsageExitCode;
					}
					settings.MaxUnchangedStats = maximumStats;
					break;
				case "pid":
					if (
						!TryApplyPositiveInteger(
							option.Value,
							"PID",
							out var processId,
							out var processIdError
						)
					) {
						await context.Diagnostics.ErrorAsync(
							processIdError,
							context.CancellationToken
						).ConfigureAwait( false );
						return UsageExitCode;
					}
					settings.ProcessIds.Add(
						processId
					);
					break;
				case "sleep-interval":
					if (
						!TryApplySleepInterval(
							option.Value,
							settings,
							out var sleepIntervalError
						)
					) {
						await context.Diagnostics.ErrorAsync(
							sleepIntervalError,
							context.CancellationToken
						).ConfigureAwait( false );
						return UsageExitCode;
					}
					break;
			}
		}

		if (
			settings.Retry
			&& FollowMode.None == settings.FollowMode
		) {
			await context.Diagnostics.WarningAsync(
				"--retry is useful only when following",
				context.CancellationToken
			).ConfigureAwait( false );
		}
		if (
			0 < settings.ProcessIds.Count
			&& FollowMode.None == settings.FollowMode
		) {
			await context.Diagnostics.WarningAsync(
				"--pid is useful only when following",
				context.CancellationToken
			).ConfigureAwait( false );
		}

		IReadOnlyList<string> operands = 0 == parseResult.Operands.Count
			? new string[] { "-" }
			: parseResult.Operands
		;
		var expansion = await PathnameOperandExpander.ExpandAsync(
			operands,
			cancellationToken: context.CancellationToken
		).ConfigureAwait( false );
		operands = expansion.Operands;
		var showHeaders = settings.Verbose
			|| (
				!settings.Quiet
				&& 1 < operands.Count
			)
		;
		var wroteHeader = false;
		var lastOutputPath = (string?)null;
		var exitCode = 0;
		var followStates = new List<FollowState>();

		try {
			foreach ( var value in operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var operand = InputOperand.Create(
					value
				);
				try {
					var followFromCurrentEnd = (
						FollowMode.None != settings.FollowMode
						&& !operand.IsStandardInput
						&& !settings.StartAt
						&& 0 == settings.Count
						&& File.Exists( operand.Value )
					);
					if ( followFromCurrentEnd ) {
						followStates.Add(
							CreateFollowState(
								operand.Value,
								settings
							)
						);
					}

					if ( showHeaders ) {
						await WriteHeaderAsync(
							output,
							operand.DisplayName,
							wroteHeader,
							context.CancellationToken
						).ConfigureAwait( false );
						wroteHeader = true;
						lastOutputPath = operand.Value;
					}

					if ( !followFromCurrentEnd ) {
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
					}

					if (
						!followFromCurrentEnd
						&& FollowMode.None != settings.FollowMode
						&& !operand.IsStandardInput
					) {
						followStates.Add(
							CreateFollowState(
								operand.Value,
								settings
							)
						);
					}
				} catch ( Exception ex ) when (
					ex is not OperationCanceledException
				) {
					await context.Diagnostics.ErrorAsync(
						$"{operand.Value}: {ex.Message}",
						context.CancellationToken
					).ConfigureAwait( false );
					exitCode = ErrorExitCode;
					if (
						FollowMode.None != settings.FollowMode
						&& settings.Retry
						&& !operand.IsStandardInput
					) {
						followStates.Add(
							new FollowState(
								operand.Value
							) {
								MissingReported = true
							}
						);
					}
				}
			}

			await output.FlushAsync(
				context.CancellationToken
			).ConfigureAwait( false );
			if (
				FollowMode.None != settings.FollowMode
				&& 0 < followStates.Count
			) {
				var followResult = await FollowAsync(
					followStates,
					settings,
					output,
					context,
					showHeaders,
					lastOutputPath
				).ConfigureAwait( false );
				if ( 0 != followResult ) {
					exitCode = followResult;
				}
			}
			return exitCode;
		} finally {
			foreach ( var state in followStates ) {
				state.Dispose();
			}
		}
	}

	#endregion command methods

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
						&& (
							'-' == token[ 0 ]
							|| '+' == token[ 0 ]
						)
						&& char.IsDigit( token[ 1 ] )
					) {
						return new string[] {
							"-n",
							token
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
		var startAt = value.StartsWith(
			"+",
			StringComparison.Ordinal
		);
		var magnitude = (
			startAt
			|| value.StartsWith(
				"-",
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
		settings.StartAt = startAt;
		error = string.Empty;
		return true;
	}

	private static bool TryApplyFollowMode(
		string? value,
		Settings settings,
		out string error
	) {
		if (
			string.IsNullOrEmpty( value )
			|| "descriptor" == value
		) {
			settings.FollowMode = FollowMode.Descriptor;
			error = string.Empty;
			return true;
		}
		if ( "name" == value ) {
			settings.FollowMode = FollowMode.Name;
			error = string.Empty;
			return true;
		}

		error = $"invalid argument '{value}' for '--follow'";
		return false;
	}

	private static bool TryApplyPositiveInteger(
		string? value,
		string description,
		out int result,
		out string error
	) {
		var parsed = QuantityParser.ParseInt64(
			value,
			NumericSuffixTable.None,
			allowLeadingPlus: true,
			allowLeadingMinus: false
		);
		if (
			!parsed.IsSuccess
			|| parsed.Value <= 0
			|| int.MaxValue < parsed.Value
		) {
			error = $"invalid {description}: '{value}'";
			result = 0;
			return false;
		}
		result = (int)parsed.Value;
		error = string.Empty;
		return true;
	}

	private static bool TryApplySleepInterval(
		string? value,
		Settings settings,
		out string error
	) {
		var parsed = QuantityParser.ParseDouble(
			value,
			allowLeadingPlus: true,
			allowLeadingMinus: false
		);
		if (
			!parsed.IsSuccess
			|| parsed.Value < 0
		) {
			error = $"invalid sleep interval '{value}'";
			return false;
		}
		settings.SleepInterval = parsed.Value;
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

	#region initial output methods

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

		if ( settings.StartAt ) {
			await StreamOperations.SkipAsync(
				stream,
				Math.Max(
					0,
					settings.Count - 1
				),
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			await StreamOperations.CopyAsync(
				stream,
				output,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			return;
		}

		if ( stream.CanSeek ) {
			stream.Seek(
				Math.Max(
					0,
					stream.Length - settings.Count
				),
				SeekOrigin.Begin
			);
			await StreamOperations.CopyAsync(
				stream,
				output,
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
		spool.Stream.Seek(
			Math.Max(
				0,
				spool.Stream.Length - settings.Count
			),
			SeekOrigin.Begin
		);
		await StreamOperations.CopyAsync(
			spool.Stream,
			output,
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

		if ( settings.StartAt ) {
			using var reader = new DelimitedByteRecordReader(
				stream,
				separator
			);
			await OutputStartingAtRecordAsync(
				reader,
				output,
				settings.Count,
				context.CancellationToken
			).ConfigureAwait( false );
			return;
		}

		if ( stream.CanSeek ) {
			var offset = await StreamOperations.FindStartOfLastDelimitedRecordsAsync(
				stream,
				separator,
				settings.Count,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			stream.Seek(
				offset,
				SeekOrigin.Begin
			);
			await StreamOperations.CopyAsync(
				stream,
				output,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			return;
		}

		using ( var reader = new DelimitedByteRecordReader(
			stream,
			separator
		) ) {
			await OutputLastRecordsAsync(
				reader,
				output,
				settings.Count,
				separator,
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
		if ( settings.StartAt ) {
			long lineNumber = 1;
			while ( true ) {
				var record = await reader.ReadAsync(
					context.CancellationToken
				).ConfigureAwait( false );
				if ( null == record ) {
					return;
				}
				if ( settings.Count <= lineNumber ) {
					await WriteTextRecordAsync(
						output,
						record,
						separator,
						context.CancellationToken
					).ConfigureAwait( false );
				}
				lineNumber++;
			}
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
				break;
			}
			if ( 0 < settings.Count ) {
				records.Enqueue(
					record
				);
				if ( settings.Count < records.Count ) {
					records.Dequeue();
				}
			}
		}
		foreach ( var record in records ) {
			await WriteTextRecordAsync(
				output,
				record,
				separator,
				context.CancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task OutputStartingAtRecordAsync(
		DelimitedByteRecordReader reader,
		ByteOutputStream output,
		long firstRecord,
		CancellationToken cancellationToken
	) {
		long recordNumber = 1;
		while ( true ) {
			var record = await reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( null == record ) {
				return;
			}
			if ( firstRecord <= recordNumber ) {
				await output.WriteAsync(
					record.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
			}
			recordNumber++;
		}
	}

	private static async Task OutputLastRecordsAsync(
		DelimitedByteRecordReader reader,
		ByteOutputStream output,
		long count,
		byte separator,
		CancellationToken cancellationToken
	) {
		if ( 0 == count ) {
			while (
				null != await reader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false )
			) {
			}
			return;
		}
		if ( MaxBufferedRecords < count ) {
			await OutputLastRecordsSpoolingAsync(
				reader,
				output,
				count,
				separator,
				cancellationToken
			).ConfigureAwait( false );
			return;
		}

		var records = new Queue<byte[]>(
			(int)count
		);
		while ( true ) {
			var record = await reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( null == record ) {
				break;
			}
			records.Enqueue(
				record
			);
			if ( count < records.Count ) {
				records.Dequeue();
			}
		}
		foreach ( var record in records ) {
			await output.WriteAsync(
				record.AsMemory(),
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task OutputLastRecordsSpoolingAsync(
		DelimitedByteRecordReader reader,
		ByteOutputStream output,
		long count,
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

		await spool.RewindAsync(
			cancellationToken
		).ConfigureAwait( false );
		using var spoolReader = new DelimitedByteRecordReader(
			spool.Stream,
			separator
		);
		var skip = Math.Max(
			0,
			recordCount - count
		);
		for (
			long index = 0;
			index < skip;
			index++
		) {
			if (
				null == await spoolReader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false )
			) {
				return;
			}
		}
		while ( true ) {
			var record = await spoolReader.ReadAsync(
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

	#endregion initial output methods

	#region follow methods

	private static FollowState CreateFollowState(
		string path,
		Settings settings
	) {
		var state = new FollowState(
			path
		);
		var information = new FileInfo(
			path
		);
		information.Refresh();
		state.Position = information.Exists
			? information.Length
			: 0
		;
		state.CreationTimeUtc = information.Exists
			? information.CreationTimeUtc
			: DateTime.MinValue
		;

		if (
			FollowMode.Descriptor == settings.FollowMode
			&& information.Exists
		) {
			var descriptor = OpenFollowStream(
				path
			);
			descriptor.Seek(
				state.Position,
				SeekOrigin.Begin
			);
			state.Descriptor = descriptor;
		}
		return state;
	}

	private static async Task<int> FollowAsync(
		IReadOnlyList<FollowState> states,
		Settings settings,
		ByteOutputStream output,
		CommandContext context,
		bool showHeaders,
		string? lastOutputPath
	) {
		if ( settings.Debug ) {
			await context.Diagnostics.WarningAsync(
				$"using asynchronous polling follow mode ({settings.FollowMode.ToString().ToLowerInvariant()})",
				context.CancellationToken
			).ConfigureAwait( false );
		}
		var delay = TimeSpan.FromSeconds(
			settings.SleepInterval
		);

		while ( true ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			if (
				states.All(
					state => !state.Active
				)
			) {
				return ErrorExitCode;
			}
			if (
				0 < settings.ProcessIds.Count
				&& settings.ProcessIds.All(
					processId => !IsProcessAlive(
						processId
					)
				)
			) {
				return 0;
			}

			foreach ( var state in states ) {
				if ( !state.Active ) {
					continue;
				}
				var wrote = FollowMode.Descriptor == settings.FollowMode
					? await PollDescriptorAsync(
						state,
						settings,
						output,
						context,
						showHeaders,
						lastOutputPath
					).ConfigureAwait( false )
					: await PollNameAsync(
						state,
						settings,
						output,
						context,
						showHeaders,
						lastOutputPath
					).ConfigureAwait( false )
				;
				if ( wrote ) {
					lastOutputPath = state.Path;
				}
			}

			await output.FlushAsync(
				context.CancellationToken
			).ConfigureAwait( false );
			await Task.Delay(
				delay,
				context.CancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task<bool> PollDescriptorAsync(
		FollowState state,
		Settings settings,
		ByteOutputStream output,
		CommandContext context,
		bool showHeaders,
		string? lastOutputPath
	) {
		if ( null == state.Descriptor ) {
			try {
				state.Descriptor = OpenFollowStream(
					state.Path
				);
				state.Position = 0;
				state.MissingReported = false;
				await context.Diagnostics.WarningAsync(
					$"'{state.Path}' has become accessible",
					context.CancellationToken
				).ConfigureAwait( false );
			} catch ( Exception ex ) when (
				ex is not OperationCanceledException
			) {
				if ( !state.MissingReported ) {
					await context.Diagnostics.ErrorAsync(
						$"cannot open '{state.Path}': {ex.Message}",
						context.CancellationToken
					).ConfigureAwait( false );
					state.MissingReported = true;
				}
				if ( !settings.Retry ) {
					state.Active = false;
				}
				return false;
			}
		}

		var stream = state.Descriptor
			?? throw new InvalidOperationException(
				"The followed file descriptor is unavailable."
			)
		;
		var length = stream.Length;
		if ( length < state.Position ) {
			await context.Diagnostics.WarningAsync(
				$"{state.Path}: file truncated",
				context.CancellationToken
			).ConfigureAwait( false );
			state.Position = 0;
		}
		if ( length <= state.Position ) {
			return false;
		}

		await WriteFollowHeaderIfNeededAsync(
			output,
			state.Path,
			showHeaders,
			lastOutputPath,
			context.CancellationToken
		).ConfigureAwait( false );
		stream.Seek(
			state.Position,
			SeekOrigin.Begin
		);
		await StreamOperations.CopyCountAsync(
			stream,
			output,
			length - state.Position,
			cancellationToken: context.CancellationToken
		).ConfigureAwait( false );
		state.Position = length;
		return true;
	}

	private static async Task<bool> PollNameAsync(
		FollowState state,
		Settings settings,
		ByteOutputStream output,
		CommandContext context,
		bool showHeaders,
		string? lastOutputPath
	) {
		var information = new FileInfo(
			state.Path
		);
		information.Refresh();
		if ( !information.Exists ) {
			if ( !state.MissingReported ) {
				await context.Diagnostics.WarningAsync(
					$"'{state.Path}' has become inaccessible",
					context.CancellationToken
				).ConfigureAwait( false );
				state.MissingReported = true;
			}
			if ( !settings.Retry ) {
				state.Active = false;
			}
			return false;
		}

		if ( state.MissingReported ) {
			await context.Diagnostics.WarningAsync(
				$"'{state.Path}' has appeared; following new file",
				context.CancellationToken
			).ConfigureAwait( false );
			state.MissingReported = false;
			state.Position = 0;
			state.CreationTimeUtc = information.CreationTimeUtc;
		}

		state.UnchangedIterations++;
		if ( settings.MaxUnchangedStats <= state.UnchangedIterations ) {
			state.UnchangedIterations = 0;
			if (
				DateTime.MinValue != state.CreationTimeUtc
				&& state.CreationTimeUtc != information.CreationTimeUtc
			) {
				await context.Diagnostics.WarningAsync(
					$"'{state.Path}' has been replaced; following new file",
					context.CancellationToken
				).ConfigureAwait( false );
				state.Position = 0;
			}
			state.CreationTimeUtc = information.CreationTimeUtc;
		}

		if ( information.Length < state.Position ) {
			await context.Diagnostics.WarningAsync(
				$"{state.Path}: file truncated",
				context.CancellationToken
			).ConfigureAwait( false );
			state.Position = 0;
		}
		if ( information.Length <= state.Position ) {
			return false;
		}

		await WriteFollowHeaderIfNeededAsync(
			output,
			state.Path,
			showHeaders,
			lastOutputPath,
			context.CancellationToken
		).ConfigureAwait( false );
		await using ( var stream = OpenFollowStream(
			state.Path
		) ) {
			stream.Seek(
				state.Position,
				SeekOrigin.Begin
			);
			await StreamOperations.CopyCountAsync(
				stream,
				output,
				information.Length - state.Position,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
		}
		state.Position = information.Length;
		return true;
	}

	private static FileStream OpenFollowStream(
		string path
	) {
		return new FileStream(
			path,
			FileMode.Open,
			FileAccess.Read,
			FileShare.ReadWrite | FileShare.Delete,
			StreamOperations.DefaultBufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
	}

	private static bool IsProcessAlive(
		int processId
	) {
		try {
			using var process = Process.GetProcessById(
				processId
			);
			return !process.HasExited;
		} catch ( ArgumentException ) {
			return false;
		} catch ( InvalidOperationException ) {
			return false;
		} catch ( System.ComponentModel.Win32Exception ) {
			return false;
		} catch ( NotSupportedException ) {
			return false;
		}
	}

	#endregion follow methods

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

	private static async Task WriteFollowHeaderIfNeededAsync(
		ByteOutputStream output,
		string path,
		bool showHeaders,
		string? lastOutputPath,
		CancellationToken cancellationToken
	) {
		if (
			showHeaders
			&& !string.Equals(
				lastOutputPath,
				path,
				StringComparison.Ordinal
			)
		) {
			await WriteHeaderAsync(
				output,
				path,
				null != lastOutputPath,
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private static Task PrintUsageAsync(
		TextWriter writer,
		CancellationToken cancellationToken
	) {
		const string usage = """
Usage: tail [OPTION]... [FILE]...
Print the last 10 lines of each FILE to standard output.
With more than one FILE, precede each with a header giving the file name.

  -c, --bytes=[+]NUM           output the last NUM bytes;
                                 with '+', start with byte NUM
      --debug                  indicate which follow implementation is used
  -f, --follow[=MODE]          output appended data as the file grows;
                                 MODE is 'name' or 'descriptor'
  -F                           same as --follow=name --retry
  -n, --lines=[+]NUM           output the last NUM lines instead of the last 10;
                                 with '+', start with line NUM
      --max-unchanged-stats=N  with --follow=name, reopen after N unchanged polls
      --pid=PID                with -f, exit after PID no longer exists;
                                 may be repeated
  -q, --quiet, --silent        never output headers giving file names
      --retry                  keep trying to open an inaccessible file
  -s, --sleep-interval=N       with -f, sleep approximately N seconds per poll
  -v, --verbose                always output headers giving file names
  -z, --zero-terminated        line delimiter is NUL, not newline
  -?, --help                   display this help and exit
      --version                output version information and exit

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
