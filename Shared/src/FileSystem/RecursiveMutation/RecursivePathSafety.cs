using Path = global::System.IO.Path;
using CanonicalPathResolutionOptions = Icod.Path.CanonicalPathResolutionOptions;
using CanonicalPathResolver = Icod.Path.CanonicalPathResolver;
using MissingPathComponentPolicy = Icod.Path.MissingPathComponentPolicy;

namespace Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;

/// <summary>Identifies the relationship between normalized source and destination paths.</summary>
public enum RecursivePathRelationship {
	/// <summary>The paths are disjoint.</summary>
	Disjoint = 0,
	/// <summary>The paths name the same normalized location.</summary>
	Same = 1,
	/// <summary>The destination is contained by the source.</summary>
	DestinationInsideSource = 2,
	/// <summary>The source is contained by the destination.</summary>
	SourceInsideDestination = 3
}

/// <summary>Describes physical preserve-root and containment preflight.</summary>
/// <param name="Succeeded">Whether source and destination resolution succeeded.</param>
/// <param name="IsSourceRoot">Whether the resolved source is a filesystem root.</param>
/// <param name="Relationship">The component-aware source and destination relationship.</param>
/// <param name="SourcePath">The resolved or normalized source pathname.</param>
/// <param name="DestinationPath">The resolved or normalized destination pathname.</param>
/// <param name="Message">An optional controlled resolution failure message.</param>
/// <param name="Exception">An optional underlying resolution exception.</param>
public sealed record RecursivePathSafetyResult(
	bool Succeeded,
	bool IsSourceRoot,
	RecursivePathRelationship Relationship,
	string? SourcePath,
	string? DestinationPath,
	string? Message = null,
	Exception? Exception = null
);

/// <summary>Provides E2-backed preserve-root and source/destination containment preflight.</summary>
public sealed class RecursivePathSafety {
	private readonly StringComparison _comparison;
	private readonly CanonicalPathResolver? _resolver;

	/// <summary>Initializes physical path safety using the system E2 canonical-path resolver.</summary>
	public RecursivePathSafety() {
		_resolver = new CanonicalPathResolver();
		_comparison = _resolver.Semantics.PathComparison;
	}

	/// <summary>Initializes deterministic lexical path safety with an explicit comparison policy.</summary>
	/// <param name="comparison">The host-compatible ordinal path comparison.</param>
	/// <remarks>This constructor is intended for synthetic providers whose paths do not exist on the host.</remarks>
	public RecursivePathSafety( StringComparison comparison ) {
		if ( comparison is not StringComparison.Ordinal and not StringComparison.OrdinalIgnoreCase ) {
			throw new ArgumentOutOfRangeException( nameof( comparison ) );
		}
		_comparison = comparison;
	}

	/// <summary>Gets whether the supplied path is a filesystem root after full-path normalization.</summary>
	/// <param name="path">The pathname to classify.</param>
	/// <returns><see langword="true"/> when the normalized pathname is its own filesystem root.</returns>
	public bool IsFileSystemRoot( string path ) {
		var fullPath = Normalize( path );
		var root = Path.GetPathRoot( fullPath );
		return !string.IsNullOrEmpty( root )
			&& string.Equals( TrimEndingSeparators( fullPath ), TrimEndingSeparators( root ), _comparison );
	}

	/// <summary>Classifies lexical source and destination containment.</summary>
	/// <param name="sourcePath">The source pathname.</param>
	/// <param name="destinationPath">The destination pathname.</param>
	/// <returns>The component-aware relationship after lexical normalization.</returns>
	public RecursivePathRelationship Classify( string sourcePath, string destinationPath ) {
		var source = Normalize( sourcePath );
		var destination = Normalize( destinationPath );
		return ClassifyNormalized( source, destination );
	}

