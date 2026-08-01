// Original behavior/reference: GNU patch 2.8
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.Patch;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

/// <summary>Implements the GNU-compatible <c>patch</c> command front end.</summary>
public static class Command {
	private const string VersionText = "patch (Icod.Patch) 0.2";
	private static readonly HashSet<string> ImplementedOptionKeys = new( StringComparer.Ordinal ) {
		"binary",
		"help",
		"input",
		"version"
	};

	private sealed class PatchUsageException : Exception {
		/// <summary>Initializes a usage exception.</summary>
		/// <param name="message">The diagnostic message.</param>
		public PatchUsageException( string message )
			: base( message ) {
		}
	}

	/// <summary>Runs the command synchronously using supplied text streams.</summary>
	/// <param name="arguments">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <returns>The process status.</returns>
	public static int Run(
		IReadOnlyList<string>? arguments,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		return RunAsync( arguments, stdin, stdout, stderr ).GetAwaiter().GetResult();
	}

	/// <summary>Runs the command asynchronously using supplied streams.</summary>
	/// <param name="arguments">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="stdinStream">The byte-preserving standard-input stream.</param>
	/// <returns>A task whose result is the process status.</returns>
	public static async Task<int> RunAsync(
		IReadOnlyList<string>? arguments,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default,
		Stream? stdinStream = null
	) {
		stdin ??= TextReader.Null;
		stdout ??= TextWriter.Null;
		stderr ??= TextWriter.Null;
		TextReaderStream? adapter = null;
		if ( null == stdinStream ) {
			adapter = new TextReaderStream( stdin, leaveOpen: true );
			stdinStream = adapter;
		}
		try {
			return await RunAsync(
				arguments,
				new CommandContext(
					"patch",
					stdin,
					stdout,
					stderr,
					stdinStream,
					cancellationToken: cancellationToken
				)
			).ConfigureAwait( false );
		} finally {
			adapter?.Dispose();
		}
	}

	/// <summary>Runs the command within an existing command context.</summary>
	/// <param name="arguments">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the process status.</returns>
	public static async Task<int> RunAsync(
		IReadOnlyList<string>? arguments,
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull( context );
		try {
			var parsed = CreateParser().Parse( arguments );
			if ( !parsed.IsSuccess ) {
				foreach ( var error in parsed.Errors ) {
					await context.StandardError.WriteLineAsync(
						OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
						context.CancellationToken
					).ConfigureAwait( false );
				}
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return (int)PatchExitStatus.Trouble;
			}
			if ( parsed.HasOption( "help" ) ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return (int)PatchExitStatus.Success;
			}
			if ( parsed.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					VersionText.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return (int)PatchExitStatus.Success;
			}
			ValidateImplementedOptions( parsed );
			var options = CreateOptions( parsed );
			return await PatchApplication.ExecuteAsync( options, context ).ConfigureAwait( false );
		} catch ( PatchUsageException exception ) {
			await context.Diagnostics.ErrorAsync(
				exception.Message,
				CancellationToken.None
			).ConfigureAwait( false );
			await WriteTryHelpAsync( context ).ConfigureAwait( false );
			return (int)PatchExitStatus.Trouble;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when ( PatchApplication.IsOperationalException( exception ) ) {
			await context.Diagnostics.ErrorAsync(
				exception.Message,
				CancellationToken.None
			).ConfigureAwait( false );
			return (int)PatchExitStatus.Trouble;
		}
	}

	/// <summary>Creates the declarative option parser used by <c>patch</c>.</summary>
	/// <returns>The option parser.</returns>
	public static OptionParser CreateParser() {
		return new OptionParser(
			new OptionDefinition[] {
				new( "backup", 'b', new[] { "backup" } ),
				new( "prefix", 'B', new[] { "prefix" }, OptionValueArity.Required ),
				new( "context", 'c', new[] { "context" } ),
				new( "directory", 'd', new[] { "directory" }, OptionValueArity.Required ),
				new( "ifdef", 'D', new[] { "ifdef" }, OptionValueArity.Required ),
				new( "ed", 'e', new[] { "ed" } ),
				new( "remove-empty-files", 'E', new[] { "remove-empty-files" } ),
				new( "force", 'f', new[] { "force" } ),
				new( "fuzz", 'F', new[] { "fuzz" }, OptionValueArity.Required ),
				new( "get", 'g', new[] { "get" }, OptionValueArity.Required ),
				new( "input", 'i', new[] { "input" }, OptionValueArity.Required ),
				new( "ignore-whitespace", 'l', new[] { "ignore-whitespace" } ),
				new( "merge-short", 'm' ),
				new( "merge", longNames: new[] { "merge" }, valueArity: OptionValueArity.Optional ),
				new( "normal", 'n', new[] { "normal" } ),
				new( "forward", 'N', new[] { "forward" } ),
				new( "output", 'o', new[] { "output" }, OptionValueArity.Required ),
				new( "strip", 'p', new[] { "strip" }, OptionValueArity.Required ),
				new( "reject-file", 'r', new[] { "reject-file" }, OptionValueArity.Required ),
				new( "reverse", 'R', new[] { "reverse" } ),
				new( "quiet", 's', new[] { "quiet", "silent" } ),
				new( "batch", 't', new[] { "batch" } ),
				new( "set-time", 'T', new[] { "set-time" } ),
				new( "unified", 'u', new[] { "unified" } ),
				new( "version", 'v', new[] { "version" } ),
				new( "version-control", 'V', new[] { "version-control" }, OptionValueArity.Required ),
				new( "debug", 'x', new[] { "debug" }, OptionValueArity.Required ),
				new( "basename-prefix", 'Y', new[] { "basename-prefix" }, OptionValueArity.Required ),
				new( "suffix", 'z', new[] { "suffix" }, OptionValueArity.Required ),
				new( "set-utc", 'Z', new[] { "set-utc" } ),
				new( "dry-run", longNames: new[] { "dry-run" } ),
				new( "verbose", longNames: new[] { "verbose" } ),
				new( "binary", longNames: new[] { "binary" } ),
				new( "help", longNames: new[] { "help" } ),
				new( "backup-if-mismatch", longNames: new[] { "backup-if-mismatch" } ),
				new( "no-backup-if-mismatch", longNames: new[] { "no-backup-if-mismatch" } ),
				new( "posix", longNames: new[] { "posix" } ),
				new( "quoting-style", longNames: new[] { "quoting-style" }, valueArity: OptionValueArity.Required ),
				new( "reject-format", longNames: new[] { "reject-format" }, valueArity: OptionValueArity.Required ),
				new( "read-only", longNames: new[] { "read-only" }, valueArity: OptionValueArity.Required ),
				new( "follow-symlinks", longNames: new[] { "follow-symlinks" } )
			},
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute
			}
		);
	}

