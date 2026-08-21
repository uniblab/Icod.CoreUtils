// Original behavior/reference: GNU coreutils split
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Split;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.FileSystem.Traversal;
using Icod.CoreUtils.Shared.IO;

/// <summary>Implements GNU-compatible byte-preserving file splitting.</summary>
public static class Command {
	private const string VersionText = "split (Icod.CoreUtils) 1.0";
	private const int BufferSize = 64 * 1024;

	private enum SplitMode {
		Lines,
		Bytes,
		LineBytes,
		Chunks
	}

	private enum ChunkMode {
		Bytes,
		Lines,
		RoundRobin
	}

	private enum SuffixStyle {
		Alphabetic,
		Decimal,
		Hexadecimal
	}

	private sealed record ChunkSpecification( ChunkMode Mode, int Count, int? Selected );

	private sealed class SplitOptions {
		public SplitMode Mode { get; set; } = SplitMode.Lines;
		public long UnitCount { get; set; } = 1000;
		public ChunkSpecification? Chunks { get; set; }
		public SuffixStyle SuffixStyle { get; set; } = SuffixStyle.Alphabetic;
		public int SuffixLength { get; set; } = 2;
		public bool SuffixLengthExplicit { get; set; }
		public long SuffixStart { get; set; }
		public bool SuffixStartExplicit { get; set; }
		public bool ExpandSuffixes { get; set; } = true;
		public string AdditionalSuffix { get; set; } = string.Empty;
		public string InputPath { get; set; } = "-";
		public string Prefix { get; set; } = "x";
		public byte Separator { get; set; } = (byte)'\n';
		public bool ElideEmptyFiles { get; set; }
		public bool Unbuffered { get; set; }
		public bool Verbose { get; set; }
		public string? Filter { get; set; }
	}

	private sealed class BufferedRecordReader {
		private readonly Stream source;
		private readonly byte separator;
		private readonly byte[] buffer = new byte[BufferSize];
		private int offset;
		private int count;
		private bool endOfInput;

		public BufferedRecordReader( Stream source, byte separator ) {
			this.source = source;
			this.separator = separator;
		}

		public async ValueTask<long?> ReadRecordAsync(
			Stream? destination,
			CancellationToken cancellationToken
		) {
			if ( null != destination ) {
				destination.SetLength( 0 );
				destination.Position = 0;
			}
			long length = 0;
			var hasData = false;
			while ( true ) {
				if ( this.offset >= this.count ) {
					if ( this.endOfInput ) {
						if ( null != destination ) {
							destination.Position = 0;
						}
						return hasData ? length : null;
					}
					this.count = await this.source.ReadAsync(
						this.buffer.AsMemory(),
						cancellationToken
					).ConfigureAwait( false );
					this.offset = 0;
					if ( 0 == this.count ) {
						this.endOfInput = true;
						continue;
					}
				}

				var available = this.buffer.AsSpan( this.offset, this.count - this.offset );
				var relative = available.IndexOf( this.separator );
				var take = 0 <= relative ? relative + 1 : available.Length;
				if ( null != destination ) {
					await destination.WriteAsync(
						this.buffer.AsMemory( this.offset, take ),
						cancellationToken
					).ConfigureAwait( false );
				}
				this.offset += take;
				length = checked( length + take );
				hasData = true;
				if ( 0 <= relative ) {
					if ( null != destination ) {
						destination.Position = 0;
					}
					return length;
				}
			}
		}
	}

	private sealed class FilterExitException : IOException {
		public int ExitStatus { get; }

		public FilterExitException( string fileName, string command, int exitStatus )
			: base( $"with FILE={fileName}, exit {exitStatus} from command: {command}" ) {
			this.ExitStatus = exitStatus;
		}
	}

	private sealed class PieceWriter : IAsyncDisposable {
		private readonly Func<CancellationToken, Task>? finish;
		private readonly CancellationToken cancellationToken;
		private bool disposed;

		public Stream Stream { get; }

		public PieceWriter(
			Stream stream,
			CancellationToken cancellationToken,
			Func<CancellationToken, Task>? finish = null
		) {
			this.Stream = stream;
			this.cancellationToken = cancellationToken;
			this.finish = finish;
		}

		public async ValueTask DisposeAsync() {
			if ( this.disposed ) {
				return;
			}
			this.disposed = true;
			Exception? disposeFailure = null;
			try {
				await this.Stream.FlushAsync( this.cancellationToken ).ConfigureAwait( false );
			} catch ( Exception exception ) {
				disposeFailure = exception;
			}
			try {
				await this.Stream.DisposeAsync().ConfigureAwait( false );
			} catch ( Exception exception ) {
				disposeFailure ??= exception;
			}
			if ( null != this.finish ) {
				await this.finish( this.cancellationToken ).ConfigureAwait( false );
			}
			if ( null != disposeFailure ) {
				throw disposeFailure;
			}
		}
	}

	private sealed class OutputManager {
		private readonly SplitOptions options;
		private readonly CommandContext context;
		private readonly ByteOutputStream standardOutput;
		private readonly ByteOutputStream standardError;
		private readonly string? inputFullPath;
		private FileSystemEntryIdentity? inputIdentity;

