// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Wc;

using System.Buffers;
using System.Globalization;
using System.Text;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

/// <summary>
/// Counts lines, words, characters, bytes, and maximum display width.
/// </summary>
/// <remarks>
/// Each input is processed in one streaming pass. UTF-8 scalar decoding retains
/// at most three bytes between input buffers and invalid bytes are not counted
/// as characters.
/// </remarks>
public static class Command {

	private const string VersionText = "Icod.CoreUtils.Wc 1.0";

	private enum TotalMode {
		Auto,
		Always,
		Only,
		Never
	}

	private sealed class Options {

		public bool Debug {
			get;
			set;
		}

		public string? FilesZeroFrom {
			get;
			set;
		}

		public bool ShowBytes {
			get;
			set;
		}

		public bool ShowCharacters {
			get;
			set;
		}

		public bool ShowLines {
			get;
			set;
		}

		public bool ShowMaximumLineLength {
			get;
			set;
		}

		public bool ShowWords {
			get;
			set;
		}

		public TotalMode TotalMode {
			get;
			set;
		}

		public bool NeedsUnicodeAnalysis {
			get {
				return this.ShowCharacters
					|| this.ShowWords
					|| this.ShowMaximumLineLength
				;
			}
		}

	}

	private sealed record InputRequest(
		string Value,
		bool Explicit
	);

	private sealed record CountResult(
		string? Name,
		long Lines,
		long Words,
		long Characters,
		long Bytes,
		long MaximumLineLength
	);

	private sealed class CounterState {

		private readonly bool myCountCharacters;
		private readonly bool myCountMaximumLineLength;
		private readonly bool myCountWords;
		private long myCurrentLineLength;
		private long myCurrentLineMaximum;
		private bool myInWord;
		private readonly byte[] myPendingBytes = new byte[ 4 ];
		private int myPendingCount;

		public long Bytes {
			get;
			private set;
		}

		public long Characters {
			get;
			private set;
		}

		public long Lines {
			get;
			private set;
		}

		public long MaximumLineLength {
			get;
			private set;
		}

		public long Words {
			get;
			private set;
		}

		public CounterState(
			bool countCharacters,
			bool countWords,
			bool countMaximumLineLength
		) {
			this.myCountCharacters = countCharacters;
			this.myCountWords = countWords;
			this.myCountMaximumLineLength = countMaximumLineLength;
		}

		public void Process(
			ReadOnlySpan<byte> bytes
		) {
			this.Bytes += bytes.Length;
			foreach ( var value in bytes ) {
				if ( (byte)'\n' == value ) {
					this.Lines++;
				}
			}

			if (
				!this.myCountCharacters
				&& !this.myCountWords
				&& !this.myCountMaximumLineLength
			) {
				return;
			}

			var combined = ArrayPool<byte>.Shared.Rent(
				this.myPendingCount + bytes.Length
			);
			try {
				this.myPendingBytes.AsSpan(
					0,
					this.myPendingCount
				).CopyTo(
					combined
				);
				bytes.CopyTo(
					combined.AsSpan(
						this.myPendingCount
					)
				);
				var length = this.myPendingCount + bytes.Length;
				this.myPendingCount = 0;

				var index = 0;
				while ( index < length ) {
					var status = Rune.DecodeFromUtf8(
						combined.AsSpan(
							index,
							length - index
						),
						out var rune,
						out var consumed
					);
					if ( OperationStatus.Done == status ) {
						this.ProcessRune(
							rune
						);
						index += consumed;
						continue;
					}
					if ( OperationStatus.NeedMoreData == status ) {
						this.myPendingCount = length - index;
						combined.AsSpan(
							index,
							this.myPendingCount
						).CopyTo(
							this.myPendingBytes
						);
						break;
					}

					this.ProcessInvalidByte();
					index++;
				}
			} finally {
				ArrayPool<byte>.Shared.Return(
					combined
				);
			}
		}

