namespace Icod.CoreUtils.Env;

using System.Text;
using Icod.Processes;

/// <summary>
/// Implements GNU <c>env</c> 9.11 command behavior.
/// </summary>
public static class Command {
	private const int InternalFailure = 125;
	private static readonly Encoding Utf8 = new UTF8Encoding( false );

	/// <summary>Runs the command synchronously for compatibility with older callers.</summary>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		if ( null == stdin && null == stdout && null == stderr ) {
			return RunAsync( args ).GetAwaiter().GetResult();
		}
		using var input = null == stdin ? null : new MemoryStream( Utf8.GetBytes( stdin.ReadToEnd() ), writable: false );
		using var output = new MemoryStream();
		using var error = new MemoryStream();
		var exitCode = RunAsync( args, input, output, error ).GetAwaiter().GetResult();
		( stdout ?? Console.Out ).Write( Utf8.GetString( output.ToArray() ) );
		( stderr ?? Console.Error ).Write( Utf8.GetString( error.ToArray() ) );
		return exitCode;
	}

	/// <summary>Runs GNU <c>env</c> asynchronously with injectable process and signal providers.</summary>
	public static async Task<int> RunAsync(
		string[] args,
		Stream? stdin = null,
		Stream? stdout = null,
		Stream? stderr = null,
		IProcessExecutor? processExecutor = null,
		IProcessSignalProvider? signalProvider = null,
		ProcessEnvironment? sourceEnvironment = null,
		CancellationToken cancellationToken = default,
		bool replaceCurrentProcess = false
	) {
		ArgumentNullException.ThrowIfNull( args );
		var executor = processExecutor ?? SystemProcessExecutor.Instance;
		var signals = signalProvider ?? SystemProcessSignalProvider.Instance;
		var originalEnvironment = sourceEnvironment ?? ProcessEnvironment.CreateInheritedBuilder().Build();
		EnvOptions options;
		try {
			options = EnvOptions.Parse( args, originalEnvironment, signals );
		} catch ( EnvUsageException exception ) {
			await WriteDiagnosticAsync( stderr, $"env: {exception.Message}", cancellationToken ).ConfigureAwait( false );
			await WriteDiagnosticAsync( stderr, "Try 'env --help' for more information.", cancellationToken ).ConfigureAwait( false );
			return InternalFailure;
		}
		if ( options.ShowHelp ) {
			await WriteAsync( stdout, string.Concat( NormalizeLineEndings( HelpText ), Environment.NewLine ), cancellationToken ).ConfigureAwait( false );
			return 0;
		}
		if ( options.ShowVersion ) {
			await WriteAsync( stdout, string.Concat( "env (Icod.CoreUtils) 9.11", Environment.NewLine ), cancellationToken ).ConfigureAwait( false );
			return 0;
		}

		var operands = options.Operands.ToList();
		var ignoreEnvironment = options.IgnoreEnvironment;
		if ( 0 < operands.Count && "-" == operands[ 0 ] ) {
			ignoreEnvironment = true;
			operands.RemoveAt( 0 );
		}
		ProcessEnvironment environment;
		try {
			var builder = ProcessEnvironment.CreateEmptyBuilder();
			if ( !ignoreEnvironment ) {
				foreach ( var pair in originalEnvironment.Variables ) builder.Set( pair.Key, pair.Value );
				foreach ( var name in options.UnsetNames ) {
					if ( options.Debug ) await WriteDiagnosticAsync( stderr, $"unset:    {Quote( name )}", cancellationToken ).ConfigureAwait( false );
					if ( 0 == name.Length ) {
						throw new ArgumentException( "cannot unset '': Invalid argument" );
					}
					builder.Remove( name );
				}
			} else if ( options.Debug ) {
				await WriteDiagnosticAsync( stderr, "cleaning environ", cancellationToken ).ConfigureAwait( false );
			}
			while ( 0 < operands.Count && TrySplitAssignment( operands[ 0 ], out var name, out var value ) ) {
				if ( options.Debug ) await WriteDiagnosticAsync( stderr, $"setenv:   {Quote( operands[ 0 ] )}", cancellationToken ).ConfigureAwait( false );
				builder.Set( name, value );
				operands.RemoveAt( 0 );
			}
			environment = builder.Build();
		} catch ( ArgumentException exception ) {
			await WriteDiagnosticAsync( stderr, $"env: {exception.Message}", cancellationToken ).ConfigureAwait( false );
			return InternalFailure;
		}

		if ( 0 == operands.Count ) {
			if ( null != options.WorkingDirectory ) {
				await WriteDiagnosticAsync( stderr, "env: must specify command with --chdir (-C)", cancellationToken ).ConfigureAwait( false );
				return InternalFailure;
			}
			if ( null != options.ArgumentZero ) {
				await WriteDiagnosticAsync( stderr, "env: must specify command with --argv0 (-a)", cancellationToken ).ConfigureAwait( false );
				return InternalFailure;
			}
			return await PrintEnvironmentAsync( environment, options.NullOutput, stdout, cancellationToken ).ConfigureAwait( false );
		}
		if ( options.NullOutput ) {
			await WriteDiagnosticAsync( stderr, "env: cannot specify --null (-0) with command", cancellationToken ).ConfigureAwait( false );
			return InternalFailure;
		}

		if ( options.ListSignalHandling ) {
			await WriteSignalPolicyAsync( options.SignalPolicy, signals, stderr, cancellationToken ).ConfigureAwait( false );
		}
		var command = operands[ 0 ];
		var commandArguments = operands.Skip( 1 ).ToArray();
		if ( options.Debug ) {
			if ( null != options.WorkingDirectory ) await WriteDiagnosticAsync( stderr, $"chdir:    {Quote( options.WorkingDirectory )}", cancellationToken ).ConfigureAwait( false );
			if ( null != options.ArgumentZero ) await WriteDiagnosticAsync( stderr, $"argv0:    {Quote( options.ArgumentZero )}", cancellationToken ).ConfigureAwait( false );
			await WriteDiagnosticAsync( stderr, $"executing: {Quote( command )}", cancellationToken ).ConfigureAwait( false );
			for ( var index = 0; index < commandArguments.Length; index++ ) {
				await WriteDiagnosticAsync( stderr, $"   arg[{index}]= {Quote( commandArguments[ index ] )}", cancellationToken ).ConfigureAwait( false );
			}
		}

		var argumentZero = options.ArgumentZero;
		if ( null == argumentZero
			&& !OperatingSystem.IsWindows()
			&& null == stdin
			&& null == stdout
			&& null == stderr ) {
			argumentZero = command;
		}

		var runOptions = new ProcessRunOptions( command ) {
			ArgumentZero = argumentZero,
			CancellationPolicy = ProcessCancellationPolicy.KillProcessTree,
			Environment = environment,
			ReplaceCurrentProcess = replaceCurrentProcess,
			ResolveExecutable = true,
			ReturnLaunchFailureResult = true,
			SignalPolicy = options.SignalPolicy.IsEmpty ? null : options.SignalPolicy,
			StandardInput = stdin,
			StandardOutput = stdout,
			StandardError = stderr,
			WorkingDirectory = options.WorkingDirectory
		};
		foreach ( var argument in commandArguments ) runOptions.Arguments.Add( argument );
		ProcessResult result;
		try {
			result = await executor.RunAsync( runOptions, cancellationToken ).ConfigureAwait( false );
		} catch ( OperationCanceledException ) {
			return InternalFailure;
		} catch ( Exception exception ) {
			await WriteDiagnosticAsync( stderr, $"env: {Quote( command )}: {exception.Message}", CancellationToken.None ).ConfigureAwait( false );
			return InternalFailure;
		}
		if ( ProcessTerminationKind.LaunchFailed == result.Termination.Kind ) {
			await WriteDiagnosticAsync(
				stderr,
				$"env: {Quote( command )}: {result.Termination.Message ?? "cannot execute"}",
				CancellationToken.None
			).ConfigureAwait( false );
			if ( ProcessLaunchFailureKind.NotFound == result.Termination.LaunchFailureKind
				&& ContainsShellWhitespace( command ) ) {
				await WriteDiagnosticAsync(
					stderr,
					"env: use -[v]S to pass options in shebang lines",
					CancellationToken.None
				).ConfigureAwait( false );
			}
		}
		return result.Termination.ToPortableExitCode();
	}

	private static async Task<int> PrintEnvironmentAsync(
		ProcessEnvironment environment,
		bool nullOutput,
		Stream? output,
		CancellationToken cancellationToken
	) {
		var terminator = nullOutput ? "\0" : Environment.NewLine;
		try {
			foreach ( var pair in environment.Variables ) {
				await WriteAsync( output, string.Concat( pair.Key, "=", pair.Value, terminator ), cancellationToken, flush: false ).ConfigureAwait( false );
			}
			if ( null == output ) await Console.Out.FlushAsync( cancellationToken ).ConfigureAwait( false );
			else await output.FlushAsync( cancellationToken ).ConfigureAwait( false );
			return 0;
		} catch ( OperationCanceledException ) {
			return InternalFailure;
		} catch ( IOException ) {
			return InternalFailure;
		}
	}

	private static bool TrySplitAssignment( string value, out string name, out string assignedValue ) {
		var equals = value.IndexOf( '=' );
		if ( 0 > equals ) {
			name = string.Empty;
			assignedValue = string.Empty;
			return false;
		}
		name = value[ ..equals ];
		assignedValue = value[ ( equals + 1 ).. ];
		return true;
	}

	private static async Task WriteSignalPolicyAsync(
		ProcessLaunchSignalPolicy policy,
		IProcessSignalProvider provider,
		Stream? error,
		CancellationToken cancellationToken
	) {
		var signals = provider.ListSignals()
			.Where( static signal => 0 < signal.Number )
			.ToDictionary( static signal => signal.Number );
		foreach ( var directive in policy.Directives.Values ) {
			if ( signals.ContainsKey( directive.SignalNumber ) ) continue;
			var translated = provider.TranslateSignal( directive.SignalNumber );
			if ( translated.Succeeded && null != translated.Value ) signals[ directive.SignalNumber ] = translated.Value;
		}
		var identity = new ProcessIdentity( Environment.ProcessId );
		foreach ( var signal in signals.Values.OrderBy( static item => item.Number ) ) {
			policy.Directives.TryGetValue( signal.Number, out var directive );
			var blocked = true == directive?.Blocked;
			if ( null == directive?.Blocked && provider is IProcessSignalMaskProvider maskProvider ) {
				var observed = maskProvider.ObserveBlocked( identity, signal );
				blocked = observed.Succeeded && true == observed.Value;
			}
			var ignored = ProcessSignalLaunchDisposition.Ignored == directive?.Disposition;
			if ( null == directive?.Disposition ) {
				var observed = provider.ObserveDisposition( identity, signal );
				ignored = observed.Succeeded && ProcessSignalDisposition.Ignored == observed.Value;
			}
			if ( !blocked && !ignored ) continue;
			var state = blocked && ignored ? "BLOCK,IGNORE" : blocked ? "BLOCK" : "IGNORE";
			await WriteDiagnosticAsync(
				error,
				$"{signal.Name,-10} ({signal.Number,2}): {state}",
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task WriteDiagnosticAsync( Stream? stream, string message, CancellationToken cancellationToken ) {
		if ( null == stream ) {
			await Console.Error.WriteLineAsync( message.AsMemory(), cancellationToken ).ConfigureAwait( false );
			return;
		}
		var bytes = Utf8.GetBytes( string.Concat( message, Environment.NewLine ) );
		await stream.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
		await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
	}

	private static async Task WriteAsync( Stream? stream, string text, CancellationToken cancellationToken, bool flush = true ) {
		if ( null == stream ) {
			await Console.Out.WriteAsync( text.AsMemory(), cancellationToken ).ConfigureAwait( false );
			if ( flush ) await Console.Out.FlushAsync( cancellationToken ).ConfigureAwait( false );
			return;
		}
		var bytes = Utf8.GetBytes( text );
		await stream.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
		if ( flush ) await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
	}

	private static bool ContainsShellWhitespace( string value ) => value.Any(
		static character => character is ' ' or '\t' or '\n' or '\v' or '\f' or '\r'
	);

	private static string Quote( string value ) => string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );

	private static string NormalizeLineEndings( string value ) => "\n" == Environment.NewLine
		? value
		: value.Replace( "\n", Environment.NewLine, StringComparison.Ordinal )
	;

	private const string HelpText = """
Usage: env [OPTION]... [-] [NAME=VALUE]... [COMMAND [ARG]...]
Set each NAME to VALUE in the environment and run COMMAND.

  -a, --argv0=ARG            pass ARG as the zeroth argument of COMMAND
  -i, --ignore-environment   start with an empty environment
  -0, --null                 end each output line with NUL, not newline
  -u, --unset=NAME           remove variable from the environment
  -C, --chdir=DIR            change working directory to DIR
  -S, --split-string=S       process and split S into separate arguments
      --block-signal[=SIG]   block delivery of SIG signal(s) to COMMAND
      --default-signal[=SIG] reset handling of SIG signal(s) to default
      --ignore-signal[=SIG]  set handling of SIG signal(s) to do nothing
      --list-signal-handling list non-default signal handling to standard error
  -v, --debug                print verbose information for each processing step
      --help                 display this help and exit
      --version              output version information and exit

A mere - implies -i. If no COMMAND, print the resulting environment.

SIG may be a signal name like 'PIPE', or a signal number like '13'.
Without SIG, all known signals are included. Multiple signals can be
comma-separated. An empty SIG argument is a no-op.
""";
}