		public OutputManager(
			SplitOptions options,
			CommandContext context,
			ByteOutputStream standardOutput,
			ByteOutputStream standardError
		) {
			this.options = options;
			this.context = context;
			this.standardOutput = standardOutput;
			this.standardError = standardError;
			this.inputFullPath = "-" == options.InputPath
				? null
				: System.IO.Path.GetFullPath( options.InputPath );
		}

		public string GetName( long index ) {
			var suffix = CreateSuffix(
				checked( this.options.SuffixStart + index ),
				this.options.SuffixStyle,
				this.options.SuffixLength,
				this.options.ExpandSuffixes
			);
			return string.Concat( this.options.Prefix, suffix, this.options.AdditionalSuffix );
		}

		public async Task<PieceWriter> OpenAsync( long index ) {
			var path = this.GetName( index );
			await this.EnsureDoesNotOverwriteInputAsync( path ).ConfigureAwait( false );
			if ( this.options.Verbose ) {
				await this.context.StandardError.WriteLineAsync(
					$"creating file '{path}'".AsMemory(),
					this.context.CancellationToken
				).ConfigureAwait( false );
			}
			if ( null == this.options.Filter ) {
				var stream = new FileStream(
					path,
					FileMode.Create,
					FileAccess.Write,
					FileShare.Read,
					BufferSize,
					FileOptions.Asynchronous | FileOptions.SequentialScan
				);
				return new PieceWriter( stream, this.context.CancellationToken );
			}
			return await this.OpenFilterAsync( path ).ConfigureAwait( false );
		}

