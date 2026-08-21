// Original behavior/reference: GNU Coreutils 9.11 link.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Link;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CommandFramework.FileSystem.Traversal;

/// <summary>Implements GNU <c>link</c>, the strict two-operand hard-link command.</summary>
public static class Command {
	private static readonly OptionParser Parser = new(
		new[] {
			new OptionDefinition( "help", longNames: new[] { "help" }, allowMultiple: false ),
			new OptionDefinition( "version", longNames: new[] { "version" }, allowMultiple: false )
		},
		new OptionParserSettings { AllowLongOptionAbbreviations = true, Ordering = OptionOrdering.Permute }
	);

	/// <summary>Runs <c>link</c> synchronously against optional caller-owned streams.</summary>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		var context = new CommandContext( "link", stdin ?? Console.In, stdout ?? Console.Out, stderr ?? Console.Error );
		return RunAsync( args, context ).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>link</c> asynchronously with the system mutation provider.</summary>
	public static ValueTask<int> RunAsync( string[] args, CommandContext? context = null ) {
		return RunAsync( args, context ?? CommandContext.CreateConsole( "link" ), SystemFileSystemMutationProvider.Instance );
	}

	/// <summary>Runs <c>link</c> asynchronously with an injected mutation provider.</summary>
	public static async ValueTask<int> RunAsync(
		string[] args,
		CommandContext context,
		IFileSystemMutationProvider mutationProvider
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( mutationProvider );
		args ??= Array.Empty<string>();
		try {
			var parsed = Parser.Parse( args );
			if ( !parsed.IsSuccess ) {
				foreach ( var error in parsed.Errors )
					await context.StandardError.WriteLineAsync( OptionDiagnosticFormatter.Format( context.ProgramName, error ) ).ConfigureAwait( false );
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( parsed.HasOption( "help" ) ) {
				await WriteUsageAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync( "link (Icod.CoreUtils) 0.1" ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.Operands.Count < 2 ) {
				await context.StandardError.WriteLineAsync( parsed.Operands.Count == 0 ? "link: missing operand" : string.Concat( "link: missing operand after ", Quote( parsed.Operands[ 0 ] ) ) ).ConfigureAwait( false );
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( parsed.Operands.Count > 2 ) {
				await context.StandardError.WriteLineAsync( string.Concat( "link: extra operand ", Quote( parsed.Operands[ 2 ] ) ) ).ConfigureAwait( false );
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var result = await mutationProvider.CreateHardLinkAsync(
				parsed.Operands[ 1 ],
				parsed.Operands[ 0 ],
				PathDereferenceMode.FollowEligiblePathIndirection,
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			if ( result.Succeeded ) return CommandExitCodes.Success;
			await context.StandardError.WriteLineAsync(
				string.Concat( "link: cannot create link ", Quote( parsed.Operands[ 1 ] ), " to ", Quote( parsed.Operands[ 0 ] ), ": ", Describe( result ) )
			).ConfigureAwait( false );
			return result.ErrorCode == FileSystemMutationErrorCode.Cancelled ? CommandExitCodes.Canceled : CommandExitCodes.Failure;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		}
	}

	/// <summary>Writes command usage.</summary>
	public static async ValueTask WriteUsageAsync( TextWriter output, CancellationToken cancellationToken = default ) {
		ArgumentNullException.ThrowIfNull( output );
		foreach ( var line in new[] { "Usage: link FILE1 FILE2", "  or:  link OPTION", "Call the link function to create a link named FILE2 to an existing FILE1.", string.Empty, "      --help     display this help and exit", "      --version  output version information and exit" } ) {
			cancellationToken.ThrowIfCancellationRequested();
			await output.WriteLineAsync( line ).ConfigureAwait( false );
		}
	}

	private static string Describe( FileSystemMutationResult result ) => result.ErrorCode switch {
		FileSystemMutationErrorCode.AlreadyExists => "File exists",
		FileSystemMutationErrorCode.NotFound or FileSystemMutationErrorCode.ParentNotFound => "No such file or directory",
		FileSystemMutationErrorCode.CrossDevice => "Invalid cross-device link",
		FileSystemMutationErrorCode.WrongObjectKind => "Invalid argument",
		FileSystemMutationErrorCode.AccessDenied => "Permission denied",
		FileSystemMutationErrorCode.PrivilegeRequired => "Operation not permitted",
		FileSystemMutationErrorCode.Unsupported => result.Message ?? "Operation not supported",
		_ => result.Message ?? "Input/output error"
	};
	private static string Quote( string value ) => string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );
	private static async ValueTask WriteTryHelpAsync( CommandContext context ) => await context.StandardError.WriteLineAsync( "Try 'link --help' for more information." ).ConfigureAwait( false );
}