		public CountResult Complete(
			string? name
		) {
			for (
				var index = 0;
				index < this.myPendingCount;
				index++
			) {
				this.ProcessInvalidByte();
			}
			this.myPendingCount = 0;
			if ( this.myInWord ) {
				this.Words++;
				this.myInWord = false;
			}
			this.MaximumLineLength = Math.Max(
				this.MaximumLineLength,
				this.myCurrentLineMaximum
			);

			return new CountResult(
				name,
				this.Lines,
				this.Words,
				this.Characters,
				this.Bytes,
				this.MaximumLineLength
			);
		}

		private void ProcessInvalidByte() {
			if ( this.myCountWords ) {
				this.myInWord = true;
			}
		}

		private void ProcessRune(
			Rune rune
		) {
			if ( this.myCountCharacters ) {
				this.Characters++;
			}

			var whiteSpace = IsWordSeparator(
				rune
			);
			if ( this.myCountWords ) {
				if ( whiteSpace ) {
					if ( this.myInWord ) {
						this.Words++;
						this.myInWord = false;
					}
				} else {
					this.myInWord = true;
				}
			}

			if ( !this.myCountMaximumLineLength ) {
				return;
			}

			switch ( rune.Value ) {
				case '\n':
					this.MaximumLineLength = Math.Max(
						this.MaximumLineLength,
						this.myCurrentLineMaximum
					);
					this.myCurrentLineLength = 0;
					this.myCurrentLineMaximum = 0;
					return;
				case '\r':
					this.myCurrentLineLength = 0;
					return;
				case '\b':
					return;
				case '\t':
					this.myCurrentLineLength += 8 - this.myCurrentLineLength % 8;
					this.myCurrentLineMaximum = Math.Max(
						this.myCurrentLineMaximum,
						this.myCurrentLineLength
					);
					return;
			}

			this.myCurrentLineLength += GetDisplayWidth(
				rune
			);
			this.myCurrentLineMaximum = Math.Max(
				this.myCurrentLineMaximum,
				this.myCurrentLineLength
			);
		}

	}