		private async Task EnsureDoesNotOverwriteInputAsync( string outputPath ) {
			if ( null == this.inputFullPath ) {
				return;
			}
			var outputFullPath = System.IO.Path.GetFullPath( outputPath );
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

		private async Task<PieceWriter> OpenFilterAsync( string fileName ) {
			var startInfo = new ProcessStartInfo {
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			string? commandFilePath = null;
			if ( OperatingSystem.IsWindows() ) {
				commandFilePath = System.IO.Path.Combine(
					System.IO.Path.GetTempPath(),
					$"icod-split-filter-{Guid.NewGuid():N}.cmd"
				);
				try {
					await File.WriteAllTextAsync(
						commandFilePath,
						string.Concat( this.options.Filter!, Environment.NewLine ),
						new UTF8Encoding( encoderShouldEmitUTF8Identifier: false ),
						this.context.CancellationToken
					).ConfigureAwait( false );
				} catch {
					DeleteTemporaryCommandFile( commandFilePath );
					throw;
				}
				startInfo.FileName = Environment.GetEnvironmentVariable( "ComSpec" ) ?? "cmd.exe";
				startInfo.ArgumentList.Add( "/d" );
				startInfo.ArgumentList.Add( "/q" );
				startInfo.ArgumentList.Add( "/c" );
				startInfo.ArgumentList.Add( commandFilePath );
			} else {
				startInfo.FileName = "/bin/sh";
				startInfo.ArgumentList.Add( "-c" );
				startInfo.ArgumentList.Add( this.options.Filter! );
			}
			startInfo.Environment["FILE"] = fileName;
			var process = new Process { StartInfo = startInfo };
			try {
				if ( !process.Start() ) {
					throw new IOException( "unable to start output filter" );
				}
			} catch {
				process.Dispose();
				DeleteTemporaryCommandFile( commandFilePath );
				throw;
			}
			var outputTask = process.StandardOutput.BaseStream.CopyToAsync(
				this.standardOutput,
				BufferSize,
				this.context.CancellationToken
			);
			var errorTask = process.StandardError.BaseStream.CopyToAsync(
				this.standardError,
				BufferSize,
				this.context.CancellationToken
			);
			await Task.Yield();
			return new PieceWriter(
				process.StandardInput.BaseStream,
				this.context.CancellationToken,
				async cancellationToken => {
					try {
						await process.WaitForExitAsync( cancellationToken ).ConfigureAwait( false );
						await Task.WhenAll( outputTask, errorTask ).ConfigureAwait( false );
						if ( 0 != process.ExitCode ) {
							throw new FilterExitException( fileName, this.options.Filter!, process.ExitCode );
						}
					} finally {
						process.Dispose();
						DeleteTemporaryCommandFile( commandFilePath );
					}
				}
			);
		}

		private static void DeleteTemporaryCommandFile( string? commandFilePath ) {
			if ( null == commandFilePath ) {
				return;
			}
			try {
				File.Delete( commandFilePath );
			} catch {
				// Temporary command-file cleanup must not replace the filter result.
			}
		}
	}

	/// <summary>Runs <c>split</c> synchronously with optional injected text streams.</summary>
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

	/// <summary>Runs <c>split</c> asynchronously with optional injected text streams.</summary>
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
				"split",
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

	/// <summary>Runs <c>split</c> asynchronously against a byte-capable command context.</summary>
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

			var options = await TryCreateOptionsAsync( parsed, context ).ConfigureAwait( false );
			if ( null == options ) {
				return CommandExitCodes.UsageError;
			}
			var standardInput = context.StandardInputStream;
			if ( null == standardInput ) {
				inputAdapter = new TextReaderStream( context.StandardInput, leaveOpen: true );
				standardInput = inputAdapter;
			}
			await using var output = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
			await using var errorOutput = new ByteOutputStream( context.StandardError, context.StandardErrorStream );
			var manager = new OutputManager( options, context, output, errorOutput );
			var status = await ExecuteAsync(
				options,
				standardInput,
				output,
				manager,
				context
			).ConfigureAwait( false );
			await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			await errorOutput.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return status;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( FilterExitException exception ) {
			try {
				await context.Diagnostics.ErrorAsync( exception.Message, CancellationToken.None ).ConfigureAwait( false );
			} catch {
				// A diagnostic failure must not replace the filter exit status.
			}
			return exception.ExitStatus is > 0 and <= 255
				? exception.ExitStatus
				: CommandExitCodes.Failure;
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

	private static OptionParser CreateParser() {
		var settings = new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		};
		settings.TokenRewriteRules.Add(
			new OptionTokenRewriteRule(
				static token => IsLegacyLineCountOption( token )
					? new[] { string.Concat( "--lines=", token.Substring( 1 ) ) }
					: null
			)
		);
		return new OptionParser(
			new[] {
				new OptionDefinition( "suffix-length", 'a', new[] { "suffix-length" }, OptionValueArity.Required ),
				new OptionDefinition( "additional-suffix", null, new[] { "additional-suffix" }, OptionValueArity.Required ),
				new OptionDefinition( "bytes", 'b', new[] { "bytes" }, OptionValueArity.Required ),
				new OptionDefinition( "line-bytes", 'C', new[] { "line-bytes" }, OptionValueArity.Required ),
				new OptionDefinition( "numeric-suffixes-short", 'd' ),
				new OptionDefinition( "numeric-suffixes", null, new[] { "numeric-suffixes" }, OptionValueArity.Optional ),
				new OptionDefinition( "hex-suffixes-short", 'x' ),
				new OptionDefinition( "hex-suffixes", null, new[] { "hex-suffixes" }, OptionValueArity.Optional ),
				new OptionDefinition( "elide-empty-files", 'e', new[] { "elide-empty-files" } ),
				new OptionDefinition( "filter", null, new[] { "filter" }, OptionValueArity.Required ),
				new OptionDefinition( "lines", 'l', new[] { "lines" }, OptionValueArity.Required ),
				new OptionDefinition( "number", 'n', new[] { "number" }, OptionValueArity.Required ),
				new OptionDefinition( "separator", 't', new[] { "separator" }, OptionValueArity.Required ),
				new OptionDefinition( "unbuffered", 'u', new[] { "unbuffered" } ),
				new OptionDefinition( "verbose", null, new[] { "verbose" } ),
				new OptionDefinition( "help", null, new[] { "help" } ),
				new OptionDefinition( "version", null, new[] { "version" } )
			},
			settings
		);
	}

	private static async Task<SplitOptions?> TryCreateOptionsAsync(
		OptionParseResult parsed,
		CommandContext context
	) {
		if ( 2 < parsed.Operands.Count ) {
			await context.Diagnostics.ErrorAsync( "extra operand", context.CancellationToken ).ConfigureAwait( false );
			return null;
		}
		var options = new SplitOptions();
		var splitMethodCount = 0;
		foreach ( var occurrence in parsed.Options ) {
			var value = occurrence.Value;
			switch ( occurrence.Definition.Key ) {
				case "suffix-length":
					if ( !int.TryParse( value, NumberStyles.None, CultureInfo.InvariantCulture, out var suffixLength ) || suffixLength < 0 ) {
						await context.Diagnostics.ErrorAsync( $"invalid suffix length: '{value}'", context.CancellationToken ).ConfigureAwait( false );
						return null;
					}
					options.SuffixLength = 0 == suffixLength ? 2 : suffixLength;
					options.SuffixLengthExplicit = 0 != suffixLength;
					break;
				case "additional-suffix":
					if ( null == value || value.IndexOfAny( new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar } ) >= 0 ) {
						await context.Diagnostics.ErrorAsync( "additional suffix must not contain a directory separator", context.CancellationToken ).ConfigureAwait( false );
						return null;
					}
					options.AdditionalSuffix = value;
					break;
				case "bytes":
					splitMethodCount++;
					if ( !TryParseSize( value, out var bytes ) || bytes <= 0 ) {
						await context.Diagnostics.ErrorAsync( $"invalid number of bytes: '{value}'", context.CancellationToken ).ConfigureAwait( false );
						return null;
					}
					options.Mode = SplitMode.Bytes;
					options.UnitCount = bytes;
					options.Chunks = null;
					break;
				case "line-bytes":
					splitMethodCount++;
					if ( !TryParseSize( value, out var lineBytes ) || lineBytes <= 0 ) {
						await context.Diagnostics.ErrorAsync( $"invalid number of bytes: '{value}'", context.CancellationToken ).ConfigureAwait( false );
						return null;
					}
					options.Mode = SplitMode.LineBytes;
					options.UnitCount = lineBytes;
					options.Chunks = null;
					break;
				case "numeric-suffixes-short":
					options.SuffixStyle = SuffixStyle.Decimal;
					options.SuffixStart = 0;
					options.SuffixStartExplicit = false;
					break;
				case "numeric-suffixes": {
					options.SuffixStyle = SuffixStyle.Decimal;
					long numericStart = 0;
					if ( !string.IsNullOrEmpty( value ) && !TryParseNonnegative( value, 10, out numericStart ) ) {
						await context.Diagnostics.ErrorAsync( $"invalid starting value for numeric suffix: '{value}'", context.CancellationToken ).ConfigureAwait( false );
						return null;
					}
					options.SuffixStart = numericStart;
					options.SuffixStartExplicit = null != value;
					break;
				}
				case "hex-suffixes-short":
					options.SuffixStyle = SuffixStyle.Hexadecimal;
					options.SuffixStart = 0;
					options.SuffixStartExplicit = false;
					break;
				case "hex-suffixes": {
					options.SuffixStyle = SuffixStyle.Hexadecimal;
					long hexadecimalStart = 0;
					if ( !string.IsNullOrEmpty( value ) && !TryParseNonnegative( value, 10, out hexadecimalStart ) ) {
						await context.Diagnostics.ErrorAsync( $"invalid starting value for hexadecimal suffix: '{value}'", context.CancellationToken ).ConfigureAwait( false );
						return null;
					}
					options.SuffixStart = hexadecimalStart;
					options.SuffixStartExplicit = null != value;
					break;
				}
				case "elide-empty-files":
					options.ElideEmptyFiles = true;
					break;
				case "filter":
					options.Filter = value;
					break;
				case "lines":
					splitMethodCount++;
					if ( !TryParsePositiveLong( value, out var lines ) ) {
						await context.Diagnostics.ErrorAsync( $"invalid number of lines: '{value}'", context.CancellationToken ).ConfigureAwait( false );
						return null;
					}
					options.Mode = SplitMode.Lines;
					options.UnitCount = lines;
					options.Chunks = null;
					break;
				case "number":
					splitMethodCount++;
					if ( !TryParseChunkSpecification( value, out var chunks ) ) {
						await context.Diagnostics.ErrorAsync( $"invalid number of chunks: '{value}'", context.CancellationToken ).ConfigureAwait( false );
						return null;
					}
					options.Mode = SplitMode.Chunks;
					options.Chunks = chunks;
					break;
				case "separator":
					if ( !TryParseSeparator( value, out var separator ) ) {
						await context.Diagnostics.ErrorAsync( "multi-character separator", context.CancellationToken ).ConfigureAwait( false );
						return null;
					}
					options.Separator = separator;
					break;
				case "unbuffered":
					options.Unbuffered = true;
					break;
				case "verbose":
					options.Verbose = true;
					break;
			}
		}
		if ( 1 < splitMethodCount ) {
			await context.Diagnostics.ErrorAsync( "cannot split in more than one way", context.CancellationToken ).ConfigureAwait( false );
			return null;
		}
		if ( 0 < parsed.Operands.Count ) {
			options.InputPath = parsed.Operands[0];
		}
		if ( 1 < parsed.Operands.Count ) {
			options.Prefix = parsed.Operands[1];
		}
		if ( !TryConfigureSuffixes( options ) ) {
			await context.Diagnostics.ErrorAsync( "numerical suffix start value is too large for the suffix length", context.CancellationToken ).ConfigureAwait( false );
			return null;
		}
		if ( null != options.Filter && SplitMode.Chunks == options.Mode && null != options.Chunks!.Selected ) {
			await context.Diagnostics.ErrorAsync( "option --filter is incompatible with a selected chunk written to standard output", context.CancellationToken ).ConfigureAwait( false );
			return null;
		}
		return options;
	}

