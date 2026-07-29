// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Tee;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

/// <summary>
/// Copies standard input to standard output and each named file.
/// </summary>
public static class Command {

	private const string VersionText = "Icod.CoreUtils.Tee 1.0";

	private enum OutputErrorMode {
		Default,
		Warn,
		WarnNoPipe,
		Exit,
		ExitNoPipe
	}

	private sealed class Options {

		public bool Append {
			get;
			set;
		}

		public bool IgnoreInterrupts {
			get;
			set;
		}

		public OutputErrorMode OutputErrorMode {
			get;
			set;
		}

	}

	private sealed class OutputTarget : IAsyncDisposable {

		public bool Active {
			get;
			set;
		} = true;

		public bool IsPipe {
			get;
		}

		public string Name {
			get;
		}

		public bool OwnsStream {
			get;
		}

		public Stream Stream {
			get;
		}

		public OutputTarget(
			string name,
			Stream stream,
			bool isPipe,
			bool ownsStream
		) {
			this.Name = name;
			this.Stream = stream;
			this.IsPipe = isPipe;
			this.OwnsStream = ownsStream;
		}

		public async ValueTask DisposeAsync() {
			if ( this.OwnsStream ) {
				await this.Stream.DisposeAsync().ConfigureAwait( false );
			}
		}

	}

	/// <summary>
	/// Executes <c>tee</c> synchronously with optional standard-stream substitution.
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
	/// Executes <c>tee</c> asynchronously with optional injected standard streams.
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
					"tee",
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
	/// Executes <c>tee</c> asynchronously using a complete shared command context.
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

		var targets = new List<OutputTarget>();
		ByteOutputStream? standardOutput = null;
		try {
			var options = new Options();
			var files = new List<string>();
			var parseExitCode = await ParseArgumentsAsync(
				args,
				options,
				files,
				context
			).ConfigureAwait( false );
			if ( parseExitCode.HasValue ) {
				return parseExitCode.Value;
			}

			var cancellationToken = options.IgnoreInterrupts
				? CancellationToken.None
				: context.CancellationToken
			;
			standardOutput = new ByteOutputStream(
				context.StandardOutput,
				context.StandardOutputStream
			);
			targets.Add(
				new OutputTarget(
					"standard output",
					standardOutput,
					isPipe: true,
					ownsStream: false
				)
			);

			var exitCode = CommandExitCodes.Success;
			foreach ( var file in files ) {
				try {
					var stream = new FileStream(
						file,
						options.Append
							? FileMode.Append
							: FileMode.Create,
						FileAccess.Write,
						FileShare.Read | FileShare.Delete,
						StreamOperations.DefaultBufferSize,
						FileOptions.Asynchronous | FileOptions.SequentialScan
					);
					targets.Add(
						new OutputTarget(
							file,
							stream,
							isPipe: false,
							ownsStream: true
						)
					);
				} catch ( Exception ex ) {
					await context.StandardError.WriteLineAsync(
						$"tee: {file}: {ex.Message}"
					).ConfigureAwait( false );
					exitCode = CommandExitCodes.Failure;
				}
			}

			var input = context.StandardInputStream ?? throw new InvalidOperationException(
				"A binary standard-input stream was not supplied."
			);
			var buffer = new byte[
				StreamOperations.DefaultBufferSize
			];

			while ( true ) {
				cancellationToken.ThrowIfCancellationRequested();
				var count = await input.ReadAsync(
					buffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == count ) {
					break;
				}

				foreach ( var target in targets ) {
					if ( !target.Active ) {
						continue;
					}
					try {
						await target.Stream.WriteAsync(
							buffer.AsMemory(
								0,
								count
							),
							cancellationToken
						).ConfigureAwait( false );
					} catch ( Exception ex ) when (
						ex is not OperationCanceledException
					) {
						target.Active = false;
						var action = await HandleOutputErrorAsync(
							target,
							ex,
							options.OutputErrorMode,
							context.StandardError
						).ConfigureAwait( false );
						exitCode = CommandExitCodes.Failure;
						if ( action ) {
							return exitCode;
						}
					}
				}

				if (
					targets.All(
						target => !target.Active
					)
				) {
					return CommandExitCodes.Failure;
				}
			}

			foreach ( var target in targets ) {
				if ( !target.Active ) {
					continue;
				}
				try {
					if ( ReferenceEquals( target.Stream, standardOutput ) ) {
						await standardOutput.CompleteAsync(
							cancellationToken
						).ConfigureAwait( false );
					} else {
						await target.Stream.FlushAsync(
							cancellationToken
						).ConfigureAwait( false );
					}
				} catch ( Exception ex ) when (
					ex is not OperationCanceledException
				) {
					var immediate = await HandleOutputErrorAsync(
						target,
						ex,
						options.OutputErrorMode,
						context.StandardError
					).ConfigureAwait( false );
					exitCode = CommandExitCodes.Failure;
					if ( immediate ) {
						return exitCode;
					}
				}
			}

			return exitCode;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) {
			await context.StandardError.WriteLineAsync(
				$"tee: {ex.Message}"
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		} finally {
			foreach ( var target in targets ) {
				try {
					await target.DisposeAsync().ConfigureAwait( false );
				} catch {
				}
			}
			standardOutput?.Dispose();
		}
	}

