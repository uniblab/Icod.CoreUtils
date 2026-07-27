using System.Runtime.InteropServices;
using System.Text;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

namespace Icod.CoreUtils.DD;

/// <summary>
/// Implements the GNU-compatible block copy and conversion utility.
/// <para>Usage: <c>dd [OPERAND]...</c> or <c>dd OPTION</c>.</para>
/// </summary>
public static class Command {
	private const string ProgramName = "dd";
	private const string Version = "dd (Icod.CoreUtils) 1.0";

	/// <summary>Runs the command synchronously for compatibility.</summary>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) => RunAsync(
		args,
		stdin,
		stdout,
		stderr
	).GetAwaiter().GetResult();

	/// <summary>Runs the command asynchronously with injectable text streams.</summary>
	public static Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) => RunAsync(
		args ?? [],
		new CommandContext(
			ProgramName,
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error,
			cancellationToken: cancellationToken
		)
	);

	/// <summary>Runs the command asynchronously with a complete command context.</summary>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull(
			context
		);
		var parser = CreateParser();
		try {
			var result = parser.Parse(
				args ?? []
			);
			if (
				await WriteParseErrorsAsync(
					result,
					context
				).ConfigureAwait( false )
			) {
				return CommandExitCodes.Failure;
			}
			if ( result.HasOption( "help" ) ) {
				await WriteUsageAsync(
					context
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					Version.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var options = new DdOptions();
			if (
				!await TryParseOperandsAsync(
					result.Operands,
					options,
					context
				).ConfigureAwait( false )
			) {
				return CommandExitCodes.Failure;
			}
			if (
				!await ValidateOptionsAsync(
					options,
					context
				).ConfigureAwait( false )
			) {
				return CommandExitCodes.Failure;
			}
			return await CopyAsync(
				options,
				context
			).ConfigureAwait( false );
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( OverflowException ) {
			await context.Diagnostics.ErrorAsync(
				"offset or transfer size is too large",
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or NotSupportedException
			or ArgumentException
		) {
			await context.Diagnostics.ErrorAsync(
				exception.Message,
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	/// <summary>Writes the complete command usage and operand reference.</summary>
	public static Task WriteUsageAsync(
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull(
			context
		);
		const string usage = """
Usage: dd [OPERAND]...
  or:  dd OPTION
Copy a file, converting and formatting according to the operands.

  bs=BYTES        read and write up to BYTES bytes at a time (default: 512);
                  overrides ibs and obs
  cbs=BYTES       convert BYTES bytes at a time
  conv=CONVS      convert the file as per the comma separated symbol list
  count=N         copy only N input blocks
  ibs=BYTES       read up to BYTES bytes at a time (default: 512)
  if=FILE         read from FILE instead of standard input
  iflag=FLAGS     read as per the comma separated symbol list
  obs=BYTES       write BYTES bytes at a time (default: 512)
  of=FILE         write to FILE instead of standard output
  oflag=FLAGS     write as per the comma separated symbol list
  seek=N          skip N obs-sized output blocks (also oseek=N)
  skip=N          skip N ibs-sized input blocks (also iseek=N)
  status=LEVEL    LEVEL is none, noxfer, or progress

N and BYTES may be followed by multiplicative suffixes: c=1, w=2, b=512,
kB=1000, K=1024, MB=1000*1000, M=1024*1024, and so on for G, T,
P, E, Z, Y, R, and Q.  Binary prefixes may also use KiB, MiB, and so on.
If N ends in B, count, skip, and seek count bytes rather than blocks.
Numbers may be multiplied using x; xM is equivalent to M.

CONVS:
  ascii     from EBCDIC to ASCII
  ebcdic    from ASCII to EBCDIC
  ibm       from ASCII to alternate EBCDIC
  block     pad newline-terminated records with spaces to cbs-size
  unblock   replace trailing spaces in cbs-size records with newline
  lcase     change upper case to lower case
  ucase     change lower case to upper case
  sparse    seek rather than write NUL output blocks
  swab      swap every pair of input bytes
  sync      pad every input block to ibs-size with NULs, or spaces with block/unblock
  excl      fail if the output file already exists
  nocreat   do not create the output file
  notrunc   do not truncate the output file
  noerror   continue after read errors where the input can be advanced safely
  fdatasync physically write output file data before finishing
  fsync     likewise, but also write metadata

FLAGS:
  append    append mode (output only)
  direct    use direct I/O for data
  directory fail unless a directory
  dsync     use synchronized I/O for data
  sync      likewise, but also for metadata
  fullblock accumulate full input blocks (input only)
  nonblock  use non-blocking I/O
  noatime   do not update access time
  nocache   request that the kernel discard cached data
  noctty    do not assign a controlling terminal from the file
  nofollow  do not follow symbolic links

Sending the USR1 signal to a running dd process makes it print I/O statistics
and then resume copying.

      --help        display this help and exit
      --version     output version information and exit
""";
		return context.StandardOutput.WriteAsync(
			usage.ReplaceLineEndings(
				Environment.NewLine
			).AsMemory(),
			context.CancellationToken
		);
	}

	private static async Task<int> CopyAsync(
		DdOptions options,
		CommandContext context
	) {
		Stream? input = null;
		Stream? output = null;
		var ownsInput = false;
		var ownsOutput = false;
		ByteOutputStream? textOutputAdapter = null;
		PosixSignalRegistration? signalRegistration = null;
		var statistics = new DdStatistics();
		await using var reporter = new DdStatisticsReporter(
			context.StandardError,
			statistics,
			options.Status
		);
		var copyStarted = false;
		var reportWritten = false;
		try {
			(input, ownsInput) = OpenInput(
				options,
				context
			);
			(output, ownsOutput, textOutputAdapter) = OpenOutput(
				options,
				context
			);
			var engine = new DdCopyEngine(
				options,
				input,
				output,
				context,
				statistics,
				reporter
			);
			signalRegistration = TryRegisterSignal(
				engine
			);
			reporter.StartProgress();
			copyStarted = true;
			await engine.CopyAsync().ConfigureAwait( false );
			await FlushPhysicalAsync(
				options,
				output,
				context.CancellationToken
			).ConfigureAwait( false );
			if ( null != textOutputAdapter ) {
				await textOutputAdapter.CompleteAsync(
					context.CancellationToken
				).ConfigureAwait( false );
			}
			await reporter.WriteFinalReportAsync(
				context.CancellationToken
			).ConfigureAwait( false );
			reportWritten = true;
			return CommandExitCodes.Success;
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or NotSupportedException
			or ArgumentException
		) {
			await context.Diagnostics.ErrorAsync(
				exception.Message,
				context.CancellationToken
			).ConfigureAwait( false );
			if (
				copyStarted
				&& !reportWritten
			) {
				await reporter.WriteFinalReportAsync(
					context.CancellationToken
				).ConfigureAwait( false );
			}
			return CommandExitCodes.Failure;
		} finally {
			signalRegistration?.Dispose();
			if (
				ownsOutput
				&& null != output
			) {
				await output.DisposeAsync().ConfigureAwait( false );
			} else if ( null != textOutputAdapter ) {
				await textOutputAdapter.DisposeAsync().ConfigureAwait( false );
			}
			if (
				ownsInput
				&& null != input
			) {
				await input.DisposeAsync().ConfigureAwait( false );
			}
		}
	}

	private static (
		Stream Stream,
		bool OwnsStream
	) OpenInput(
		DdOptions options,
		CommandContext context
	) {
		if ( null == options.InputFile ) {
			if ( null != context.StandardInputStream ) {
				return (
					context.StandardInputStream,
					false
				);
			}
			return (
				new TextReaderStream(
					context.StandardInput,
					leaveOpen: true
				),
				true
			);
		}
		ValidatePathFlags(
			options.InputFile,
			options.InputFlags,
			forOutput: false
		);
		if (
			options.HasInputFlag( DdFlag.Directory )
			&& Directory.Exists( options.InputFile )
		) {
			return (
				new DdDirectoryInputStream( options.InputFile ),
				true
			);
		}
		return (
			new FileStream(
				options.InputFile,
				new FileStreamOptions {
					Access = FileAccess.Read,
					Mode = FileMode.Open,
					Share = FileShare.ReadWrite,
					BufferSize = Math.Min(
						options.InputBlockSize,
						64 * 1024
					),
					Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
				}
			),
			true
		);
	}

	private static (
		Stream Stream,
		bool OwnsStream,
		ByteOutputStream? TextAdapter
	) OpenOutput(
		DdOptions options,
		CommandContext context
	) {
		if ( null == options.OutputFile ) {
			if ( null != context.StandardOutputStream ) {
				return (
					context.StandardOutputStream,
					false,
					null
				);
			}
			var adapter = new ByteOutputStream(
				context.StandardOutput
			);
			return (
				adapter,
				false,
				adapter
			);
		}
		ValidatePathFlags(
			options.OutputFile,
			options.OutputFlags,
			forOutput: true
		);
		var preserveOutput = options.HasConversion( DdConversion.NoTruncate );
		var hasOutputSeek = 0L < options.Seek.Value;
		var mode = options.HasConversion( DdConversion.Exclusive )
			? FileMode.CreateNew
			: options.HasConversion( DdConversion.NoCreate )
				? preserveOutput || hasOutputSeek
					? FileMode.Open
					: FileMode.Truncate
				: preserveOutput || hasOutputSeek
					? FileMode.OpenOrCreate
					: FileMode.Create
		;
		var fileOptions = FileOptions.Asynchronous;
		if (
			options.HasOutputFlag( DdFlag.DataSync )
			|| options.HasOutputFlag( DdFlag.Sync )
		) {
			fileOptions |= FileOptions.WriteThrough;
		}
		var stream = new FileStream(
			options.OutputFile,
			new FileStreamOptions {
				Access = FileAccess.Write,
				Mode = mode,
				Share = FileShare.ReadWrite,
				BufferSize = Math.Min(
					options.OutputBlockSize,
					64 * 1024
				),
				Options = fileOptions,
			}
		);
		return (
			stream,
			true,
			null
		);
	}

	private static async Task FlushPhysicalAsync(
		DdOptions options,
		Stream output,
		CancellationToken cancellationToken
	) {
		await output.FlushAsync(
			cancellationToken
		).ConfigureAwait( false );
		if (
			!options.HasConversion( DdConversion.FDataSync )
			&& !options.HasConversion( DdConversion.FileSystemSync )
		) {
			return;
		}
		if ( output is not FileStream file ) {
			throw new NotSupportedException(
				"physical output synchronization is unavailable for this standard-output stream"
			);
		}
		file.Flush(
			flushToDisk: true
		);
	}

	private static PosixSignalRegistration? TryRegisterSignal(
		DdCopyEngine engine
	) {
		if ( !TryGetSigUsr1( out var signal ) ) {
			return null;
		}
		try {
			return PosixSignalRegistration.Create(
				signal,
				context => {
					context.Cancel = true;
					engine.RequestSignalReport();
				}
			);
		} catch ( PlatformNotSupportedException ) {
			return null;
		} catch ( IOException ) {
			return null;
		}
	}

	private static bool TryGetSigUsr1(
		out PosixSignal signal
	) {
		if ( OperatingSystem.IsLinux() ) {
			signal = (PosixSignal)10;
			return true;
		}
		if (
			OperatingSystem.IsMacOS()
			|| OperatingSystem.IsFreeBSD()
		) {
			signal = (PosixSignal)30;
			return true;
		}
		signal = default;
		return false;
	}

	private static void ValidatePathFlags(
		string path,
		ISet<DdFlag> flags,
		bool forOutput
	) {
		if ( flags.Contains( DdFlag.NoFollow ) ) {
			try {
				FileSystemInfo information = Directory.Exists( path )
					? new DirectoryInfo( path )
					: new FileInfo( path )
				;
				if ( null != information.LinkTarget ) {
					throw new IOException(
						string.Concat(
							"refusing to follow symbolic link '",
							path,
							"'"
						)
					);
				}
			} catch ( FileNotFoundException ) when ( forOutput ) {
			}
		}
		if (
			flags.Contains( DdFlag.Directory )
			&& !Directory.Exists( path )
		) {
			throw new IOException(
				string.Concat(
					"'",
					path,
					"' is not a directory"
				)
			);
		}
	}

	private static async Task<bool> TryParseOperandsAsync(
		IReadOnlyList<string> operands,
		DdOptions options,
		CommandContext context
	) {
		foreach ( var operand in operands ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			var equalsIndex = operand.IndexOf(
				'='
			);
			if ( equalsIndex <= 0 ) {
				await context.Diagnostics.ErrorAsync(
					string.Concat(
						"unrecognized operand '",
						operand,
						"'"
					),
					context.CancellationToken
				).ConfigureAwait( false );
				return false;
			}
			var name = operand.Substring(
				0,
				equalsIndex
			);
			var value = operand.Substring(
				equalsIndex + 1
			);
			if (
				!await ApplyOperandAsync(
					name,
					value,
					options,
					context
				).ConfigureAwait( false )
			) {
				return false;
			}
		}
		options.ApplyBlockSizeOverride();
		return true;
	}

	private static async Task<bool> ApplyOperandAsync(
		string name,
		string value,
		DdOptions options,
		CommandContext context
	) {
		switch ( name ) {
			case "bs": {
					var blockSize = await ParseBlockSizeAsync( value, context ).ConfigureAwait( false );
					if ( !blockSize.HasValue ) {
						return false;
					}
					options.BlockSizeOverride = blockSize.Value;
					return true;
				}
			case "cbs": {
					var conversionBlockSize = await ParseBlockSizeAsync( value, context ).ConfigureAwait( false );
					if ( !conversionBlockSize.HasValue ) {
						return false;
					}
					options.ConversionBlockSize = conversionBlockSize.Value;
					return true;
				}
			case "count": {
					var count = await ParseQuantityAsync( value, context ).ConfigureAwait( false );
					if ( !count.HasValue ) {
						return false;
					}
					options.Count = count.Value;
					return true;
				}
			case "ibs": {
					var inputBlockSize = await ParseBlockSizeAsync( value, context ).ConfigureAwait( false );
					if ( !inputBlockSize.HasValue ) {
						return false;
					}
					options.InputBlockSize = inputBlockSize.Value;
					return true;
				}
			case "if":
				options.InputFile = value;
				return true;
			case "obs": {
					var outputBlockSize = await ParseBlockSizeAsync( value, context ).ConfigureAwait( false );
					if ( !outputBlockSize.HasValue ) {
						return false;
					}
					options.OutputBlockSize = outputBlockSize.Value;
					return true;
				}
			case "of":
				options.OutputFile = value;
				return true;
			case "seek":
			case "oseek": {
					var seek = await ParseQuantityAsync( value, context ).ConfigureAwait( false );
					if ( !seek.HasValue ) {
						return false;
					}
					options.Seek = seek.Value;
					return true;
				}
			case "skip":
			case "iseek": {
					var skip = await ParseQuantityAsync( value, context ).ConfigureAwait( false );
					if ( !skip.HasValue ) {
						return false;
					}
					options.Skip = skip.Value;
					return true;
				}
			case "conv":
				return await AddConversionsAsync(
					value,
					options,
					context
				).ConfigureAwait( false );
			case "iflag":
				return await AddFlagsAsync(
					value,
					options.InputFlags,
					input: true,
					context
				).ConfigureAwait( false );
			case "oflag":
				return await AddFlagsAsync(
					value,
					options.OutputFlags,
					input: false,
					context
				).ConfigureAwait( false );
			case "status":
				return await SetStatusAsync(
					value,
					options,
					context
				).ConfigureAwait( false );
			default:
				await context.Diagnostics.ErrorAsync(
					string.Concat(
						"unrecognized operand '",
						name,
						"'"
					),
					context.CancellationToken
				).ConfigureAwait( false );
				return false;

		}
	}

	private static async Task<int?> ParseBlockSizeAsync(
		string value,
		CommandContext context
	) {
		if (
			DdNumberParser.TryParseBlockSize(
				value,
				out var size,
				out var error
			)
		) {
			return size;
		}
		await context.Diagnostics.ErrorAsync(
			error,
			context.CancellationToken
		).ConfigureAwait( false );
		return null;
	}

	private static async Task<DdQuantity?> ParseQuantityAsync(
		string value,
		CommandContext context
	) {
		if (
			DdNumberParser.TryParseQuantity(
				value,
				out var quantity,
				out var error
			)
		) {
			return quantity;
		}
		await context.Diagnostics.ErrorAsync(
			error,
			context.CancellationToken
		).ConfigureAwait( false );
		return null;
	}

	private static async Task<bool> AddConversionsAsync(
		string value,
		DdOptions options,
		CommandContext context
	) {
		foreach ( var symbol in SplitSymbols( value ) ) {
			var conversion = symbol switch {
				"ascii" => DdConversion.Ascii,
				"ebcdic" => DdConversion.Ebcdic,
				"ibm" => DdConversion.Ibm,
				"block" => DdConversion.Block,
				"unblock" => DdConversion.Unblock,
				"lcase" => DdConversion.LowerCase,
				"ucase" => DdConversion.UpperCase,
				"sparse" => DdConversion.Sparse,
				"swab" => DdConversion.Swab,
				"sync" => DdConversion.Sync,
				"excl" => DdConversion.Exclusive,
				"nocreat" => DdConversion.NoCreate,
				"notrunc" => DdConversion.NoTruncate,
				"noerror" => DdConversion.NoError,
				"fdatasync" => DdConversion.FDataSync,
				"fsync" => DdConversion.FileSystemSync,
				_ => (DdConversion?)null,
			};
			if ( !conversion.HasValue ) {
				await context.Diagnostics.ErrorAsync(
					string.Concat(
						"invalid conversion: '",
						symbol,
						"'"
					),
					context.CancellationToken
				).ConfigureAwait( false );
				return false;
			}
			options.Conversions.Add(
				conversion.Value
			);
		}
		return true;
	}

	private static async Task<bool> AddFlagsAsync(
		string value,
		ISet<DdFlag> output,
		bool input,
		CommandContext context
	) {
		foreach ( var symbol in SplitSymbols( value ) ) {
			var flag = symbol switch {
				"append" => DdFlag.Append,
				"direct" => DdFlag.Direct,
				"directory" => DdFlag.Directory,
				"dsync" => DdFlag.DataSync,
				"sync" => DdFlag.Sync,
				"fullblock" => DdFlag.FullBlock,
				"nonblock" => DdFlag.NonBlock,
				"noatime" => DdFlag.NoAccessTime,
				"nocache" => DdFlag.NoCache,
				"noctty" => DdFlag.NoControllingTerminal,
				"nofollow" => DdFlag.NoFollow,
				_ => (DdFlag?)null,
			};
			if ( !flag.HasValue ) {
				await context.Diagnostics.ErrorAsync(
					string.Concat(
						"invalid flag: '",
						symbol,
						"'"
					),
					context.CancellationToken
				).ConfigureAwait( false );
				return false;
			}
			if (
				(
					input
					&& DdFlag.Append == flag.Value
				)
				|| (
					!input
					&& DdFlag.FullBlock == flag.Value
				)
			) {
				await context.Diagnostics.ErrorAsync(
					string.Concat(
						"invalid flag for ",
						input ? "input" : "output",
						": '",
						symbol,
						"'"
					),
					context.CancellationToken
				).ConfigureAwait( false );
				return false;
			}
			output.Add(
				flag.Value
			);
		}
		return true;
	}

	private static async Task<bool> SetStatusAsync(
		string value,
		DdOptions options,
		CommandContext context
	) {
		var status = value switch {
			"none" => DdStatusMode.None,
			"noxfer" => DdStatusMode.NoTransfer,
			"progress" => DdStatusMode.Progress,
			_ => (DdStatusMode?)null,
		};
		if ( !status.HasValue ) {
			await context.Diagnostics.ErrorAsync(
				string.Concat(
					"invalid status level: '",
					value,
					"'"
				),
				context.CancellationToken
			).ConfigureAwait( false );
			return false;
		}
		options.Status = status.Value;
		return true;
	}

	private static IEnumerable<string> SplitSymbols(
		string value
	) {
		if ( string.IsNullOrEmpty( value ) ) {
			yield return string.Empty;
			yield break;
		}
		foreach (
			var symbol in value.Split(
				',',
				StringSplitOptions.None
			)
		) {
			yield return symbol;
		}
	}

	private static async Task<bool> ValidateOptionsAsync(
		DdOptions options,
		CommandContext context
	) {
		if (
			!await RejectCombinationAsync(
				options,
				context,
				"cannot combine any two of {ascii,ebcdic,ibm}",
				DdConversion.Ascii,
				DdConversion.Ebcdic,
				DdConversion.Ibm
			).ConfigureAwait( false )
			|| !await RejectCombinationAsync(
				options,
				context,
				"cannot combine lcase and ucase",
				DdConversion.LowerCase,
				DdConversion.UpperCase
			).ConfigureAwait( false )
			|| !await RejectCombinationAsync(
				options,
				context,
				"cannot combine excl and nocreat",
				DdConversion.Exclusive,
				DdConversion.NoCreate
			).ConfigureAwait( false )
		) {
			return false;
		}
		if (
			options.UsesBlockConversion
			&& options.UsesUnblockConversion
		) {
			await context.Diagnostics.ErrorAsync(
				"cannot combine block and unblock",
				context.CancellationToken
			).ConfigureAwait( false );
			return false;
		}
		foreach (
			var flag in options.InputFlags.Concat(
				options.OutputFlags
			)
		) {
			if (
				flag is DdFlag.Direct
				or DdFlag.NonBlock
				or DdFlag.NoAccessTime
				or DdFlag.NoCache
			) {
				await context.Diagnostics.ErrorAsync(
					string.Concat(
						"flag '",
						FormatFlag( flag ),
						"' is not supported by the portable .NET file APIs on this platform"
					),
					context.CancellationToken
				).ConfigureAwait( false );
				return false;
			}
		}
		return true;
	}

	private static async Task<bool> RejectCombinationAsync(
		DdOptions options,
		CommandContext context,
		string message,
		params DdConversion[] conversions
	) {
		if (
			conversions.Count(
				options.HasConversion
			) <= 1
		) {
			return true;
		}
		await context.Diagnostics.ErrorAsync(
			message,
			context.CancellationToken
		).ConfigureAwait( false );
		return false;
	}

	private static string FormatFlag(
		DdFlag flag
	) => flag switch {
		DdFlag.Append => "append",
		DdFlag.Direct => "direct",
		DdFlag.Directory => "directory",
		DdFlag.DataSync => "dsync",
		DdFlag.Sync => "sync",
		DdFlag.FullBlock => "fullblock",
		DdFlag.NonBlock => "nonblock",
		DdFlag.NoAccessTime => "noatime",
		DdFlag.NoCache => "nocache",
		DdFlag.NoControllingTerminal => "noctty",
		DdFlag.NoFollow => "nofollow",
		_ => flag.ToString(),
	};

	private static OptionParser CreateParser() => new(
		[
			new OptionDefinition(
				"help",
				null,
				[ "help" ]
			),
			new OptionDefinition(
				"version",
				null,
				[ "version" ]
			),
		],
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute,
		}
	);

	private static async Task<bool> WriteParseErrorsAsync(
		OptionParseResult result,
		CommandContext context
	) {
		if ( result.IsSuccess ) {
			return false;
		}
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format(
					context.ProgramName,
					error
				).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return true;
	}
}

internal sealed class DdDirectoryInputStream : Stream {
	private readonly string myPath;

	public DdDirectoryInputStream(
		string path
	) {
		this.myPath = path;
	}

	public override bool CanRead => true;
	public override bool CanSeek => false;
	public override bool CanWrite => false;
	public override long Length => throw new NotSupportedException();
	public override long Position {
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	public override void Flush() {
	}

	public override int Read(
		byte[] buffer,
		int offset,
		int count
	) => throw this.CreateReadException();

	public override ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) => ValueTask.FromException<int>(
		this.CreateReadException()
	);

	public override long Seek(
		long offset,
		SeekOrigin origin
	) => throw new NotSupportedException();

	public override void SetLength(
		long value
	) => throw new NotSupportedException();

	public override void Write(
		byte[] buffer,
		int offset,
		int count
	) => throw new NotSupportedException();

	private IOException CreateReadException() => new(
		string.Concat(
			"cannot read directory '",
			this.myPath,
			"'"
		)
	);
}