	private static async Task<int> ExecuteAsync(
		SplitOptions options,
		Stream standardInput,
		ByteOutputStream standardOutput,
		OutputManager manager,
		CommandContext context
	) {
		Stream input;
		var disposeInput = false;
		if ( "-" == options.InputPath ) {
			input = standardInput;
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
			return options.Mode switch {
				SplitMode.Bytes => await SplitByBytesAsync( input, options.UnitCount, manager, context.CancellationToken ).ConfigureAwait( false ),
				SplitMode.LineBytes => await SplitByLineBytesAsync( input, options, manager, context.CancellationToken ).ConfigureAwait( false ),
				SplitMode.Chunks => await SplitByChunksAsync( input, options, standardOutput, manager, context.CancellationToken ).ConfigureAwait( false ),
				_ => await SplitByLinesAsync( input, options, manager, context.CancellationToken ).ConfigureAwait( false )
			};
		} finally {
			if ( disposeInput ) {
				await input.DisposeAsync().ConfigureAwait( false );
			}
		}
	}

	private static async Task<int> SplitByBytesAsync(
		Stream input,
		long bytesPerPiece,
		OutputManager manager,
		CancellationToken cancellationToken
	) {
		var buffer = new byte[BufferSize];
		PieceWriter? piece = null;
		long pieceIndex = 0;
		long remaining = bytesPerPiece;
		try {
			while ( true ) {
				var read = await input.ReadAsync(
					buffer.AsMemory( 0, (int)Math.Min( buffer.Length, remaining ) ),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}
				piece ??= await manager.OpenAsync( pieceIndex ).ConfigureAwait( false );
				await piece.Stream.WriteAsync( buffer.AsMemory( 0, read ), cancellationToken ).ConfigureAwait( false );
				remaining -= read;
				if ( 0 == remaining ) {
					await piece.DisposeAsync().ConfigureAwait( false );
					piece = null;
					pieceIndex++;
					remaining = bytesPerPiece;
				}
			}
		} finally {
			if ( null != piece ) {
				await piece.DisposeAsync().ConfigureAwait( false );
			}
		}
		return CommandExitCodes.Success;
	}

