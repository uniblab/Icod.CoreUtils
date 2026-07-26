namespace Icod.CoreUtils.BaseName;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;

public static class Command {
	private const string PROGRAM = "basename";
	private const string VERSION = "basename (Icod.CoreUtils) 1.0";

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
			new OptionDefinition( "multiple", 'a', new[] { "multiple" } ),
			new OptionDefinition( "suffix", 's', new[] { "suffix" }, OptionValueArity.Required ),
			new OptionDefinition( "zero", 'z', new[] { "zero" } ),
			new OptionDefinition( "help", longNames: new[] { "help" } ),
			new OptionDefinition( "version", longNames: new[] { "version" } )
		);
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) return CommandExitCodes.Failure;
			if ( result.HasOption( "help" ) ) { await WriteHelpAsync( context ).ConfigureAwait( false ); return 0; }
			if ( result.HasOption( "version" ) ) { await context.StandardOutput.WriteLineAsync( VERSION.AsMemory(), context.CancellationToken ).ConfigureAwait( false ); return 0; }
			var multiple = result.HasOption( "multiple" ) || result.HasOption( "suffix" );
			var suffix = result.GetLastValue( "suffix" );
			if ( result.Operands.Count == 0 ) return await UsageErrorAsync( context, "missing operand" ).ConfigureAwait( false );
			if ( !multiple && result.Operands.Count > 2 ) return await UsageErrorAsync( context, $"extra operand '{result.Operands[2]}'" ).ConfigureAwait( false );
			var names = multiple ? result.Operands : result.Operands.Take( 1 ).ToArray();
			if ( !multiple && result.Operands.Count == 2 ) suffix = result.Operands[1];
			var separator = result.HasOption( "zero" ) ? "\0" : Environment.NewLine;
			foreach ( var name in names ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var value = GetBaseName( name );
				if ( !string.IsNullOrEmpty( suffix ) && suffix.Length < value.Length && value.EndsWith( suffix, StringComparison.Ordinal ) ) value = value[..^suffix.Length];
				await context.StandardOutput.WriteAsync( value.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
				await context.StandardOutput.WriteAsync( separator.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			}
			return 0;
		} catch ( OperationCanceledException ) { return CommandExitCodes.Canceled; }
		catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) { await context.Diagnostics.ErrorAsync( ex.Message ).ConfigureAwait( false ); return 1; }
	}
	internal static string GetBaseName( string name ) {
		if ( name.Length == 0 ) return string.Empty;
		var end = name.Length - 1;
		while ( end >= 0 && name[end] == '/' ) end--;
		if ( end < 0 ) return "/";
		var start = name.LastIndexOf( '/', end );
		return name.Substring( start + 1, end - start );
	}
	private static async Task<int> UsageErrorAsync( CommandContext c, string message ) { await c.Diagnostics.ErrorAsync( message, c.CancellationToken ).ConfigureAwait( false ); return 1; }
	private static Task WriteHelpAsync( CommandContext c ) => c.StandardOutput.WriteAsync("Usage: basename NAME [SUFFIX]\n  or:  basename OPTION... NAME...\nPrint NAME with any leading directory components removed.\n\n  -a, --multiple       support multiple arguments\n  -s, --suffix=SUFFIX  remove a trailing SUFFIX\n  -z, --zero           end each output with NUL, not newline\n      --help            display this help and exit\n      --version         output version information and exit\n".AsMemory(), c.CancellationToken);

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
