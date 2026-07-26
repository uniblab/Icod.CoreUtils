// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Head;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implements GNU-style <c>head</c>: output the first part of files.
/// </summary>
/// <remarks>
/// <para>
/// The implementation uses TAP and streams input. Positive line and byte
/// counts retain no input beyond the current buffer. Negative line counts
/// retain only the final requested number of records. Negative byte counts
/// use direct seeking for seekable files and a temporary-file spool for
/// non-seekable input.
/// </para>
/// <para>
/// Supported options include <c>-c</c>/<c>--bytes</c>,
/// <c>-n</c>/<c>--lines</c>, <c>-q</c>/<c>--quiet</c>,
/// <c>-v</c>/<c>--verbose</c>, <c>-z</c>/<c>--zero-terminated</c>,
/// <c>--help</c>, and <c>--version</c>.
/// </para>
/// </remarks>
public static class Command {

	#region fields
	private const int BufferSize = 65536;
	private const int MaxBufferedRecords = 65536;
	private const int DefaultCount = 10;
	private const int ErrorExitCode = 1;
	private const int UsageExitCode = 1;
	private const string VersionText = "Icod.CoreUtils.Head 1.0";
	#endregion fields


	#region nested types
	private enum CountKind {
		Lines,
		Bytes
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
				await this.WriteBytesAsync(
					Encoding.UTF8.GetBytes(
						header
					),
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
			if ( null != this.myBinary ) {
				await this.myBinary.WriteAsync(
					buffer,
					offset,
					count,
					cancellationToken
				).ConfigureAwait( false );
				return;
			}

			var characters = new char[
				this.myEncoding.GetMaxCharCount(
					count
				)
			];
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

		public Task WriteBytesAsync(
			byte[] buffer,
			CancellationToken cancellationToken
		) {
			return this.WriteBytesAsync(
				buffer,
				0,
				buffer.Length,
				cancellationToken
			);
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
				} catch ( Exception ex ) when (
					ex is not OperationCanceledException
				) {
					await stderr.WriteLineAsync(
						$"head: {path}: {ex.Message}"
					).ConfigureAwait( false );
					exitCode = ErrorExitCode;
				}
			}

			await output.FlushAsync(
				CountKind.Bytes == options.CountKind,
				cancellationToken
			).ConfigureAwait( false );
			return exitCode;
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

				case "-n":
				case "--lines":
					if ( args.Length <= index + 1 ) {
						stderr.WriteLine(
							"head: option requires an argument -- 'n'"
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
							"head: option requires an argument -- 'c'"
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
					} else {
						stderr.WriteLine(
							$"head: unrecognized option '{argument}'"
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
		var excludeLast = value.StartsWith(
			"-",
			StringComparison.Ordinal
		);
		if (
			excludeLast
			|| value.StartsWith(
				"+",
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
				$"head: invalid number of {( CountKind.Bytes == countKind ? "bytes" : "lines" )}: '{value}'"
			);
			return false;
		}

		options.CountKind = countKind;
		options.Count = count;
		options.ExcludeLast = excludeLast;
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

	#endregion argument methods


	#region processing methods

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

			if ( options.ExcludeLast ) {
				await OutputAllButLastRecordsAsync(
					recordReader,
					output,
					options.Count,
					options.ZeroTerminated,
					cancellationToken
				).ConfigureAwait( false );
			} else {
				await OutputFirstRecordsAsync(
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

	private static async Task OutputFirstRecordsAsync(
		RecordReader reader,
		OutputSink output,
		long count,
		bool zeroTerminated,
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
				break;
			}
			await output.WriteRecordAsync(
				record,
				zeroTerminated,
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task OutputAllButLastRecordsAsync(
		RecordReader reader,
		OutputSink output,
		long discard,
		bool zeroTerminated,
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
				await output.WriteRecordAsync(
					record,
					zeroTerminated,
					cancellationToken
				).ConfigureAwait( false );
			}
		}

		if ( MaxBufferedRecords < discard ) {
			await OutputAllButLastRecordsSpoolingAsync(
				reader,
				output,
				discard,
				zeroTerminated,
				cancellationToken
			).ConfigureAwait( false );
			return;
		}

		var buffer = new Queue<string>(
			(int)discard
		);

		while ( true ) {
			var record = await reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( null == record ) {
				return;
			}

			buffer.Enqueue(
				record
			);
			if ( discard < buffer.Count ) {
				await output.WriteRecordAsync(
					buffer.Dequeue(),
					zeroTerminated,
					cancellationToken
				).ConfigureAwait( false );
			}
		}
	}

	private static async Task OutputAllButLastRecordsSpoolingAsync(
		RecordReader reader,
		OutputSink output,
		long discard,
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

			var outputCount = Math.Max(
				0,
				recordCount - discard
			);
			if ( 0 == outputCount ) {
				return;
			}

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
				await OutputFirstRecordsAsync(
					spoolReader,
					output,
					outputCount,
					zeroTerminated,
					cancellationToken
				).ConfigureAwait( false );
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
			if ( options.ExcludeLast ) {
				await OutputAllButLastBytesAsync(
					stream,
					output,
					options.Count,
					cancellationToken
				).ConfigureAwait( false );
			} else {
				await CopyBytesAsync(
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

	private static async Task OutputAllButLastBytesAsync(
		Stream input,
		OutputSink output,
		long discard,
		CancellationToken cancellationToken
	) {
		if ( input.CanSeek ) {
			var count = Math.Max(
				0,
				input.Length - discard
			);
			input.Seek(
				0,
				SeekOrigin.Begin
			);
			await CopyBytesAsync(
				input,
				output,
				count,
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

				var count = Math.Max(
					0,
					temporary.Length - discard
				);
				temporary.Seek(
					0,
					SeekOrigin.Begin
				);
				await CopyBytesAsync(
					temporary,
					output,
					count,
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			File.Delete(
				temporaryPath
			);
		}
	}

	private static async Task CopyBytesAsync(
		Stream input,
		OutputSink output,
		long count,
		CancellationToken cancellationToken
	) {
		var buffer = new byte[ BufferSize ];
		var remaining = count;

		while ( 0 < remaining ) {
			var requested = (int)Math.Min(
				buffer.Length,
				remaining
			);
			var read = await input.ReadAsync(
				buffer,
				0,
				requested,
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				break;
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

	#endregion processing methods


	#region usage methods

	private static void PrintUsage(
		TextWriter writer
	) {
		writer.WriteLine(
			"Usage: head [OPTION]... [FILE]..."
		);
		writer.WriteLine(
			"Print the first 10 lines of each FILE to standard output."
		);
		writer.WriteLine();
		writer.WriteLine(
			"  -c, --bytes=[-]NUM       print the first NUM bytes;"
		);
		writer.WriteLine(
			"                             with '-', print all but the last NUM bytes"
		);
		writer.WriteLine(
			"  -n, --lines=[-]NUM       print the first NUM lines;"
		);
		writer.WriteLine(
			"                             with '-', print all but the last NUM lines"
		);
		writer.WriteLine(
			"  -q, --quiet, --silent    never print file-name headers"
		);
		writer.WriteLine(
			"  -v, --verbose            always print file-name headers"
		);
		writer.WriteLine(
			"  -z, --zero-terminated    use NUL as the line delimiter"
		);
		writer.WriteLine(
			"      --help               display this help and exit"
		);
		writer.WriteLine(
			"      --version            output version information and exit"
		);
		writer.WriteLine();
		writer.WriteLine(
			"NUM may use b, decimal or binary prefixes through Q/QiB."
		);
	}
	#endregion usage methods

}