	private static async Task<int> SplitByLinesAsync(
		Stream input,
		SplitOptions options,
		OutputManager manager,
		CancellationToken cancellationToken
	) {
		await using var record = TemporarySpool.Create();
		var reader = new BufferedRecordReader( input, options.Separator );
		PieceWriter? piece = null;
		long pieceIndex = 0;
		long recordCount = 0;
		try {
			while ( null != await reader.ReadRecordAsync( record.Stream, cancellationToken ).ConfigureAwait( false ) ) {
				piece ??= await manager.OpenAsync( pieceIndex ).ConfigureAwait( false );
				await CopyAllAsync( record.Stream, piece.Stream, cancellationToken ).ConfigureAwait( false );
				recordCount++;
				if ( recordCount == options.UnitCount ) {
					await piece.DisposeAsync().ConfigureAwait( false );
					piece = null;
					pieceIndex++;
					recordCount = 0;
				}
			}
		} finally {
			if ( null != piece ) {
				await piece.DisposeAsync().ConfigureAwait( false );
			}
		}
		return CommandExitCodes.Success;
	}

	private static async Task<int> SplitByLineBytesAsync(
		Stream input,
		SplitOptions options,
		OutputManager manager,
		CancellationToken cancellationToken
	) {
		await using var record = TemporarySpool.Create();
		var reader = new BufferedRecordReader( input, options.Separator );
		PieceWriter? piece = null;
		long pieceIndex = 0;
		long pieceLength = 0;
		try {
			while ( true ) {
				var recordLength = await reader.ReadRecordAsync( record.Stream, cancellationToken ).ConfigureAwait( false );
				if ( null == recordLength ) {
					break;
				}
				if ( recordLength.Value <= options.UnitCount ) {
					if ( null != piece && pieceLength + recordLength.Value > options.UnitCount ) {
						await piece.DisposeAsync().ConfigureAwait( false );
						piece = null;
						pieceIndex++;
						pieceLength = 0;
					}
					piece ??= await manager.OpenAsync( pieceIndex ).ConfigureAwait( false );
					await CopyAllAsync( record.Stream, piece.Stream, cancellationToken ).ConfigureAwait( false );
					pieceLength += recordLength.Value;
					continue;
				}

				if ( null != piece ) {
					await piece.DisposeAsync().ConfigureAwait( false );
					piece = null;
					pieceIndex++;
					pieceLength = 0;
				}
				record.Stream.Position = 0;
				var remaining = recordLength.Value;
				while ( remaining > options.UnitCount ) {
					await using var fullPiece = await manager.OpenAsync( pieceIndex++ ).ConfigureAwait( false );
					await CopyExactlyAsync( record.Stream, fullPiece.Stream, options.UnitCount, cancellationToken ).ConfigureAwait( false );
					remaining -= options.UnitCount;
				}
				if ( 0 < remaining ) {
					piece = await manager.OpenAsync( pieceIndex ).ConfigureAwait( false );
					await CopyExactlyAsync( record.Stream, piece.Stream, remaining, cancellationToken ).ConfigureAwait( false );
					pieceLength = remaining;
				}
			}
		} finally {
			if ( null != piece ) {
				await piece.DisposeAsync().ConfigureAwait( false );
			}
		}
		return CommandExitCodes.Success;
	}

	private static async Task<int> SplitByChunksAsync(
		Stream input,
		SplitOptions options,
		ByteOutputStream standardOutput,
		OutputManager manager,
		CancellationToken cancellationToken
	) {
		var chunks = options.Chunks!;
		if ( ChunkMode.RoundRobin == chunks.Mode ) {
			return await SplitRoundRobinAsync( input, options, standardOutput, manager, cancellationToken ).ConfigureAwait( false );
		}
		await using var spool = TemporarySpool.Create();
		await input.CopyToAsync( spool.Stream, BufferSize, cancellationToken ).ConfigureAwait( false );
		await spool.RewindAsync( cancellationToken ).ConfigureAwait( false );
		return ChunkMode.Bytes == chunks.Mode
			? await SplitBalancedBytesAsync( spool.Stream, options, standardOutput, manager, cancellationToken ).ConfigureAwait( false )
			: await SplitBalancedRecordsAsync( spool.Stream, options, standardOutput, manager, cancellationToken ).ConfigureAwait( false );
	}

	private static async Task<int> SplitBalancedBytesAsync(
		Stream input,
		SplitOptions options,
		ByteOutputStream standardOutput,
		OutputManager manager,
		CancellationToken cancellationToken
	) {
		var chunks = options.Chunks!;
		var total = input.Length;
		var quotient = total / chunks.Count;
		var remainder = total % chunks.Count;
		for ( var index = 0; index < chunks.Count; index++ ) {
			var count = quotient + ( index < remainder ? 1 : 0 );
			var selected = null == chunks.Selected || chunks.Selected.Value - 1 == index;
			if ( null != chunks.Selected ) {
				if ( selected ) {
					await CopyExactlyAsync( input, standardOutput, count, cancellationToken ).ConfigureAwait( false );
				} else {
					input.Seek( count, SeekOrigin.Current );
				}
				continue;
			}
			if ( 0 == count && options.ElideEmptyFiles ) {
				continue;
			}
			await using var piece = await manager.OpenAsync( index ).ConfigureAwait( false );
			await CopyExactlyAsync( input, piece.Stream, count, cancellationToken ).ConfigureAwait( false );
		}
		return CommandExitCodes.Success;
	}

