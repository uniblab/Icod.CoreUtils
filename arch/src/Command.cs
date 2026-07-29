namespace Icod.CoreUtils.Arch;
using System.Runtime.InteropServices;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>
/// Implements GNU-compatible <c>arch</c> and prints the host machine architecture name.
/// </summary>
/// <remarks>
/// The architecture name is normalized to GNU spellings such as <c>x86_64</c> and <c>aarch64</c>.
/// </remarks>
public static class Command {
	private const string PROGRAM = "arch";
	private const string VERSION = "arch (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>arch</c> synchronously with optional standard-stream substitution.
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
	/// Executes <c>arch</c> asynchronously with optional injected standard streams.
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
	/// Executes <c>arch</c> asynchronously using a complete shared command context.
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
		var parser = CreateParser( new OptionDefinition( "help", longNames: new[] { "help" } ), new OptionDefinition( "version", longNames: new[] { "version" } ) );
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) )
				return 1;
			if ( result.HasOption( "help" ) ) {
				const string help = """
Usage: arch [OPTION]...
Print machine architecture.

      --help     display this help and exit
      --version  output version information and exit
""";
				await context.StandardOutput.WriteAsync(
					help.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return 0;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync( VERSION.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
				return 0;
			}
			if ( result.Operands.Count > 0 ) {
				await context.Diagnostics.ErrorAsync( $"extra operand '{result.Operands[ 0 ]}'", context.CancellationToken ).ConfigureAwait( false );
				return 1;
			}
			context.CancellationToken.ThrowIfCancellationRequested();
			await context.StandardOutput.WriteLineAsync( GetArchitecture().AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			return 0;
		} catch ( OperationCanceledException ) { return CommandExitCodes.Canceled; }
	}
	/// <summary>
	/// Gets the GNU spelling for the current process architecture.
	/// </summary>
	/// <returns>The normalized architecture name, or the BCL architecture name when no special spelling is required.</returns>
	internal static string GetArchitecture() => RuntimeInformation.OSArchitecture.ToString() switch {
		"X64" => "x86_64",
		"X86" => "i686",
		"Arm64" => "aarch64",
		"Arm" => "armv7l",
		"Armv6" => "armv6l",
		"Ppc64le" => "ppc64le",
		"S390x" => "s390x",
		"LoongArch64" => "loongarch64",
		"RiscV64" => "riscv64",
		"Wasm" => "wasm",
		var value => value.ToLowerInvariant()
	};

	private static OptionParser CreateParser( params OptionDefinition[] options ) => new(
		options,
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);
	private static async Task<bool> WriteParseErrorsAsync( OptionParseResult result, CommandContext context ) {
		if ( result.IsSuccess )
			return false;
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return true;
	}

}
