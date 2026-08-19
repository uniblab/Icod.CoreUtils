// Original behavior/reference: GNU coreutils csplit 9.11
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.CSplit;

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Implements GNU-compatible pattern-directed byte-preserving file splitting.</summary>
public static class Command {
	private const string VersionText = "csplit (Icod.CoreUtils) 1.0";
	private const int BufferSize = 64 * 1024;
	private const int IndexRecordSize = sizeof( long ) * 2;


	private sealed class CsplitUsageException : Exception {
		public CsplitUsageException( string message )
			: base( message ) {
		}
	}

	private sealed class CsplitOptions {
		public string InputPath { get; set; } = string.Empty;
		public string Prefix { get; set; } = "xx";
		public int Digits { get; set; } = 2;
		public bool KeepFiles { get; set; }
		public bool SuppressMatched { get; set; }
		public bool Quiet { get; set; }
		public bool ElideEmptyFiles { get; set; }
		public SuffixFormatter? SuffixFormatter { get; set; }
		public List<SplitControl> Controls { get; } = new();
		public List<string> Warnings { get; } = new();
	}

	private abstract class SplitControl {
		public string SourceText { get; }
		public long RepeatCount { get; set; }
		public bool RepeatForever { get; set; }

		public SplitControl( string sourceText ) {
			this.SourceText = sourceText;
		}
	}

	private sealed class NumericControl : SplitControl {
		public long LineNumber { get; }

		public NumericControl( string sourceText, long lineNumber )
			: base( sourceText ) {
			this.LineNumber = lineNumber;
		}
	}

	private sealed class RegexControl : SplitControl {
		public bool Ignore { get; }
		public long Offset { get; }
		public ICompiledRegularExpression Expression { get; }

		public RegexControl(
			string sourceText,
			bool ignore,
			long offset,
			ICompiledRegularExpression expression
		) : base( sourceText ) {
			this.Ignore = ignore;
			this.Offset = offset;
			this.Expression = expression;
		}
	}

	private readonly record struct LineExtent( long Offset, long Length );

	private sealed class IndexedInput : IAsyncDisposable {
		private readonly TemporarySpool data;
		private readonly TemporarySpool index;

		public long DataLength { get; private set; }
		public long LineCount { get; private set; }

		public IndexedInput( TemporarySpool data, TemporarySpool index ) {
			this.data = data;
			this.index = index;
		}

		public static async Task<IndexedInput> CreateAsync(
			Stream source,
			CancellationToken cancellationToken
		) {
			ArgumentNullException.ThrowIfNull( source );
			var data = TemporarySpool.Create();
			var index = TemporarySpool.Create();
			var result = new IndexedInput( data, index );
			try {
				await result.BuildAsync( source, cancellationToken ).ConfigureAwait( false );
				return result;
			} catch {
				try {
					await result.DisposeAsync().ConfigureAwait( false );
				} catch {
					// Temporary-spool cleanup must not replace the original failure.
				}
				throw;
			}
		}

		public async ValueTask DisposeAsync() {
			try {
				await this.index.DisposeAsync().ConfigureAwait( false );
			} finally {
				await this.data.DisposeAsync().ConfigureAwait( false );
			}
		}

		public async Task<LineExtent> GetExtentAsync(
			long lineNumber,
			CancellationToken cancellationToken
		) {
			if ( lineNumber < 1 || lineNumber > this.LineCount ) {
				throw new ArgumentOutOfRangeException( nameof( lineNumber ) );
			}
			var position = checked( ( lineNumber - 1 ) * IndexRecordSize );
			this.index.Stream.Seek( position, SeekOrigin.Begin );
			var buffer = new byte[IndexRecordSize];
			await ReadExactlyAsync( this.index.Stream, buffer, cancellationToken ).ConfigureAwait( false );
			return new LineExtent(
				BinaryPrimitives.ReadInt64LittleEndian( buffer.AsSpan( 0, sizeof( long ) ) ),
				BinaryPrimitives.ReadInt64LittleEndian( buffer.AsSpan( sizeof( long ), sizeof( long ) ) )
			);
		}

		public async Task<byte[]> ReadMatchLineAsync(
			long lineNumber,
			CancellationToken cancellationToken
		) {
			var extent = await this.GetExtentAsync( lineNumber, cancellationToken ).ConfigureAwait( false );
			if ( extent.Length > int.MaxValue ) {
				throw new IOException( "input line is too large to match" );
			}
			var buffer = new byte[checked( (int)extent.Length )];
			this.data.Stream.Seek( extent.Offset, SeekOrigin.Begin );
			await ReadExactlyAsync( this.data.Stream, buffer, cancellationToken ).ConfigureAwait( false );
			if ( 0 < buffer.Length && (byte)'\n' == buffer[^1] ) {
				Array.Resize( ref buffer, buffer.Length - 1 );
			}
			return buffer;
		}