	private static void ValidateImplementedOptions( OptionParseResult parsed ) {
		var unsupported = parsed.Options.FirstOrDefault(
			item => !ImplementedOptionKeys.Contains( item.Definition.Key )
		);
		if ( null != unsupported ) {
			throw new PatchUsageException(
				string.Concat(
					unsupported.Spelling,
					": option is reserved for a later Icod.Patch phase"
				)
			);
		}
	}

	private static PatchOptions CreateOptions( OptionParseResult parsed ) {
		if ( 2 < parsed.Operands.Count ) {
			throw new PatchUsageException( string.Concat( "extra operand '", parsed.Operands[2], "'" ) );
		}
		var optionInput = parsed.GetLastValue( "input" );
		var operandInput = 1 < parsed.Operands.Count ? parsed.Operands[1] : null;
		if ( null != optionInput && null != operandInput ) {
			throw new PatchUsageException( "patch source specified by both -i and an operand" );
		}
		return new PatchOptions {
			OriginalFile = 0 < parsed.Operands.Count ? parsed.Operands[0] : null,
			PatchFile = optionInput ?? operandInput,
			Binary = parsed.HasOption( "binary" )
		};
	}

	private static async Task WriteTryHelpAsync( CommandContext context ) {
		await context.StandardError.WriteLineAsync(
			string.Concat(
				context.ProgramName,
				": Try '",
				context.ProgramName,
				" --help' for more information."
			).AsMemory(),
			CancellationToken.None
		).ConfigureAwait( false );
	}

	private static async Task WriteHelpAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		var text = string.Join(
			Environment.NewLine,
			new[] {
				"Usage: patch [OPTION]... [ORIGFILE [PATCHFILE]]",
				"Apply a difference listing to an original file or files.",
				string.Empty,
				"  -i, --input=PATCHFILE  read patch from PATCHFILE instead of standard input",
				"      --binary           read and write data in binary mode",
				"      --help             display this help and exit",
				"  -v, --version          output version information and exit",
				string.Empty,
				"P0-P2 provide GNU-style invocation and byte-preserving format detection.",
				"Target-file application is introduced by later Patch phases."
			}
		);
		await output.WriteLineAsync( text.AsMemory(), cancellationToken ).ConfigureAwait( false );
	}
}
