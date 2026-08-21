using Icod.CommandFramework.FileSystem.RecursiveMutation;

namespace Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;

/// <summary>Provides E2/E5-backed containment and pathname-escape validation for E6.</summary>
public sealed class TransactionalReplacementPathSafety {
	private readonly RecursivePathSafety recursivePathSafety;

	/// <summary>Initializes physical containment checks through the system E2 resolver.</summary>
	public TransactionalReplacementPathSafety() {
		recursivePathSafety = new RecursivePathSafety();
	}

	/// <summary>Initializes deterministic lexical checks for synthetic providers.</summary>
	/// <param name="comparison">The host-compatible ordinal path comparison.</param>
	public TransactionalReplacementPathSafety( StringComparison comparison ) {
		recursivePathSafety = new RecursivePathSafety( comparison );
	}

	/// <summary>Validates that a destination is the containment root or one of its descendants.</summary>
	/// <param name="containmentRootPath">The allowed root.</param>
	/// <param name="candidatePath">The destination, backup, or output pathname.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The normalized safe candidate pathname.</returns>
	/// <exception cref="InvalidOperationException">Physical resolution fails or the candidate escapes the root.</exception>
	public async ValueTask<string> RequireContainedAsync(
		string containmentRootPath,
		string candidatePath,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( containmentRootPath );
		ArgumentException.ThrowIfNullOrWhiteSpace( candidatePath );
		var result = await recursivePathSafety.EvaluateAsync(
			containmentRootPath,
			candidatePath,
			cancellationToken
		).ConfigureAwait( false );
		if ( !result.Succeeded ) {
			throw new InvalidOperationException(
				result.Message ?? "Path containment resolution failed.",
				result.Exception
			);
		}
		if ( result.Relationship is not RecursivePathRelationship.Same
			and not RecursivePathRelationship.DestinationInsideSource ) {
			throw new InvalidOperationException(
				string.Concat( "pathname escapes transaction root: ", candidatePath )
			);
		}
		return result.DestinationPath!;
	}
}