		public async Task<long> CopyLinesAsync(
			long firstLine,
			long lastLine,
			Stream destination,
			CancellationToken cancellationToken
		) {
			ArgumentNullException.ThrowIfNull( destination );
			if ( firstLine < 1 || lastLine < firstLine || lastLine > this.LineCount + 1 ) {
				throw new ArgumentOutOfRangeException( nameof( firstLine ) );
			}
			if ( firstLine == lastLine ) {
				return 0;
			}
			var start = ( await this.GetExtentAsync( firstLine, cancellationToken ).ConfigureAwait( false ) ).Offset;
			var end = lastLine == this.LineCount + 1
				? this.DataLength
				: ( await this.GetExtentAsync( lastLine, cancellationToken ).ConfigureAwait( false ) ).Offset;
			var remaining = checked( end - start );
			this.data.Stream.Seek( start, SeekOrigin.Begin );
			var buffer = new byte[BufferSize];
			while ( 0 < remaining ) {
				cancellationToken.ThrowIfCancellationRequested();
				var wanted = (int)Math.Min( buffer.Length, remaining );
				var read = await this.data.Stream.ReadAsync(
					buffer.AsMemory( 0, wanted ),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					throw new EndOfStreamException( "unexpected end of temporary input spool" );
				}
				await destination.WriteAsync(
					buffer.AsMemory( 0, read ),
					cancellationToken
				).ConfigureAwait( false );
				remaining -= read;
			}
			return checked( end - start );
		}

		public async Task<long> GetRangeLengthAsync(
			long firstLine,
			long lastLine,
			CancellationToken cancellationToken
		) {
			if ( firstLine < 1 || lastLine < firstLine || lastLine > this.LineCount + 1 ) {
				throw new ArgumentOutOfRangeException( nameof( firstLine ) );
			}
			if ( firstLine == lastLine ) {
				return 0;
			}
			var start = ( await this.GetExtentAsync( firstLine, cancellationToken ).ConfigureAwait( false ) ).Offset;
			var end = lastLine == this.LineCount + 1
				? this.DataLength
				: ( await this.GetExtentAsync( lastLine, cancellationToken ).ConfigureAwait( false ) ).Offset;
			return checked( end - start );
		}

		public async Task BuildAsync( Stream source, CancellationToken cancellationToken ) {
			var buffer = new byte[BufferSize];
			long lineStart = 0;
			while ( true ) {
				cancellationToken.ThrowIfCancellationRequested();
				var read = await source.ReadAsync( buffer.AsMemory(), cancellationToken ).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}
				await this.data.Stream.WriteAsync(
					buffer.AsMemory( 0, read ),
					cancellationToken
				).ConfigureAwait( false );
				for ( var indexInBuffer = 0; indexInBuffer < read; indexInBuffer++ ) {
					this.DataLength = checked( this.DataLength + 1 );
					if ( (byte)'\n' == buffer[indexInBuffer] ) {
						await this.WriteExtentAsync(
							new LineExtent( lineStart, this.DataLength - lineStart ),
							cancellationToken
						).ConfigureAwait( false );
						lineStart = this.DataLength;
					}
				}
			}
			if ( lineStart < this.DataLength ) {
				await this.WriteExtentAsync(
					new LineExtent( lineStart, this.DataLength - lineStart ),
					cancellationToken
				).ConfigureAwait( false );
			}
			await this.data.Stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
			await this.index.Stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
		}

