namespace Icod.CoreUtils.DirName;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;

public static class Command {
	private const string PROGRAM = "dirname";
	private const string VERSION = "dirname (Icod.CoreUtils) 1.0";

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
			new OptionDefinition( "zero", 'z', new[] { "zero" } ),
			new OptionDefinition( "help", longNames: new[] { "help" } ),
			new OptionDefinition( "version", longNames: new[] { "version" } )
		);
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) return 1;
			if ( result.HasOption( "help" ) ) { await WriteHelpAsync( context ).ConfigureAwait( false ); return 0; }
			if ( result.HasOption( "version" ) ) { await context.StandardOutput.WriteLineAsync( VERSION.AsMemory(), context.CancellationToken ).ConfigureAwait( false ); return 0; }
			if ( result.Operands.Count == 0 ) { await context.Diagnostics.ErrorAsync( "missing operand", context.CancellationToken ).ConfigureAwait( false ); return 1; }
			var separator = result.HasOption( "zero" ) ? "\0" : Environment.NewLine;
			foreach ( var operand in result.Operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				await context.StandardOutput.WriteAsync( GetDirName( operand ).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
				await context.StandardOutput.WriteAsync( separator.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			}
			return 0;
		} catch ( OperationCanceledException ) { return CommandExitCodes.Canceled; }
	}
	internal static string GetDirName( string name ) {
		if ( name.Length == 0 ) return ".";
		var end = name.Length - 1;
		while ( end >= 0 && name[end] == '/' ) end--;
		if ( end < 0 ) return "/";
		var slash = name.LastIndexOf( '/', end );
		if ( slash < 0 ) return ".";
		var directoryEnd = slash;
		while ( directoryEnd > 0 && name[directoryEnd - 1] == '/' ) directoryEnd--;
		return directoryEnd == 0 ? "/" : name[..directoryEnd];
	}
	private static Task WriteHelpAsync( CommandContext c ) => c.StandardOutput.WriteAsync("Usage: dirname [OPTION] NAME...\nOutput each NAME with its last non-slash component and trailing slashes removed.\n\n  -z, --zero     end each output with NUL, not newline\n      --help     display this help and exit\n      --version  output version information and exit\n".AsMemory(), c.CancellationToken);

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
