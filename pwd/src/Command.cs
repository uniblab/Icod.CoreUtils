namespace Icod.CoreUtils.Pwd;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;

public static class Command {
	private const string PROGRAM = "pwd";
	private const string VERSION = "pwd (Icod.CoreUtils) 1.0";

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) =>
		RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();
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
		if ( string.IsNullOrEmpty( path ) || !Path.IsPathRooted( path ) ) return false;
		if ( path.Split( new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries ).Any( x => x is "." or ".." ) ) return false;
		try { return string.Equals( ResolvePhysicalPath( path ), physical, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal ); } catch { return false; }
	}
	internal static string ResolvePhysicalPath( string path ) {
		var full = Path.GetFullPath( path );
		var root = Path.GetPathRoot( full ) ?? string.Empty;
		var current = root;
		foreach ( var component in full[root.Length..].Split( new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries ) ) {
			var candidate = Path.Combine( current, component );
			var target = Directory.ResolveLinkTarget( candidate, true );
			current = target?.FullName ?? candidate;
		}
		return Path.TrimEndingDirectorySeparator( Path.GetFullPath( current ) );
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
