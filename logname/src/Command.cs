namespace Icod.CoreUtils.LogName;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;

/// <summary>Implements the <c>logname</c> command.</summary>
public static class Command {
	private const string ProgramName = "logname";
	private const string Version = "logname (Icod.CoreUtils) 1.0";

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
		new CommandContext( ProgramName, stdin ?? Console.In, stdout ?? Console.Out, stderr ?? Console.Error, cancellationToken: cancellationToken )
	);

	public static async Task<int> RunAsync( string[] args, CommandContext context, IIdentityProvider? provider = null ) {
		ArgumentNullException.ThrowIfNull( context );
		provider ??= SystemIdentityProvider.Instance;
		var parser = CreateParser(
			new OptionDefinition( "help", null, new[] { "help" } ),
			new OptionDefinition( "version", null, new[] { "version" } )
		);
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) return CommandExitCodes.Failure;
			if ( result.HasOption( "help" ) ) { await WriteHelpAsync( context ).ConfigureAwait( false ); return CommandExitCodes.Success; }
			if ( result.HasOption( "version" ) ) { await context.StandardOutput.WriteLineAsync( Version.AsMemory(), context.CancellationToken ).ConfigureAwait( false ); return CommandExitCodes.Success; }
			if ( 0 < result.Operands.Count ) {
				await context.Diagnostics.ErrorAsync( $"extra operand '{result.Operands[0]}'", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var name = await provider.GetLoginNameAsync( context.CancellationToken ).ConfigureAwait( false );
			if ( string.IsNullOrEmpty( name ) ) {
				await context.Diagnostics.ErrorAsync( "no login name", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			await context.StandardOutput.WriteLineAsync( name.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) {
			await context.Diagnostics.ErrorAsync( ex.Message, context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static Task WriteHelpAsync( CommandContext context ) => context.StandardOutput.WriteAsync(
		"Usage: logname [OPTION]...\nPrint the user's login name.\n\n      --help     display this help and exit\n      --version  output version information and exit\n".AsMemory(),
		context.CancellationToken
	);
	private static OptionParser CreateParser( params OptionDefinition[] options ) => new( options, new OptionParserSettings { AllowLongOptionAbbreviations = true, Ordering = OptionOrdering.Permute } );
	private static async Task<bool> WriteParseErrorsAsync( OptionParseResult result, CommandContext context ) {
		if ( result.IsSuccess ) return false;
		foreach ( var error in result.Errors ) await context.StandardError.WriteLineAsync( OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
		return true;
	}
}