	private static async Task<int> SplitBalancedRecordsAsync(
		Stream input,
		SplitOptions options,
		ByteOutputStream standardOutput,
		OutputManager manager,
		CancellationToken cancellationToken
	) {
		var chunks = options.Chunks!;
		var totalBytes = input.Length;
		var reader = new BufferedRecordReader( input, options.Separator );
		await using var record = TemporarySpool.Create();
		PieceWriter? piece = null;
		var currentChunk = -1;
		long recordStart = 0;
		long outputIndex = 0;
		try {
			while ( true ) {
				var recordLength = await reader.ReadRecordAsync( record.Stream, cancellationToken ).ConfigureAwait( false );
				if ( null == recordLength ) {
					break;
				}
				var targetChunk = GetBalancedChunkIndex( recordStart, totalBytes, chunks.Count );
				recordStart = checked( recordStart + recordLength.Value );
				if ( null != chunks.Selected ) {
					if ( chunks.Selected.Value - 1 == targetChunk ) {
						await CopyAllAsync( record.Stream, standardOutput, cancellationToken ).ConfigureAwait( false );
					}
					continue;
				}
				if ( targetChunk != currentChunk ) {
					if ( null != piece ) {
						await piece.DisposeAsync().ConfigureAwait( false );
						piece = null;
					}
					if ( !options.ElideEmptyFiles ) {
						for ( var emptyChunk = currentChunk + 1; emptyChunk < targetChunk; emptyChunk++ ) {
							await using var emptyPiece = await manager.OpenAsync( emptyChunk ).ConfigureAwait( false );
						}
					}
					currentChunk = targetChunk;
					piece = await manager.OpenAsync(
						options.ElideEmptyFiles ? outputIndex++ : targetChunk
					).ConfigureAwait( false );
				}
				await CopyAllAsync( record.Stream, piece!.Stream, cancellationToken ).ConfigureAwait( false );
			}
			if ( null == chunks.Selected && !options.ElideEmptyFiles ) {
				if ( null != piece ) {
					await piece.DisposeAsync().ConfigureAwait( false );
					piece = null;
				}
				for ( var emptyChunk = currentChunk + 1; emptyChunk < chunks.Count; emptyChunk++ ) {
					await using var emptyPiece = await manager.OpenAsync( emptyChunk ).ConfigureAwait( false );
				}
			}
		} finally {
			if ( null != piece ) {
				await piece.DisposeAsync().ConfigureAwait( false );
			}
		}
		return CommandExitCodes.Success;
	}

	private static int GetBalancedChunkIndex( long offset, long totalBytes, int count ) {
		if ( totalBytes <= 0 ) {
			return 0;
		}
		var quotient = totalBytes / count;
		var remainder = totalBytes % count;
		var longerRegion = checked( ( quotient + 1 ) * remainder );
		if ( offset < longerRegion ) {
			return (int)( offset / ( quotient + 1 ) );
		}
		if ( 0 == quotient ) {
			return (int)Math.Min( offset, count - 1L );
		}
		return checked( (int)( remainder + ( offset - longerRegion ) / quotient ) );
	}

	private static async Task<int> SplitRoundRobinAsync(
		Stream input,
		SplitOptions options,
		ByteOutputStream standardOutput,
		OutputManager manager,
		CancellationToken cancellationToken
	) {
		var chunks = options.Chunks!;
		var writers = null == chunks.Selected ? new PieceWriter?[chunks.Count] : null;
		var reader = new BufferedRecordReader( input, options.Separator );
		await using var record = TemporarySpool.Create();
		long recordIndex = 0;
		try {
			while ( null != await reader.ReadRecordAsync( record.Stream, cancellationToken ).ConfigureAwait( false ) ) {
				var target = (int)( recordIndex % chunks.Count );
				if ( null != chunks.Selected ) {
					if ( chunks.Selected.Value - 1 == target ) {
						await CopyAllAsync( record.Stream, standardOutput, cancellationToken ).ConfigureAwait( false );
						if ( options.Unbuffered ) {
							await standardOutput.FlushAsync( cancellationToken ).ConfigureAwait( false );
						}
					}
				} else {
					writers![target] ??= await manager.OpenAsync( target ).ConfigureAwait( false );
					await CopyAllAsync( record.Stream, writers[target]!.Stream, cancellationToken ).ConfigureAwait( false );
					if ( options.Unbuffered ) {
						await writers[target]!.Stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
					}
				}
				recordIndex++;
			}
			if ( null != writers && !options.ElideEmptyFiles ) {
				for ( var index = 0; index < writers.Length; index++ ) {
					writers[index] ??= await manager.OpenAsync( index ).ConfigureAwait( false );
				}
			}
		} finally {
			if ( null != writers ) {
				await DisposeWritersAsync( writers ).ConfigureAwait( false );
			}
		}
		return CommandExitCodes.Success;
	}

	private static async Task DisposeWritersAsync( IEnumerable<PieceWriter?> writers ) {
		Exception? failure = null;
		foreach ( var writer in writers ) {
			if ( null == writer ) {
				continue;
			}
			try {
				await writer.DisposeAsync().ConfigureAwait( false );
			} catch ( Exception exception ) {
				failure ??= exception;
			}
		}
		if ( null != failure ) {
			throw failure;
		}
	}

