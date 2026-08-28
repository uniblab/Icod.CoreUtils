namespace Icod.CoreUtils.PrintEnv;
using System.Collections;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;

/// <summary>
/// Implements GNU-compatible <c>printenv</c> and prints selected environment variables or the complete environment.
/// </summary>
/// <remarks>
/// Unset requested variables affect the exit status without producing placeholder output.
/// </remarks>
public static class Command {
	private const string PROGRAM = "printenv";
	private const string VERSION = "printenv (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>printenv</c> synchronously with optional standard-stream substitution.
	/// </summary>
	/// <remarks>
	/// This compatibility entry point blocks on the TAP implementation. A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream; caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) =>
		RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();
	/// <summary>
	/// Executes <c>printenv</c> asynchronously with optional injected standard streams.
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
	public static Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) => RunAsync(
		args ?? Array.Empty<string>(),
		new CommandContext(
			PROGRAM,
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error,
			cancellationToken: cancellationToken
		)
	);

	/// <summary>
	/// Executes <c>printenv</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( context );
		var parser = CreateParser(
			new OptionDefinition( "null", '0', new[] { "null" } ),
			new OptionDefinition( "help", longNames: new[] { "help" } ),
			new OptionDefinition( "version", longNames: new[] { "version" } )
		);
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) {
				return 1;
			}
			if ( result.HasOption( "help" ) ) {
				await WriteHelpAsync(
					context
				).ConfigureAwait( false );
				return 0;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					VERSION.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return 0;
			}
			var nullTerminated = result.HasOption( "null" );
			var failed = false;
			if ( result.Operands.Count == 0 ) {
				foreach ( DictionaryEntry entry in Environment.GetEnvironmentVariables() ) {
					await WriteAsync(
						System.String.Concat( entry.Key, "=", entry.Value ),
						nullTerminated,
						context
					).ConfigureAwait( false );
				}
			} else {
				foreach ( var name in result.Operands ) {
					context.CancellationToken.ThrowIfCancellationRequested();
					var value = Environment.GetEnvironmentVariable( name );
					if ( value is null ) {
						failed = true;
					} else {
						await WriteAsync(
							value,
							nullTerminated,
							context
						).ConfigureAwait( false );
					}
				}
			}
			return ( failed )
				? 1
				: 0
			;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		}
	}

	private static async Task WriteAsync(
		string value,
		bool nullTerminated,
		CommandContext context
	) {
		if ( nullTerminated ) {
			await context.StandardOutput.WriteAsync(
				value.AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
			await context.StandardOutput.WriteAsync(
				"\0".AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		} else {
			await context.StandardOutput.WriteLineAsync(
				value.AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string text = """
Usage: printenv [OPTION]... [VARIABLE]...
Print values of the specified environment VARIABLE(s).

  -0, --null     end each output with NUL, not newline
      --help     display this help and exit
      --version  output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static OptionParser CreateParser( params OptionDefinition[] options ) => new(
		options,
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	private static async Task<bool> WriteParseErrorsAsync(
		OptionParseResult result,
		CommandContext context
	) {
		if ( result.IsSuccess ) {
			return false;
		}
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format(
					context.ProgramName,
					error
				).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return true;
	}

}
