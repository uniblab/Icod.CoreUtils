namespace Icod.CoreUtils.HostName;

using System.ComponentModel;
using System.Net.Sockets;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;

/// <summary>
/// Implements GNU Coreutils-compatible <c>hostname</c> reporting and mutation.
/// </summary>
/// <remarks>
/// GNU Coreutils 9.11 defines only the zero-operand query form, the one-operand
/// mutation form, and the common <c>--help</c> and <c>--version</c> options.
/// </remarks>
public static class Command {
	private const string PROGRAM = "hostname";
	private const string VERSION = "hostname (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>hostname</c> synchronously with optional standard-stream substitution.
	/// </summary>
	/// <remarks>
	/// This compatibility entry point blocks on the TAP implementation. A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream; caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="args"/> is <see langword="null"/>.</exception>
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

	/// <summary>
	/// Executes <c>hostname</c> asynchronously with optional injected standard streams.
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
	/// <exception cref="ArgumentNullException"><paramref name="args"/> is <see langword="null"/>.</exception>
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
				PROGRAM,
				stdin ?? Console.In,
				stdout ?? Console.Out,
				stderr ?? Console.Error,
				cancellationToken: cancellationToken
			)
		);
	}

	/// <summary>
	/// Executes <c>hostname</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="args"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
	public static Task<int> RunAsync(
		string[] args,
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		return RunAsync(
			args,
			context,
			SystemHostNamePlatform.Instance
		);
	}

	/// <summary>
	/// Executes <c>hostname</c> asynchronously using an injected host-name provider.
	/// </summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <param name="platform">The platform boundary used to read or mutate the active host name.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="args"/>, <paramref name="context"/>, or <paramref name="platform"/> is <see langword="null"/>.</exception>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		IHostNamePlatform platform
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( platform );

		var parser = CreateParser();
		try {
			context.CancellationToken.ThrowIfCancellationRequested();
			var result = parser.Parse( args );
			if (
				await WriteParseErrorsAsync(
					result,
					context
				).ConfigureAwait( false )
			) {
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
			if ( 1 < result.Operands.Count ) {
				await context.Diagnostics.ErrorAsync(
					$"extra operand '{result.Operands[ 1 ]}'",
					context.CancellationToken
				).ConfigureAwait( false );
				return 1;
			}

			if ( 1 == result.Operands.Count ) {
				var name = result.Operands[ 0 ];
				try {
					context.CancellationToken.ThrowIfCancellationRequested();
					platform.SetHostName(
						name
					);
					return 0;
				} catch ( Exception ex ) when (
					ex is PlatformNotSupportedException
					or Win32Exception
					or UnauthorizedAccessException
				) {
					await context.Diagnostics.ErrorAsync(
						$"cannot set name to '{name}': {ex.Message}",
						context.CancellationToken
					).ConfigureAwait( false );
					return 1;
				}
			}

			context.CancellationToken.ThrowIfCancellationRequested();
			var hostName = platform.GetHostName();
			await context.StandardOutput.WriteLineAsync(
				hostName.AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
			return 0;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) when (
			ex is IOException
				or SocketException
				or InvalidOperationException
		) {
			await context.Diagnostics.ErrorAsync(
				$"cannot determine hostname: {ex.Message}",
				context.CancellationToken
			).ConfigureAwait( false );
			return 1;
		}
	}

	private static async Task WriteHelpAsync(
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull( context );
		const string text = """
Usage: hostname [NAME]
  or:  hostname OPTION
Print or set the hostname of the current system.

      --help        display this help and exit
      --version     output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static OptionParser CreateParser() {
		return new OptionParser(
			new[] {
				new OptionDefinition(
					"help",
					longNames: new[] { "help" }
				),
				new OptionDefinition(
					"version",
					longNames: new[] { "version" }
				)
			},
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute
			}
		);
	}

	private static async Task<bool> WriteParseErrorsAsync(
		OptionParseResult result,
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull( result );
		ArgumentNullException.ThrowIfNull( context );
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
