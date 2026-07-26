// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Tail;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implements GNU-style <c>tail</c>: output the last part of files and,
/// optionally, follow files as they grow.
/// </summary>
/// <remarks>
/// <para>
/// The implementation uses TAP throughout. Last-N line mode retains only N
/// records. Seekable byte input is read directly from the calculated offset;
/// non-seekable byte input is spooled to a temporary file rather than held
/// wholly in memory. Follow mode uses cancellable asynchronous polling.
/// </para>
/// <para>
/// Supported options include <c>-c</c>/<c>--bytes</c>,
/// <c>-n</c>/<c>--lines</c>, <c>-f</c>/<c>--follow</c>, <c>-F</c>,
/// <c>--retry</c>, <c>--pid</c>, <c>-s</c>/<c>--sleep-interval</c>,
/// <c>--max-unchanged-stats</c>, <c>-q</c>/<c>--quiet</c>,
/// <c>-v</c>/<c>--verbose</c>, <c>-z</c>/<c>--zero-terminated</c>,
/// <c>--debug</c>, <c>--help</c>, and <c>--version</c>.
/// </para>
/// </remarks>
public static class Command {

	#region fields
	private const int BufferSize = 65536;
	private const int MaxBufferedRecords = 65536;
	private const int DefaultCount = 10;
	private const int DefaultMaxUnchangedStats = 5;
	private const int ErrorExitCode = 1;
	private const int UsageExitCode = 1;
	private const string VersionText = "Icod.CoreUtils.Tail 1.0";
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

	private sealed class Options {

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

	private sealed class RecordReader {

		private readonly char[] myBuffer;
		private int myCount;
		private int myIndex;
		private readonly TextReader myReader;
		private readonly char mySeparator;

		public RecordReader(
			TextReader reader,
			char separator
		) {
			this.myReader = reader ?? throw new ArgumentNullException(
				nameof( reader )
			);
			this.mySeparator = separator;
			this.myBuffer = new char[ 4096 ];
		}

		public async Task<string?> ReadAsync(
			CancellationToken cancellationToken
		) {
			if ( '\n' == this.mySeparator ) {
				cancellationToken.ThrowIfCancellationRequested();
				return await this.myReader.ReadLineAsync().ConfigureAwait( false );
			}

			var output = new StringBuilder();
			while ( true ) {
				if ( this.myCount <= this.myIndex ) {
					cancellationToken.ThrowIfCancellationRequested();
					this.myCount = await this.myReader.ReadAsync(
						this.myBuffer,
						0,
						this.myBuffer.Length
					).ConfigureAwait( false );
					this.myIndex = 0;
					if ( 0 == this.myCount ) {
						return 0 == output.Length
							? null
							: output.ToString()
						;
					}
				}

				var start = this.myIndex;
				while (
					this.myIndex < this.myCount
					&& this.mySeparator != this.myBuffer[ this.myIndex ]
				) {
					this.myIndex++;
				}

				output.Append(
					this.myBuffer,
					start,
					this.myIndex - start
				);

				if (
					this.myIndex < this.myCount
					&& this.mySeparator == this.myBuffer[ this.myIndex ]
				) {
					this.myIndex++;
					return output.ToString();
				}
			}
		}

	}

	private sealed class OutputSink {

		private readonly Stream? myBinary;
		private readonly Decoder myDecoder;
		private readonly Encoding myEncoding;
		private readonly TextWriter myText;

		public OutputSink(
			TextWriter text,
			Stream? binary
		) {
			this.myText = text ?? throw new ArgumentNullException(
				nameof( text )
			);
			this.myBinary = binary;
			this.myEncoding = Encoding.UTF8;
			this.myDecoder = this.myEncoding.GetDecoder();
		}

		public async Task WriteHeaderAsync(
			string path,
			bool precedingBlankLine,
			bool binaryMode,
			CancellationToken cancellationToken
		) {
			var header = string.Concat(
				precedingBlankLine
					? Environment.NewLine
					: string.Empty,
				"==> ",
				path,
				" <==",
				Environment.NewLine
			);

			if ( binaryMode ) {
				var bytes = Encoding.UTF8.GetBytes(
					header
				);
				await this.WriteBytesAsync(
					bytes,
					0,
					bytes.Length,
					cancellationToken
				).ConfigureAwait( false );
			} else {
				cancellationToken.ThrowIfCancellationRequested();
				await this.myText.WriteAsync(
					header
				).ConfigureAwait( false );
			}
		}

