namespace Icod.CoreUtils.Expr;

using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.RegularExpressions;

/// <summary>Implements <c>expr EXPRESSION</c> according to GNU Coreutils.</summary>
public static class Command {
	private const int NullOrZeroStatus = 1;
	private const int InvalidExpressionStatus = 2;
	private const int FailureStatus = 3;
	private const string VersionText = "expr (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>expr</c> synchronously with optional standard-stream substitution.
	/// </summary>
	/// <remarks>
	/// This compatibility entry point blocks on the TAP implementation. A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream; caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <returns>The GNU <c>expr</c> status: 0 for a non-null result, 1 for a null result, 2 for an invalid expression, or 3 for an internal failure.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		return RunAsync(
			args,
			new CommandContext(
				"expr",
				stdin ?? Console.In,
				stdout ?? Console.Out,
				stderr ?? Console.Error
			)
		).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Executes <c>expr</c> asynchronously with optional injected standard streams.
	/// </summary>
	/// <remarks>
	/// A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream. Caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU <c>expr</c> status: 0 for a non-null result, 1 for a null result, 2 for an invalid expression, or 3 for an internal failure.</returns>
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
				"expr",
				stdin ?? Console.In,
				stdout ?? Console.Out,
				stderr ?? Console.Error,
				cancellationToken: cancellationToken
			)
		);
	}

	/// <summary>
	/// Executes <c>expr</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <param name="regularExpressionProvider">The shared GNU basic-regular-expression provider used by match operations.</param>
	/// <param name="localeProvider">The provider used for collation and logical-character operations.</param>
	/// <returns>The GNU <c>expr</c> status: 0 for a non-null result, 1 for a null result, 2 for an invalid expression, or 3 for an internal failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		IRegularExpressionProvider? regularExpressionProvider = null,
		IExpressionLocaleProvider? localeProvider = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		regularExpressionProvider ??= GnuBasicRegularExpressionProvider.Default;
		localeProvider ??= SystemExpressionLocaleProvider.CurrentCulture;
		try {
			context.CancellationToken.ThrowIfCancellationRequested();
			if ( 1 == args.Length && IsHelpOption( args[ 0 ] ) ) {
				await context.StandardOutput.WriteAsync(
					GetHelpText( context.ProgramName ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( 1 == args.Length && IsVersionOption( args[ 0 ] ) ) {
				await context.StandardOutput.WriteLineAsync(
					VersionText.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			var expressionArguments = args;
			if ( 0 < expressionArguments.Length && "--" == expressionArguments[ 0 ] ) {
				expressionArguments = expressionArguments[ 1.. ];
			}
			if ( 0 == expressionArguments.Length ) {
				throw new ExpressionEvaluationException(
					[
						"missing operand",
						string.Concat(
							"Try '",
							context.ProgramName,
							" --help' for more information."
						)
					],
					InvalidExpressionStatus
				);
			}
			var evaluator = new ExpressionEvaluator(
				expressionArguments,
				regularExpressionProvider,
				localeProvider,
				context.CancellationToken
			);
			var result = evaluator.Evaluate();
			await context.StandardOutput.WriteLineAsync(
				result.AsString().AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
			await context.StandardOutput.FlushAsync(
				context.CancellationToken
			).ConfigureAwait( false );
			return result.IsNull ? NullOrZeroStatus : CommandExitCodes.Success;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		} catch ( ExpressionEvaluationException exception ) {
			if ( !await WriteDiagnosticsAsync( context, exception.DiagnosticMessages ).ConfigureAwait( false ) ) {
				return FailureStatus;
			}
			return exception.ExitStatus;
		} catch ( Exception exception ) {
			_ = await WriteDiagnosticsAsync(
				context,
				[ exception.Message ]
			).ConfigureAwait( false );
			return FailureStatus;
		}
	}

	private static async Task<bool> WriteDiagnosticsAsync(
		CommandContext context,
		IReadOnlyList<string> messages
	) {
		try {
			foreach ( var message in messages ) {
				if ( message.StartsWith( "Try '", StringComparison.Ordinal ) ) {
					await context.StandardError.WriteLineAsync(
						message.AsMemory(),
						CancellationToken.None
					).ConfigureAwait( false );
				} else {
					await context.Diagnostics.ErrorAsync(
						message,
						CancellationToken.None
					).ConfigureAwait( false );
				}
			}
			return true;
		} catch ( Exception ) {
			return false;
		}
	}

	private static bool IsHelpOption( string value ) {
		return 3 <= value.Length
			&& "--help".StartsWith( value, StringComparison.Ordinal );
	}

	private static bool IsVersionOption( string value ) {
		return 3 <= value.Length
			&& "--version".StartsWith( value, StringComparison.Ordinal );
	}

	private static string GetHelpText( string programName ) {
		return string.Join(
			Environment.NewLine,
			[
				string.Concat( "Usage: ", programName, " EXPRESSION" ),
				string.Concat( "  or:  ", programName, " OPTION" ),
				string.Empty,
				"      --help        display this help and exit",
				"      --version     output version information and exit",
				string.Empty,
				"Print the value of EXPRESSION to standard output.  A blank line below",
				"separates increasing precedence groups.  EXPRESSION may be:",
				string.Empty,
				"  ARG1 | ARG2       ARG1 if it is neither null nor 0, otherwise ARG2",
				string.Empty,
				"  ARG1 & ARG2       ARG1 if neither argument is null or 0, otherwise 0",
				string.Empty,
				"  ARG1 < ARG2       ARG1 is less than ARG2",
				"  ARG1 <= ARG2      ARG1 is less than or equal to ARG2",
				"  ARG1 = ARG2       ARG1 is equal to ARG2",
				"  ARG1 != ARG2      ARG1 is unequal to ARG2",
				"  ARG1 >= ARG2      ARG1 is greater than or equal to ARG2",
				"  ARG1 > ARG2       ARG1 is greater than ARG2",
				string.Empty,
				"  ARG1 + ARG2       arithmetic sum of ARG1 and ARG2",
				"  ARG1 - ARG2       arithmetic difference of ARG1 and ARG2",
				string.Empty,
				"  ARG1 * ARG2       arithmetic product of ARG1 and ARG2",
				"  ARG1 / ARG2       arithmetic quotient of ARG1 divided by ARG2",
				"  ARG1 % ARG2       arithmetic remainder of ARG1 divided by ARG2",
				string.Empty,
				"  STRING : REGEXP   anchored pattern match of REGEXP in STRING",
				string.Empty,
				"  match STRING REGEXP        same as STRING : REGEXP",
				"  substr STRING POS LENGTH   substring of STRING, POS counted from 1",
				"  index STRING CHARS         index in STRING where any CHARS is found, or 0",
				"  length STRING              length of STRING",
				"  + TOKEN                    interpret TOKEN as a string, even if it is a",
				"                               keyword like 'match' or an operator like '/'",
				string.Empty,
				"  ( EXPRESSION )             value of EXPRESSION",
				string.Empty,
				"Beware that many operators need to be escaped or quoted for shells.",
				"Comparisons are arithmetic if both ARGs are numbers, else lexicographical.",
				"Pattern matches return the string matched between \\( and \\) or null; if",
				"\\( and \\) are not used, they return the number of characters matched or 0.",
				string.Empty,
				"Exit status is 0 if EXPRESSION is neither null nor 0, 1 if EXPRESSION is null",
				"or 0, 2 if EXPRESSION is syntactically invalid, and 3 if an error occurred.",
				string.Empty,
				"GNU coreutils online help: <https://www.gnu.org/software/coreutils/>",
				"Full documentation <https://www.gnu.org/software/coreutils/expr>",
				"or available locally via: info '(coreutils) expr invocation'",
				string.Empty
			]
		);
	}
}
