// Original behavior/reference: GNU Coreutils 9.11 chown.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Chown;

using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Ownership;
using Icod.CommandFramework.FileSystem.Traversal;
using Icod.CommandFramework.Platform;

/// <summary>Implements GNU <c>chown</c> through the shared ownership policy.</summary>
public static class Command {
	/// <summary>Runs <c>chown</c> synchronously against optional caller-owned streams.</summary>
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
	) {
		var context = new CommandContext(
			"chown",
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error
		);
		return RunAsync( args, context ).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>chown</c> asynchronously with system providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context, or <see langword="null"/> for console streams.</param>
	/// <returns>The command exit status.</returns>
	public static ValueTask<int> RunAsync( string[] args, CommandContext? context = null ) {
		return RunAsync(
			args,
			context ?? CommandContext.CreateConsole( "chown" ),
			SystemReadOnlyFileSystemProvider.Instance,
			SystemFileSystemMetadataProvider.Instance,
			SystemFileSystemMutationProvider.Instance,
			SystemIdentityProvider.Instance
		);
	}

	/// <summary>Runs <c>chown</c> asynchronously with injected providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <param name="readOnlyProvider">The E1 traversal provider.</param>
	/// <param name="metadataProvider">The E3 metadata provider.</param>
	/// <param name="mutationProvider">The E4 mutation provider.</param>
	/// <param name="identityProvider">The user and group identity provider.</param>
	/// <returns>The command exit status.</returns>
	public static ValueTask<int> RunAsync(
		string[] args,
		CommandContext context,
		IReadOnlyFileSystemProvider readOnlyProvider,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		IIdentityProvider identityProvider
	) => OwnershipCommandRunner.RunAsync(
		OwnershipCommandKind.Chown,
		args,
		context,
		readOnlyProvider,
		metadataProvider,
		mutationProvider,
		identityProvider
	);

	/// <summary>Writes GNU-compatible <c>chown</c> usage text.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when usage is written.</returns>
	public static ValueTask WriteUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken = default
	) => OwnershipCommandRunner.WriteUsageAsync(
		OwnershipCommandKind.Chown,
		output,
		cancellationToken
	);
}