		public async Task WriteRecordAsync(
			string value,
			bool zeroTerminated,
			CancellationToken cancellationToken
		) {
			cancellationToken.ThrowIfCancellationRequested();
			await this.myText.WriteAsync(
				value
			).ConfigureAwait( false );
			if ( zeroTerminated ) {
				await this.myText.WriteAsync(
					'\0'
				).ConfigureAwait( false );
			} else {
				await this.myText.WriteLineAsync().ConfigureAwait( false );
			}
		}

		public async Task WriteBytesAsync(
			byte[] buffer,
			int offset,
			int count,
			CancellationToken cancellationToken
		) {
			if ( 0 == count ) {
				return;
			}

			if ( null != this.myBinary ) {
				await this.myBinary.WriteAsync(
					buffer,
					offset,
					count,
					cancellationToken
				).ConfigureAwait( false );
				return;
			}

			var maximumCharacters = this.myEncoding.GetMaxCharCount(
				count
			);
			var characters = new char[ maximumCharacters ];
			this.myDecoder.Convert(
				buffer,
				offset,
				count,
				characters,
				0,
				characters.Length,
				flush: false,
				out _,
				out var charactersUsed,
				out _
			);
			cancellationToken.ThrowIfCancellationRequested();
			await this.myText.WriteAsync(
				characters,
				0,
				charactersUsed
			).ConfigureAwait( false );
		}