		public async Task WriteExtentAsync(
			LineExtent extent,
			CancellationToken cancellationToken
		) {
			var buffer = new byte[IndexRecordSize];
			BinaryPrimitives.WriteInt64LittleEndian( buffer.AsSpan( 0, sizeof( long ) ), extent.Offset );
			BinaryPrimitives.WriteInt64LittleEndian( buffer.AsSpan( sizeof( long ), sizeof( long ) ), extent.Length );
			await this.index.Stream.WriteAsync( buffer.AsMemory(), cancellationToken ).ConfigureAwait( false );
			this.LineCount = checked( this.LineCount + 1 );
		}
	}

	private sealed class SuffixFormatter {
		private readonly string after;
		private readonly bool alternate;
		private readonly char conversion;
		private readonly int? precision;
		private readonly string before;
		private readonly bool leftAligned;
		private readonly bool thousands;
		private readonly int width;
		private readonly bool zeroPadded;

		public SuffixFormatter(
			string before,
			string after,
			char conversion,
			bool leftAligned,
			bool zeroPadded,
			bool thousands,
			bool alternate,
			int width,
			int? precision
		) {
			this.before = before;
			this.after = after;
			this.conversion = conversion;
			this.leftAligned = leftAligned;
			this.zeroPadded = zeroPadded;
			this.thousands = thousands;
			this.alternate = alternate;
			this.width = width;
			this.precision = precision;
		}

		public static SuffixFormatter Parse( string format ) {
			ArgumentNullException.ThrowIfNull( format );
			var before = new StringBuilder();
			var after = new StringBuilder();
			var destination = before;
			var found = false;
			var left = false;
			var zero = false;
			var thousands = false;
			var alternate = false;
			var width = 0;
			int? precision = null;
			var conversion = '\0';
			for ( var index = 0; index < format.Length; index++ ) {
				var character = format[index];
				if ( '%' != character ) {
					destination.Append( character );
					continue;
				}
				if ( index + 1 < format.Length && '%' == format[index + 1] ) {
					destination.Append( '%' );
					index++;
					continue;
				}
				if ( found ) {
					throw new CsplitUsageException( "too many % conversion specifications in suffix" );
				}
				found = true;
				index++;
				while ( index < format.Length ) {
					switch ( format[index] ) {
						case '-': left = true; index++; continue;
						case '0': zero = true; index++; continue;
						case '\'': thousands = true; index++; continue;
						case '#': alternate = true; index++; continue;
					}
					break;
				}
				var widthStart = index;
				while ( index < format.Length && char.IsAsciiDigit( format[index] ) ) {
					index++;
				}
				if ( widthStart < index ) {
					width = ParseNonnegativeInt( format[widthStart..index], "suffix width" );
				}
				if ( index < format.Length && '.' == format[index] ) {
					index++;
					var precisionStart = index;
					while ( index < format.Length && char.IsAsciiDigit( format[index] ) ) {
						index++;
					}
					precision = precisionStart == index
						? 0
						: ParseNonnegativeInt( format[precisionStart..index], "suffix precision" );
				}
				if ( index >= format.Length ) {
					throw new CsplitUsageException( "missing conversion specifier in suffix" );
				}
				conversion = format[index];
				if ( conversion is not ( 'd' or 'i' or 'u' or 'o' or 'x' or 'X' ) ) {
					throw new CsplitUsageException( $"invalid conversion specifier in suffix: {conversion}" );
				}
				if ( thousands && conversion is not ( 'd' or 'i' or 'u' ) ) {
					throw new CsplitUsageException( $"invalid flags in conversion specification: %'{conversion}" );
				}
				if ( alternate && conversion is not ( 'o' or 'x' or 'X' ) ) {
					throw new CsplitUsageException( $"invalid flags in conversion specification: %#{conversion}" );
				}
				destination = after;
			}
			if ( !found ) {
				throw new CsplitUsageException( "missing % conversion specification in suffix" );
			}
			return new SuffixFormatter(
				before.ToString(),
				after.ToString(),
				conversion,
				left,
				zero,
				thousands,
				alternate,
				width,
				precision
			);
		}

		public string Format( int value ) {
			var digits = this.conversion switch {
				'o' => Convert.ToString( value, 8 )!,
				'x' => value.ToString( "x", CultureInfo.InvariantCulture ),
				'X' => value.ToString( "X", CultureInfo.InvariantCulture ),
				_ when this.thousands => value.ToString( "N0", CultureInfo.CurrentCulture ),
				_ => value.ToString( CultureInfo.InvariantCulture )
			};
			if ( this.precision is int requestedPrecision ) {
				if ( 0 == requestedPrecision && 0 == value ) {
					digits = string.Empty;
				} else if ( digits.Length < requestedPrecision ) {
					digits = digits.PadLeft( requestedPrecision, '0' );
				}
			}
			var prefix = string.Empty;
			if ( this.alternate ) {
				if ( 'o' == this.conversion ) {
					if ( 0 == digits.Length || '0' != digits[0] ) {
						prefix = "0";
					}
				} else if ( 0 != value ) {
					prefix = 'X' == this.conversion ? "0X" : "0x";
				}
			}
			var converted = string.Concat( prefix, digits );
			if ( converted.Length < this.width ) {
				var padCount = this.width - converted.Length;
				if ( this.leftAligned ) {
					converted = converted.PadRight( this.width, ' ' );
				} else if ( this.zeroPadded && !this.precision.HasValue ) {
					converted = string.Concat( prefix, new string( '0', padCount ), digits );
				} else {
					converted = converted.PadLeft( this.width, ' ' );
				}
			}
			return string.Concat( this.before, converted, this.after );
		}
	}

	private sealed class OutputManager {
		private readonly CsplitOptions options;
		private readonly CommandContext context;
		private readonly ByteOutputStream standardOutput;
		private readonly string? inputFullPath;
		private readonly List<string> createdFiles = new();
		private FileSystemEntryIdentity? inputIdentity;
		private int outputNumber;

		public OutputManager(
			CsplitOptions options,
			CommandContext context,
			ByteOutputStream standardOutput
		) {
			this.options = options;
			this.context = context;
			this.standardOutput = standardOutput;
			this.inputFullPath = "-" == options.InputPath
				? null
				: Path.GetFullPath( options.InputPath );
		}

		public async Task WritePieceAsync(
			IndexedInput input,
			long firstLine,
			long lastLine
		) {
			var length = await input.GetRangeLengthAsync(
				firstLine,
				lastLine,
				this.context.CancellationToken
			).ConfigureAwait( false );
			if ( 0 == length && this.options.ElideEmptyFiles ) {
				return;
			}
			if ( int.MaxValue == this.outputNumber ) {
				throw new IOException( "output file number is too large" );
			}
			var fileName = string.Concat(
				this.options.Prefix,
				this.options.SuffixFormatter?.Format( this.outputNumber )
					?? this.outputNumber.ToString( $"D{this.options.Digits}", CultureInfo.InvariantCulture )
			);
			await this.EnsureDoesNotOverwriteInputAsync( fileName ).ConfigureAwait( false );
			await using ( var destination = new FileStream(
				fileName,
				FileMode.Create,
				FileAccess.Write,
				FileShare.Read,
				BufferSize,
				FileOptions.Asynchronous | FileOptions.SequentialScan
			) ) {
				this.createdFiles.Add( fileName );
				this.outputNumber++;
				await input.CopyLinesAsync(
					firstLine,
					lastLine,
					destination,
					this.context.CancellationToken
				).ConfigureAwait( false );
				await destination.FlushAsync( this.context.CancellationToken ).ConfigureAwait( false );
			}
			if ( !this.options.Quiet ) {
				await this.standardOutput.WriteTextAsync(
					string.Concat( length.ToString( CultureInfo.InvariantCulture ), Environment.NewLine ),
					this.context.CancellationToken
				).ConfigureAwait( false );
			}
		}

		public async Task CleanupAsync() {
			if ( this.options.KeepFiles ) {
				return;
			}
			for ( var index = this.createdFiles.Count - 1; 0 <= index; index-- ) {
				try {
					File.Delete( this.createdFiles[index] );
				} catch ( Exception exception ) when (
					exception is IOException
					or UnauthorizedAccessException
					or System.Security.SecurityException
				) {
					try {
						await this.context.Diagnostics.ErrorAsync(
							$"{this.createdFiles[index]}: {exception.Message}",
							CancellationToken.None
						).ConfigureAwait( false );
					} catch {
						// Cleanup diagnostics must not replace the original result.
					}
				}
			}
			this.createdFiles.Clear();
		}

		public async Task EnsureDoesNotOverwriteInputAsync( string outputPath ) {
			if ( null == this.inputFullPath ) {
				return;
			}
			var outputFullPath = Path.GetFullPath( outputPath );
			var comparison = OperatingSystem.IsWindows()
				? StringComparison.OrdinalIgnoreCase
				: StringComparison.Ordinal;
			if ( string.Equals( this.inputFullPath, outputFullPath, comparison ) ) {
				throw new IOException( $"'{outputPath}' would overwrite input; aborting" );
			}
			if ( !File.Exists( outputFullPath ) && !Directory.Exists( outputFullPath ) ) {
				return;
			}
			this.inputIdentity ??= (
				await SystemReadOnlyFileSystemProvider.Instance.ObserveAsync(
					this.inputFullPath,
					followSymbolicLink: true,
					cancellationToken: this.context.CancellationToken
				).ConfigureAwait( false )
			).EntryIdentity;
			var outputIdentity = (
				await SystemReadOnlyFileSystemProvider.Instance.ObserveAsync(
					outputFullPath,
					followSymbolicLink: true,
					cancellationToken: this.context.CancellationToken
				).ConfigureAwait( false )
			).EntryIdentity;
			if (
				this.inputIdentity.Value.IsAvailable
				&& outputIdentity.IsAvailable
				&& this.inputIdentity.Value == outputIdentity
			) {
				throw new IOException( $"'{outputPath}' would overwrite input; aborting" );
			}
		}
	}

	private sealed class Engine {
		private static readonly RegularExpressionInputOptions InputOptions = new();
		private static readonly RegularExpressionByteMatchOptions MatchOptions = new();
		private readonly CsplitOptions options;
		private readonly IndexedInput input;
		private readonly OutputManager output;
		private readonly CancellationToken cancellationToken;
		private long currentLine;
		private long firstAvailable = 1;

		public Engine(
			CsplitOptions options,
			IndexedInput input,
			OutputManager output,
			CancellationToken cancellationToken
		) {
			this.options = options;
			this.input = input;
			this.output = output;
			this.cancellationToken = cancellationToken;
		}

		public async Task ExecuteAsync() {
			foreach ( var control in this.options.Controls ) {
				if ( control.RepeatForever ) {
					for ( long repetition = 0; ; repetition++ ) {
						this.cancellationToken.ThrowIfCancellationRequested();
						if ( control is RegexControl regex ) {
							if ( !await this.ProcessRegexAsync( regex, repetition ).ConfigureAwait( false ) ) {
								return;
							}
						} else {
							await this.ProcessNumericAsync( (NumericControl)control, repetition ).ConfigureAwait( false );
						}
					}
				}
				for ( long repetition = 0; ; repetition++ ) {
					this.cancellationToken.ThrowIfCancellationRequested();
					if ( control is RegexControl regex ) {
						await this.ProcessRegexAsync( regex, repetition ).ConfigureAwait( false );
					} else {
						await this.ProcessNumericAsync( (NumericControl)control, repetition ).ConfigureAwait( false );
					}
					if ( repetition == control.RepeatCount ) {
						break;
					}
				}
			}
			await this.output.WritePieceAsync(
				this.input,
				this.firstAvailable,
				this.input.LineCount + 1
			).ConfigureAwait( false );
			this.firstAvailable = this.input.LineCount + 1;
		}

		public async Task ProcessNumericAsync( NumericControl control, long repetition ) {
			long target;
			try {
				target = checked( control.LineNumber * checked( repetition + 1 ) );
			} catch ( OverflowException ) {
				throw new IOException( CreateLineRangeMessage( control.SourceText, repetition ) );
			}
			var hadLine = this.firstAvailable <= this.input.LineCount;
			var copyEnd = target <= this.firstAvailable
				? this.firstAvailable
				: Math.Min( target, this.input.LineCount + 1 );
			await this.output.WritePieceAsync(
				this.input,
				this.firstAvailable,
				copyEnd
			).ConfigureAwait( false );
			if ( copyEnd > this.firstAvailable ) {
				this.currentLine = Math.Max( this.currentLine, copyEnd - 1 );
			}
			this.firstAvailable = copyEnd;
			if ( this.options.SuppressMatched ) {
				if ( !hadLine || target > this.input.LineCount + 1 ) {
					throw new IOException( CreateLineRangeMessage( control.SourceText, repetition ) );
				}
				this.RemoveLine();
			} else if ( this.firstAvailable > this.input.LineCount ) {
				throw new IOException( CreateLineRangeMessage( control.SourceText, repetition ) );
			}
		}

		public async Task<bool> ProcessRegexAsync( RegexControl control, long repetition ) {
			var pieceStart = this.firstAvailable;
			long matchedLine = 0;
			for ( var lineNumber = checked( this.currentLine + 1 ); lineNumber <= this.input.LineCount; lineNumber++ ) {
				this.cancellationToken.ThrowIfCancellationRequested();
				var line = await this.input.ReadMatchLineAsync( lineNumber, this.cancellationToken ).ConfigureAwait( false );
				var result = control.Expression.Match(
					line,
					InputOptions,
					MatchOptions,
					this.cancellationToken
				);
				if ( !result.IsSuccess ) {
					throw new IOException( result.Diagnostic?.Message ?? "error in regular expression search" );
				}
				if ( result.IsMatch ) {
					matchedLine = lineNumber;
					break;
				}
			}
			if ( 0 == matchedLine ) {
				if ( control.RepeatForever ) {
					if ( !control.Ignore ) {
						await this.output.WritePieceAsync(
							this.input,
							pieceStart,
							this.input.LineCount + 1
						).ConfigureAwait( false );
					}
					this.firstAvailable = this.input.LineCount + 1;
					return false;
				}
				if ( !control.Ignore ) {
					await this.output.WritePieceAsync(
						this.input,
						pieceStart,
						this.input.LineCount + 1
					).ConfigureAwait( false );
				}
				throw new IOException( CreateMatchNotFoundMessage( control.SourceText, repetition ) );
			}
			this.currentLine = matchedLine;
			long breakLine;
			try {
				breakLine = checked( matchedLine + control.Offset );
			} catch ( OverflowException ) {
				throw new IOException( $"{control.SourceText}: line number out of range" );
			}
			if ( breakLine < this.firstAvailable || breakLine > this.input.LineCount + 1 ) {
				if ( !control.Ignore ) {
					await this.output.WritePieceAsync(
						this.input,
						breakLine < this.firstAvailable ? this.firstAvailable : pieceStart,
						breakLine < this.firstAvailable ? this.firstAvailable : this.input.LineCount + 1
					).ConfigureAwait( false );
				}
				throw new IOException( $"{control.SourceText}: line number out of range" );
			}
			if ( !control.Ignore ) {
				await this.output.WritePieceAsync(
					this.input,
					pieceStart,
					breakLine
				).ConfigureAwait( false );
			}
			this.firstAvailable = breakLine;
			if ( 0 < control.Offset ) {
				this.currentLine = breakLine;
			}
			if ( this.options.SuppressMatched ) {
				this.RemoveLine();
			}
			return true;
		}

		public void RemoveLine() {
			if ( this.firstAvailable > this.input.LineCount ) {
				return;
			}
			if ( this.currentLine < this.firstAvailable ) {
				this.currentLine = this.firstAvailable;
			}
			this.firstAvailable++;
		}
	}

	/// <summary>Runs <c>csplit</c> synchronously with optional injected text streams.</summary>
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

	/// <summary>Runs <c>csplit</c> asynchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task whose result is the process exit status.</returns>
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
				"csplit",
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

	/// <summary>Runs <c>csplit</c> asynchronously against a byte-capable command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		TextReaderStream? inputAdapter = null;
		OutputManager? outputManager = null;
		await using var standardOutput = new ByteOutputStream(
			context.StandardOutput,
			context.StandardOutputStream
		);
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
				await WriteHelpAsync( standardOutput, context.CancellationToken ).ConfigureAwait( false );
				await standardOutput.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.HasOption( "version" ) ) {
				await standardOutput.WriteTextAsync(
					string.Concat( VersionText, Environment.NewLine ),
					context.CancellationToken
				).ConfigureAwait( false );
				await standardOutput.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			var options = CreateOptions( parsed, context.CancellationToken );
			foreach ( var warning in options.Warnings ) {
				await context.Diagnostics.WarningAsync(
					warning,
					context.CancellationToken
				).ConfigureAwait( false );
			}
			Stream input;
			var disposeInput = false;
			if ( "-" == options.InputPath ) {
				input = context.StandardInputStream ?? (
					inputAdapter = new TextReaderStream( context.StandardInput, leaveOpen: true )
				);
			} else {
				input = new FileStream(
					options.InputPath,
					FileMode.Open,
					FileAccess.Read,
					FileShare.Read,
					BufferSize,
					FileOptions.Asynchronous | FileOptions.SequentialScan
				);
				disposeInput = true;
			}
			try {
				await using var indexedInput = await IndexedInput.CreateAsync(
					input,
					context.CancellationToken
				).ConfigureAwait( false );
				outputManager = new OutputManager( options, context, standardOutput );
				var engine = new Engine( options, indexedInput, outputManager, context.CancellationToken );
				await engine.ExecuteAsync().ConfigureAwait( false );
			} finally {
				if ( disposeInput ) {
					await input.DisposeAsync().ConfigureAwait( false );
				}
			}
			await standardOutput.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		} catch ( OperationCanceledException ) {
			if ( null != outputManager ) {
				await outputManager.CleanupAsync().ConfigureAwait( false );
			}
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or InvalidOperationException
			or ArgumentException
			or FormatException
			or NotSupportedException
			or OverflowException
			or System.Security.SecurityException
			or CsplitUsageException
		) {
			if ( null != outputManager ) {
				await outputManager.CleanupAsync().ConfigureAwait( false );
			}
			try {
				await context.Diagnostics.ErrorAsync( exception.Message, CancellationToken.None ).ConfigureAwait( false );
			} catch {
				// A diagnostic failure must not replace the command failure status.
			}
			return exception is CsplitUsageException
				? CommandExitCodes.UsageError
				: CommandExitCodes.Failure;
		} finally {
			inputAdapter?.Dispose();
		}
	}

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( "suffix-format", 'b', new[] { "suffix-format" }, OptionValueArity.Required ),
			new OptionDefinition( "prefix", 'f', new[] { "prefix" }, OptionValueArity.Required ),
			new OptionDefinition( "keep-files", 'k', new[] { "keep-files" } ),
			new OptionDefinition( "digits", 'n', new[] { "digits" }, OptionValueArity.Required ),
			new OptionDefinition( "quiet-s", 's', new[] { "silent" } ),
			new OptionDefinition( "quiet-q", 'q', new[] { "quiet" } ),
			new OptionDefinition( "elide-empty", 'z', new[] { "elide-empty-files" } ),
			new OptionDefinition( "suppress-matched", null, new[] { "suppress-matched" } ),
			new OptionDefinition( "help", null, new[] { "help" } ),
			new OptionDefinition( "version", null, new[] { "version" } )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	private static CsplitOptions CreateOptions(
		OptionParseResult parsed,
		CancellationToken cancellationToken
	) {
		if ( parsed.Operands.Count < 2 ) {
			throw new CsplitUsageException(
				0 == parsed.Operands.Count
					? "missing operand"
					: $"missing operand after '{parsed.Operands[0]}'"
			);
		}
		var options = new CsplitOptions {
			InputPath = parsed.Operands[0],
			Prefix = parsed.GetLastValue( "prefix" ) ?? "xx",
			KeepFiles = parsed.HasOption( "keep-files" ),
			SuppressMatched = parsed.HasOption( "suppress-matched" ),
			Quiet = parsed.HasOption( "quiet-s" ) || parsed.HasOption( "quiet-q" ),
			ElideEmptyFiles = parsed.HasOption( "elide-empty" )
		};
		var digitsText = parsed.GetLastValue( "digits" );
		if ( null != digitsText ) {
			options.Digits = ParseNonnegativeInt( digitsText, "number of digits" );
		}
		var format = parsed.GetLastValue( "suffix-format" );
		if ( null != format ) {
			options.SuffixFormatter = SuffixFormatter.Parse( format );
		}
		ParseControls(
			parsed.Operands.Skip( 1 ),
			options.Controls,
			options.Warnings,
			cancellationToken
		);
		return options;
	}

	private static void ParseControls(
		IEnumerable<string> sourcePatterns,
		ICollection<SplitControl> controls,
		ICollection<string> warnings,
		CancellationToken cancellationToken
	) {
		var patterns = sourcePatterns.ToList();
		long lastNumeric = 0;
		var provider = new GnuBasicRegularExpressionProvider();
		for ( var index = 0; index < patterns.Count; index++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			var source = patterns[index];
			SplitControl control;
			if ( 0 < source.Length && source[0] is '/' or '%' ) {
				var delimiter = source[0];
				var closing = source.LastIndexOf( delimiter );
				if ( closing <= 0 ) {
					throw new CsplitUsageException( $"{source}: closing delimiter '{delimiter}' missing" );
				}
				var pattern = source.Substring( 1, closing - 1 );
				var offsetText = source[( closing + 1 )..];
				var offset = 0L;
				if (
					0 < offsetText.Length
					&& !long.TryParse(
						offsetText,
						NumberStyles.AllowLeadingSign,
						CultureInfo.InvariantCulture,
						out offset
					)
				) {
					throw new CsplitUsageException( $"{source}: integer expected after delimiter" );
				}
				var compiled = provider.Compile(
					pattern,
					RegularExpressionOptions.GnuExprCompatibility,
					cancellationToken
				);
				var expression = compiled.Expression;
				if ( !compiled.IsSuccess || null == expression ) {
					throw new CsplitUsageException(
						$"{source}: invalid regular expression: {compiled.Diagnostic?.Message ?? "invalid pattern"}"
					);
				}
				control = new RegexControl( source, '%' == delimiter, offset, expression );
			} else {
				if (
					source.StartsWith( '-' )
					|| !long.TryParse(
						source,
						NumberStyles.AllowLeadingSign,
						CultureInfo.InvariantCulture,
						out var lineNumber
					)
				) {
					throw new CsplitUsageException( $"{source}: invalid pattern" );
				}
				if ( 0 == lineNumber ) {
					throw new CsplitUsageException( $"{source}: line number must be greater than zero" );
				}
				if ( lineNumber < lastNumeric ) {
					throw new CsplitUsageException(
						$"line number {source} is smaller than preceding line number, {lastNumeric.ToString( CultureInfo.InvariantCulture )}"
					);
				}
				if ( lineNumber == lastNumeric ) {
					warnings.Add( $"line number {source} is the same as preceding line number" );
				}
				lastNumeric = lineNumber;
				control = new NumericControl( source, lineNumber );
			}
			if ( index + 1 < patterns.Count && patterns[index + 1].StartsWith( "{", StringComparison.Ordinal ) ) {
				index++;
				ParseRepeat( patterns[index], control );
			}
			controls.Add( control );
		}
	}

	private static void ParseRepeat( string source, SplitControl control ) {
		if ( source.Length < 2 || '}' != source[^1] ) {
			throw new CsplitUsageException( $"{source}: '}}' is required in repeat count" );
		}
		var content = source[1..^1];
		if ( "*" == content ) {
			control.RepeatForever = true;
			return;
		}
		if (
			content.StartsWith( '-' )
			|| !long.TryParse(
				content,
				NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture,
				out var count
			)
		) {
			throw new CsplitUsageException( $"{source}: integer required between '{{' and '}}'" );
		}
		control.RepeatCount = count;
	}

	private static int ParseNonnegativeInt( string value, string description ) {
		if (
			!int.TryParse(
				value,
				NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture,
				out var result
			)
			|| result < 0
		) {
			throw new CsplitUsageException( $"invalid {description}: '{value}'" );
		}
		return result;
	}

	private static string CreateLineRangeMessage( string source, long repetition ) => 0 == repetition
		? $"{source}: line number out of range"
		: $"{source}: line number out of range on repetition {repetition.ToString( CultureInfo.InvariantCulture )}";

	private static string CreateMatchNotFoundMessage( string source, long repetition ) => 0 == repetition
		? $"{source}: match not found"
		: $"{source}: match not found on repetition {repetition.ToString( CultureInfo.InvariantCulture )}";

	private static async Task ReadExactlyAsync(
		Stream stream,
		Memory<byte> buffer,
		CancellationToken cancellationToken
	) {
		var offset = 0;
		while ( offset < buffer.Length ) {
			var read = await stream.ReadAsync( buffer[offset..], cancellationToken ).ConfigureAwait( false );
			if ( 0 == read ) {
				throw new EndOfStreamException();
			}
			offset += read;
		}
	}

	private static Task WriteHelpAsync(
		ByteOutputStream output,
		CancellationToken cancellationToken
	) => output.WriteTextAsync(
		string.Concat(
			"Usage: csplit [OPTION]... FILE PATTERN...", Environment.NewLine,
			"Output pieces of FILE separated by PATTERN(s) to files 'xx00', 'xx01', ...,", Environment.NewLine,
			"and output byte counts of each piece to standard output.", Environment.NewLine,
			Environment.NewLine,
			"Read standard input if FILE is -", Environment.NewLine,
			Environment.NewLine,
			"  -b, --suffix-format=FORMAT  use sprintf FORMAT instead of %02d", Environment.NewLine,
			"  -f, --prefix=PREFIX        use PREFIX instead of 'xx'", Environment.NewLine,
			"  -k, --keep-files           do not remove output files on errors", Environment.NewLine,
			"      --suppress-matched     suppress lines matching PATTERN", Environment.NewLine,
			"  -n, --digits=DIGITS        use specified number of digits instead of 2", Environment.NewLine,
			"  -s, -q, --silent, --quiet do not print output file byte counts", Environment.NewLine,
			"  -z, --elide-empty-files    suppress empty output files", Environment.NewLine,
			"      --help                 display this help and exit", Environment.NewLine,
			"      --version              output version information and exit", Environment.NewLine,
			Environment.NewLine,
			"Each PATTERN may be:", Environment.NewLine,
			"  INTEGER            copy up to but not including specified line number", Environment.NewLine,
			"  /REGEXP/[OFFSET]   copy up to but not including a matching line", Environment.NewLine,
			"  %REGEXP%[OFFSET]   skip to, but not including a matching line", Environment.NewLine,
			"  {INTEGER}          repeat the previous pattern specified number of times", Environment.NewLine,
			"  {*}                repeat the previous pattern as many times as possible", Environment.NewLine,
			Environment.NewLine,
			"A line OFFSET is an integer optionally preceded by '+' or '-'.", Environment.NewLine
		),
		cancellationToken
	).AsTask();
}