	/// <summary>
	/// Runs the command synchronously.
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
	/// Runs the command asynchronously using optionally injected streams.
	/// </summary>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		Stream? stdinStream = null,
		CancellationToken cancellationToken = default
	) {
		var useConsoleInput = null == stdin;
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		TextReaderStream? inputAdapter = null;
		if ( null == stdinStream ) {
			if ( useConsoleInput ) {
				stdinStream = Console.OpenStandardInput();
			} else {
				inputAdapter = new TextReaderStream(
					stdin
				);
				stdinStream = inputAdapter;
			}
		}

		try {
			return await RunAsync(
				args,
				new CommandContext(
					"wc",
					stdin,
					stdout,
					stderr,
					stdinStream,
					cancellationToken: cancellationToken
				)
			).ConfigureAwait( false );
		} finally {
			inputAdapter?.Dispose();
		}
	}

	/// <summary>
	/// Runs the command using a shared command context.
	/// </summary>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull(
			args
		);
		ArgumentNullException.ThrowIfNull(
			context
		);

		try {
			var options = new Options();
			var operands = new List<string>();
			var parseExitCode = await ParseArgumentsAsync(
				args,
				options,
				operands,
				context
			).ConfigureAwait( false );
			if ( parseExitCode.HasValue ) {
				return parseExitCode.Value;
			}

			if (
				!options.ShowLines
				&& !options.ShowWords
				&& !options.ShowCharacters
				&& !options.ShowBytes
				&& !options.ShowMaximumLineLength
			) {
				options.ShowLines = true;
				options.ShowWords = true;
				options.ShowBytes = true;
			}

			var requests = new List<InputRequest>();
			var implicitStandardInput = false;
			var exitCode = CommandExitCodes.Success;
			if ( null != options.FilesZeroFrom ) {
				if ( 0 < operands.Count ) {
					await context.StandardError.WriteLineAsync(
						"wc: extra operand with --files0-from"
					).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				exitCode = await ReadInputRequestsAsync(
					options.FilesZeroFrom,
					requests,
					context
				).ConfigureAwait( false );
			} else if ( 0 == operands.Count ) {
				requests.Add(
					new InputRequest(
						"-",
						Explicit: false
					)
				);
				implicitStandardInput = true;
			} else {
				foreach ( var operand in operands ) {
					requests.Add(
						new InputRequest(
							operand,
							Explicit: true
						)
					);
				}
			}

			if ( options.Debug ) {
				await context.StandardError.WriteLineAsync(
					"wc: using streaming byte counts and incremental UTF-8 scalar decoding"
				).ConfigureAwait( false );
			}

			var results = new List<CountResult>();
			foreach ( var request in requests ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var operand = InputOperand.Create(
					request.Value
				);
				try {
					await using var source = InputSource.OpenBinary(
						operand,
						context
					);
					results.Add(
						await CountAsync(
							source.BinaryStream!,
							request.Explicit
								? request.Value
								: null,
							options,
							context.CancellationToken
						).ConfigureAwait( false )
					);
				} catch ( Exception ex ) when (
					ex is not OperationCanceledException
				) {
					await context.StandardError.WriteLineAsync(
						$"wc: {operand.DisplayName}: {ex.Message}"
					).ConfigureAwait( false );
					exitCode = CommandExitCodes.Failure;
				}
			}

			WriteResults(
				results,
				options,
				context.StandardOutput,
				implicitStandardInput
			);
			await context.StandardOutput.FlushAsync(
				context.CancellationToken
			).ConfigureAwait( false );
			return exitCode;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) {
			await context.StandardError.WriteLineAsync(
				$"wc: {ex.Message}"
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static async Task<int?> ParseArgumentsAsync(
		string[] args,
		Options options,
		ICollection<string> operands,
		CommandContext context
	) {
		var parser = new OptionParser(
			new OptionDefinition[] {
				new OptionDefinition( "bytes", 'c', new string[] { "bytes" } ),
				new OptionDefinition( "chars", 'm', new string[] { "chars" } ),
				new OptionDefinition( "lines", 'l', new string[] { "lines" } ),
				new OptionDefinition( "debug", longNames: new string[] { "debug" } ),
				new OptionDefinition(
					"files0-from",
					longNames: new string[] { "files0-from" },
					valueArity: OptionValueArity.Required
				),
				new OptionDefinition( "max-line-length", 'L', new string[] { "max-line-length" } ),
				new OptionDefinition( "words", 'w', new string[] { "words" } ),
				new OptionDefinition(
					"total",
					longNames: new string[] { "total" },
					valueArity: OptionValueArity.Required
				),
				new OptionDefinition( "help", longNames: new string[] { "help" } ),
				new OptionDefinition( "version", longNames: new string[] { "version" } )
			},
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute
			}
		);
		var result = parser.Parse(
			args
		);
		if ( !result.IsSuccess ) {
			foreach ( var error in result.Errors ) {
				await context.StandardError.WriteLineAsync(
					OptionDiagnosticFormatter.Format(
						"wc",
						error
					)
				).ConfigureAwait( false );
			}
			return CommandExitCodes.Failure;
		}

		foreach ( var occurrence in result.Options ) {
			switch ( occurrence.Definition.Key ) {
				case "bytes":
					options.ShowBytes = true;
					break;
				case "chars":
					options.ShowCharacters = true;
					break;
				case "lines":
					options.ShowLines = true;
					break;
				case "debug":
					options.Debug = true;
					break;
				case "files0-from":
					options.FilesZeroFrom = occurrence.Value;
					break;
				case "max-line-length":
					options.ShowMaximumLineLength = true;
					break;
				case "words":
					options.ShowWords = true;
					break;
				case "total":
					if (
						!TryParseTotalMode(
							occurrence.Value,
							out var totalMode
						)
					) {
						await context.StandardError.WriteLineAsync(
							$"wc: invalid argument '{occurrence.Value}' for '--total'"
						).ConfigureAwait( false );
						return CommandExitCodes.Failure;
					}
					options.TotalMode = totalMode;
					break;
				case "help":
					PrintUsage(
						context.StandardOutput
					);
					return CommandExitCodes.Success;
				case "version":
					await context.StandardOutput.WriteLineAsync(
						VersionText
					).ConfigureAwait( false );
					return CommandExitCodes.Success;
			}
		}

		foreach ( var operand in result.Operands ) {
			operands.Add(
				operand
			);
		}
		return null;
	}

	private static bool TryParseTotalMode(
		string? value,
		out TotalMode mode
	) {
		switch ( value ) {
			case "auto":
				mode = TotalMode.Auto;
				return true;
			case "always":
				mode = TotalMode.Always;
				return true;
			case "only":
				mode = TotalMode.Only;
				return true;
			case "never":
				mode = TotalMode.Never;
				return true;
			default:
				mode = TotalMode.Auto;
				return false;
		}
	}

	private static async Task<int> ReadInputRequestsAsync(
		string sourceName,
		ICollection<InputRequest> requests,
		CommandContext context
	) {
		var exitCode = CommandExitCodes.Success;
		var listOperand = InputOperand.Create(
			sourceName
		);
		await using var listSource = InputSource.OpenBinary(
			listOperand,
			context
		);
		using var reader = new DelimitedByteRecordReader(
			listSource.BinaryStream!,
			separator: 0
		);

		while ( true ) {
			var record = await reader.ReadAsync(
				context.CancellationToken
			).ConfigureAwait( false );
			if ( null == record ) {
				break;
			}
			var length = (
				0 < record.Length
				&& 0 == record[ ^1 ]
			)
				? record.Length - 1
				: record.Length
			;
			if ( 0 == length ) {
				await context.StandardError.WriteLineAsync(
					"wc: invalid zero-length file name"
				).ConfigureAwait( false );
				exitCode = CommandExitCodes.Failure;
				continue;
			}

			var name = Encoding.UTF8.GetString(
				record,
				0,
				length
			);
			if (
				listOperand.IsStandardInput
				&& "-" == name
			) {
				await context.StandardError.WriteLineAsync(
					"wc: when reading file names from standard input, no file name of '-' is allowed"
				).ConfigureAwait( false );
				exitCode = CommandExitCodes.Failure;
				continue;
			}
			requests.Add(
				new InputRequest(
					name,
					Explicit: true
				)
			);
		}
		return exitCode;
	}

	private static async Task<CountResult> CountAsync(
		Stream input,
		string? name,
		Options options,
		CancellationToken cancellationToken
	) {
		var state = new CounterState(
			options.ShowCharacters,
			options.ShowWords,
			options.ShowMaximumLineLength
		);
		var buffer = ArrayPool<byte>.Shared.Rent(
			StreamOperations.DefaultBufferSize
		);
		try {
			while ( true ) {
				var count = await input.ReadAsync(
					buffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == count ) {
					break;
				}
				state.Process(
					buffer.AsSpan(
						0,
						count
					)
				);
			}
		} finally {
			ArrayPool<byte>.Shared.Return(
				buffer
			);
		}
		return state.Complete(
			name
		);
	}

	private static void WriteResults(
		IReadOnlyList<CountResult> results,
		Options options,
		TextWriter output,
		bool implicitStandardInput
	) {
		var total = Sum(
			results,
			TotalMode.Only == options.TotalMode
				? null
				: "total"
		);
		var includeTotal = options.TotalMode switch {
			TotalMode.Always => true,
			TotalMode.Only => true,
			TotalMode.Never => false,
			_ => 1 < results.Count
		};

		var outputRows = new List<CountResult>();
		if ( TotalMode.Only != options.TotalMode ) {
			outputRows.AddRange(
				results
			);
		}
		if ( includeTotal ) {
			outputRows.Add(
				TotalMode.Only == options.TotalMode
					? total with {
						Name = null
					}
					: total
			);
		}

		var width = implicitStandardInput
			? 7
			: GetFieldWidth(
				outputRows,
				options
			)
		;
		foreach ( var row in outputRows ) {
			var values = GetSelectedValues(
				row,
				options
			);
			for (
				var index = 0;
				index < values.Count;
				index++
			) {
				if ( 0 < index ) {
					output.Write(
						' '
					);
				}
				output.Write(
					values[ index ].ToString(
						CultureInfo.InvariantCulture
					).PadLeft(
						width
					)
				);
			}
			if ( null != row.Name ) {
				if ( 0 < values.Count ) {
					output.Write(
						' '
					);
				}
				output.Write(
					row.Name
				);
			}
			output.WriteLine();
		}
	}

	private static CountResult Sum(
		IReadOnlyList<CountResult> results,
		string? name
	) {
		return new CountResult(
			name,
			results.Sum( result => result.Lines ),
			results.Sum( result => result.Words ),
			results.Sum( result => result.Characters ),
			results.Sum( result => result.Bytes ),
			0 == results.Count
				? 0
				: results.Max( result => result.MaximumLineLength )
		);
	}

	private static int GetFieldWidth(
		IReadOnlyList<CountResult> rows,
		Options options
	) {
		var width = 1;
		foreach ( var row in rows ) {
			foreach ( var value in GetSelectedValues( row, options ) ) {
				width = Math.Max(
					width,
					value.ToString(
						CultureInfo.InvariantCulture
					).Length
				);
			}
		}
		return width;
	}

	private static List<long> GetSelectedValues(
		CountResult result,
		Options options
	) {
		var output = new List<long>();
		if ( options.ShowLines ) {
			output.Add(
				result.Lines
			);
		}
		if ( options.ShowWords ) {
			output.Add(
				result.Words
			);
		}
		if ( options.ShowCharacters ) {
			output.Add(
				result.Characters
			);
		}
		if ( options.ShowBytes ) {
			output.Add(
				result.Bytes
			);
		}
		if ( options.ShowMaximumLineLength ) {
			output.Add(
				result.MaximumLineLength
			);
		}
		return output;
	}

	private static bool IsWordSeparator(
		Rune rune
	) {
		return Rune.IsWhiteSpace(
			rune
		)
			|| 0x00A0 == rune.Value
			|| 0x2007 == rune.Value
			|| 0x202F == rune.Value
			|| 0x2060 == rune.Value
		;
	}

	private static int GetDisplayWidth(
		Rune rune
	) {
		var category = Rune.GetUnicodeCategory(
			rune
		);
		if (
			UnicodeCategory.Control == category
			|| UnicodeCategory.Format == category
			|| UnicodeCategory.NonSpacingMark == category
			|| UnicodeCategory.EnclosingMark == category
		) {
			return 0;
		}

		var value = rune.Value;
		return (
			0x1100 <= value
			&& (
				value <= 0x115F
				|| 0x2329 == value
				|| 0x232A == value
				|| (
					0x2E80 <= value
					&& value <= 0xA4CF
					&& 0x303F != value
				)
				|| (
					0xAC00 <= value
					&& value <= 0xD7A3
				)
				|| (
					0xF900 <= value
					&& value <= 0xFAFF
				)
				|| (
					0xFE10 <= value
					&& value <= 0xFE19
				)
				|| (
					0xFE30 <= value
					&& value <= 0xFE6F
				)
				|| (
					0xFF00 <= value
					&& value <= 0xFF60
				)
				|| (
					0xFFE0 <= value
					&& value <= 0xFFE6
				)
				|| (
					0x1F300 <= value
					&& value <= 0x1FAFF
				)
				|| (
					0x20000 <= value
					&& value <= 0x3FFFD
				)
			)
		)
			? 2
			: 1
		;
	}

	private static void PrintUsage(
		TextWriter output
	) {
		output.WriteLine(
			"Usage: wc [OPTION]... [FILE]..."
		);
		output.WriteLine(
			"Print newline, word, and byte counts for each FILE."
		);
		output.WriteLine();
		output.WriteLine(
			"  -c, --bytes            print byte counts"
		);
		output.WriteLine(
			"  -m, --chars            print character counts"
		);
		output.WriteLine(
			"  -l, --lines            print newline counts"
		);
		output.WriteLine(
			"      --files0-from=F    read NUL-terminated file names from F"
		);
		output.WriteLine(
			"  -L, --max-line-length  print maximum display width"
		);
		output.WriteLine(
			"  -w, --words            print word counts"
		);
		output.WriteLine(
			"      --total=WHEN       WHEN is auto, always, only, or never"
		);
		output.WriteLine(
			"      --debug            print implementation diagnostics"
		);
		output.WriteLine(
			"      --help             display this help and exit"
		);
		output.WriteLine(
			"      --version          output version information and exit"
		);
	}

}