		public async Task FlushAsync(
			bool binaryMode,
			CancellationToken cancellationToken
		) {
			if (
				binaryMode
				&& null != this.myBinary
			) {
				await this.myBinary.FlushAsync(
					cancellationToken
				).ConfigureAwait( false );
				return;
			}

			if (
				binaryMode
				&& null == this.myBinary
			) {
				var characters = new char[
					this.myEncoding.GetMaxCharCount(
						0
					)
				];
				this.myDecoder.Convert(
					Array.Empty<byte>(),
					0,
					0,
					characters,
					0,
					characters.Length,
					flush: true,
					out _,
					out var charactersUsed,
					out _
				);

				if ( 0 < charactersUsed ) {
					cancellationToken.ThrowIfCancellationRequested();
					await this.myText.WriteAsync(
						characters,
						0,
						charactersUsed
					).ConfigureAwait( false );
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			await this.myText.FlushAsync().ConfigureAwait( false );
		}

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

	#endregion nested types


	#region public methods

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

		try {
			var options = new Options();
			var files = new List<string>();
			var parseResult = ParseArguments(
				args,
				options,
				files,
				stdout,
				stderr
			);
			if ( parseResult.HasValue ) {
				return parseResult.Value;
			}

			if ( 0 == files.Count ) {
				files.Add(
					"-"
				);
			}

			var output = new OutputSink(
				stdout,
				stdoutStream
			);
			var showHeaders = options.Verbose
				|| (
					!options.Quiet
					&& 1 < files.Count
				)
			;
			var wroteHeader = false;
			var exitCode = 0;
			var followStates = new List<FollowState>();

			try {
				foreach ( var path in files ) {
					cancellationToken.ThrowIfCancellationRequested();

					try {
						if ( showHeaders ) {
							await output.WriteHeaderAsync(
								"-" == path
									? "standard input"
									: path,
								wroteHeader,
								CountKind.Bytes == options.CountKind,
								cancellationToken
							).ConfigureAwait( false );
							wroteHeader = true;
						}

						if ( CountKind.Lines == options.CountKind ) {
							await ProcessLinesAsync(
								path,
								stdin,
								output,
								options,
								cancellationToken
							).ConfigureAwait( false );
						} else {
							await ProcessBytesAsync(
								path,
								stdinStream,
								output,
								options,
								cancellationToken
							).ConfigureAwait( false );
						}

						if (
							FollowMode.None != options.FollowMode
							&& "-" != path
						) {
							followStates.Add(
								CreateFollowState(
									path,
									options
								)
							);
						}
					} catch ( Exception ex ) when (
						ex is not OperationCanceledException
					) {
						await stderr.WriteLineAsync(
							$"tail: {path}: {ex.Message}"
						).ConfigureAwait( false );
						exitCode = ErrorExitCode;

						if (
							FollowMode.None != options.FollowMode
							&& options.Retry
							&& "-" != path
						) {
							followStates.Add(
								new FollowState(
									path
								) {
									MissingReported = true
								}
							);
						}
					}
				}

				await output.FlushAsync(
					CountKind.Bytes == options.CountKind,
					cancellationToken
				).ConfigureAwait( false );

				if (
					FollowMode.None != options.FollowMode
					&& 0 < followStates.Count
				) {
					var followResult = await FollowAsync(
						followStates,
						options,
						output,
						stderr,
						showHeaders,
						cancellationToken
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
		} catch ( OperationCanceledException ) {
			return 130;
		}
	}

	#endregion public methods


	#region argument methods

	private static int? ParseArguments(
		string[] args,
		Options options,
		ICollection<string> files,
		TextWriter stdout,
		TextWriter stderr
	) {
		var index = 0;
		while ( index < args.Length ) {
			var argument = args[ index ];
			if ( "--" == argument ) {
				index++;
				break;
			}
			if (
				"-" == argument
				|| !argument.StartsWith(
					"-",
					StringComparison.Ordinal
				)
			) {
				break;
			}

			if (
				1 < argument.Length
				&& '-' == argument[ 0 ]
				&& char.IsDigit(
					argument[ 1 ]
				)
			) {
				if (
					!TrySetCount(
						argument.Substring(
							1
						),
						CountKind.Lines,
						options,
						stderr
					)
				) {
					return UsageExitCode;
				}
				index++;
				continue;
			}

			switch ( argument ) {
				case "-?":
				case "--help":
					PrintUsage(
						stdout
					);
					return 0;

				case "--version":
					stdout.WriteLine(
						VersionText
					);
					return 0;

				case "--debug":
					options.Debug = true;
					index++;
					break;

				case "-q":
				case "--quiet":
				case "--silent":
					options.Quiet = true;
					options.Verbose = false;
					index++;
					break;

				case "-v":
				case "--verbose":
					options.Verbose = true;
					options.Quiet = false;
					index++;
					break;

				case "-z":
				case "--zero-terminated":
					options.ZeroTerminated = true;
					index++;
					break;

				case "-f":
				case "--follow":
					options.FollowMode = FollowMode.Descriptor;
					index++;
					break;

				case "-F":
					options.FollowMode = FollowMode.Name;
					options.Retry = true;
					index++;
					break;

				case "--retry":
					options.Retry = true;
					index++;
					break;

				case "-n":
				case "--lines":
					if ( args.Length <= index + 1 ) {
						stderr.WriteLine(
							"tail: option requires an argument -- 'n'"
						);
						return UsageExitCode;
					}
					if (
						!TrySetCount(
							args[ index + 1 ],
							CountKind.Lines,
							options,
							stderr
						)
					) {
						return UsageExitCode;
					}
					index += 2;
					break;

				case "-c":
				case "--bytes":
					if ( args.Length <= index + 1 ) {
						stderr.WriteLine(
							"tail: option requires an argument -- 'c'"
						);
						return UsageExitCode;
					}
					if (
						!TrySetCount(
							args[ index + 1 ],
							CountKind.Bytes,
							options,
							stderr
						)
					) {
						return UsageExitCode;
					}
					index += 2;
					break;

				case "-s":
				case "--sleep-interval":
					if (
						args.Length <= index + 1
						|| !TryParseInterval(
							args[ index + 1 ],
							out var interval
						)
					) {
						stderr.WriteLine(
							"tail: invalid sleep interval"
						);
						return UsageExitCode;
					}
					options.SleepInterval = interval;
					index += 2;
					break;

				default:
					if (
						TryGetAttachedValue(
							argument,
							"-n",
							"--lines=",
							out var lineValue
						)
					) {
						if (
							!TrySetCount(
								lineValue,
								CountKind.Lines,
								options,
								stderr
							)
						) {
							return UsageExitCode;
						}
						index++;
					} else if (
						TryGetAttachedValue(
							argument,
							"-c",
							"--bytes=",
							out var byteValue
						)
					) {
						if (
							!TrySetCount(
								byteValue,
								CountKind.Bytes,
								options,
								stderr
							)
						) {
							return UsageExitCode;
						}
						index++;
					} else if (
						argument.StartsWith(
							"--follow=",
							StringComparison.Ordinal
						)
					) {
						var mode = argument.Substring(
							"--follow=".Length
						);
						if ( "name" == mode ) {
							options.FollowMode = FollowMode.Name;
						} else if ( "descriptor" == mode ) {
							options.FollowMode = FollowMode.Descriptor;
						} else {
							stderr.WriteLine(
								$"tail: invalid follow mode '{mode}'"
							);
							return UsageExitCode;
						}
						index++;
					} else if (
						argument.StartsWith(
							"-s",
							StringComparison.Ordinal
						)
						&& 2 < argument.Length
						&& TryParseInterval(
							argument.Substring(
								2
							),
							out var shortInlineInterval
						)
					) {
						options.SleepInterval = shortInlineInterval;
						index++;
					} else if (
						argument.StartsWith(
							"--sleep-interval=",
							StringComparison.Ordinal
						)
						&& TryParseInterval(
							argument.Substring(
								"--sleep-interval=".Length
							),
							out var inlineInterval
						)
					) {
						options.SleepInterval = inlineInterval;
						index++;
					} else if (
						argument.StartsWith(
							"--max-unchanged-stats=",
							StringComparison.Ordinal
						)
						&& int.TryParse(
							argument.Substring(
								"--max-unchanged-stats=".Length
							),
							NumberStyles.None,
							CultureInfo.InvariantCulture,
							out var maximumStats
						)
						&& 0 < maximumStats
					) {
						options.MaxUnchangedStats = maximumStats;
						index++;
					} else if (
						argument.StartsWith(
							"--pid=",
							StringComparison.Ordinal
						)
						&& int.TryParse(
							argument.Substring(
								"--pid=".Length
							),
							NumberStyles.None,
							CultureInfo.InvariantCulture,
							out var processId
						)
						&& 0 < processId
					) {
						options.ProcessIds.Add(
							processId
						);
						index++;
					} else {
						stderr.WriteLine(
							$"tail: unrecognized option '{argument}'"
						);
						return UsageExitCode;
					}
					break;
			}
		}

		while ( index < args.Length ) {
			files.Add(
				args[ index ]
			);
			index++;
		}

		if (
			options.Retry
			&& FollowMode.None == options.FollowMode
		) {
			stderr.WriteLine(
				"tail: warning: --retry is useful only when following"
			);
		}

		return null;
	}

	private static bool TryGetAttachedValue(
		string argument,
		string shortName,
		string longPrefix,
		out string value
	) {
		if (
			argument.StartsWith(
				longPrefix,
				StringComparison.Ordinal
			)
		) {
			value = argument.Substring(
				longPrefix.Length
			);
			return true;
		}

		if (
			argument.StartsWith(
				shortName,
				StringComparison.Ordinal
			)
			&& shortName.Length < argument.Length
		) {
			value = argument.Substring(
				shortName.Length
			);
			if (
				value.StartsWith(
					"=",
					StringComparison.Ordinal
				)
			) {
				value = value.Substring(
					1
				);
			}
			return true;
		}

		value = string.Empty;
		return false;
	}

	private static bool TrySetCount(
		string value,
		CountKind countKind,
		Options options,
		TextWriter stderr
	) {
		var startAt = value.StartsWith(
			"+",
			StringComparison.Ordinal
		);
		if (
			startAt
			|| value.StartsWith(
				"-",
				StringComparison.Ordinal
			)
		) {
			value = value.Substring(
				1
			);
		}

		if (
			!TryParseCount(
				value,
				out var count
			)
		) {
			stderr.WriteLine(
				$"tail: invalid number of {( CountKind.Bytes == countKind ? "bytes" : "lines" )}: '{value}'"
			);
			return false;
		}

		options.CountKind = countKind;
		options.Count = count;
		options.StartAt = startAt;
		return true;
	}

	private static bool TryParseCount(
		string value,
		out long count
	) {
		count = 0;
		if ( string.IsNullOrEmpty( value ) ) {
			return false;
		}

		var index = 0;
		while (
			index < value.Length
			&& char.IsDigit(
				value[ index ]
			)
		) {
			index++;
		}
		if (
			0 == index
			|| !BigInteger.TryParse(
				value.Substring(
					0,
					index
				),
				NumberStyles.None,
				CultureInfo.InvariantCulture,
				out var number
			)
		) {
			return false;
		}

		if (
			!TryGetMultiplier(
				value.Substring(
					index
				),
				out var multiplier
			)
		) {
			return false;
		}

		var result = number * multiplier;
		count = long.MaxValue < result
			? long.MaxValue
			: (long)result
		;
		return true;
	}

	private static bool TryGetMultiplier(
		string suffix,
		out BigInteger multiplier
	) {
		switch ( suffix ) {
			case "":
				multiplier = BigInteger.One;
				return true;
			case "b":
				multiplier = new BigInteger( 512 );
				return true;
			case "kB":
				multiplier = BigInteger.Pow( 1000, 1 );
				return true;
			case "K":
			case "KiB":
				multiplier = BigInteger.One << 10;
				return true;
			case "MB":
				multiplier = BigInteger.Pow( 1000, 2 );
				return true;
			case "M":
			case "MiB":
				multiplier = BigInteger.One << 20;
				return true;
			case "GB":
				multiplier = BigInteger.Pow( 1000, 3 );
				return true;
			case "G":
			case "GiB":
				multiplier = BigInteger.One << 30;
				return true;
			case "TB":
				multiplier = BigInteger.Pow( 1000, 4 );
				return true;
			case "T":
			case "TiB":
				multiplier = BigInteger.One << 40;
				return true;
			case "PB":
				multiplier = BigInteger.Pow( 1000, 5 );
				return true;
			case "P":
			case "PiB":
				multiplier = BigInteger.One << 50;
				return true;
			case "EB":
				multiplier = BigInteger.Pow( 1000, 6 );
				return true;
			case "E":
			case "EiB":
				multiplier = BigInteger.One << 60;
				return true;
			case "ZB":
				multiplier = BigInteger.Pow( 1000, 7 );
				return true;
			case "Z":
			case "ZiB":
				multiplier = BigInteger.One << 70;
				return true;
			case "YB":
				multiplier = BigInteger.Pow( 1000, 8 );
				return true;
			case "Y":
			case "YiB":
				multiplier = BigInteger.One << 80;
				return true;
			case "RB":
				multiplier = BigInteger.Pow( 1000, 9 );
				return true;
			case "R":
			case "RiB":
				multiplier = BigInteger.One << 90;
				return true;
			case "QB":
				multiplier = BigInteger.Pow( 1000, 10 );
				return true;
			case "Q":
			case "QiB":
				multiplier = BigInteger.One << 100;
				return true;
			default:
				multiplier = BigInteger.Zero;
				return false;
		}
	}

	private static bool TryParseInterval(
		string value,
		out double interval
	) {
		return (
			double.TryParse(
				value,
				NumberStyles.AllowDecimalPoint,
				CultureInfo.InvariantCulture,
				out interval
			)
			&& 0 <= interval
		);
	}

	#endregion argument methods


	#region initial output methods

	private static async Task ProcessLinesAsync(
		string path,
		TextReader standardInput,
		OutputSink output,
		Options options,
		CancellationToken cancellationToken
	) {
		TextReader reader;
		var ownsReader = false;

		if ( "-" == path ) {
			reader = standardInput;
		} else {
			reader = new StreamReader(
				new FileStream(
					path,
					FileMode.Open,
					FileAccess.Read,
					FileShare.ReadWrite | FileShare.Delete,
					BufferSize,
					FileOptions.Asynchronous | FileOptions.SequentialScan
				),
				Encoding.UTF8,
				detectEncodingFromByteOrderMarks: true
			);
			ownsReader = true;
		}

		try {
			var recordReader = new RecordReader(
				reader,
				options.ZeroTerminated
					? '\0'
					: '\n'
			);

			if ( options.StartAt ) {
				await OutputStartingAtRecordAsync(
					recordReader,
					output,
					options.Count,
					options.ZeroTerminated,
					cancellationToken
				).ConfigureAwait( false );
			} else {
				await OutputLastRecordsAsync(
					recordReader,
					output,
					options.Count,
					options.ZeroTerminated,
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			if ( ownsReader ) {
				reader.Dispose();
			}
		}
	}

	private static async Task OutputStartingAtRecordAsync(
		RecordReader reader,
		OutputSink output,
		long firstRecord,
		bool zeroTerminated,
		CancellationToken cancellationToken
	) {
		var lineNumber = 1L;
		while ( true ) {
			var record = await reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( null == record ) {
				return;
			}

			if ( firstRecord <= lineNumber ) {
				await output.WriteRecordAsync(
					record,
					zeroTerminated,
					cancellationToken
				).ConfigureAwait( false );
			}
			lineNumber++;
		}
	}

	private static async Task OutputLastRecordsAsync(
		RecordReader reader,
		OutputSink output,
		long count,
		bool zeroTerminated,
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
				zeroTerminated,
				cancellationToken
			).ConfigureAwait( false );
			return;
		}

		var buffer = new Queue<string>(
			(int)count
		);
		while ( true ) {
			var record = await reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( null == record ) {
				break;
			}

			buffer.Enqueue(
				record
			);
			if ( count < buffer.Count ) {
				buffer.Dequeue();
			}
		}

		foreach ( var record in buffer ) {
			await output.WriteRecordAsync(
				record,
				zeroTerminated,
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task OutputLastRecordsSpoolingAsync(
		RecordReader reader,
		OutputSink output,
		long count,
		bool zeroTerminated,
		CancellationToken cancellationToken
	) {
		var temporaryPath = Path.GetTempFileName();
		long recordCount = 0;

		try {
			using ( var stream = new FileStream(
				temporaryPath,
				FileMode.Create,
				FileAccess.Write,
				FileShare.None,
				BufferSize,
				FileOptions.Asynchronous | FileOptions.SequentialScan
			) )
			using ( var writer = new StreamWriter(
				stream,
				new UTF8Encoding(
					encoderShouldEmitUTF8Identifier: false
				)
			) ) {
				while ( true ) {
					var record = await reader.ReadAsync(
						cancellationToken
					).ConfigureAwait( false );
					if ( null == record ) {
						break;
					}

					await writer.WriteAsync(
						record
					).ConfigureAwait( false );
					await writer.WriteAsync(
						zeroTerminated
							? '\0'
							: '\n'
					).ConfigureAwait( false );
					recordCount++;
				}
				await writer.FlushAsync().ConfigureAwait( false );
			}

			var skip = Math.Max(
				0,
				recordCount - count
			);

			using ( var stream = new FileStream(
				temporaryPath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read,
				BufferSize,
				FileOptions.Asynchronous | FileOptions.SequentialScan
			) )
			using ( var textReader = new StreamReader(
				stream,
				Encoding.UTF8,
				detectEncodingFromByteOrderMarks: true
			) ) {
				var spoolReader = new RecordReader(
					textReader,
					zeroTerminated
						? '\0'
						: '\n'
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
					await output.WriteRecordAsync(
						record,
						zeroTerminated,
						cancellationToken
					).ConfigureAwait( false );
				}
			}
		} finally {
			File.Delete(
				temporaryPath
			);
		}
	}

	private static async Task ProcessBytesAsync(
		string path,
		Stream? standardInput,
		OutputSink output,
		Options options,
		CancellationToken cancellationToken
	) {
		Stream stream;
		var ownsStream = false;

		if ( "-" == path ) {
			stream = standardInput ?? throw new InvalidOperationException(
				"Byte mode on standard input requires a binary input stream."
			);
		} else {
			stream = new FileStream(
				path,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete,
				BufferSize,
				FileOptions.Asynchronous | FileOptions.SequentialScan
			);
			ownsStream = true;
		}

		try {
			if ( options.StartAt ) {
				await SkipBytesAsync(
					stream,
					Math.Max(
						0,
						options.Count - 1
					),
					cancellationToken
				).ConfigureAwait( false );
				await CopyToEndAsync(
					stream,
					output,
					cancellationToken
				).ConfigureAwait( false );
			} else {
				await OutputLastBytesAsync(
					stream,
					output,
					options.Count,
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			if ( ownsStream ) {
				stream.Dispose();
			}
		}
	}

	private static async Task OutputLastBytesAsync(
		Stream input,
		OutputSink output,
		long count,
		CancellationToken cancellationToken
	) {
		if ( input.CanSeek ) {
			input.Seek(
				Math.Max(
					0,
					input.Length - count
				),
				SeekOrigin.Begin
			);
			await CopyToEndAsync(
				input,
				output,
				cancellationToken
			).ConfigureAwait( false );
			return;
		}

		var temporaryPath = Path.GetTempFileName();
		try {
			using ( var temporary = new FileStream(
				temporaryPath,
				FileMode.Create,
				FileAccess.ReadWrite,
				FileShare.None,
				BufferSize,
				FileOptions.Asynchronous | FileOptions.SequentialScan
			) ) {
				await input.CopyToAsync(
					temporary,
					BufferSize,
					cancellationToken
				).ConfigureAwait( false );
				temporary.Seek(
					Math.Max(
						0,
						temporary.Length - count
					),
					SeekOrigin.Begin
				);
				await CopyToEndAsync(
					temporary,
					output,
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			File.Delete(
				temporaryPath
			);
		}
	}

	private static async Task SkipBytesAsync(
		Stream input,
		long count,
		CancellationToken cancellationToken
	) {
		if ( input.CanSeek ) {
			input.Seek(
				Math.Min(
					input.Length,
					count
				),
				SeekOrigin.Begin
			);
			return;
		}

		var buffer = new byte[ BufferSize ];
		var remaining = count;
		while ( 0 < remaining ) {
			var read = await input.ReadAsync(
				buffer,
				0,
				(int)Math.Min(
					buffer.Length,
					remaining
				),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				return;
			}
			remaining -= read;
		}
	}

	private static async Task CopyToEndAsync(
		Stream input,
		OutputSink output,
		CancellationToken cancellationToken
	) {
		var buffer = new byte[ BufferSize ];
		while ( true ) {
			var read = await input.ReadAsync(
				buffer,
				0,
				buffer.Length,
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				return;
			}
			await output.WriteBytesAsync(
				buffer,
				0,
				read,
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	#endregion initial output methods


	#region follow methods

	private static FollowState CreateFollowState(
		string path,
		Options options
	) {
		var state = new FollowState(
			path
		);
		var info = new FileInfo(
			path
		);
		info.Refresh();
		state.Position = info.Exists
			? info.Length
			: 0
		;
		state.CreationTimeUtc = info.Exists
			? info.CreationTimeUtc
			: DateTime.MinValue
		;

		if (
			FollowMode.Descriptor == options.FollowMode
			&& info.Exists
		) {
			state.Descriptor = OpenFollowStream(
				path
			);
			state.Descriptor.Seek(
				state.Position,
				SeekOrigin.Begin
			);
		}

		return state;
	}

	private static async Task<int> FollowAsync(
		IReadOnlyList<FollowState> states,
		Options options,
		OutputSink output,
		TextWriter stderr,
		bool showHeaders,
		CancellationToken cancellationToken
	) {
		if ( options.Debug ) {
			await stderr.WriteLineAsync(
				$"tail: using asynchronous polling follow mode ({options.FollowMode.ToString().ToLowerInvariant()})"
			).ConfigureAwait( false );
		}

		string? lastOutputPath = showHeaders
			? states.LastOrDefault(
				state => state.Active
			)?.Path
			: null
		;
		var delay = TimeSpan.FromSeconds(
			options.SleepInterval
		);

		while ( true ) {
			cancellationToken.ThrowIfCancellationRequested();

			if (
				states.All(
					state => !state.Active
				)
			) {
				return ErrorExitCode;
			}

			if (
				0 < options.ProcessIds.Count
				&& options.ProcessIds.All(
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

				var wrote = FollowMode.Descriptor == options.FollowMode
					? await PollDescriptorAsync(
						state,
						options,
						output,
						stderr,
						showHeaders,
						lastOutputPath,
						cancellationToken
					).ConfigureAwait( false )
					: await PollNameAsync(
						state,
						options,
						output,
						stderr,
						showHeaders,
						lastOutputPath,
						cancellationToken
					).ConfigureAwait( false )
				;

				if ( wrote ) {
					lastOutputPath = state.Path;
				}
			}

			await output.FlushAsync(
				binaryMode: true,
				cancellationToken
			).ConfigureAwait( false );

			await Task.Delay(
				delay,
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task<bool> PollDescriptorAsync(
		FollowState state,
		Options options,
		OutputSink output,
		TextWriter stderr,
		bool showHeaders,
		string? lastOutputPath,
		CancellationToken cancellationToken
	) {
		if ( null == state.Descriptor ) {
			try {
				state.Descriptor = OpenFollowStream(
					state.Path
				);
				state.Position = 0;
				state.MissingReported = false;
				await stderr.WriteLineAsync(
					$"tail: '{state.Path}' has become accessible"
				).ConfigureAwait( false );
			} catch ( Exception ex ) when (
				ex is not OperationCanceledException
			) {
				if ( !state.MissingReported ) {
					await stderr.WriteLineAsync(
						$"tail: cannot open '{state.Path}': {ex.Message}"
					).ConfigureAwait( false );
					state.MissingReported = true;
				}
				if ( !options.Retry ) {
					state.Active = false;
				}
				return false;
			}
		}

		var stream = state.Descriptor;
		var length = stream.Length;
		if ( length < state.Position ) {
			await stderr.WriteLineAsync(
				$"tail: {state.Path}: file truncated"
			).ConfigureAwait( false );
			state.Position = 0;
		}

		if ( length <= state.Position ) {
			return false;
		}

		if (
			showHeaders
			&& !string.Equals(
				lastOutputPath,
				state.Path,
				StringComparison.Ordinal
			)
		) {
			await output.WriteHeaderAsync(
				state.Path,
				null != lastOutputPath,
				binaryMode: true,
				cancellationToken
			).ConfigureAwait( false );
		}

		stream.Seek(
			state.Position,
			SeekOrigin.Begin
		);
		await CopyAvailableAsync(
			stream,
			output,
			length - state.Position,
			cancellationToken
		).ConfigureAwait( false );
		state.Position = length;
		return true;
	}

	private static async Task<bool> PollNameAsync(
		FollowState state,
		Options options,
		OutputSink output,
		TextWriter stderr,
		bool showHeaders,
		string? lastOutputPath,
		CancellationToken cancellationToken
	) {
		var info = new FileInfo(
			state.Path
		);
		info.Refresh();

		if ( !info.Exists ) {
			if ( !state.MissingReported ) {
				await stderr.WriteLineAsync(
					$"tail: '{state.Path}' has become inaccessible"
				).ConfigureAwait( false );
				state.MissingReported = true;
			}
			if ( !options.Retry ) {
				state.Active = false;
			}
			return false;
		}

		if ( state.MissingReported ) {
			await stderr.WriteLineAsync(
				$"tail: '{state.Path}' has appeared; following new file"
			).ConfigureAwait( false );
			state.MissingReported = false;
			state.Position = 0;
			state.CreationTimeUtc = info.CreationTimeUtc;
		}

		state.UnchangedIterations++;
		var replacementCheck = options.MaxUnchangedStats <= state.UnchangedIterations;
		if ( replacementCheck ) {
			state.UnchangedIterations = 0;
			if (
				DateTime.MinValue != state.CreationTimeUtc
				&& state.CreationTimeUtc != info.CreationTimeUtc
			) {
				await stderr.WriteLineAsync(
					$"tail: '{state.Path}' has been replaced; following new file"
				).ConfigureAwait( false );
				state.Position = 0;
			}
			state.CreationTimeUtc = info.CreationTimeUtc;
		}

		if ( info.Length < state.Position ) {
			await stderr.WriteLineAsync(
				$"tail: {state.Path}: file truncated"
			).ConfigureAwait( false );
			state.Position = 0;
		}

		if ( info.Length <= state.Position ) {
			return false;
		}

		if (
			showHeaders
			&& !string.Equals(
				lastOutputPath,
				state.Path,
				StringComparison.Ordinal
			)
		) {
			await output.WriteHeaderAsync(
				state.Path,
				null != lastOutputPath,
				binaryMode: true,
				cancellationToken
			).ConfigureAwait( false );
		}

		using ( var stream = OpenFollowStream(
			state.Path
		) ) {
			stream.Seek(
				state.Position,
				SeekOrigin.Begin
			);
			await CopyAvailableAsync(
				stream,
				output,
				info.Length - state.Position,
				cancellationToken
			).ConfigureAwait( false );
		}
		state.Position = info.Length;
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
			BufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
	}

	private static async Task CopyAvailableAsync(
		Stream input,
		OutputSink output,
		long count,
		CancellationToken cancellationToken
	) {
		var buffer = new byte[ BufferSize ];
		var remaining = count;

		while ( 0 < remaining ) {
			var read = await input.ReadAsync(
				buffer,
				0,
				(int)Math.Min(
					buffer.Length,
					remaining
				),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				return;
			}
			await output.WriteBytesAsync(
				buffer,
				0,
				read,
				cancellationToken
			).ConfigureAwait( false );
			remaining -= read;
		}
	}

	private static bool IsProcessAlive(
		int processId
	) {
		try {
			using ( var process = Process.GetProcessById(
				processId
			) ) {
				return !process.HasExited;
			}
		} catch ( ArgumentException ) {
			return false;
		} catch ( InvalidOperationException ) {
			return false;
		}
	}

	#endregion follow methods


	#region usage methods

	private static void PrintUsage(
		TextWriter writer
	) {
		writer.WriteLine(
			"Usage: tail [OPTION]... [FILE]..."
		);
		writer.WriteLine(
			"Print the last 10 lines of each FILE to standard output."
		);
		writer.WriteLine();
		writer.WriteLine(
			"  -c, --bytes=[+]NUM           output the last NUM bytes;"
		);
		writer.WriteLine(
			"                                 with '+', start with byte NUM"
		);
		writer.WriteLine(
			"  -n, --lines=[+]NUM           output the last NUM lines;"
		);
		writer.WriteLine(
			"                                 with '+', start with line NUM"
		);
		writer.WriteLine(
			"  -f, --follow[=descriptor]    output appended data as files grow"
		);
		writer.WriteLine(
			"      --follow=name            follow file names across replacement"
		);
		writer.WriteLine(
			"  -F                           same as --follow=name --retry"
		);
		writer.WriteLine(
			"      --retry                  keep trying inaccessible files"
		);
		writer.WriteLine(
			"  -s, --sleep-interval=N       poll approximately every N seconds"
		);
		writer.WriteLine(
			"      --max-unchanged-stats=N  recheck followed names after N polls"
		);
		writer.WriteLine(
			"      --pid=PID                stop after all specified PIDs exit"
		);
		writer.WriteLine(
			"  -q, --quiet, --silent        never print file-name headers"
		);
		writer.WriteLine(
			"  -v, --verbose                always print file-name headers"
		);
		writer.WriteLine(
			"  -z, --zero-terminated        use NUL as the line delimiter"
		);
		writer.WriteLine(
			"      --debug                  describe the follow implementation"
		);
		writer.WriteLine(
			"      --help                   display this help and exit"
		);
		writer.WriteLine(
			"      --version                output version information and exit"
		);
		writer.WriteLine();
		writer.WriteLine(
			"NUM may use b, decimal or binary prefixes through Q/QiB."
		);
	}

	#endregion usage methods

}