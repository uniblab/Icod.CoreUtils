namespace Icod.CoreUtils.PrintEnv;
using System.Collections;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;

public static class Command {
	private const string PROGRAM = "printenv";
	private const string VERSION = "printenv (Icod.CoreUtils) 1.0";

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
			new OptionDefinition( "null", '0', new[] { "null" } ),
			new OptionDefinition( "help", longNames: new[] { "help" } ),
			new OptionDefinition( "version", longNames: new[] { "version" } )
		);
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) return 1;
			if ( result.HasOption( "help" ) ) { await WriteHelpAsync( context ).ConfigureAwait( false ); return 0; }
			if ( result.HasOption( "version" ) ) { await context.StandardOutput.WriteLineAsync( VERSION.AsMemory(), context.CancellationToken ).ConfigureAwait( false ); return 0; }
			var nullTerminated = result.HasOption( "null" );
			var failed = false;
			if ( result.Operands.Count == 0 ) {
				foreach ( DictionaryEntry entry in Environment.GetEnvironmentVariables() ) await WriteAsync( System.String.Concat( entry.Key, "=", entry.Value ), nullTerminated, context ).ConfigureAwait( false );
			} else {
				foreach ( var name in result.Operands ) {
					context.CancellationToken.ThrowIfCancellationRequested();
					var value = Environment.GetEnvironmentVariable( name );
					if ( value is null ) failed = true; else await WriteAsync( value, nullTerminated, context ).ConfigureAwait( false );
				}
			}
			return failed ? 1 : 0;
		} catch ( OperationCanceledException ) { return CommandExitCodes.Canceled; }
	}
	private static async Task WriteAsync( string value, bool nullTerminated, CommandContext context ) {
		if ( nullTerminated ) {
			await context.StandardOutput.WriteAsync( value.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			await context.StandardOutput.WriteAsync( "\0".AsMemory(), context.CancellationToken ).ConfigureAwait( false );
		} else {
			await context.StandardOutput.WriteLineAsync( value.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
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
