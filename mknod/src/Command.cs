// Original behavior/reference: GNU Coreutils 9.11 mknod.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.MkNod;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Implements GNU <c>mknod</c> over the shared special-file mutation primitives.
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

	/// <summary>Runs <c>mknod</c> synchronously against optional caller-owned text streams.</summary>
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
			"mknod",
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error
		);
		return RunAsync( args, context ).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>mknod</c> asynchronously with the system filesystem providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context, or <see langword="null"/> to use console streams.</param>
	/// <returns>The command exit status.</returns>
	public static ValueTask<int> RunAsync( string[] args, CommandContext? context = null ) {
		return RunAsync(
			args,
			context ?? CommandContext.CreateConsole( "mknod" ),
			SystemFileSystemMutationProvider.Instance,
			SystemFileCreationMaskProvider.Instance
		);
	}

	/// <summary>Runs <c>mknod</c> asynchronously with injected mutation and creation-mask providers.</summary>
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
			if ( !await ValidateOperandCountAsync( parsed.Operands, context ).ConfigureAwait( false ) ) {
				return CommandExitCodes.Failure;
			}

			var path = parsed.Operands[ 0 ];
			var typeText = parsed.Operands[ 1 ];
			var kind = ParseKind( typeText );
			if ( kind is null ) {
				await context.StandardError.WriteLineAsync(
					string.Concat( "mknod: invalid device type ", Quote( typeText ) )
				).ConfigureAwait( false );
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
					"mknod: warning: ignoring --context; security-context labeling is unavailable"
				).ConfigureAwait( false );
			}

			FileSystemMutationResult result;
			if ( kind == FileSystemEntryKind.Fifo ) {
				result = await mutationProvider.CreateFifoAsync(
					path,
					modeResult.Mode,
					modeResult.CreationMask,
					FileSystemMutationPrecondition.DestinationMustNotExist(),
					context.CancellationToken
				).ConfigureAwait( false );
			} else {
				if ( !TryParseDeviceNumber( parsed.Operands[ 2 ], out var major ) ) {
					await WriteInvalidDeviceNumberAsync( context, "major", parsed.Operands[ 2 ] ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				if ( !TryParseDeviceNumber( parsed.Operands[ 3 ], out var minor ) ) {
					await WriteInvalidDeviceNumberAsync( context, "minor", parsed.Operands[ 3 ] ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				result = await mutationProvider.CreateDeviceNodeAsync(
					path,
					kind.Value,
					new DeviceNumber( major, minor ),
					modeResult.Mode,
					modeResult.CreationMask,
					FileSystemMutationPrecondition.DestinationMustNotExist(),
					context.CancellationToken
				).ConfigureAwait( false );
			}

			if ( result.Succeeded ) return CommandExitCodes.Success;
			await context.StandardError.WriteLineAsync(
				string.Concat( "mknod: ", Quote( path ), ": ", DescribeFailure( result ) )
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		}
	}

	/// <summary>Writes the GNU-compatible <c>mknod</c> usage text.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when usage has been written.</returns>
	public static async ValueTask WriteUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		var lines = new[] {
			"Usage: mknod [OPTION]... NAME TYPE [MAJOR MINOR]",
			"Create the special file NAME of the given TYPE.",
			string.Empty,
			"  -m, --mode=MODE     set file permission bits to MODE, not a=rw - umask",
			"  -Z                   set the SELinux security context to the default type",
			"      --context[=CTX]  like -Z, or set the specified context",
			"      --help           display this help and exit",
			"      --version        output version information and exit",
			string.Empty,
			"Both MAJOR and MINOR must be specified when TYPE is b, c, or u, and",
			"must be omitted when TYPE is p.  Prefix 0x selects hexadecimal and a",
			"leading 0 selects octal; otherwise device numbers are decimal.",
			string.Empty,
			"  b      create a block (buffered) special file",
			"  c, u   create a character (unbuffered) special file",
			"  p      create a FIFO"
		};
		foreach ( var line in lines ) {
			cancellationToken.ThrowIfCancellationRequested();
			await output.WriteLineAsync( line ).ConfigureAwait( false );
		}
	}

	private static async ValueTask<bool> ValidateOperandCountAsync(
		IReadOnlyList<string> operands,
		CommandContext context
	) {
		if ( operands.Count == 0 ) {
			await context.StandardError.WriteLineAsync( "mknod: missing operand" ).ConfigureAwait( false );
			await WriteTryHelpAsync( context ).ConfigureAwait( false );
			return false;
		}
		if ( operands.Count == 1 ) {
			await context.StandardError.WriteLineAsync(
				string.Concat( "mknod: missing operand after ", Quote( operands[ 0 ] ) )
			).ConfigureAwait( false );
			await WriteTryHelpAsync( context ).ConfigureAwait( false );
			return false;
		}

		var isFifo = string.Equals( operands[ 1 ], "p", StringComparison.Ordinal );
		var expectedCount = isFifo ? 2 : 4;
		if ( operands.Count < expectedCount ) {
			await context.StandardError.WriteLineAsync(
				string.Concat( "mknod: missing operand after ", Quote( operands[ ^1 ] ) )
			).ConfigureAwait( false );
			if ( operands.Count == 2 ) {
				await context.StandardError.WriteLineAsync(
					"Special files require major and minor device numbers."
				).ConfigureAwait( false );
			}
			await WriteTryHelpAsync( context ).ConfigureAwait( false );
			return false;
		}
		if ( operands.Count > expectedCount ) {
			await context.StandardError.WriteLineAsync(
				string.Concat( "mknod: extra operand ", Quote( operands[ expectedCount ] ) )
			).ConfigureAwait( false );
			if ( isFifo && operands.Count == 4 ) {
				await context.StandardError.WriteLineAsync(
					"FIFOs do not have major and minor device numbers."
				).ConfigureAwait( false );
			}
			await WriteTryHelpAsync( context ).ConfigureAwait( false );
			return false;
		}
		return true;
	}

	private static FileSystemEntryKind? ParseKind( string typeText ) {
		return typeText switch {
			"p" => FileSystemEntryKind.Fifo,
			"b" => FileSystemEntryKind.BlockDevice,
			"c" or "u" => FileSystemEntryKind.CharacterDevice,
			_ => null
		};
	}

	private static bool TryParseDeviceNumber( string text, out uint value ) {
		value = 0;
		if ( string.IsNullOrEmpty( text ) ) return false;

		var index = text[ 0 ] == '+' ? 1 : 0;
		if ( index == text.Length || text[ index ] == '-' ) return false;
		var numberBase = 10u;
		if ( text.Length - index >= 2 && text[ index ] == '0' && (text[ index + 1 ] is 'x' or 'X') ) {
			numberBase = 16;
			index += 2;
		} else if ( text.Length - index > 1 && text[ index ] == '0' ) {
			numberBase = 8;
		}
		if ( index == text.Length ) return false;

		uint result = 0;
		for ( ; index < text.Length; index++ ) {
			var digit = GetDigit( text[ index ] );
			if ( digit < 0 || (uint)digit >= numberBase ) return false;
			if ( result > (uint.MaxValue - (uint)digit) / numberBase ) return false;
			result = (result * numberBase) + (uint)digit;
		}
		value = result;
		return true;
	}

	private static int GetDigit( char value ) {
		if ( value is >= '0' and <= '9' ) return value - '0';
		if ( value is >= 'a' and <= 'f' ) return value - 'a' + 10;
		if ( value is >= 'A' and <= 'F' ) return value - 'A' + 10;
		return -1;
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
				string.Concat( "mknod: invalid mode ", Quote( modeText ), ": ", detail )
			);
		}
		var mode = parsed.Expression.Apply( requestedMode, false, creationMask );
		if ( (mode.Value & SpecialModeBits) != 0 ) {
			return ModeSelection.Failure( "mknod: mode must specify only file permission bits" );
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

	private static async ValueTask WriteInvalidDeviceNumberAsync(
		CommandContext context,
		string component,
		string value
	) {
		await context.StandardError.WriteLineAsync(
			string.Concat( "mknod: invalid ", component, " device number ", Quote( value ) )
		).ConfigureAwait( false );
	}

	private static ValueTask WriteTryHelpAsync( CommandContext context ) {
		return new ValueTask(
			context.StandardError.WriteLineAsync( "Try 'mknod --help' for more information." )
		);
	}

	private static async ValueTask WriteVersionAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await output.WriteLineAsync( "mknod (Icod.CoreUtils) 0.1.0" ).ConfigureAwait( false );
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
