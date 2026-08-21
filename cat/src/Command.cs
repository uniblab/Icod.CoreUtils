// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Cat;

using System.Buffers;
using System.Globalization;
using System.Text;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;

/// <summary>
/// Concatenates files to standard output.
/// </summary>
/// <remarks>
/// Input and output are processed as bytes. When no display transformation is
/// requested, data is copied without decoding or newline normalization.
/// </remarks>
public static class Command {

	private const string VersionText = "Icod.CoreUtils.Cat 1.0";

	private sealed class Options {

		public bool Number {
			get;
			set;
		}

		public bool NumberNonblank {
			get;
			set;
		}

		public bool ShowEnds {
			get;
			set;
		}

		public bool ShowNonprinting {
			get;
			set;
		}

		public bool ShowTabs {
			get;
			set;
		}

		public bool SqueezeBlank {
			get;
			set;
		}

		public bool RequiresTransformation {
			get {
				return this.Number
					|| this.NumberNonblank
					|| this.ShowEnds
					|| this.ShowNonprinting
					|| this.ShowTabs
					|| this.SqueezeBlank
				;
			}
		}

	}

	private sealed class TransformState {

		public bool AtLineStart {
			get;
			set;
		} = true;

		public long LineNumber {
			get;
			set;
		} = 1;

		public bool PendingCarriageReturn {
			get;
			set;
		}

		public bool PreviousLineBlank {
			get;
			set;
		}

	}

	/// <summary>
	/// Executes <c>cat</c> synchronously with optional standard-stream substitution.
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
	/// Executes <c>cat</c> asynchronously with optional injected standard streams.
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
		var useConsoleInput = null == stdin;
		var useConsoleOutput = null == stdout;
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
		if (
			null == stdoutStream
			&& useConsoleOutput
		) {
			stdoutStream = Console.OpenStandardOutput();
		}

