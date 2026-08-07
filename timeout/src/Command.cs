// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Timeout;

using System.Globalization;
using System.Text;
using Icod.CoreUtils.Shared.Processes;
using Icod.CoreUtils.Shared.Time;

/// <summary>Implements GNU Coreutils 9.11 <c>timeout</c>.</summary>
public static class Command {
	private const int InternalFailure = 125;
	private static readonly Encoding Utf8 = new UTF8Encoding( false );

	/// <summary>Runs <c>timeout</c> synchronously for compatibility with historical callers.</summary>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		if ( null == stdin && null == stdout && null == stderr ) return RunAsync( args, forwardHostSignals: true ).GetAwaiter().GetResult();
		using var input = null == stdin ? null : new MemoryStream( Utf8.GetBytes( stdin.ReadToEnd() ), writable: false );
		using var output = new MemoryStream();
		using var error = new MemoryStream();
		var status = RunAsync( args, input, output, error ).GetAwaiter().GetResult();
		( stdout ?? Console.Out ).Write( Utf8.GetString( output.ToArray() ) );
		( stderr ?? Console.Error ).Write( Utf8.GetString( error.ToArray() ) );
		return status;
	}

	/// <summary>Runs GNU <c>timeout</c> asynchronously over the Completion Gate F4 process providers.</summary>
	public static async Task<int> RunAsync(
		string[] args,
		Stream? stdin = null,
		Stream? stdout = null,
		Stream? stderr = null,
		IProcessExecutor? processExecutor = null,
		IProcessSignalProvider? signalProvider = null,
		IMonotonicClock? clock = null,
		bool forwardHostSignals = false,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		var usingSystemExecutor = null == processExecutor;
		var executor = processExecutor ?? SystemProcessExecutor.Instance;
		var signals = signalProvider ?? SystemProcessSignalProvider.Instance;
		var monotonicClock = clock ?? SystemMonotonicClock.Instance;
		var parsed = ParseArguments( args );
		if ( null != parsed.Error ) {
			await WriteDiagnosticAsync( stderr, $"timeout: {parsed.Error}", cancellationToken ).ConfigureAwait( false );
			await WriteDiagnosticAsync( stderr, "Try 'timeout --help' for more information.", cancellationToken ).ConfigureAwait( false );
			return InternalFailure;
		}
		if ( parsed.ShowHelp ) {
			await WriteAsync( stdout, string.Concat( NormalizeLineEndings( HelpText ), Environment.NewLine ), cancellationToken ).ConfigureAwait( false );
			return 0;
		}
		if ( parsed.ShowVersion ) {
			await WriteAsync( stdout, string.Concat( "timeout (Icod.CoreUtils) 9.11", Environment.NewLine ), cancellationToken ).ConfigureAwait( false );
			return 0;
		}
		var durationText = parsed.DurationText;
		var command = parsed.Command;
		if ( null == durationText || null == command ) {
			await WriteDiagnosticAsync( stderr, "timeout: missing operand", cancellationToken ).ConfigureAwait( false );
			await WriteDiagnosticAsync( stderr, "Try 'timeout --help' for more information.", cancellationToken ).ConfigureAwait( false );
			return InternalFailure;
		}
		if ( !TryParseDuration( durationText, out var duration ) ) {
			await WriteDiagnosticAsync( stderr, $"timeout: invalid time interval {Quote( durationText )}", cancellationToken ).ConfigureAwait( false );
			await WriteDiagnosticAsync( stderr, "Try 'timeout --help' for more information.", cancellationToken ).ConfigureAwait( false );
			return InternalFailure;
		}
		DurationSpec killAfter = default;
		var killAfterText = parsed.KillAfterText;
		if ( null != killAfterText && !TryParseDuration( killAfterText, out killAfter ) ) {
			await WriteDiagnosticAsync( stderr, $"timeout: invalid time interval {Quote( killAfterText )}", cancellationToken ).ConfigureAwait( false );
			await WriteDiagnosticAsync( stderr, "Try 'timeout --help' for more information.", cancellationToken ).ConfigureAwait( false );
			return InternalFailure;
		}
		var signalResult = ParseSignal( signals, parsed.SignalText ?? "TERM" );
		if ( !signalResult.Succeeded ) {
			await WriteDiagnosticAsync( stderr, $"timeout: {Quote( parsed.SignalText ?? string.Empty )}: invalid signal", cancellationToken ).ConfigureAwait( false );
			await WriteDiagnosticAsync( stderr, "Try 'timeout --help' for more information.", cancellationToken ).ConfigureAwait( false );
			return InternalFailure;
		}
		var timeoutSignal = signalResult.Value!;
		var killSignalResult = signals.ParseSignal( "KILL" );
		var continueSignalResult = signals.ParseSignal( "CONT" );
		if ( !killSignalResult.Succeeded || !continueSignalResult.Succeeded ) {
			await WriteDiagnosticAsync( stderr, "timeout: required host signals are unavailable", cancellationToken ).ConfigureAwait( false );
			return InternalFailure;
		}

		if ( cancellationToken.IsCancellationRequested ) return InternalFailure;
		var started = new TaskCompletionSource<ProcessIdentity>( TaskCreationOptions.RunContinuationsAsynchronously );
		using var executorCancellation = new CancellationTokenSource();
		var windowsTreeSubstitution = usingSystemExecutor && OperatingSystem.IsWindows() && !parsed.Foreground;
		var runOptions = new ProcessRunOptions( command ) {
			CancellationPolicy = windowsTreeSubstitution ? ProcessCancellationPolicy.KillProcessTree : ProcessCancellationPolicy.LeaveRunning,
			CreateProcessGroup = !parsed.Foreground,
			ResolveExecutable = true,
			ReturnLaunchFailureResult = true,
			StandardInput = stdin,
			StandardOutput = stdout,
			StandardError = stderr,
			ProcessStarted = identity => started.TrySetResult( identity )
		};
		foreach ( var argument in parsed.CommandArguments ) runOptions.Arguments.Add( argument );

		Task<ProcessResult> runTask;
		try {
			runTask = executor.RunAsync( runOptions, executorCancellation.Token );
		} catch ( Exception exception ) {
			await WriteDiagnosticAsync( stderr, $"timeout: failed to run command {Quote( command )}: {exception.Message}", CancellationToken.None ).ConfigureAwait( false );
			return InternalFailure;
		}
		var first = await Task.WhenAny( runTask, started.Task ).ConfigureAwait( false );
		if ( ReferenceEquals( first, runTask ) ) return await FinishProcessAsync( runTask, stderr, command ).ConfigureAwait( false );
		var identity = await started.Task.ConfigureAwait( false );
		using var forwardingScope = forwardHostSignals && usingSystemExecutor
			? TimeoutSignalForwardingScope.Create( timeoutSignal, name => _ = ForwardExternalSignalAsync( name, identity, parsed.Foreground, parsed.Verbose, command, stderr, signals, continueSignalResult.Value! ) )
			: null
		;
		var cancellationTask = CancellationAsTask( cancellationToken );
		if ( !duration.Enabled ) {
			var completed = await Task.WhenAny( runTask, cancellationTask ).ConfigureAwait( false );
			if ( ReferenceEquals( completed, runTask ) ) return await FinishProcessAsync( runTask, stderr, command ).ConfigureAwait( false );
			await ForceCleanupAsync( identity, parsed.Foreground, signals, killSignalResult.Value!, executorCancellation ).ConfigureAwait( false );
			_ = await ObserveRunResultAsync( runTask ).ConfigureAwait( false );
			return InternalFailure;
		}

		using var timeoutDelayCancellation = new CancellationTokenSource();
		var timeoutTask = DelayAsync( monotonicClock, duration.Delay, timeoutDelayCancellation.Token );
		var winner = await Task.WhenAny( runTask, timeoutTask, cancellationTask ).ConfigureAwait( false );
		if ( ReferenceEquals( winner, runTask ) ) {
			timeoutDelayCancellation.Cancel();
			return await FinishProcessAsync( runTask, stderr, command ).ConfigureAwait( false );
		}
		if ( ReferenceEquals( winner, cancellationTask ) ) {
			timeoutDelayCancellation.Cancel();
			await ForceCleanupAsync( identity, parsed.Foreground, signals, killSignalResult.Value!, executorCancellation ).ConfigureAwait( false );
			_ = await ObserveRunResultAsync( runTask ).ConfigureAwait( false );
			return InternalFailure;
		}
		if ( runTask.IsCompleted ) return await FinishProcessAsync( runTask, stderr, command ).ConfigureAwait( false );

		var timedOut = true;
		var lastSignal = timeoutSignal;
		if ( parsed.Verbose ) await WriteSignalDiagnosticAsync( stderr, timeoutSignal, command ).ConfigureAwait( false );
		ProcessOperationResult initialDelivery;
		if ( windowsTreeSubstitution && timeoutSignal.Number is 9 or 15 ) {
			executorCancellation.Cancel();
			initialDelivery = ProcessOperationResult.Success( "Windows process-tree termination substitution used.", usedPlatformSubstitution: true );
		} else {
			initialDelivery = await DeliverTimeoutSignalAsync(
				identity,
				parsed.Foreground,
				timeoutSignal,
				continueSignalResult.Value!,
				signals
			).ConfigureAwait( false );
		}
		if ( ProcessOperationStatus.Unsupported == initialDelivery.Status ) {
			await WriteDiagnosticAsync( stderr, $"timeout: cannot send signal {timeoutSignal.Name}: {initialDelivery.Message ?? "unsupported on this host"}", CancellationToken.None ).ConfigureAwait( false );
			await ForceCleanupAsync( identity, parsed.Foreground, signals, killSignalResult.Value!, executorCancellation ).ConfigureAwait( false );
			_ = await ObserveRunResultAsync( runTask ).ConfigureAwait( false );
			return InternalFailure;
		}

		if ( killAfter.Enabled ) {
			using var killDelayCancellation = new CancellationTokenSource();
			var killDelayTask = DelayAsync( monotonicClock, killAfter.Delay, killDelayCancellation.Token );
			winner = await Task.WhenAny( runTask, killDelayTask, cancellationTask ).ConfigureAwait( false );
			if ( ReferenceEquals( winner, cancellationTask ) ) {
				killDelayCancellation.Cancel();
				await ForceCleanupAsync( identity, parsed.Foreground, signals, killSignalResult.Value!, executorCancellation ).ConfigureAwait( false );
				_ = await ObserveRunResultAsync( runTask ).ConfigureAwait( false );
				return InternalFailure;
			}
			if ( !ReferenceEquals( winner, runTask ) && !runTask.IsCompleted ) {
				lastSignal = killSignalResult.Value!;
				if ( parsed.Verbose ) await WriteSignalDiagnosticAsync( stderr, lastSignal, command ).ConfigureAwait( false );
				if ( windowsTreeSubstitution ) executorCancellation.Cancel();
				else _ = await DeliverTimeoutSignalAsync( identity, parsed.Foreground, lastSignal, continueSignalResult.Value!, signals ).ConfigureAwait( false );
			} else killDelayCancellation.Cancel();
		}

		var result = await ObserveRunResultAsync( runTask ).ConfigureAwait( false );
		if ( null == result ) return InternalFailure;
		if ( timedOut
			&& ProcessTerminationKind.Signaled == result.Termination.Kind
			&& 9 == result.Termination.Signal?.Number
		) return 137;
		if ( windowsTreeSubstitution
			&& ProcessTerminationKind.Canceled == result.Termination.Kind
			&& 9 == lastSignal.Number
		) return 137;
		if ( timedOut && !parsed.PreserveStatus ) return 124;
		if ( windowsTreeSubstitution && ProcessTerminationKind.Canceled == result.Termination.Kind ) return 128 + lastSignal.Number;
		return result.Termination.ToPortableExitCode();
	}

	private static async Task<int> FinishProcessAsync( Task<ProcessResult> runTask, Stream? stderr, string command ) {
		var result = await ObserveRunResultAsync( runTask ).ConfigureAwait( false );
		if ( null == result ) return InternalFailure;
		if ( ProcessTerminationKind.LaunchFailed == result.Termination.Kind ) {
			await WriteDiagnosticAsync( stderr, $"timeout: failed to run command {Quote( command )}: {result.Termination.Message ?? "cannot execute"}", CancellationToken.None ).ConfigureAwait( false );
		}
		return result.Termination.ToPortableExitCode();
	}

	private static async Task<ProcessResult?> ObserveRunResultAsync( Task<ProcessResult> runTask ) {
		try {
			return await runTask.ConfigureAwait( false );
		} catch ( OperationCanceledException ) {
			return ProcessResult.FromTermination( ProcessTermination.Canceled() );
		} catch {
			return null;
		}
	}

	private static async Task ForwardExternalSignalAsync(
		string signalName,
		ProcessIdentity identity,
		bool foreground,
		bool verbose,
		string command,
		Stream? stderr,
		IProcessSignalProvider signals,
		ProcessSignal continueSignal
	) {
		try {
			var parsed = signals.ParseSignal( signalName );
			if ( !parsed.Succeeded ) return;
			if ( verbose ) await WriteSignalDiagnosticAsync( stderr, parsed.Value!, command ).ConfigureAwait( false );
			_ = await DeliverTimeoutSignalAsync( identity, foreground, parsed.Value!, continueSignal, signals ).ConfigureAwait( false );
		} catch {
			// A signal callback cannot safely surface an asynchronous forwarding failure.
		}
	}

	private static async Task<ProcessOperationResult> DeliverTimeoutSignalAsync(
		ProcessIdentity identity,
		bool foreground,
		ProcessSignal signal,
		ProcessSignal continueSignal,
		IProcessSignalProvider signals
	) {
		var direct = await signals.DeliverAsync( ProcessTarget.ForProcess( identity ), signal ).ConfigureAwait( false );
		var group = ProcessOperationResult.Success();
		if ( !foreground ) group = await signals.DeliverAsync( ProcessTarget.ForProcessGroup( identity.ProcessId ), signal ).ConfigureAwait( false );
		if ( signal.Number != 9 && signal.Number != continueSignal.Number ) {
			_ = await signals.DeliverAsync( ProcessTarget.ForProcess( identity ), continueSignal ).ConfigureAwait( false );
			if ( !foreground ) _ = await signals.DeliverAsync( ProcessTarget.ForProcessGroup( identity.ProcessId ), continueSignal ).ConfigureAwait( false );
		}
		if ( direct.Succeeded ) return group.Succeeded || ProcessOperationStatus.Unsupported == group.Status ? direct : group;
		return direct;
	}

	private static async Task ForceCleanupAsync(
		ProcessIdentity identity,
		bool foreground,
		IProcessSignalProvider signals,
		ProcessSignal killSignal,
		CancellationTokenSource executorCancellation
	) {
		if ( OperatingSystem.IsWindows() && !foreground ) {
			executorCancellation.Cancel();
			return;
		}
		_ = await signals.DeliverAsync( ProcessTarget.ForProcess( identity ), killSignal ).ConfigureAwait( false );
		if ( !foreground ) _ = await signals.DeliverAsync( ProcessTarget.ForProcessGroup( identity.ProcessId ), killSignal ).ConfigureAwait( false );
		executorCancellation.Cancel();
	}

	private static ProcessOperationResult<ProcessSignal> ParseSignal( IProcessSignalProvider signals, string text ) {
		if ( string.IsNullOrEmpty( text ) || text[ 0 ] is '+' or '-' ) {
			return ProcessOperationResult<ProcessSignal>.Failure( ProcessOperationStatus.InvalidArgument, "invalid signal" );
		}
		return signals.ParseSignal( text );
	}

	private static async Task DelayAsync( IMonotonicClock clock, TimeSpan delay, CancellationToken cancellationToken ) {
		try {
			await clock.DelayAsync( delay, cancellationToken ).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
		}
	}

	private static Task CancellationAsTask( CancellationToken cancellationToken ) {
		if ( !cancellationToken.CanBeCanceled ) return Task.Delay( System.Threading.Timeout.InfiniteTimeSpan );
		return Task.Delay( System.Threading.Timeout.InfiniteTimeSpan, cancellationToken ).ContinueWith(
			static _ => { },
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default
		);
	}

	private static TimeoutArguments ParseArguments( string[] args ) {
		var foreground = false;
		var preserve = false;
		var verbose = false;
		string? killAfter = null;
		string? signal = null;
		var index = 0;
		while ( index < args.Length ) {
			var token = args[ index ];
			if ( "--" == token ) { index++; break; }
			if ( !token.StartsWith( "-", StringComparison.Ordinal ) || "-" == token ) break;
			if ( token.StartsWith( "--", StringComparison.Ordinal ) ) {
				var equal = token.IndexOf( '=' );
				var name = 0 > equal ? token[ 2.. ] : token[ 2..equal ];
				var value = 0 > equal ? null : token[ ( equal + 1 ).. ];
				var option = ResolveLongOption( name );
				if ( null == option ) return TimeoutArguments.Failure( $"unrecognized option '{token}'" );
				if ( option is "help" or "version" or "foreground" or "preserve-status" or "verbose" ) {
					if ( null != value ) return TimeoutArguments.Failure( $"option '--{option}' doesn't allow an argument" );
					if ( "help" == option ) return TimeoutArguments.Help;
					if ( "version" == option ) return TimeoutArguments.Version;
					if ( "foreground" == option ) foreground = true;
					else if ( "preserve-status" == option ) preserve = true;
					else verbose = true;
					index++;
					continue;
				}
				if ( null == value ) {
					if ( index + 1 >= args.Length ) return TimeoutArguments.Failure( $"option '--{option}' requires an argument" );
					value = args[ ++index ];
				}
				if ( "kill-after" == option ) killAfter = value;
				else signal = value;
				index++;
				continue;
			}

			var position = 1;
			while ( position < token.Length ) {
				var option = token[ position++ ];
				if ( 'f' == option ) foreground = true;
				else if ( 'p' == option ) preserve = true;
				else if ( 'v' == option ) verbose = true;
				else if ( option is 'k' or 's' ) {
					string value;
					if ( position < token.Length ) value = token[ position.. ];
					else {
						if ( index + 1 >= args.Length ) return TimeoutArguments.Failure( $"option requires an argument -- '{option}'" );
						value = args[ ++index ];
					}
					if ( 'k' == option ) killAfter = value;
					else signal = value;
					break;
				} else return TimeoutArguments.Failure( $"invalid option -- '{option}'" );
			}
			index++;
		}
		if ( index >= args.Length ) return new TimeoutArguments( foreground, preserve, verbose, killAfter, signal, null, null, Array.Empty<string>(), false, false, null );
		var duration = args[ index++ ];
		if ( index >= args.Length ) return new TimeoutArguments( foreground, preserve, verbose, killAfter, signal, duration, null, Array.Empty<string>(), false, false, null );
		var command = args[ index++ ];
		return new TimeoutArguments( foreground, preserve, verbose, killAfter, signal, duration, command, args.Skip( index ).ToArray(), false, false, null );
	}

	private static string? ResolveLongOption( string name ) {
		string[] options = [ "foreground", "kill-after", "preserve-status", "signal", "verbose", "help", "version" ];
		var matches = options.Where( option => option.StartsWith( name, StringComparison.Ordinal ) ).ToArray();
		return 1 == matches.Length ? matches[ 0 ] : null;
	}

	private static bool TryParseDuration( string text, out DurationSpec duration ) {
		duration = default;
		if ( string.IsNullOrEmpty( text ) ) return false;
		var trimmedStart = text.TrimStart();
		if ( 0 == trimmedStart.Length || char.IsWhiteSpace( trimmedStart[ ^1 ] ) ) return false;
		var suffix = '\0';
		var numeric = trimmedStart;
		var hexadecimalWithoutExponent = trimmedStart.StartsWith( "0x", StringComparison.OrdinalIgnoreCase )
			&& 0 > trimmedStart.IndexOfAny( [ 'p', 'P' ] )
		;
		if ( trimmedStart[ ^1 ] is 's' or 'm' or 'h'
			|| ( 'd' == trimmedStart[ ^1 ] && !hexadecimalWithoutExponent )
		) {
			suffix = trimmedStart[ ^1 ];
			numeric = trimmedStart[ ..^1 ];
			if ( 0 == numeric.Length ) return false;
		}
		if ( char.IsWhiteSpace( numeric[ ^1 ] ) ) return false;
		if ( !TryParseGnuDouble( numeric, out var value ) || double.IsNaN( value ) || 0 > value ) return false;
		var multiplier = suffix switch { 'm' => 60d, 'h' => 3600d, 'd' => 86400d, _ => 1d };
		var seconds = value * multiplier;
		if ( double.IsNaN( seconds ) || 0 > seconds ) return false;
		var enabled = 0d != value || RepresentsNonZeroNumber( numeric );
		if ( !enabled ) {
			duration = new DurationSpec( false, TimeSpan.Zero );
			return true;
		}
		var maximumSeconds = TimeSpan.MaxValue.TotalSeconds;
		var delay = double.IsPositiveInfinity( seconds ) || maximumSeconds <= seconds
			? TimeSpan.MaxValue
			: TimeSpan.FromSeconds( seconds );
		if ( TimeSpan.Zero == delay ) delay = TimeSpan.FromTicks( 1 );
		duration = new DurationSpec( true, delay );
		return true;
	}

	private static bool RepresentsNonZeroNumber( string text ) {
		var value = text.TrimStart( '+', '-' );
		if ( value.StartsWith( "inf", StringComparison.OrdinalIgnoreCase ) ) return true;
		if ( value.StartsWith( "0x", StringComparison.OrdinalIgnoreCase ) ) {
			value = value[ 2.. ];
			var exponent = value.IndexOfAny( [ 'p', 'P' ] );
			if ( 0 <= exponent ) value = value[ ..exponent ];
			return value.Any( static character => character is not '0' and not '.' );
		}
		var decimalExponent = value.IndexOfAny( [ 'e', 'E' ] );
		if ( 0 <= decimalExponent ) value = value[ ..decimalExponent ];
		return value.Any( static character => character is >= '1' and <= '9' );
	}

	private static bool TryParseGnuDouble( string text, out double value ) {
		value = 0d;
		var sign = 1d;
		if ( text.StartsWith( "+", StringComparison.Ordinal ) ) text = text[ 1.. ];
		else if ( text.StartsWith( "-", StringComparison.Ordinal ) ) { sign = -1d; text = text[ 1.. ]; }
		if ( 0 == text.Length || char.IsWhiteSpace( text[ 0 ] ) ) return false;
		if ( text.Equals( "inf", StringComparison.OrdinalIgnoreCase ) || text.Equals( "infinity", StringComparison.OrdinalIgnoreCase ) ) {
			value = sign * double.PositiveInfinity;
			return true;
		}
		if ( text.Equals( "nan", StringComparison.OrdinalIgnoreCase ) ) { value = double.NaN; return true; }
		if ( text.StartsWith( "0x", StringComparison.OrdinalIgnoreCase ) ) {
			if ( !TryParseHexFloat( text[ 2.. ], out value ) ) return false;
			value *= sign;
			return true;
		}
		if ( !double.TryParse( ( 0 > sign ? "-" : string.Empty ) + text, NumberStyles.Float, CultureInfo.InvariantCulture, out value ) ) return false;
		return true;
	}

	private static bool TryParseHexFloat( string text, out double value ) {
		value = 0d;
		var exponentIndex = text.IndexOfAny( [ 'p', 'P' ] );
		var mantissa = 0 <= exponentIndex ? text[ ..exponentIndex ] : text;
		var exponentText = 0 <= exponentIndex ? text[ ( exponentIndex + 1 ).. ] : null;
		if ( 0 == mantissa.Length ) return false;
		long parsedExponent = 0;
		if ( null != exponentText ) {
			var exponentDigits = exponentText;
			var negativeExponent = exponentDigits.StartsWith( "-", StringComparison.Ordinal );
			if ( negativeExponent || exponentDigits.StartsWith( "+", StringComparison.Ordinal ) ) exponentDigits = exponentDigits[ 1.. ];
			if ( 0 == exponentDigits.Length || exponentDigits.Any( static character => !char.IsAsciiDigit( character ) ) ) return false;
			if ( !long.TryParse( exponentText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsedExponent ) ) {
				parsedExponent = negativeExponent ? int.MinValue : int.MaxValue;
			}
		}
		var point = mantissa.IndexOf( '.' );
		if ( 0 <= point && point != mantissa.LastIndexOf( '.' ) ) return false;
		var digits = mantissa.Replace( ".", string.Empty, StringComparison.Ordinal );
		if ( 0 == digits.Length || digits.Any( static c => !Uri.IsHexDigit( c ) ) ) return false;
		double accumulator = 0d;
		foreach ( var digit in digits ) accumulator = ( accumulator * 16d ) + HexValue( digit );
		var fractionDigits = 0 <= point ? mantissa.Length - point - 1 : 0;
		var exponentValue = parsedExponent - ( 4L * fractionDigits );
		var binaryExponent = exponentValue > int.MaxValue ? int.MaxValue : exponentValue < int.MinValue ? int.MinValue : (int)exponentValue;
		value = Math.ScaleB( accumulator, binaryExponent );
		return true;
	}

	private static int HexValue( char value ) => value switch {
		>= '0' and <= '9' => value - '0',
		>= 'a' and <= 'f' => value - 'a' + 10,
		>= 'A' and <= 'F' => value - 'A' + 10,
		_ => 0
	};

	private static async Task WriteSignalDiagnosticAsync( Stream? stderr, ProcessSignal signal, string command ) => await WriteDiagnosticAsync(
		stderr,
		$"timeout: sending signal {signal.Name} to command {Quote( command )}",
		CancellationToken.None
	).ConfigureAwait( false );

	private static string Quote( string value ) => string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );

	private static async Task WriteAsync( Stream? stream, string text, CancellationToken cancellationToken ) {
		if ( null == stream ) { await Console.Out.WriteAsync( text.AsMemory(), cancellationToken ).ConfigureAwait( false ); return; }
		var bytes = Utf8.GetBytes( text );
		await stream.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
		await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
	}

	private static async Task WriteDiagnosticAsync( Stream? stream, string text, CancellationToken cancellationToken ) {
		if ( null == stream ) { await Console.Error.WriteLineAsync( text.AsMemory(), cancellationToken ).ConfigureAwait( false ); return; }
		var bytes = Utf8.GetBytes( string.Concat( text, Environment.NewLine ) );
		await stream.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
		await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
	}

	private static string NormalizeLineEndings( string value ) => "\n" == Environment.NewLine ? value : value.Replace( "\n", Environment.NewLine, StringComparison.Ordinal );

	private readonly record struct DurationSpec( bool Enabled, TimeSpan Delay );

	private sealed record TimeoutArguments(
		bool Foreground,
		bool PreserveStatus,
		bool Verbose,
		string? KillAfterText,
		string? SignalText,
		string? DurationText,
		string? Command,
		IReadOnlyList<string> CommandArguments,
		bool ShowHelp,
		bool ShowVersion,
		string? Error
	) {
		/// <summary>Gets the parsed help request.</summary>
		internal static TimeoutArguments Help { get; } = new( false, false, false, null, null, null, null, Array.Empty<string>(), true, false, null );
		/// <summary>Gets the parsed version request.</summary>
		internal static TimeoutArguments Version { get; } = new( false, false, false, null, null, null, null, Array.Empty<string>(), false, true, null );
		/// <summary>Creates a parser-failure result.</summary>
		internal static TimeoutArguments Failure( string error ) => new( false, false, false, null, null, null, null, Array.Empty<string>(), false, false, error );
	}

	private const string HelpText = """
Usage: timeout [OPTION]... DURATION COMMAND [ARG]...
Start COMMAND, and kill it if still running after DURATION.

  -f, --foreground
                 allow COMMAND to read from the TTY and get TTY signals;
                 in this mode, children of COMMAND will not be timed out
  -k, --kill-after=DURATION
                 also send a KILL signal if COMMAND is still running
                 this long after the initial signal was sent
  -p, --preserve-status
                 exit with the same status as COMMAND, even when it times out
  -s, --signal=SIGNAL
                 specify the signal to be sent on timeout
  -v, --verbose  diagnose to standard error any signal sent upon timeout
      --help     display this help and exit
      --version  output version information and exit

DURATION is a floating point number with an optional suffix:
's' for seconds (the default), 'm' for minutes, 'h' for hours or 'd' for days.
A duration of 0 disables the associated timeout.

Exit status:
  124  if COMMAND times out, and --preserve-status is not specified
  125  if timeout itself fails
  126  if COMMAND is found but cannot be invoked
  127  if COMMAND cannot be found
  137  if COMMAND (or timeout itself) is sent the KILL (9) signal (128+9)
  -    the exit status of COMMAND otherwise
""";
}
