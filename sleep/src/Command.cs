namespace Icod.CoreUtils.Sleep;

using System.Globalization;

/// <summary>
/// Implements the sleep utility.
/// </summary>
public static class Command {
	private static readonly TimeSpan MaximumDelayChunk = TimeSpan.FromDays( 1 );
	private const string VersionText = "sleep (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>sleep</c> synchronously with optional standard-stream substitution.
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
	/// Executes <c>sleep</c> asynchronously with optional injected standard streams.
	/// </summary>
	/// <remarks>
	/// A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream. Caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			var operands = new List<string>();
			var optionsEnded = false;
			foreach ( var argument in args ) {
				if ( !optionsEnded ) {
					if ( "--" == argument ) {
						optionsEnded = true;
						continue;
					}
					if ( "--help" == argument ) {
						await PrintUsageAsync(
							stdout,
							cancellationToken
						).ConfigureAwait( false );
						return 0;
					}
					if ( "--version" == argument ) {
						await stdout.WriteLineAsync(
							VersionText.AsMemory(),
							cancellationToken
						).ConfigureAwait( false );
						return 0;
					}
					if (
						argument.Length > 1
						&& '-' == argument[ 0 ]
					) {
						await WriteInvalidOptionAsync(
							argument,
							stderr,
							cancellationToken
						).ConfigureAwait( false );
						return 1;
					}
				}
				operands.Add( argument );
			}

			if ( 0 == operands.Count ) {
				await stderr.WriteAsync(
					System.String.Concat(
						"sleep: missing operand",
						Environment.NewLine,
						"Try 'sleep --help' for more information.",
						Environment.NewLine
					).AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				return 1;
			}

			var totalSeconds = 0.0;
			foreach ( var operand in operands ) {
				if ( !TryParseDuration( operand, out var seconds ) ) {
					await stderr.WriteAsync(
						System.String.Concat(
							"sleep: invalid time interval '",
							operand,
							"'",
							Environment.NewLine,
							"Try 'sleep --help' for more information.",
							Environment.NewLine
						).AsMemory(),
						cancellationToken
					).ConfigureAwait( false );
					return 1;
				}
				totalSeconds += seconds;
			}

			if ( double.IsPositiveInfinity( totalSeconds ) ) {
				await Task.Delay(
					Timeout.InfiniteTimeSpan,
					cancellationToken
				).ConfigureAwait( false );
				return 0;
			}

			var remaining = TimeSpan.FromSeconds( totalSeconds );
			while ( TimeSpan.Zero < remaining ) {
				var delay = remaining > MaximumDelayChunk
					? MaximumDelayChunk
					: remaining
				;
				await Task.Delay(
					delay,
					cancellationToken
				).ConfigureAwait( false );
				remaining -= delay;
			}
			return 0;
		} catch ( OperationCanceledException ) {
			return 130;
		} catch ( Exception exception ) when (
			exception is ArgumentOutOfRangeException
			or OverflowException
		) {
			await TryWriteErrorAsync(
				stderr,
				System.String.Concat(
					"sleep: ",
					exception.Message,
					Environment.NewLine
				)
			).ConfigureAwait( false );
			return 1;
		} catch ( IOException ) {
			return 1;
		}
	}

	private static bool TryParseDuration(
		string operand,
		out double seconds
	) {
		seconds = 0;
		if ( string.IsNullOrEmpty( operand ) ) {
			return false;
		}
		var multiplier = 1.0;
		var number = operand;
		switch ( operand[ ^1 ] ) {
			case 's':
				number = operand[ ..^1 ];
				break;
			case 'm':
				number = operand[ ..^1 ];
				multiplier = 60;
				break;
			case 'h':
				number = operand[ ..^1 ];
				multiplier = 60 * 60;
				break;
			case 'd':
				number = operand[ ..^1 ];
				multiplier = 24 * 60 * 60;
				break;
		}
		if ( string.IsNullOrEmpty( number ) ) {
			return false;
		}
		if (
			"inf".Equals( number.TrimStart( '+' ), StringComparison.OrdinalIgnoreCase )
			|| "infinity".Equals( number.TrimStart( '+' ), StringComparison.OrdinalIgnoreCase )
		) {
			seconds = double.PositiveInfinity;
			return true;
		}
		if (
			!double.TryParse(
				number,
				NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
				CultureInfo.InvariantCulture,
				out var value
			)
			|| double.IsNaN( value )
			|| 0 > value
		) {
			return false;
		}
		seconds = value * multiplier;
		return !double.IsNaN( seconds );
	}

	private static async Task WriteInvalidOptionAsync(
		string argument,
		TextWriter error,
		CancellationToken cancellationToken
	) {
		var option = argument.StartsWith( "--", StringComparison.Ordinal )
			? argument
			: $"-{argument[ 1 ]}"
		;
		await error.WriteAsync(
			System.String.Concat(
				"sleep: unrecognized option '",
				option,
				"'",
				Environment.NewLine,
				"Try 'sleep --help' for more information.",
				Environment.NewLine
			).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	private static async Task TryWriteErrorAsync(
		TextWriter error,
		string message
	) {
		try {
			await error.WriteAsync( message ).ConfigureAwait( false );
		} catch ( IOException ) {
		}
	}

	private static async Task PrintUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		const string text = """
Usage: sleep NUMBER[SUFFIX]...
  or:  sleep OPTION
Pause for NUMBER seconds.  SUFFIX may be 's' for seconds (the default),
'm' for minutes, 'h' for hours, or 'd' for days.  NUMBER need not be an
integer.  Given two or more arguments, pause for the sum of their values.

      --help        display this help and exit
      --version     output version information and exit
""";
		await output.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}
}
