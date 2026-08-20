namespace Icod.CoreUtils.Pwd;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>
/// Implements GNU-compatible <c>pwd</c> and prints the logical or physical current working directory.
/// </summary>
/// <remarks>
/// Logical and physical modes are resolved without changing the process working directory.
/// </remarks>
public static class Command {
	private const string PROGRAM = "pwd";
	private const string VERSION = "pwd (Icod.CoreUtils) 1.0";

	private static readonly char[] theSlashes;

	static Command() {
		theSlashes = ['/', '\\'];
	}

	/// <summary>
	/// Executes <c>pwd</c> synchronously with optional standard-stream substitution.
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
	/// Executes <c>pwd</c> asynchronously with optional injected standard streams.
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
	/// Executes <c>pwd</c> asynchronously using a complete shared command context.
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
			new OptionDefinition( "logical", 'L', new[] { "logical" } ),
			new OptionDefinition( "physical", 'P', new[] { "physical" } ),
			new OptionDefinition( "help", longNames: new[] { "help" } ),
			new OptionDefinition( "version", longNames: new[] { "version" } )
		);
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) return 1;
			if ( result.HasOption( "help" ) ) { await WriteHelpAsync( context ).ConfigureAwait( false ); return 0; }
			if ( result.HasOption( "version" ) ) { await context.StandardOutput.WriteLineAsync( VERSION.AsMemory(), context.CancellationToken ).ConfigureAwait( false ); return 0; }
			var logical = Environment.GetEnvironmentVariable( "POSIXLY_CORRECT" ) is not null;
			foreach ( var option in result.Options ) { if ( option.Definition.Key == "logical" ) logical = true; else if ( option.Definition.Key == "physical" ) logical = false; }
			var physical = ResolvePhysicalPath( Directory.GetCurrentDirectory() );
			var output = physical;
			if ( logical ) {
				var pwd = Environment.GetEnvironmentVariable( "PWD" );
				if ( IsValidLogicalPath( pwd, physical ) ) output = pwd!;
			}
			await context.StandardOutput.WriteLineAsync( output.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			return 0;
		} catch ( OperationCanceledException ) { return CommandExitCodes.Canceled; }
		catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) { await context.Diagnostics.ErrorAsync( ex.Message, context.CancellationToken ).ConfigureAwait( false ); return 1; }
	}
	private static bool IsValidLogicalPath( string? path, string physical ) {
		if ( string.IsNullOrEmpty( path ) || !System.IO.Path.IsPathRooted( path ) ) return false;
		if ( path.Split( theSlashes, StringSplitOptions.RemoveEmptyEntries ).Any( x => x is "." or ".." ) ) return false;
		try { return string.Equals( ResolvePhysicalPath( path ), physical, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal ); } catch { return false; }
	}
	/// <summary>
	/// Resolves symbolic links component by component to produce a physical absolute path.
	/// </summary>
	/// <param name="path">The absolute or relative pathname to resolve.</param>
	/// <returns>The normalized physical pathname.</returns>
	internal static string ResolvePhysicalPath( string path ) {
		var full = System.IO.Path.GetFullPath( path );
		var root = System.IO.Path.GetPathRoot( full ) ?? string.Empty;
		var current = root;
		foreach ( var component in full[root.Length..].Split( new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries ) ) {
			var candidate = System.IO.Path.Combine( current, component );
			var target = Directory.ResolveLinkTarget( candidate, true );
			current = target?.FullName ?? candidate;
		}
		return System.IO.Path.TrimEndingDirectorySeparator( System.IO.Path.GetFullPath( current ) );
	}
	private static async Task WriteHelpAsync( CommandContext context ) {
		const string text = """
Usage: pwd [OPTION]...
Print the full filename of the current working directory.

  -L, --logical   use PWD from the environment, even if it contains symlinks
  -P, --physical  avoid all symlinks
      --help      display this help and exit
      --version   output version information and exit
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
