// Original behavior/reference: GNU Coreutils 9.11 install.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Install;

using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Mutation;
using Icod.CommandFramework.FileSystem.TransactionalReplacement;
using Icod.CommandFramework.Platform;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>Implements GNU <c>install</c> through the shared filesystem contracts.</summary>
public static class Command {
	/// <summary>Runs <c>install</c> synchronously against optional caller-owned streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <returns>The command exit status.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) => RunAsync(
		args,
		stdin ?? Console.In,
		stdout ?? Console.Out,
		stderr ?? Console.Error
	).AsTask().GetAwaiter().GetResult();

	/// <summary>Runs <c>install</c> asynchronously with system providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context, or <see langword="null"/> for the console context.</param>
	/// <returns>The command exit status.</returns>
	public static ValueTask<int> RunAsync(
		string[] args,
		CommandContext? context = null
	) => RunAsync(
		args,
		context ?? CommandContext.CreateConsole( "install" ),
		SystemFileSystemMetadataProvider.Instance,
		SystemFileSystemMutationProvider.Instance,
		SystemIdentityProvider.Instance,
		SystemTransactionalReplacementFileSystem.Instance,
		SystemInstallSecurityContextProvider.Instance
	);

	/// <summary>Runs <c>install</c> asynchronously against caller-owned streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The command exit status.</returns>
	public static ValueTask<int> RunAsync(
		string[] args,
		TextReader stdin,
		TextWriter stdout,
		TextWriter stderr,
		CancellationToken cancellationToken = default
	) => RunAsync(
		args,
		new CommandContext(
			"install",
			stdin,
			stdout,
			stderr,
			cancellationToken: cancellationToken
		),
		SystemFileSystemMetadataProvider.Instance,
		SystemFileSystemMutationProvider.Instance,
		SystemIdentityProvider.Instance,
		SystemTransactionalReplacementFileSystem.Instance,
		SystemInstallSecurityContextProvider.Instance
	);

	/// <summary>Runs <c>install</c> asynchronously over injected providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <param name="metadataProvider">The filesystem metadata provider.</param>
	/// <param name="mutationProvider">The filesystem mutation provider.</param>
	/// <param name="identityProvider">The user and group identity provider.</param>
	/// <param name="transactionFileSystem">The transactional-replacement filesystem.</param>
	/// <param name="securityContextProvider">The SELinux security-context provider.</param>
	/// <returns>The command exit status.</returns>
	internal static async ValueTask<int> RunAsync(
		string[] args,
		CommandContext context,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		IIdentityProvider identityProvider,
		ITransactionalReplacementFileSystem transactionFileSystem,
		IInstallSecurityContextProvider securityContextProvider
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		var output = context.StandardOutput;
		var error = context.StandardError;
		var cancellationToken = context.CancellationToken;
		if ( !InstallArgumentParser.TryParse( args, out var options, out var parseError ) ) {
			await error.WriteLineAsync( string.Concat( "install: ", parseError ) ).ConfigureAwait( false );
			await error.WriteLineAsync( "Try 'install --help' for more information." ).ConfigureAwait( false );
			return 1;
		}
		if ( options.ShowHelp ) {
			await WriteUsageAsync( output, cancellationToken ).ConfigureAwait( false );
			return 0;
		}
		if ( options.ShowVersion ) {
			await output.WriteLineAsync( "install (Icod.CoreUtils) 10.0" ).ConfigureAwait( false );
			return 0;
		}
		try {
			await ExpandSourceOperandsAsync(
				options,
				cancellationToken
			).ConfigureAwait( false );
			var engine = new InstallEngine(
				metadataProvider,
				mutationProvider,
				identityProvider,
				transactionFileSystem,
				securityContextProvider,
				output,
				error
			);
			return await engine.ExecuteAsync( options, cancellationToken ).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			await error.WriteLineAsync( "install: operation cancelled" ).ConfigureAwait( false );
			return 1;
		} catch ( Exception exception ) when (
			exception is IOException
				or UnauthorizedAccessException
				or ArgumentException
				or NotSupportedException
				or System.Security.SecurityException
		) {
			await error.WriteLineAsync( string.Concat( "install: ", exception.Message ) ).ConfigureAwait( false );
			return 1;
		}
	}

	private static async ValueTask ExpandSourceOperandsAsync(
		InstallOptions options,
		CancellationToken cancellationToken
	) {
		if ( options.DirectoryMode ) {
			return;
		}

		var sourceCount = null == options.TargetDirectory
			? Math.Max( 0, options.Operands.Count - 1 )
			: options.Operands.Count
		;
		if ( 0 == sourceCount ) {
			return;
		}

		var destination = null == options.TargetDirectory
			? options.Operands[^1]
			: null
		;
		var expansion = await PathnameOperandExpander.ExpandAsync(
			options.Operands.Take( sourceCount ),
			cancellationToken: cancellationToken
		).ConfigureAwait( false );

		options.Operands.Clear();
		foreach ( var source in expansion.Operands ) {
			options.Operands.Add( source );
		}
		if ( null != destination ) {
			options.Operands.Add( destination );
		}
	}

	/// <summary>Writes GNU-compatible usage text.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the write.</returns>
	public static async ValueTask WriteUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();
		await output.WriteLineAsync( "Usage: install [OPTION]... [-T] SOURCE DEST" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  or:  install [OPTION]... SOURCE... DIRECTORY" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  or:  install [OPTION]... -t DIRECTORY SOURCE..." ).ConfigureAwait( false );
		await output.WriteLineAsync( "  or:  install [OPTION]... -d DIRECTORY..." ).ConfigureAwait( false );
		await output.WriteLineAsync( "Copy SOURCE to DEST, or multiple SOURCE(s) to DIRECTORY, while setting attributes." ).ConfigureAwait( false );
		await output.WriteLineAsync().ConfigureAwait( false );
		await output.WriteLineAsync( "  -b, --backup[=CONTROL]       make a backup of each existing destination file" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -C, --compare                compare content and attributes; do not modify matching files" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -c                           ignored for historical compatibility" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -d, --directory              treat all arguments as directory names" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -D                           create all leading components of DEST" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -g, --group=GROUP            set group ownership" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -m, --mode=MODE              set mode (default u=rwx,go=rx,a-s)" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -o, --owner=OWNER            set owner" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -p, --preserve-timestamps    apply SOURCE access/modification times" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -s, --strip                  strip symbol tables" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --strip-program=PROGRAM  program used to strip binaries" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -S, --suffix=SUFFIX          override the usual backup suffix" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -t, --target-directory=DIR   copy all SOURCE arguments into DIR" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -T, --no-target-directory    treat DEST as a normal file" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -v, --verbose                print each created directory or installed file" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --debug                  explain actions and imply --verbose" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --preserve-context       preserve SOURCE SELinux context" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -Z, --context[=CTX]          use destination-default or explicit SELinux context" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --help                   display this help and exit" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --version                output version information and exit" ).ConfigureAwait( false );
	}
}