	private static async Task CopyAllAsync(
		Stream source,
		Stream destination,
		CancellationToken cancellationToken
	) {
		source.Position = 0;
		await source.CopyToAsync( destination, BufferSize, cancellationToken ).ConfigureAwait( false );
	}

	private static async Task CopyExactlyAsync(
		Stream source,
		Stream destination,
		long count,
		CancellationToken cancellationToken
	) {
		var buffer = new byte[BufferSize];
		var remaining = count;
		while ( 0 < remaining ) {
			var read = await source.ReadAsync(
				buffer.AsMemory( 0, (int)Math.Min( buffer.Length, remaining ) ),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				throw new EndOfStreamException();
			}
			await destination.WriteAsync( buffer.AsMemory( 0, read ), cancellationToken ).ConfigureAwait( false );
			remaining -= read;
		}
	}

	private static bool IsLegacyLineCountOption( string token ) {
		if ( token.Length < 2 || '-' != token[0] ) {
			return false;
		}
		for ( var index = 1; index < token.Length; index++ ) {
			if ( !char.IsAsciiDigit( token[index] ) ) {
				return false;
			}
		}
		return true;
	}

	private static bool TryParsePositiveLong( string? value, out long result ) => long.TryParse(
		value,
		NumberStyles.None,
		CultureInfo.InvariantCulture,
		out result
	) && 0 < result;

	private static bool TryParseNonnegative( string value, int radix, out long result ) {
		result = 0;
		if ( string.IsNullOrEmpty( value ) ) {
			return false;
		}
		try {
			foreach ( var character in value ) {
				var digit = character >= '0' && character <= '9'
					? character - '0'
					: character >= 'a' && character <= 'f'
						? character - 'a' + 10
						: character >= 'A' && character <= 'F'
							? character - 'A' + 10
							: -1;
				if ( digit < 0 || digit >= radix ) {
					return false;
				}
				result = checked( result * radix + digit );
			}
			return true;
		} catch ( OverflowException ) {
			result = 0;
			return false;
		}
	}

	private static bool TryParseSize( string? value, out long size ) {
		size = 0;
		if ( string.IsNullOrEmpty( value ) ) {
			return false;
		}
		var digitCount = 0;
		while ( digitCount < value.Length && char.IsAsciiDigit( value[digitCount] ) ) {
			digitCount++;
		}
		if ( 0 == digitCount || !long.TryParse( value.AsSpan( 0, digitCount ), NumberStyles.None, CultureInfo.InvariantCulture, out var number ) ) {
			return false;
		}
		var suffix = value.Substring( digitCount );
		if ( !TryGetSizeMultiplier( suffix, out var multiplier ) ) {
			return false;
		}
		try {
			checked {
				size = number * multiplier;
			}
			return true;
		} catch ( OverflowException ) {
			return false;
		}
	}

	private static bool TryGetSizeMultiplier( string suffix, out long multiplier ) {
		multiplier = 1;
		if ( 0 == suffix.Length ) {
			return true;
		}
		if ( "b" == suffix ) {
			multiplier = 512;
			return true;
		}
		var decimalPower = suffix.EndsWith( "B", StringComparison.Ordinal ) && !suffix.EndsWith( "iB", StringComparison.Ordinal );
		var unitText = suffix;
		if ( suffix.EndsWith( "iB", StringComparison.Ordinal ) ) {
			unitText = suffix.Substring( 0, suffix.Length - 2 );
		} else if ( decimalPower ) {
			unitText = suffix.Substring( 0, suffix.Length - 1 );
		}
		if ( 1 != unitText.Length ) {
			return false;
		}
		var exponent = "KMGTPEZYRQ".IndexOf( char.ToUpperInvariant( unitText[0] ) );
		if ( exponent < 0 ) {
			return false;
		}
		exponent++;
		try {
			checked {
				var radix = decimalPower ? 1000L : 1024L;
				for ( var index = 0; index < exponent; index++ ) {
					multiplier *= radix;
				}
			}
			return true;
		} catch ( OverflowException ) {
			return false;
		}
	}

	private static bool TryParseSeparator( string? value, out byte separator ) {
		separator = 0;
		if ( "\\0" == value ) {
			return true;
		}
		if ( string.IsNullOrEmpty( value ) ) {
			return false;
		}
		var bytes = Encoding.UTF8.GetBytes( value );
		if ( 1 != bytes.Length ) {
			return false;
		}
		separator = bytes[0];
		return true;
	}

	private static bool TryParseChunkSpecification(
		string? value,
		out ChunkSpecification specification
	) {
		specification = new ChunkSpecification( ChunkMode.Bytes, 0, null );
		if ( string.IsNullOrEmpty( value ) ) {
			return false;
		}
		var mode = ChunkMode.Bytes;
		if ( value.StartsWith( "l/", StringComparison.Ordinal ) ) {
			mode = ChunkMode.Lines;
			value = value.Substring( 2 );
		} else if ( value.StartsWith( "r/", StringComparison.Ordinal ) ) {
			mode = ChunkMode.RoundRobin;
			value = value.Substring( 2 );
		}
		var parts = value.Split( '/' );
		if ( 1 == parts.Length ) {
			if ( !int.TryParse( parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count ) || count <= 0 ) {
				return false;
			}
			specification = new ChunkSpecification( mode, count, null );
			return true;
		}
		if (
			2 != parts.Length
			|| !int.TryParse( parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var selected )
			|| !int.TryParse( parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var total )
			|| selected <= 0
			|| total <= 0
			|| selected > total
		) {
			return false;
		}
		specification = new ChunkSpecification( mode, total, selected );
		return true;
	}