	/// <summary>
	/// Examines command-line options to determine whether interrupt signals should be ignored before command execution begins.
	/// </summary>
	/// <param name="args">The raw command-line arguments to inspect.</param>
	/// <returns><see langword="true"/> when <c>--ignore-interrupts</c> or <c>-i</c> is present.</returns>
	public static bool RequestsIgnoredInterrupts(
		IReadOnlyList<string> args
	) {
		var parsingOptions = true;
		foreach ( var argument in args ) {
			if ( parsingOptions && "--" == argument ) {
				parsingOptions = false;
				continue;
			}
			if ( !parsingOptions ) {
				continue;
			}
			if (
				argument.StartsWith(
					"--",
					StringComparison.Ordinal
				)
				&& !argument.Contains(
					'='
				)
				&& "--ignore-interrupts".StartsWith(
					argument,
					StringComparison.Ordinal
				)
			) {
				return true;
			}
			if (
				argument.StartsWith(
					"-",
					StringComparison.Ordinal
				)
				&& !argument.StartsWith(
					"--",
					StringComparison.Ordinal
				)
				&& argument.Contains(
					'i'
				)
			) {
				return true;
			}
		}
		return false;
	}

	private static async Task<int?> ParseArgumentsAsync(
		string[] args,
		Options options,
		ICollection<string> files,
		CommandContext context
	) {
		var parser = new OptionParser(
			new OptionDefinition[] {
				new OptionDefinition( "append", 'a', new string[] { "append" } ),
				new OptionDefinition( "ignore-interrupts", 'i', new string[] { "ignore-interrupts" } ),
				new OptionDefinition( "diagnose-pipe-errors", 'p' ),
				new OptionDefinition(
					"output-error",
					longNames: new string[] { "output-error" },
					valueArity: OptionValueArity.Optional
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
						"tee",
						error
					)
				).ConfigureAwait( false );
			}
			return CommandExitCodes.Failure;
		}

		var explicitOutputMode = false;
		foreach ( var occurrence in result.Options ) {
			switch ( occurrence.Definition.Key ) {
				case "append":
					options.Append = true;
					break;
				case "ignore-interrupts":
					options.IgnoreInterrupts = true;
					break;
				case "diagnose-pipe-errors":
					if ( !explicitOutputMode ) {
						options.OutputErrorMode = OutputErrorMode.WarnNoPipe;
					}
					break;
				case "output-error":
					explicitOutputMode = true;
					if (
						!TryParseOutputErrorMode(
							occurrence.Value,
							out var mode
						)
					) {
						await context.StandardError.WriteLineAsync(
							$"tee: invalid argument '{occurrence.Value}' for '--output-error'"
						).ConfigureAwait( false );
						return CommandExitCodes.Failure;
					}
					options.OutputErrorMode = mode;
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
			files.Add(
				operand
			);
		}
		return null;
	}

	private static bool TryParseOutputErrorMode(
		string? value,
		out OutputErrorMode mode
	) {
		switch ( value ) {
			case null:
			case "":
			case "warn":
				mode = OutputErrorMode.Warn;
				return true;
			case "warn-nopipe":
				mode = OutputErrorMode.WarnNoPipe;
				return true;
			case "exit":
				mode = OutputErrorMode.Exit;
				return true;
			case "exit-nopipe":
				mode = OutputErrorMode.ExitNoPipe;
				return true;
			default:
				mode = OutputErrorMode.Default;
				return false;
		}
	}

	private static async Task<bool> HandleOutputErrorAsync(
		OutputTarget target,
		Exception exception,
		OutputErrorMode mode,
		TextWriter error
	) {
		var diagnose = mode switch {
			OutputErrorMode.Warn => true,
			OutputErrorMode.WarnNoPipe => !target.IsPipe,
			OutputErrorMode.Exit => true,
			OutputErrorMode.ExitNoPipe => !target.IsPipe,
			_ => !target.IsPipe
		};
		var exitImmediately = mode switch {
			OutputErrorMode.Exit => true,
			OutputErrorMode.ExitNoPipe => !target.IsPipe,
			OutputErrorMode.Default => target.IsPipe,
			_ => false
		};

		if ( diagnose ) {
			await error.WriteLineAsync(
				$"tee: {target.Name}: {exception.Message}"
			).ConfigureAwait( false );
		}
		return exitImmediately;
	}

	private static void PrintUsage(
		TextWriter output
	) {
		output.WriteLine(
			"Usage: tee [OPTION]... [FILE]..."
		);
		output.WriteLine(
			"Copy standard input to each FILE, and also to standard output."
		);
		output.WriteLine();
		output.WriteLine(
			"  -a, --append              append to the given FILEs"
		);
		output.WriteLine(
			"  -i, --ignore-interrupts   ignore interrupt signals"
		);
		output.WriteLine(
			"  -p                        diagnose errors writing to non pipes"
		);
		output.WriteLine(
			"      --output-error[=MODE] set write-error behavior"
		);
		output.WriteLine(
			"      --help                display this help and exit"
		);
		output.WriteLine(
			"      --version             output version information and exit"
		);
		output.WriteLine();
		output.WriteLine(
			"MODE is warn, warn-nopipe, exit, or exit-nopipe."
		);
	}

}
