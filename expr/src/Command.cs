namespace Icod.CoreUtils.Expr;

using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Implements <c>expr EXPRESSION</c> according to GNU Coreutils.</summary>
public static class Command {
	private const int NullOrZeroStatus = 1;
	private const int InvalidExpressionStatus = 2;
	private const int FailureStatus = 3;
	private const string VersionText = "expr (Icod.CoreUtils) 1.0";

	/// <summary>Runs <c>expr</c> synchronously.</summary>
	/// <param name="args">The expression tokens.</param>
	/// <param name="stdin">Optional standard input; <c>expr</c> does not read it.</param>
	/// <param name="stdout">Optional standard output.</param>
	/// <param name="stderr">Optional standard error.</param>
	/// <returns>The command exit status.</returns>
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

	/// <summary>Runs <c>expr</c> asynchronously with injectable text streams.</summary>
	/// <param name="args">The expression tokens.</param>
	/// <param name="stdin">Optional standard input; <c>expr</c> does not read it.</param>
	/// <param name="stdout">Optional standard output.</param>
	/// <param name="stderr">Optional standard error.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The command exit status.</returns>
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

	/// <summary>Runs <c>expr</c> with an injected command context and semantic providers.</summary>
	/// <param name="args">The expression tokens.</param>
	/// <param name="context">The command execution context.</param>
	/// <param name="regularExpressionProvider">Optional GNU BRE provider.</param>
	/// <param name="localeProvider">Optional collation and logical-character provider.</param>
	/// <returns>The command exit status.</returns>
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
