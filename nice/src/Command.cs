// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Nice;

using System.Globalization;
using System.Text;
using Icod.CommandFramework.Processes;

/// <summary>Implements GNU Coreutils 9.11 <c>nice</c>.</summary>
public static class Command {
	private const int InternalFailure = 125;
	private static readonly Encoding Utf8 = new UTF8Encoding( false );

	/// <summary>Runs <c>nice</c> synchronously for compatibility with historical callers.</summary>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		using var input = null == stdin ? null : new MemoryStream( Utf8.GetBytes( stdin.ReadToEnd() ), writable: false );
		using var output = new MemoryStream();
		using var error = new MemoryStream();
		var status = RunAsync( args, input, output, error ).GetAwaiter().GetResult();
		( stdout ?? Console.Out ).Write( Utf8.GetString( output.ToArray() ) );
		( stderr ?? Console.Error ).Write( Utf8.GetString( error.ToArray() ) );
		return status;
	}

	/// <summary>Runs GNU <c>nice</c> asynchronously over the F4 process providers.</summary>
	public static async Task<int> RunAsync(
		string[] args,
		Stream? stdin = null,
		Stream? stdout = null,
		Stream? stderr = null,
		IProcessExecutor? processExecutor = null,
		IProcessPriorityProvider? priorityProvider = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		var executor = processExecutor ?? SystemProcessExecutor.Instance;
		var priorities = priorityProvider ?? SystemProcessPriorityProvider.Instance;
		var parsed = ParseArguments( args );
		if ( null != parsed.Error ) {
			await WriteDiagnosticAsync( stderr, $"nice: {parsed.Error}", cancellationToken ).ConfigureAwait( false );
			await WriteDiagnosticAsync( stderr, "Try 'nice --help' for more information.", cancellationToken ).ConfigureAwait( false );
			return InternalFailure;
		}
		if ( parsed.ShowHelp ) {
			await WriteAsync( stdout, string.Concat( NormalizeLineEndings( HelpText ), Environment.NewLine ), cancellationToken ).ConfigureAwait( false );
			return 0;
		}
		if ( parsed.ShowVersion ) {
			await WriteAsync( stdout, string.Concat( "nice (Icod.CoreUtils) 9.11", Environment.NewLine ), cancellationToken ).ConfigureAwait( false );
			return 0;
		}

		var self = ProcessTarget.ForProcess( Environment.ProcessId );
		if ( null == parsed.Command ) {
			if ( parsed.AdjustmentSpecified ) {
				await WriteDiagnosticAsync( stderr, "nice: a command must be given with an adjustment", cancellationToken ).ConfigureAwait( false );
				await WriteDiagnosticAsync( stderr, "Try 'nice --help' for more information.", cancellationToken ).ConfigureAwait( false );
				return InternalFailure;
			}
			var current = priorities.GetPriority( self );
			if ( !current.Succeeded ) {
				await WritePriorityFailureAsync( stderr, "cannot get niceness", current.Message, cancellationToken ).ConfigureAwait( false );
				return InternalFailure;
			}
			await WriteAsync(
				stdout,
				string.Concat( current.Value!.NiceValue.ToString( CultureInfo.InvariantCulture ), Environment.NewLine ),
				cancellationToken
			).ConfigureAwait( false );
			return 0;
		}

		var currentForAdjustment = priorities.GetPriority( self );
		if ( !currentForAdjustment.Succeeded ) {
			await WritePriorityFailureAsync( stderr, "cannot get niceness", currentForAdjustment.Message, cancellationToken ).ConfigureAwait( false );
			return InternalFailure;
		}
		var targetNiceValue = checked( (int)Math.Clamp(
			(long)currentForAdjustment.Value!.NiceValue + parsed.Adjustment,
			-20L,
			19L
		) );
		var changed = priorities.SetPriority( self, targetNiceValue );
		if ( !changed.Succeeded ) {
			await WritePriorityFailureAsync( stderr, "cannot set niceness", changed.Message, cancellationToken ).ConfigureAwait( false );
			if ( ProcessOperationStatus.AccessDenied != changed.Status ) return InternalFailure;
		}

		ProcessOperationResult? childPriorityFailure = null;
		var runOptions = new ProcessRunOptions( parsed.Command ) {
			ResolveExecutable = true,
			ReturnLaunchFailureResult = true,
			StandardInput = stdin,
			StandardOutput = stdout,
			StandardError = stderr
		};
		if ( OperatingSystem.IsWindows() && changed.Succeeded ) {
			runOptions.ProcessStarted = identity => {
				childPriorityFailure = priorities.SetPriority( ProcessTarget.ForProcess( identity ), targetNiceValue );
			};
		}
		foreach ( var argument in parsed.CommandArguments ) runOptions.Arguments.Add( argument );

		ProcessResult result;
		try {
			result = await executor.RunAsync( runOptions, cancellationToken ).ConfigureAwait( false );
		} catch ( OperationCanceledException ) {
			return InternalFailure;
		} catch ( Exception exception ) {
			await WriteDiagnosticAsync( stderr, $"nice: '{parsed.Command}': {exception.Message}", CancellationToken.None ).ConfigureAwait( false );
			return InternalFailure;
		}
		if ( null != childPriorityFailure && !childPriorityFailure.Succeeded ) {
			await WritePriorityFailureAsync( stderr, "cannot set child niceness", childPriorityFailure.Message, CancellationToken.None ).ConfigureAwait( false );
		}
		if ( ProcessTerminationKind.LaunchFailed == result.Termination.Kind ) {
			await WriteDiagnosticAsync(
				stderr,
				$"nice: '{parsed.Command}': {result.Termination.Message ?? "cannot execute"}",
				CancellationToken.None
			).ConfigureAwait( false );
		}
		return result.Termination.ToPortableExitCode();
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
			if ( "--help" == token ) return NiceArguments.Help;
			if ( "--version" == token ) return NiceArguments.Version;
			if ( "--" == token ) {
				index++;
				break;
			}
			if ( "-n" == token || "--adjustment" == token ) {
				if ( index + 1 >= args.Length ) return NiceArguments.Failure( $"option '{token}' requires an argument" );
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
			adjustment = checked( (int)Math.Clamp( parsed, -39L, 39L ) );
		}
		var command = index < args.Length ? args[ index ] : null;
		var commandArguments = null == command ? Array.Empty<string>() : args.Skip( index + 1 ).ToArray();
		return new NiceArguments( adjustment, null != adjustmentText, command, commandArguments, false, false, null );
	}

	private static bool IsHistoricalAdjustment( string token ) {
		if ( 2 > token.Length || '-' != token[ 0 ] ) return false;
		var digitIndex = 1 + ( token[ 1 ] is '-' or '+' ? 1 : 0 );
		return digitIndex < token.Length && char.IsAsciiDigit( token[ digitIndex ] );
	}

	private static async Task WritePriorityFailureAsync(
		Stream? stderr,
		string operation,
		string? detail,
		CancellationToken cancellationToken
	) => await WriteDiagnosticAsync(
		stderr,
		null == detail ? $"nice: {operation}" : $"nice: {operation}: {detail}",
		cancellationToken
	).ConfigureAwait( false );

	private static async Task WriteAsync( Stream? stream, string text, CancellationToken cancellationToken ) {
		if ( null == stream ) {
			await Console.Out.WriteAsync( text.AsMemory(), cancellationToken ).ConfigureAwait( false );
			return;
		}
		var bytes = Utf8.GetBytes( text );
		await stream.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
		await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
	}

	private static async Task WriteDiagnosticAsync( Stream? stream, string text, CancellationToken cancellationToken ) {
		if ( null == stream ) {
			await Console.Error.WriteLineAsync( text.AsMemory(), cancellationToken ).ConfigureAwait( false );
			return;
		}
		var bytes = Utf8.GetBytes( string.Concat( text, Environment.NewLine ) );
		await stream.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
		await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
	}

	private static string NormalizeLineEndings( string value ) => "\n" == Environment.NewLine
		? value
		: value.Replace( "\n", Environment.NewLine, StringComparison.Ordinal );

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