	private static bool TryConfigureSuffixes( SplitOptions options ) {
		options.ExpandSuffixes = !options.SuffixLengthExplicit
			&& !options.SuffixStartExplicit
			&& SplitMode.Chunks != options.Mode;
		if ( SplitMode.Chunks != options.Mode || null != options.Chunks!.Selected ) {
			return !options.SuffixStartExplicit
				|| FitsSuffixWidth( options.SuffixStart, options.SuffixStyle, options.SuffixLength );
		}
		options.ExpandSuffixes = false;
		if ( options.SuffixLengthExplicit ) {
			return FitsSuffixWidth( options.SuffixStart, options.SuffixStyle, options.SuffixLength );
		}
		long maximumValue;
		try {
			maximumValue = options.SuffixStartExplicit && options.Chunks.Count < options.SuffixStart
				? options.Chunks.Count - 1L
				: checked( options.SuffixStart + options.Chunks.Count - 1L );
		} catch ( OverflowException ) {
			return false;
		}
		options.SuffixLength = Math.Max(
			2,
			GetMinimumSuffixWidth( maximumValue, GetSuffixRadix( options.SuffixStyle ) )
		);
		return FitsSuffixWidth( options.SuffixStart, options.SuffixStyle, options.SuffixLength );
	}

	private static bool FitsSuffixWidth( long value, SuffixStyle style, int width ) {
		if ( value < 0 ) {
			return false;
		}
		var radix = GetSuffixRadix( style );
		for ( var index = 0; index < width; index++ ) {
			value /= radix;
		}
		return 0 == value;
	}

	private static int GetMinimumSuffixWidth( long value, int radix ) {
		var width = 1;
		while ( value >= radix ) {
			value /= radix;
			width++;
		}
		return width;
	}

	private static int GetSuffixRadix( SuffixStyle style ) => style switch {
		SuffixStyle.Decimal => 10,
		SuffixStyle.Hexadecimal => 16,
		_ => 26
	};

	private static string CreateSuffix(
		long value,
		SuffixStyle style,
		int width,
		bool expand
	) {
		var alphabet = style switch {
			SuffixStyle.Decimal => "0123456789",
			SuffixStyle.Hexadecimal => "0123456789abcdef",
			_ => "abcdefghijklmnopqrstuvwxyz"
		};
		return expand
			? EncodeExpandingSuffix( value, width, alphabet )
			: EncodeFixedSuffix( value, alphabet.Length, width, alphabet );
	}

	private static string EncodeFixedSuffix( long value, int radix, int width, string alphabet ) {
		if ( value < 0 ) {
			throw new IOException( "output file suffixes exhausted" );
		}
		var characters = new char[width];
		for ( var index = width - 1; index >= 0; index-- ) {
			characters[index] = alphabet[(int)( value % radix )];
			value /= radix;
		}
		if ( 0 != value ) {
			throw new IOException( "output file suffixes exhausted" );
		}
		return new string( characters );
	}

	private static string EncodeExpandingSuffix( long value, int initialWidth, string alphabet ) {
		if ( value < 0 ) {
			throw new IOException( "output file suffixes exhausted" );
		}
		var stage = 0;
		while ( true ) {
			var tailLength = checked( initialWidth + stage );
			long capacity = alphabet.Length - 1;
			try {
				checked {
					for ( var index = 1; index < tailLength; index++ ) {
						capacity *= alphabet.Length;
					}
				}
			} catch ( OverflowException ) {
				capacity = long.MaxValue;
			}
			if ( value < capacity ) {
				var tail = EncodeFixedSuffix( value, alphabet.Length, tailLength, alphabet );
				return string.Concat( new string( alphabet[^1], stage ), tail );
			}
			value -= capacity;
			stage++;
		}
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string help = """
Usage: split [OPTION]... [FILE [PREFIX]]
Output pieces of FILE to PREFIXaa, PREFIXab, ...; default size is 1000 records.

  -a, --suffix-length=N       generate suffixes of length N
      --additional-suffix=S   append S to output file names
  -b, --bytes=SIZE            put SIZE bytes per output file
  -C, --line-bytes=SIZE       put at most SIZE bytes of records per output file
  -d, --numeric-suffixes[=FROM] use decimal suffixes
  -x, --hex-suffixes[=FROM]   use hexadecimal suffixes
  -e, --elide-empty-files     omit empty output files generated by --number
      --filter=COMMAND        write each piece to shell COMMAND; name is in $FILE
  -l, --lines=NUMBER          put NUMBER records per output file
  -n, --number=CHUNKS         generate CHUNKS outputs (N, K/N, l/N, l/K/N, r/N, r/K/N)
  -t, --separator=SEP         use SEP instead of newline; \\0 specifies NUL
  -u, --unbuffered            flush each record with --number=r/...
      --verbose               announce output files before opening them
      --help                  display this help and exit
      --version               output version information and exit
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
