namespace Icod.CoreUtils.Groups;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;

/// <summary>Implements the <c>groups</c> command.</summary>
public static class Command {
	private const string ProgramName = "groups";
	private const string Version = "groups (Icod.CoreUtils) 1.0";

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

			if ( 0 == result.Operands.Count ) {
				var current = await provider.GetCurrentAsync( context.CancellationToken ).ConfigureAwait( false );
				await WriteGroupsAsync( null, current.RealGroup, current.Groups, context ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var exitCode = CommandExitCodes.Success;
			foreach ( var userName in result.Operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var user = await provider.FindUserAsync( userName, context.CancellationToken ).ConfigureAwait( false );
				if ( null == user ) {
					await context.Diagnostics.ErrorAsync( $"'{userName}': no such user", context.CancellationToken ).ConfigureAwait( false );
					exitCode = CommandExitCodes.Failure;
					continue;
				}
				await WriteGroupsAsync( user.Name, user.PrimaryGroup, user.Groups, context ).ConfigureAwait( false );
			}
			return exitCode;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) {
			await context.Diagnostics.ErrorAsync( ex.Message, context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static async Task WriteGroupsAsync(
		string? userName,
		GroupIdentity primary,
		IReadOnlyList<GroupIdentity> groups,
		CommandContext context
	) {
		var names = groups
			.Prepend( primary )
			.DistinctBy( group => group.Id )
			.Select( group => group.Name )
			.ToArray();
		var prefix = null == userName ? string.Empty : $"{userName} : ";
		await context.StandardOutput.WriteLineAsync( (prefix + string.Join( ' ', names )).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
	}

	private static Task WriteHelpAsync( CommandContext context ) => context.StandardOutput.WriteAsync(
		"Usage: groups [OPTION]... [USERNAME]...\nPrint group memberships for each USERNAME or, if no USERNAME is specified, for the current process.\n\n      --help     display this help and exit\n      --version  output version information and exit\n".AsMemory(),
		context.CancellationToken
	);
	private static OptionParser CreateParser( params OptionDefinition[] options ) => new( options, new OptionParserSettings { AllowLongOptionAbbreviations = true, Ordering = OptionOrdering.Permute } );
	private static async Task<bool> WriteParseErrorsAsync( OptionParseResult result, CommandContext context ) {
		if ( result.IsSuccess ) return false;
		foreach ( var error in result.Errors ) await context.StandardError.WriteLineAsync( OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
		return true;
	}
}