		try {
			return await RunAsync(
				args,
				new CommandContext(
					"cat",
					stdin,
					stdout,
					stderr,
					stdinStream,
					stdoutStream,
					cancellationToken: cancellationToken
				)
			).ConfigureAwait( false );
		} finally {
			inputAdapter?.Dispose();
		}
	}

	/// <summary>
	/// Executes <c>cat</c> asynchronously using a complete shared command context.
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

			if ( options.NumberNonblank ) {
				options.Number = false;
			}
			if ( 0 == operands.Count ) {
				operands.Add(
					"-"
				);
			}

			using var output = new ByteOutputStream(
				context.StandardOutput,
				context.StandardOutputStream
			);
			var state = new TransformState();
			var exitCode = CommandExitCodes.Success;

			foreach ( var value in operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var operand = InputOperand.Create(
					value
				);
				try {
					await using var source = InputSource.OpenBinary(
						operand,
						context
					);
					if ( options.RequiresTransformation ) {
						await CopyTransformedAsync(
							source.BinaryStream!,
							output,
							options,
							state,
							context.CancellationToken
						).ConfigureAwait( false );
					} else {
						await StreamOperations.CopyAsync(
							source.BinaryStream!,
							output,
							cancellationToken: context.CancellationToken
						).ConfigureAwait( false );
					}
				} catch ( Exception ex ) when (
					ex is not OperationCanceledException
				) {
					await context.StandardError.WriteLineAsync(
						$"cat: {operand.DisplayName}: {ex.Message}"
					).ConfigureAwait( false );
					exitCode = CommandExitCodes.Failure;
				}
			}

			if (
				options.RequiresTransformation
				&& state.PendingCarriageReturn
			) {
				var finalOutput = new ArrayBufferWriter<byte>();
				AppendVisibleByte(
					finalOutput,
					(byte)'\r',
					options,
					beforeNewLine: false
				);
				await output.WriteAsync(
					finalOutput.WrittenMemory,
					context.CancellationToken
				).ConfigureAwait( false );
			}

			await output.CompleteAsync(
				context.CancellationToken
			).ConfigureAwait( false );
			return exitCode;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) {
			await context.StandardError.WriteLineAsync(
				$"cat: {ex.Message}"
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
				new OptionDefinition( "show-all", 'A', new string[] { "show-all" } ),
				new OptionDefinition( "number-nonblank", 'b', new string[] { "number-nonblank" } ),
				new OptionDefinition( "show-ends-and-nonprinting", 'e' ),
				new OptionDefinition( "show-ends", 'E', new string[] { "show-ends" } ),
				new OptionDefinition( "number", 'n', new string[] { "number" } ),
				new OptionDefinition( "squeeze-blank", 's', new string[] { "squeeze-blank" } ),
				new OptionDefinition( "show-tabs-and-nonprinting", 't' ),
				new OptionDefinition( "show-tabs", 'T', new string[] { "show-tabs" } ),
				new OptionDefinition( "ignored-unbuffered", 'u' ),
				new OptionDefinition( "show-nonprinting", 'v', new string[] { "show-nonprinting" } ),
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
						"cat",
						error
					)
				).ConfigureAwait( false );
			}
			return CommandExitCodes.Failure;
		}

		foreach ( var occurrence in result.Options ) {
			switch ( occurrence.Definition.Key ) {
				case "show-all":
					options.ShowNonprinting = true;
					options.ShowEnds = true;
					options.ShowTabs = true;
					break;
				case "number-nonblank":
					options.NumberNonblank = true;
					break;
				case "show-ends-and-nonprinting":
					options.ShowNonprinting = true;
					options.ShowEnds = true;
					break;
				case "show-ends":
					options.ShowEnds = true;
					break;
				case "number":
					options.Number = true;
					break;
				case "squeeze-blank":
					options.SqueezeBlank = true;
					break;
				case "show-tabs-and-nonprinting":
					options.ShowNonprinting = true;
					options.ShowTabs = true;
					break;
				case "show-tabs":
					options.ShowTabs = true;
					break;
				case "show-nonprinting":
					options.ShowNonprinting = true;
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

	private static async Task CopyTransformedAsync(
		Stream input,
		Stream output,
		Options options,
		TransformState state,
		CancellationToken cancellationToken
	) {
		var inputBuffer = ArrayPool<byte>.Shared.Rent(
			StreamOperations.DefaultBufferSize
		);
		try {
			while ( true ) {
				var count = await input.ReadAsync(
					inputBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == count ) {
					return;
				}

				var transformed = new ArrayBufferWriter<byte>(
					Math.Max(
						256,
						count
					)
				);
				for (
					var index = 0;
					index < count;
					index++
				) {
					ProcessByte(
						inputBuffer[ index ],
						transformed,
						options,
						state
					);
				}
				await output.WriteAsync(
					transformed.WrittenMemory,
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			ArrayPool<byte>.Shared.Return(
				inputBuffer
			);
		}
	}

	private static void ProcessByte(
		byte value,
		ArrayBufferWriter<byte> output,
		Options options,
		TransformState state
	) {
		if ( state.PendingCarriageReturn ) {
			AppendVisibleByte(
				output,
				(byte)'\r',
				options,
				beforeNewLine: (byte)'\n' == value
			);
			state.PendingCarriageReturn = false;
		}

		if (
			(byte)'\r' == value
			&& options.ShowEnds
			&& !options.ShowNonprinting
		) {
			EnsureLineNumber(
				output,
				options,
				state,
				isBlankLine: false
			);
			state.AtLineStart = false;
			state.PendingCarriageReturn = true;
			return;
		}

		if ( (byte)'\n' == value ) {
			var blankLine = state.AtLineStart;
			if (
				options.SqueezeBlank
				&& blankLine
				&& state.PreviousLineBlank
			) {
				return;
			}

			EnsureLineNumber(
				output,
				options,
				state,
				blankLine
			);
			if ( options.ShowEnds ) {
				AppendAscii(
					output,
					"$"
				);
			}
			AppendByte(
				output,
				value
			);
			state.AtLineStart = true;
			state.PreviousLineBlank = blankLine;
			return;
		}

		EnsureLineNumber(
			output,
			options,
			state,
			isBlankLine: false
		);
		state.AtLineStart = false;
		AppendVisibleByte(
			output,
			value,
			options,
			beforeNewLine: false
		);
	}

	private static void EnsureLineNumber(
		ArrayBufferWriter<byte> output,
		Options options,
		TransformState state,
		bool isBlankLine
	) {
		if ( !state.AtLineStart ) {
			return;
		}
		if (
			options.NumberNonblank
			&& isBlankLine
		) {
			return;
		}
		if (
			!options.Number
			&& !options.NumberNonblank
		) {
			return;
		}

		AppendAscii(
			output,
			state.LineNumber.ToString(
				CultureInfo.InvariantCulture
			).PadLeft(
				6
			)
		);
		AppendByte(
			output,
			(byte)'\t'
		);
		state.LineNumber++;
	}

	private static void AppendVisibleByte(
		ArrayBufferWriter<byte> output,
		byte value,
		Options options,
		bool beforeNewLine
	) {
		if (
			(byte)'\t' == value
			&& options.ShowTabs
		) {
			AppendAscii(
				output,
				"^I"
			);
			return;
		}

		if (
			(byte)'\r' == value
			&& beforeNewLine
			&& options.ShowEnds
		) {
			AppendAscii(
				output,
				"^M"
			);
			return;
		}

		if ( !options.ShowNonprinting ) {
			AppendByte(
				output,
				value
			);
			return;
		}

		if ( 128 <= value ) {
			AppendAscii(
				output,
				"M-"
			);
			value -= 128;
		}
		if ( value < 32 ) {
			if (
				(byte)'\t' == value
				|| (byte)'\n' == value
			) {
				AppendByte(
					output,
					value
				);
			} else {
				AppendByte(
					output,
					(byte)'^'
				);
				AppendByte(
					output,
					(byte)( value + 64 )
				);
			}
			return;
		}
		if ( 127 == value ) {
			AppendAscii(
				output,
				"^?"
			);
			return;
		}
		AppendByte(
			output,
			value
		);
	}

	private static void AppendAscii(
		ArrayBufferWriter<byte> output,
		string value
	) {
		var span = output.GetSpan(
			value.Length
		);
		for (
			var index = 0;
			index < value.Length;
			index++
		) {
			span[ index ] = checked( (byte)value[ index ] );
		}
		output.Advance(
			value.Length
		);
	}

	private static void AppendByte(
		ArrayBufferWriter<byte> output,
		byte value
	) {
		var span = output.GetSpan(
			1
		);
		span[ 0 ] = value;
		output.Advance(
			1
		);
	}

	private static void PrintUsage(
		TextWriter output
	) {
		output.WriteLine(
			"Usage: cat [OPTION]... [FILE]..."
		);
		output.WriteLine(
			"Concatenate FILE(s) to standard output."
		);
		output.WriteLine();
		output.WriteLine(
			"  -A, --show-all           equivalent to -vET"
		);
		output.WriteLine(
			"  -b, --number-nonblank    number nonempty output lines"
		);
		output.WriteLine(
			"  -e                       equivalent to -vE"
		);
		output.WriteLine(
			"  -E, --show-ends          display $ at the end of each line"
		);
		output.WriteLine(
			"  -n, --number             number all output lines"
		);
		output.WriteLine(
			"  -s, --squeeze-blank      suppress repeated empty output lines"
		);
		output.WriteLine(
			"  -t                       equivalent to -vT"
		);
		output.WriteLine(
			"  -T, --show-tabs          display TAB characters as ^I"
		);
		output.WriteLine(
			"  -u                       ignored"
		);
		output.WriteLine(
			"  -v, --show-nonprinting   use ^ and M- notation"
		);
		output.WriteLine(
			"      --help               display this help and exit"
		);
		output.WriteLine(
			"      --version            output version information and exit"
		);
	}

}
