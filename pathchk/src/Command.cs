namespace Icod.CoreUtils.PathChk;
using System.Text;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;

/// <summary>
/// Implements GNU-compatible <c>pathchk</c> and validates pathnames against host or portable filename constraints.
/// </summary>
/// <remarks>
/// Portable and host limits are validated without creating or modifying filesystem objects.
/// </remarks>
public static class Command {
	private const string PROGRAM = "pathchk";
	private const string VERSION = "pathchk (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>pathchk</c> synchronously with optional standard-stream substitution.
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
	/// Executes <c>pathchk</c> asynchronously with optional injected standard streams.
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
	/// Executes <c>pathchk</c> asynchronously using a complete shared command context.
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
		var parser = new OptionParser(new[] {
			new OptionDefinition( "posix", 'p', new[] { "posix" } ),
			new OptionDefinition( "leading", 'P', new[] { "leading-hyphen" } ),
			new OptionDefinition( "portability", longNames: new[] { "portability" } ),
			new OptionDefinition( "help", longNames: new[] { "help" } ),
			new OptionDefinition( "version", longNames: new[] { "version" } )
		}, new OptionParserSettings { AllowLongOptionAbbreviations = true, Ordering = OptionOrdering.RequireOrder });
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) return 1;
			if ( result.HasOption( "help" ) ) { await WriteHelpAsync( context ).ConfigureAwait( false ); return 0; }
			if ( result.HasOption( "version" ) ) { await context.StandardOutput.WriteLineAsync( VERSION.AsMemory(), context.CancellationToken ).ConfigureAwait( false ); return 0; }
			if ( result.Operands.Count == 0 ) { await context.Diagnostics.ErrorAsync( "missing operand", context.CancellationToken ).ConfigureAwait( false ); return 1; }
			var portable = result.HasOption( "posix" ) || result.HasOption( "portability" );
			var leading = result.HasOption( "leading" ) || result.HasOption( "portability" );
			var failed = false;
			foreach ( var path in result.Operands ) failed |= !await ValidateAsync( path, portable, leading, context ).ConfigureAwait( false );
			return failed ? 1 : 0;
		} catch ( OperationCanceledException ) { return CommandExitCodes.Canceled; }
	}
	private static async Task<bool> ValidateAsync( string path, bool portable, bool leading, CommandContext context ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		var valid = true;
		async Task Bad( string text ) { valid = false; await context.Diagnostics.ErrorAsync( text, context.CancellationToken ).ConfigureAwait( false ); }
		if ( path.Length == 0 ) await Bad( "empty file name" ).ConfigureAwait( false );
		var components = path.Split( '/', StringSplitOptions.RemoveEmptyEntries );
		if ( leading ) foreach ( var component in components ) if ( component.Length > 0 && component[0] == '-' ) await Bad( $"leading '-' in a component of file name '{path}'" ).ConfigureAwait( false );
		if ( portable ) {
			if ( Encoding.UTF8.GetByteCount( path ) > 255 ) await Bad( $"limit 255 exceeded by length {Encoding.UTF8.GetByteCount(path)} of file name '{path}'" ).ConfigureAwait( false );
			foreach ( var component in components ) if ( Encoding.UTF8.GetByteCount( component ) > 14 ) await Bad( $"limit 14 exceeded by length {Encoding.UTF8.GetByteCount(component)} of file name component '{component}'" ).ConfigureAwait( false );
			foreach ( var ch in path ) if ( !IsPortable( ch ) ) { await Bad( $"nonportable character '{ch}' in file name '{path}'" ).ConfigureAwait( false ); break; }
		} else {
			var pathLimit = OperatingSystem.IsWindows() ? 32767 : 4095;
			if ( Encoding.UTF8.GetByteCount( path ) > pathLimit ) await Bad( $"file name '{path}' is too long" ).ConfigureAwait( false );
			foreach ( var component in components ) if ( Encoding.UTF8.GetByteCount( component ) > 255 ) await Bad( $"file name component '{component}' is too long" ).ConfigureAwait( false );
			if ( path.IndexOf( '\0' ) >= 0 ) await Bad( "file name contains a null character" ).ConfigureAwait( false );
			try { _ = System.IO.Path.GetFullPath( path.Length == 0 ? "." : path ); } catch ( Exception ex ) when ( ex is ArgumentException or NotSupportedException or PathTooLongException ) { await Bad( ex.Message ).ConfigureAwait( false ); }
		}
		return valid;
	}
	private static bool IsPortable( char ch ) => ch == '/' || ch == '.' || ch == '_' || ch == '-' || ch is >= '0' and <= '9' || ch is >= 'A' and <= 'Z' || ch is >= 'a' and <= 'z';
	private static async Task WriteHelpAsync( CommandContext context ) {
		const string text = """
Usage: pathchk [OPTION]... NAME...
Diagnose invalid or nonportable file names.

  -p                 check for most POSIX systems
  -P                 check for empty names and leading '-' components
      --portability  check both -p and -P
      --help         display this help and exit
      --version      output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}
	private static async Task<bool> WriteParseErrorsAsync( OptionParseResult result, CommandContext context ) {
		if ( result.IsSuccess ) return false;
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return true;
	}

}