	/// <summary>
	/// Resolves existing source components and the destination's existing prefix through E2 before evaluating policy.
	/// </summary>
	/// <param name="sourcePath">The source pathname, whose complete physical target must exist.</param>
	/// <param name="destinationPath">The optional destination pathname, whose missing suffix is permitted.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The physical preserve-root and containment result.</returns>
	public async ValueTask<RecursivePathSafetyResult> EvaluateAsync(
		string sourcePath,
		string? destinationPath,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrEmpty( sourcePath );
		if ( _resolver is null ) {
			var normalizedSource = Normalize( sourcePath );
			var normalizedDestination = destinationPath is null ? null : Normalize( destinationPath );
			return new RecursivePathSafetyResult(
				true,
				IsFileSystemRoot( normalizedSource ),
				normalizedDestination is null
					? RecursivePathRelationship.Disjoint
					: ClassifyNormalized( normalizedSource, normalizedDestination ),
				normalizedSource,
				normalizedDestination
			);
		}
		var source = await _resolver.ResolvePhysicalAsync(
			sourcePath,
			new CanonicalPathResolutionOptions {
				MissingComponentPolicy = MissingPathComponentPolicy.RequireExisting,
				FollowSymbolicLinks = true,
				FollowFinalSymbolicLink = true
			},
			cancellationToken
		).ConfigureAwait( false );
		if ( !source.Succeeded ) {
			return new RecursivePathSafetyResult(
				false,
				false,
				RecursivePathRelationship.Disjoint,
				null,
				null,
				source.Failure!.Message,
				source.Failure.Exception
			);
		}
		if ( destinationPath is null ) {
			return new RecursivePathSafetyResult(
				true,
				IsFileSystemRoot( source.Path! ),
				RecursivePathRelationship.Disjoint,
				source.Path,
				null
			);
		}
		var destination = await _resolver.ResolvePhysicalAsync(
			destinationPath,
			new CanonicalPathResolutionOptions {
				MissingComponentPolicy = MissingPathComponentPolicy.AllowMissingSuffix,
				FollowSymbolicLinks = true,
				FollowFinalSymbolicLink = true
			},
			cancellationToken
		).ConfigureAwait( false );
		if ( !destination.Succeeded ) {
			return new RecursivePathSafetyResult(
				false,
				IsFileSystemRoot( source.Path! ),
				RecursivePathRelationship.Disjoint,
				source.Path,
				null,
				destination.Failure!.Message,
				destination.Failure.Exception
			);
		}
		return new RecursivePathSafetyResult(
			true,
			IsFileSystemRoot( source.Path! ),
			ClassifyResolved( source.Path!, destination.Path! ),
			source.Path,
			destination.Path
		);
	}

	/// <summary>Normalizes a path lexically for deterministic policy comparison.</summary>
	/// <param name="path">The pathname to normalize.</param>
	/// <returns>The absolute path without non-root trailing separators.</returns>
	public static string Normalize( string path ) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		return TrimEndingSeparators( Path.GetFullPath( path ) );
	}

	private RecursivePathRelationship ClassifyResolved( string source, string destination ) {
		if ( string.Equals( source, destination, _comparison ) ) {
			return RecursivePathRelationship.Same;
		}
		var destinationContainment = _resolver!.EvaluateContainment( source, destination );
		if ( !destinationContainment.Succeeded ) {
			throw new ArgumentException( destinationContainment.Failure!.Message, nameof( destination ) );
		}
		if ( destinationContainment.IsContained ) {
			return RecursivePathRelationship.DestinationInsideSource;
		}
		var sourceContainment = _resolver.EvaluateContainment( destination, source );
		if ( !sourceContainment.Succeeded ) {
			throw new ArgumentException( sourceContainment.Failure!.Message, nameof( source ) );
		}
		return sourceContainment.IsContained
			? RecursivePathRelationship.SourceInsideDestination
			: RecursivePathRelationship.Disjoint;
	}

	private RecursivePathRelationship ClassifyNormalized( string source, string destination ) {
		if ( string.Equals( source, destination, _comparison ) ) {
			return RecursivePathRelationship.Same;
		}
		if ( IsDescendant( source, destination ) ) {
			return RecursivePathRelationship.DestinationInsideSource;
		}
		return IsDescendant( destination, source )
			? RecursivePathRelationship.SourceInsideDestination
			: RecursivePathRelationship.Disjoint;
	}

	private bool IsDescendant( string parent, string candidate ) {
		var endsWithSeparator = parent.EndsWith( Path.DirectorySeparatorChar )
			|| parent.EndsWith( Path.AltDirectorySeparatorChar );
		var prefix = endsWithSeparator ? parent : string.Concat( parent, Path.DirectorySeparatorChar );
		return candidate.StartsWith( prefix, _comparison );
	}

	private static string TrimEndingSeparators( string path ) {
		var root = Path.GetPathRoot( path );
		var minimumLength = string.IsNullOrEmpty( root ) ? 0 : root.Length;
		var length = path.Length;
		while (
			length > minimumLength
			&& (path[length - 1] == Path.DirectorySeparatorChar || path[length - 1] == Path.AltDirectorySeparatorChar)
		) {
			length--;
		}
		return length == path.Length ? path : path[..length];
	}
}
