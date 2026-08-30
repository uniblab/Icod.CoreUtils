// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Nice;

using System.Globalization;
using Icod.CommandFramework.Diagnostics;
using Icod.Processes;

/// <summary>Implements GNU Coreutils 9.11 <c>nice</c>.</summary>
public static class Command {
	private const int InternalFailure = 125;
	private const string ProgramName = "nice";

	/// <summary>Runs <c>nice</c> synchronously for compatibility with historical callers.</summary>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		return RunAsync(
			args,
			stdin,
			stdout,
			stderr
		).GetAwaiter().GetResult();
	}

	/// <summary>Runs GNU <c>nice</c> asynchronously with optional standard-stream substitution.</summary>
	/// <remarks>
	/// A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream.
	/// Child standard handles remain inherited at the native process boundary unless a binary stream is
	/// supplied through the <see cref="CommandContext"/> overload.
	/// </remarks>
	public static Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		return RunAsync(
			args,
			new CommandContext(
				ProgramName,
				stdin ?? Console.In,
				stdout ?? Console.Out,
				stderr ?? Console.Error,
				cancellationToken: cancellationToken
			)
		);
	}

	/// <summary>Runs GNU <c>nice</c> asynchronously using a complete shared command context.</summary>
	/// <remarks>
	/// Binary standard streams are passed directly to the child-process provider when supplied. When a
	/// binary stream is absent, the corresponding child standard handle is inherited unchanged.
	/// </remarks>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		IProcessExecutor? processExecutor = null,
		IProcessPriorityProvider? priorityProvider = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		var executor = processExecutor ?? SystemProcessExecutor.Instance;
		var priorities = priorityProvider ?? SystemProcessPriorityProvider.Instance;

		try {
			var parsed = ParseArguments( args );
			if ( null != parsed.Error ) {
				await context.Diagnostics.ErrorAsync(
					parsed.Error,
					context.CancellationToken
				).ConfigureAwait( false );
				await context.StandardError.WriteLineAsync(
					"Try 'nice --help' for more information.".AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return InternalFailure;
			}
			if ( parsed.ShowHelp ) {
				await context.StandardOutput.WriteAsync(
					string.Concat(
						NormalizeLineEndings( HelpText ),
						Environment.NewLine
					).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return 0;
			}
			if ( parsed.ShowVersion ) {
				await context.StandardOutput.WriteLineAsync(
					"nice (Icod.CoreUtils) 9.11".AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return 0;
			}

			var self = ProcessTarget.ForProcess( Environment.ProcessId );
			if ( null == parsed.Command ) {
				if ( parsed.AdjustmentSpecified ) {
					await context.Diagnostics.ErrorAsync(
						"a command must be given with an adjustment",
						context.CancellationToken
					).ConfigureAwait( false );
					await context.StandardError.WriteLineAsync(
						"Try 'nice --help' for more information.".AsMemory(),
						context.CancellationToken
					).ConfigureAwait( false );
					return InternalFailure;
				}
				var current = priorities.GetPriority( self );
				if ( !current.Succeeded ) {
					await WritePriorityFailureAsync(
						context,
						"cannot get niceness",
						current.Message
					).ConfigureAwait( false );
					return InternalFailure;
				}
				await context.StandardOutput.WriteLineAsync(
					current.Value!.NiceValue.ToString(
						CultureInfo.InvariantCulture
					).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return 0;
			}

			var currentForAdjustment = priorities.GetPriority( self );
			if ( !currentForAdjustment.Succeeded ) {
				await WritePriorityFailureAsync(
					context,
					"cannot get niceness",
					currentForAdjustment.Message
				).ConfigureAwait( false );
				return InternalFailure;
			}
			var targetNiceValue = checked( ( int )Math.Clamp(
				( long )currentForAdjustment.Value!.NiceValue + parsed.Adjustment,
				-20L,
				19L
			) );
			var changed = priorities.SetPriority(
				self,
				targetNiceValue
			);
			if ( !changed.Succeeded ) {
				await WritePriorityFailureAsync(
					context,
					"cannot set niceness",
					changed.Message
				).ConfigureAwait( false );
				if ( ProcessOperationStatus.AccessDenied != changed.Status ) {
					return InternalFailure;
				}
			}

			ProcessOperationResult? childPriorityFailure = null;
			var runOptions = new ProcessRunOptions( parsed.Command ) {
				ResolveExecutable = true,
				ReturnLaunchFailureResult = true,
				StandardInput = context.StandardInputStream,
				StandardOutput = context.StandardOutputStream,
				StandardError = context.StandardErrorStream
			};
			if ( OperatingSystem.IsWindows() && changed.Succeeded ) {
				runOptions.ProcessStarted = identity => {
					childPriorityFailure = priorities.SetPriority(
						ProcessTarget.ForProcess( identity ),
						targetNiceValue
					);
				};
			}
			foreach ( var argument in parsed.CommandArguments ) {
				runOptions.Arguments.Add( argument );
			}

			ProcessResult result;
			try {
				result = await executor.RunAsync(
					runOptions,
					context.CancellationToken
				).ConfigureAwait( false );
			} catch ( OperationCanceledException ) {
				throw;
			} catch ( Exception exception ) {
				await context.Diagnostics.ErrorAsync(
					$"'{parsed.Command}': {exception.Message}",
					CancellationToken.None
				).ConfigureAwait( false );
				return InternalFailure;
			}
			if ( null != childPriorityFailure && !childPriorityFailure.Succeeded ) {
				await WritePriorityFailureAsync(
					context,
					"cannot set child niceness",
					childPriorityFailure.Message,
					CancellationToken.None
				).ConfigureAwait( false );
			}
			if ( ProcessTerminationKind.LaunchFailed == result.Termination.Kind ) {
				await context.Diagnostics.ErrorAsync(
					$"'{parsed.Command}': {result.Termination.Message ?? "cannot execute"}",
					CancellationToken.None
				).ConfigureAwait( false );
			}
			if (
				ProcessTerminationKind.Canceled == result.Termination.Kind
				&& context.CancellationToken.IsCancellationRequested
			) {
				return CommandExitCodes.Canceled;
			}
			return result.Termination.ToPortableExitCode();
		} catch ( OperationCanceledException ) {
			return context.CancellationToken.IsCancellationRequested
				? CommandExitCodes.Canceled
				: InternalFailure
			;
		}
	}

	private static NiceArguments ParseArguments( string[] args ) {
		var index = 0;
		string? adjustmentText = null;
		while ( index < args.Length ) {
			var token = args[ index ];
			if ( IsHistoricalAdjustment( token ) ) {
				adjustmentText = token[ 1.. ];
				index++;
				continue;
			}
			if ( "--help" == token ) {
				return NiceArguments.Help;
			}
			if ( "--version" == token ) {
				return NiceArguments.Version;
			}
			if ( "--" == token ) {
				index++;
				break;
			}
			if ( "-n" == token || "--adjustment" == token ) {
				if ( index + 1 >= args.Length ) {
					return NiceArguments.Failure(
						$"option '{token}' requires an argument"
					);
				}
				adjustmentText = args[ index + 1 ];
				index += 2;
				continue;
			}
			if ( token.StartsWith( "--adjustment=", StringComparison.Ordinal ) ) {
				adjustmentText = token[ "--adjustment=".Length.. ];
				index++;
				continue;
			}
			if ( token.StartsWith( "-n", StringComparison.Ordinal ) && 2 < token.Length ) {
				adjustmentText = token[ 2.. ];
				index++;
				continue;
			}
			if ( token.StartsWith( '-' ) && "-" != token ) {
				return NiceArguments.Failure( $"invalid option -- '{token}'" );
			}
			break;
		}

		var adjustment = 10;
		if ( null != adjustmentText ) {
			if ( !long.TryParse( adjustmentText, NumberStyles.AllowLeadingWhite | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed ) ) {
				return NiceArguments.Failure( $"invalid adjustment '{adjustmentText}'" );
			}
			adjustment = checked( ( int )Math.Clamp( parsed, -39L, 39L ) );
		}
		var command = ( index < args.Length )
			? args[ index ]
			: null
		;
		var commandArguments = ( null == command )
			? Array.Empty<string>()
			: args.Skip( index + 1 ).ToArray()
		;
		return new NiceArguments( adjustment, null != adjustmentText, command, commandArguments, false, false, null );
	}

	private static bool IsHistoricalAdjustment( string token ) {
		if ( 2 > token.Length || '-' != token[ 0 ] ) {
			return false;
		}
		var digitIndex = 1 + (
			( token[ 1 ] is '-' or '+' )
				? 1
				: 0
		);
		return digitIndex < token.Length && char.IsAsciiDigit( token[ digitIndex ] );
	}

	private static Task WritePriorityFailureAsync(
		CommandContext context,
		string operation,
		string? detail,
		CancellationToken? cancellationToken = null
	) {
		ArgumentNullException.ThrowIfNull( context );
		return context.Diagnostics.ErrorAsync(
			( null == detail )
				? operation
				: $"{operation}: {detail}",
			cancellationToken ?? context.CancellationToken
		).AsTask();
	}

	private static string NormalizeLineEndings( string value ) => ( "\n" == Environment.NewLine )
		? value
		: value.Replace( "\n", Environment.NewLine, StringComparison.Ordinal )
	;

	private sealed record NiceArguments(
		int Adjustment,
		bool AdjustmentSpecified,
		string? Command,
		IReadOnlyList<string> CommandArguments,
		bool ShowHelp,
		bool ShowVersion,
		string? Error
	) {
		public static NiceArguments Help { get; } = new( 10, false, null, Array.Empty<string>(), true, false, null );
		public static NiceArguments Version { get; } = new( 10, false, null, Array.Empty<string>(), false, true, null );
		public static NiceArguments Failure( string error ) => new( 10, false, null, Array.Empty<string>(), false, false, error );
	}

	private const string HelpText = """
Usage: nice [OPTION] [COMMAND [ARG]...]
Run COMMAND with an adjusted niceness, which affects process scheduling.
With no COMMAND, print the current niceness.  Niceness values range from
-20 (most favorable to the process) to 19 (least favorable to the process).

  -n, --adjustment=N   add integer N to the niceness (default 10)
      --help           display this help and exit
      --version        output version information and exit

Exit status:
  125  if the nice command itself fails
  126  if COMMAND is found but cannot be invoked
  127  if COMMAND cannot be found
  -    otherwise, the exit status of COMMAND
""";
}
