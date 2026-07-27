namespace Icod.CoreUtils.Users;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;

/// <summary>Implements the <c>users</c> command.</summary>
public static class Command {
	private const string ProgramName = "users";
	private const string Version = "users (Icod.CoreUtils) 1.0";

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

	public static async Task<int> RunAsync( string[] args, CommandContext context, ILoginRecordProvider? provider = null ) {
		ArgumentNullException.ThrowIfNull( context );
		provider ??= SystemLoginRecordProvider.Instance;
		var parser = CreateParser(
			new OptionDefinition( "help", null, new[] { "help" } ),
			new OptionDefinition( "version", null, new[] { "version" } )
		);
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) return CommandExitCodes.Failure;
			if ( result.HasOption( "help" ) ) { await WriteHelpAsync( context ).ConfigureAwait( false ); return CommandExitCodes.Success; }
			if ( result.HasOption( "version" ) ) { await context.StandardOutput.WriteLineAsync( Version.AsMemory(), context.CancellationToken ).ConfigureAwait( false ); return CommandExitCodes.Success; }
			if ( 1 < result.Operands.Count ) {
				await context.Diagnostics.ErrorAsync( $"extra operand '{result.Operands[1]}'", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( !provider.IsSupported ) {
				await context.Diagnostics.ErrorAsync( "login records are not supported on this platform", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var names = new List<string>();
			await foreach ( var record in provider.ReadAsync( result.Operands.SingleOrDefault(), context.CancellationToken ).ConfigureAwait( false ) ) {
				if ( LoginRecordType.UserProcess == record.Type && !string.IsNullOrEmpty( record.User ) ) names.Add( record.User );
			}
			names.Sort( StringComparer.Ordinal );
			await context.StandardOutput.WriteLineAsync( string.Join( ' ', names ).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) {
			await context.Diagnostics.ErrorAsync( ex.Message, context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string text = """
Usage: users [OPTION]... [FILE]
Output who is currently logged in according to FILE. If FILE is not specified, use the system login database.

      --help     display this help and exit
      --version  output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}
	private static OptionParser CreateParser( params OptionDefinition[] options ) => new( options, new OptionParserSettings { AllowLongOptionAbbreviations = true, Ordering = OptionOrdering.Permute } );
	private static async Task<bool> WriteParseErrorsAsync( OptionParseResult result, CommandContext context ) {
		if ( result.IsSuccess ) return false;
		foreach ( var error in result.Errors ) await context.StandardError.WriteLineAsync( OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
		return true;
	}
}
