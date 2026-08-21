// Original behavior/reference: GNU Coreutils 9.11 mkfifo.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.MkFifo;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using FileCreationMask = Icod.CommandFramework.FileSystem.Modes.FileCreationMask;
using PosixFileMode = Icod.CommandFramework.FileSystem.Modes.PosixFileMode;
using Icod.CommandFramework.FileSystem.Mutation;

/// <summary>
/// Implements GNU <c>mkfifo</c> over the shared single-path mutation provider.
/// </summary>
public static class Command {
	private const int DefaultMode = 0x01b6;
	private const int SpecialModeBits = 0x0e00;

	private static readonly OptionParser Parser = new(
		new[] {
			new OptionDefinition( "mode", 'm', new[] { "mode" }, OptionValueArity.Required ),
			new OptionDefinition( "context-default", 'Z' ),
			new OptionDefinition( "context", longNames: new[] { "context" }, valueArity: OptionValueArity.Optional ),
			new OptionDefinition( "help", longNames: new[] { "help" }, allowMultiple: false ),
			new OptionDefinition( "version", longNames: new[] { "version" }, allowMultiple: false )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	/// <summary>Runs <c>mkfifo</c> synchronously against optional caller-owned text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <returns>The command exit status.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		var context = new CommandContext(
			"mkfifo",
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error
		);
		return RunAsync( args, context ).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>mkfifo</c> asynchronously with the system filesystem providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context, or <see langword="null"/> to use console streams.</param>
	/// <returns>The command exit status.</returns>
	public static ValueTask<int> RunAsync( string[] args, CommandContext? context = null ) {
		return RunAsync(
			args,
			context ?? CommandContext.CreateConsole( "mkfifo" ),
			SystemFileSystemMutationProvider.Instance,
			SystemFileCreationMaskProvider.Instance
		);
	}

	/// <summary>Runs <c>mkfifo</c> asynchronously with injected mutation and creation-mask providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <param name="mutationProvider">The single-path mutation provider.</param>
	/// <param name="creationMaskProvider">The current-process creation-mask provider.</param>
	/// <returns>The command exit status.</returns>
	public static async ValueTask<int> RunAsync(
		string[] args,
		CommandContext context,
		IFileSystemMutationProvider mutationProvider,
		IFileCreationMaskProvider creationMaskProvider
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( mutationProvider );
		ArgumentNullException.ThrowIfNull( creationMaskProvider );
		args ??= Array.Empty<string>();

		try {
			context.CancellationToken.ThrowIfCancellationRequested();
			var parsed = Parser.Parse( args );
			if ( !parsed.IsSuccess ) {
				foreach ( var error in parsed.Errors ) {
					await context.StandardError.WriteLineAsync(
						OptionDiagnosticFormatter.Format( context.ProgramName, error )
					).ConfigureAwait( false );
				}
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( parsed.HasOption( "help" ) ) {
				await WriteUsageAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.HasOption( "version" ) ) {
				await WriteVersionAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.Operands.Count == 0 ) {
				await context.StandardError.WriteLineAsync( "mkfifo: missing operand" ).ConfigureAwait( false );
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var modeResult = ParseMode( parsed.GetLastValue( "mode" ), creationMaskProvider.GetCurrentMask() );
			if ( !modeResult.Succeeded ) {
				await context.StandardError.WriteLineAsync( modeResult.Message ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( parsed.GetLastValue( "context" ) is not null ) {
				await context.StandardError.WriteLineAsync(
					"mkfifo: warning: ignoring --context; security-context labeling is unavailable"
				).ConfigureAwait( false );
			}

			var exitStatus = CommandExitCodes.Success;
			foreach ( var path in parsed.Operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var result = await mutationProvider.CreateFifoAsync(
					path,
					modeResult.Mode,
					modeResult.CreationMask,
					FileSystemMutationPrecondition.DestinationMustNotExist(),
					context.CancellationToken
				).ConfigureAwait( false );
				if ( result.Succeeded ) continue;

				exitStatus = CommandExitCodes.Failure;
				await context.StandardError.WriteLineAsync(
					string.Concat( "mkfifo: cannot create fifo ", Quote( path ), ": ", DescribeFailure( result ) )
				).ConfigureAwait( false );
			}
			return exitStatus;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		}
	}

	/// <summary>Writes the GNU-compatible <c>mkfifo</c> usage text.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when usage has been written.</returns>
	public static async ValueTask WriteUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		var lines = new[] {
			"Usage: mkfifo [OPTION]... NAME...",
			"Create named pipes (FIFOs) with the given NAMEs.",
			string.Empty,
			"  -m, --mode=MODE     set file permission bits to MODE, not a=rw - umask",
			"  -Z                   set the SELinux security context to the default type",
			"      --context[=CTX]  like -Z, or set the specified context",
			"      --help           display this help and exit",
			"      --version        output version information and exit"
		};
		foreach ( var line in lines ) {
			cancellationToken.ThrowIfCancellationRequested();
			await output.WriteLineAsync( line ).ConfigureAwait( false );
		}
	}

	private static ModeSelection ParseMode( string? modeText, FileCreationMask creationMask ) {
		var requestedMode = new PosixFileMode( DefaultMode );
		if ( modeText is null ) {
			return ModeSelection.Success( requestedMode, creationMask );
		}
		var parsed = FileModeParser.Parse( modeText );
		if ( !parsed.Succeeded || parsed.Expression is null ) {
			var detail = string.IsNullOrEmpty( parsed.Message ) ? "invalid mode" : parsed.Message;
			return ModeSelection.Failure(
				string.Concat( "mkfifo: invalid mode ", Quote( modeText ), ": ", detail )
			);
		}
		var mode = parsed.Expression.Apply( requestedMode, false, creationMask );
		if ( (mode.Value & SpecialModeBits) != 0 ) {
			return ModeSelection.Failure( "mkfifo: mode must specify only file permission bits" );
		}
		return ModeSelection.Success( mode, FileCreationMask.None );
	}

	private static string DescribeFailure( FileSystemMutationResult result ) {
		return result.ErrorCode switch {
			FileSystemMutationErrorCode.AlreadyExists => "File exists",
			FileSystemMutationErrorCode.NotFound or FileSystemMutationErrorCode.ParentNotFound => "No such file or directory",
			FileSystemMutationErrorCode.AccessDenied => "Permission denied",
			FileSystemMutationErrorCode.PrivilegeRequired => "Operation not permitted",
			FileSystemMutationErrorCode.InvalidDeviceNumber => "Invalid argument",
			_ => result.Message ?? "filesystem operation failed"
		};
	}

	private static ValueTask WriteTryHelpAsync( CommandContext context ) {
		return new ValueTask(
			context.StandardError.WriteLineAsync( "Try 'mkfifo --help' for more information." )
		);
	}

	private static async ValueTask WriteVersionAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await output.WriteLineAsync( "mkfifo (Icod.CoreUtils) 0.1.0" ).ConfigureAwait( false );
	}

	private static string Quote( string value ) {
		return string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );
	}

	private readonly record struct ModeSelection(
		bool Succeeded,
		PosixFileMode Mode,
		FileCreationMask CreationMask,
		string Message
	) {
		/// <summary>Creates a successful mode selection.</summary>
		public static ModeSelection Success( PosixFileMode mode, FileCreationMask creationMask ) {
			return new ModeSelection( true, mode, creationMask, string.Empty );
		}

		/// <summary>Creates a failed mode selection.</summary>
		public static ModeSelection Failure( string message ) {
			return new ModeSelection( false, default, default, message );
		}
	}
}
