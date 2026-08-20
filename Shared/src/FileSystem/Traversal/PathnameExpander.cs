using System.IO;
using System.Runtime.CompilerServices;

namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Expands eligible pathname operands over an injectable one-level filesystem provider.
/// </summary>
public sealed class PathnameExpander {
	private readonly IReadOnlyFileSystemProvider _provider;

	/// <summary>
	/// Initializes a pathname expander.
	/// </summary>
	/// <param name="provider">The one-level filesystem provider.</param>
	public PathnameExpander( IReadOnlyFileSystemProvider provider ) {
		ArgumentNullException.ThrowIfNull( provider );
		_provider = provider;
	}

	/// <summary>
	/// Expands operands in their supplied order while preserving repetitions and provenance.
	/// </summary>
	/// <param name="operands">The eligible pathname operands.</param>
	/// <param name="options">The expansion options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The asynchronous expansion event stream.</returns>
	public async IAsyncEnumerable<PathnameExpansionEvent> ExpandAsync(
		IEnumerable<string> operands,
		PathnameExpansionOptions? options = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( operands );
		options ??= PathnameExpansionOptions.Default;
		options.Validate();

		var rootOrdinal = 0L;
		var operandIndex = 0;
		foreach ( var operand in operands ) {
			ArgumentNullException.ThrowIfNull( operand );
			cancellationToken.ThrowIfCancellationRequested();

			PathnamePattern? pattern = null;
			PathTraversalError? patternError = null;
			try {
				pattern = PathnamePattern.Parse( operand, options.PatternOptions );
			} catch ( Exception exception ) when (
				exception is ArgumentException
					or NotSupportedException
					or PathTooLongException
			) {
				patternError = CreateError(
					PathTraversalErrorCode.InvalidPattern,
					operand,
					PathTraversalErrorScope.Root,
					"The pathname pattern is invalid on the current platform.",
					exception
				);
			}
			if ( patternError is not null ) {
				yield return PathnameExpansionEvent.CreateError( operand, operandIndex, patternError );
				if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
					yield break;
				}
				operandIndex++;
				continue;
			}

			var parsedPattern = pattern!;

			if ( !parsedPattern.HasMetacharacters ) {
				PathTraversalRoot? literalRoot = null;
				PathTraversalError? literalRootError = null;
				try {
					literalRoot = CreateLiteralRoot(
						operand,
						operandIndex,
						rootOrdinal,
						options.BaseDirectory,
						parsedPattern.GetLiteralPath()
					);
				} catch ( Exception exception ) when (
					exception is ArgumentException
						or NotSupportedException
						or PathTooLongException
				) {
					literalRootError = CreateError(
						PathTraversalErrorCode.InvalidPath,
						operand,
						PathTraversalErrorScope.Root,
						"The literal operand cannot be represented on the current platform.",
						exception
					);
				}
				if ( literalRootError is not null ) {
					yield return PathnameExpansionEvent.CreateError( operand, operandIndex, literalRootError );
					if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
						yield break;
					}
					operandIndex++;
					continue;
				}

				rootOrdinal++;
				yield return PathnameExpansionEvent.CreateRoot( literalRoot! );
				operandIndex++;
				continue;
			}

			ExpansionInitialization? initialization = null;
			PathTraversalError? initializationError = null;
			try {
				initialization = await InitializeAsync(
					parsedPattern,
					options,
					cancellationToken
				).ConfigureAwait( false );
			} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
				throw;
			} catch ( Exception exception ) when (
				exception is ArgumentException
					or NotSupportedException
					or PathTooLongException
			) {
				initializationError = CreateError(
					PathTraversalErrorCode.InvalidPath,
					operand,
					PathTraversalErrorScope.Root,
					"The pathname expansion starting directory cannot be represented on the current platform.",
					exception
				);
			} catch ( Exception exception ) {
				initializationError = CreateError(
					PathTraversalErrorCode.ObservationFailed,
					operand,
					PathTraversalErrorScope.Root,
					"The pathname expansion starting directory could not be observed.",
					exception
				);
			}
			if ( initializationError is not null ) {
				yield return PathnameExpansionEvent.CreateError( operand, operandIndex, initializationError );
				if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
					yield break;
				}
				operandIndex++;
				continue;
			}
			var expansionInitialization = initialization!;

			if ( expansionInitialization.StartObservation.Kind != FileSystemEntryKind.Directory ) {
				var noMatch = HandleNoMatch(
					operand,
					operandIndex,
					rootOrdinal,
					options,
					cancellationToken
				);
				if ( noMatch.Root is not null ) {
					rootOrdinal++;
				}
				yield return noMatch;
				if (
					options.ErrorMode == PathTraversalErrorMode.Stop
					&& noMatch.Kind == PathnameExpansionEventKind.Error
				) {
					yield break;
				}
				operandIndex++;
				continue;
			}

			if (
				options.FileSystemBoundaryMode == FileSystemBoundaryMode.StayOnRootFileSystem
				&& !expansionInitialization.StartObservation.FileSystemIdentity.IsAvailable
			) {
				var error = CreateError(
					PathTraversalErrorCode.IdentityUnavailable,
					expansionInitialization.StartPath,
					PathTraversalErrorScope.Root,
					"A filesystem identity is required to enforce pathname-expansion boundaries."
				);
				yield return PathnameExpansionEvent.CreateError( operand, operandIndex, error );
				if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
					yield break;
				}
				operandIndex++;
				continue;
			}

			var requiresStableDirectoryIdentity = parsedPattern.Segments.Any(
				static segment => segment.IsDoubleStar
			);
			if (
				requiresStableDirectoryIdentity
				&& !expansionInitialization.StartObservation.EntryIdentity.IsAvailable
			) {
				var error = CreateError(
					PathTraversalErrorCode.IdentityUnavailable,
					expansionInitialization.StartPath,
					PathTraversalErrorScope.Root,
					"A stable directory identity is required for cycle-safe pathname expansion."
				);
				yield return PathnameExpansionEvent.CreateError( operand, operandIndex, error );
				if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
					yield break;
				}
				operandIndex++;
				continue;
			}

			var matched = false;
			var hadExpansionIssue = false;
			var states = new Stack<ExpansionState>();
			var processedStates = new HashSet<string>(
				OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal
			);
			var initialAncestry = new List<ExpansionAncestryEntry>();
			if ( expansionInitialization.StartObservation.EntryIdentity.IsAvailable ) {
				initialAncestry.Add( new ExpansionAncestryEntry(
					expansionInitialization.StartObservation.EntryIdentity,
					expansionInitialization.StartPath
				) );
			}
			states.Push( new ExpansionState(
				expansionInitialization.StartPath,
				expansionInitialization.DisplayPrefix,
				0,
				0,
				initialAncestry,
				true,
				false
			) );

			while ( states.Count > 0 ) {
				cancellationToken.ThrowIfCancellationRequested();
				var state = states.Pop();
				if ( !processedStates.Add( CreateStateKey( state ) ) ) {
					continue;
				}
				if ( state.SegmentIndex >= parsedPattern.Segments.Count ) {
					if ( parsedPattern.RequiresDirectory && !state.IsKnownDirectory ) {
						ReadOnlyFileSystemEntry? finalObservation = null;
						PathTraversalError? finalObservationError = null;
						try {
							finalObservation = await _provider.ObserveAsync(
								state.AccessPath,
								state.AllowTerminalLinkFollow,
								cancellationToken
							).ConfigureAwait( false );
						} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
							throw;
						} catch ( Exception exception ) {
							finalObservationError = CreateError(
								PathTraversalErrorCode.ObservationFailed,
								state.AccessPath,
								PathTraversalErrorScope.Entry,
								"A terminal directory candidate could not be observed.",
								exception
							);
						}
						if ( finalObservationError is not null ) {
							hadExpansionIssue = true;
							yield return PathnameExpansionEvent.CreateError( operand, operandIndex, finalObservationError );
							if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
								yield break;
							}
							continue;
						}
						if ( finalObservation!.Kind != FileSystemEntryKind.Directory ) {
							continue;
						}
					}

					var displayPath = state.DisplayPath.Length == 0 ? "." : state.DisplayPath;
					var root = new PathTraversalRoot(
						operand,
						operandIndex,
						rootOrdinal,
						state.AccessPath,
						displayPath,
						PathTraversalRootKind.Expanded
					);
					rootOrdinal++;
					matched = true;
					yield return PathnameExpansionEvent.CreateRoot( root );
					continue;
				}

				var segment = parsedPattern.Segments[state.SegmentIndex];
				if ( segment.LiteralValue == "." ) {
					states.Push( state with {
						DisplayPath = CombineDisplayPath( state.DisplayPath, "." ),
						SegmentIndex = state.SegmentIndex + 1
					} );
					continue;
				}
				if ( segment.LiteralValue == ".." ) {
					var parentResult = await TryCreateParentStateAsync(
						operand,
						operandIndex,
						state,
						expansionInitialization.StartObservation.FileSystemIdentity,
						requiresStableDirectoryIdentity,
						options,
						cancellationToken
					).ConfigureAwait( false );
					if ( parentResult.Event is not null ) {
						hadExpansionIssue = true;
						yield return parentResult.Event;
						if (
							parentResult.Event.Kind == PathnameExpansionEventKind.Error
							&& options.ErrorMode == PathTraversalErrorMode.Stop
						) {
							yield break;
						}
					}
					if ( parentResult.State is not null ) {
						states.Push( parentResult.State );
					}
					continue;
				}

				List<ReadOnlyDirectoryEntry>? children = null;
				PathTraversalError? childrenError = null;
				try {
					children = await LoadChildrenAsync(
						state.AccessPath,
						options,
						cancellationToken
					).ConfigureAwait( false );
				} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
					throw;
				} catch ( ExpansionDirectoryLimitException exception ) {
					childrenError = CreateError(
						PathTraversalErrorCode.DirectoryEntryLimitExceeded,
						state.AccessPath,
						PathTraversalErrorScope.Subtree,
						"The configured directory-entry limit was exceeded during pathname expansion.",
						exception
					);
				} catch ( Exception exception ) {
					childrenError = CreateError(
						PathTraversalErrorCode.EnumerationFailed,
						state.AccessPath,
						PathTraversalErrorScope.Subtree,
						"A directory could not be enumerated during pathname expansion.",
						exception
					);
				}
				if ( childrenError is not null ) {
					hadExpansionIssue = true;
					yield return PathnameExpansionEvent.CreateError( operand, operandIndex, childrenError );
					if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
						yield break;
					}
					continue;
				}
				var loadedChildren = children!;
				if ( segment.IsDoubleStar ) {
					var continuations = new List<ExpansionState>();
					var isFinalDoubleStar = state.SegmentIndex + 1 == parsedPattern.Segments.Count;
					if ( options.MaximumDepth is not int maximumDepth || state.Depth < maximumDepth ) {
						foreach ( var child in loadedChildren ) {
							cancellationToken.ThrowIfCancellationRequested();
							if ( !PathnamePattern.CanDoubleStarMatchName( child.Name, parsedPattern.Options ) ) {
								continue;
							}
							if ( isFinalDoubleStar ) {
								continuations.Add( new ExpansionState(
									child.AccessPath,
									CombineDisplayPath( state.DisplayPath, child.Name ),
									state.SegmentIndex + 1,
									state.Depth + 1,
									state.Ancestry,
									false,
									options.SymbolicLinkMode is SymbolicLinkTraversalMode.RootsOnly
										or SymbolicLinkTraversalMode.Always
								) );
							}
							var result = await TryCreateDirectoryStateAsync(
								operand,
								operandIndex,
								state,
								child,
								state.SegmentIndex,
								false,
								expansionInitialization.StartObservation.FileSystemIdentity,
								requiresStableDirectoryIdentity,
								options,
								cancellationToken
							).ConfigureAwait( false );
							if ( result.Event is not null ) {
								hadExpansionIssue = true;
								yield return result.Event;
								if (
									result.Event.Kind == PathnameExpansionEventKind.Error
									&& options.ErrorMode == PathTraversalErrorMode.Stop
								) {
									yield break;
								}
							}
							if ( result.State is not null ) {
								continuations.Add( result.State );
							}
						}
					}

					for ( var index = continuations.Count - 1; index >= 0; index-- ) {
						states.Push( continuations[index] );
					}
					states.Push( state with { SegmentIndex = state.SegmentIndex + 1 } );
					continue;
				}

				var isLastSegment = state.SegmentIndex + 1 == parsedPattern.Segments.Count;
				var matchingChildren = loadedChildren.Where(
					child => PathnamePattern.IsSegmentMatch( segment, child.Name, parsedPattern.Options )
				).ToList();
				if ( isLastSegment ) {
					if ( options.MaximumDepth is int finalMaximumDepth && state.Depth >= finalMaximumDepth ) {
						continue;
					}
					for ( var index = matchingChildren.Count - 1; index >= 0; index-- ) {
						var child = matchingChildren[index];
						var childDisplayPath = CombineDisplayPath( state.DisplayPath, child.Name );
						states.Push( new ExpansionState(
							child.AccessPath,
							childDisplayPath,
							state.SegmentIndex + 1,
							state.Depth + 1,
							state.Ancestry,
							false,
							options.SymbolicLinkMode is SymbolicLinkTraversalMode.RootsOnly
								or SymbolicLinkTraversalMode.Always
						) );
					}
					continue;
				}

				var nextStates = new List<ExpansionState>( matchingChildren.Count );
				var explicitlyNamed = !segment.HasMetacharacters;
				foreach ( var child in matchingChildren ) {
					cancellationToken.ThrowIfCancellationRequested();
					var result = await TryCreateDirectoryStateAsync(
						operand,
						operandIndex,
						state,
						child,
						state.SegmentIndex + 1,
						explicitlyNamed,
						expansionInitialization.StartObservation.FileSystemIdentity,
						requiresStableDirectoryIdentity,
						options,
						cancellationToken
					).ConfigureAwait( false );
					if ( result.Event is not null ) {
						hadExpansionIssue = true;
						yield return result.Event;
						if (
							result.Event.Kind == PathnameExpansionEventKind.Error
							&& options.ErrorMode == PathTraversalErrorMode.Stop
						) {
							yield break;
						}
					}
					if ( result.State is not null ) {
						nextStates.Add( result.State );
					}
				}
				for ( var index = nextStates.Count - 1; index >= 0; index-- ) {
					states.Push( nextStates[index] );
				}
			}

			if ( !matched && !hadExpansionIssue ) {
				var noMatch = HandleNoMatch(
					operand,
					operandIndex,
					rootOrdinal,
					options,
					cancellationToken
				);
				if ( noMatch.Root is not null ) {
					rootOrdinal++;
				}
				yield return noMatch;
				if (
					options.ErrorMode == PathTraversalErrorMode.Stop
					&& noMatch.Kind == PathnameExpansionEventKind.Error
				) {
					yield break;
				}
			}
			operandIndex++;
		}
	}

	private async ValueTask<ExpansionInitialization> InitializeAsync(
		PathnamePattern pattern,
		PathnameExpansionOptions options,
		CancellationToken cancellationToken
	) {
		var baseDirectory = Path.GetFullPath( options.BaseDirectory );
		var startPath = pattern.Root.Length == 0
			? baseDirectory
			: Path.GetFullPath( pattern.Root );
		var observation = await _provider.ObserveAsync(
			startPath,
			true,
			cancellationToken
		).ConfigureAwait( false );
		return new ExpansionInitialization(
			startPath,
			pattern.Root,
			observation
		);
	}

	private async ValueTask<DirectoryStateResult> TryCreateDirectoryStateAsync(
		string operand,
		int operandIndex,
		ExpansionState parent,
		ReadOnlyDirectoryEntry child,
		int segmentIndex,
		bool explicitlyNamed,
		FileSystemIdentity rootFileSystemIdentity,
		bool requiresStableDirectoryIdentity,
		PathnameExpansionOptions options,
		CancellationToken cancellationToken
	) {
		if ( options.MaximumDepth is int maximumDepth && parent.Depth >= maximumDepth ) {
			return default;
		}

		var follow = options.SymbolicLinkMode == SymbolicLinkTraversalMode.Always
			|| (
				options.SymbolicLinkMode == SymbolicLinkTraversalMode.RootsOnly
				&& explicitlyNamed
			);
		ReadOnlyFileSystemEntry observation;
		try {
			observation = await _provider.ObserveAsync(
				child.AccessPath,
				follow,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			throw;
		} catch ( Exception exception ) {
			return new DirectoryStateResult(
				null,
				PathnameExpansionEvent.CreateError(
					operand,
					operandIndex,
					CreateError(
						PathTraversalErrorCode.ObservationFailed,
						child.AccessPath,
						PathTraversalErrorScope.Entry,
						"A matching pathname could not be observed.",
						exception
					)
				)
			);
		}

		if ( observation.Kind != FileSystemEntryKind.Directory ) {
			return default;
		}
		if (
			options.FileSystemBoundaryMode == FileSystemBoundaryMode.StayOnRootFileSystem
			&& !CanRemainOnRootFileSystem( rootFileSystemIdentity, observation.FileSystemIdentity )
		) {
			if ( !observation.FileSystemIdentity.IsAvailable ) {
				return new DirectoryStateResult(
					null,
					PathnameExpansionEvent.CreateError(
						operand,
						operandIndex,
						CreateError(
							PathTraversalErrorCode.IdentityUnavailable,
							child.AccessPath,
							PathTraversalErrorScope.Subtree,
							"A filesystem identity is required to enforce pathname-expansion boundaries."
						)
					)
				);
			}
			return new DirectoryStateResult(
				null,
				PathnameExpansionEvent.CreateFileSystemBoundary( operand, operandIndex, child.AccessPath )
			);
		}
		if (
			requiresStableDirectoryIdentity
			&& observation.EntryIdentity.IsAvailable
			&& TryFindAncestorPath( parent.Ancestry, observation.EntryIdentity, out var ancestorPath )
		) {
			return new DirectoryStateResult(
				null,
				PathnameExpansionEvent.CreateCycle( operand, operandIndex, child.AccessPath, ancestorPath )
			);
		}
		if ( requiresStableDirectoryIdentity && !observation.EntryIdentity.IsAvailable ) {
			return new DirectoryStateResult(
				null,
				PathnameExpansionEvent.CreateError(
					operand,
					operandIndex,
					CreateError(
						PathTraversalErrorCode.IdentityUnavailable,
						child.AccessPath,
						PathTraversalErrorScope.Subtree,
						"A stable directory identity is required for cycle-safe pathname expansion."
					)
				)
			);
		}

		var ancestry = new List<ExpansionAncestryEntry>( parent.Ancestry );
		if ( observation.EntryIdentity.IsAvailable ) {
			ancestry.Add( new ExpansionAncestryEntry( observation.EntryIdentity, child.AccessPath ) );
		}
		return new DirectoryStateResult(
			new ExpansionState(
				child.AccessPath,
				CombineDisplayPath( parent.DisplayPath, child.Name ),
				segmentIndex,
				parent.Depth + 1,
				ancestry,
				true,
				false
			),
			null
		);
	}

	private async ValueTask<DirectoryStateResult> TryCreateParentStateAsync(
		string operand,
		int operandIndex,
		ExpansionState state,
		FileSystemIdentity rootFileSystemIdentity,
		bool requiresStableDirectoryIdentity,
		PathnameExpansionOptions options,
		CancellationToken cancellationToken
	) {
		if ( options.MaximumDepth is int maximumDepth && state.Depth >= maximumDepth ) {
			return default;
		}

		string parentPath;
		try {
			parentPath = Path.Combine( state.AccessPath, ".." );
		} catch ( Exception exception ) when (
			exception is ArgumentException
				or NotSupportedException
				or PathTooLongException
		) {
			return new DirectoryStateResult(
				null,
				PathnameExpansionEvent.CreateError(
					operand,
					operandIndex,
					CreateError(
						PathTraversalErrorCode.InvalidPath,
						state.AccessPath,
						PathTraversalErrorScope.Entry,
						"A parent pathname could not be represented.",
						exception
					)
				)
			);
		}

		ReadOnlyFileSystemEntry observation;
		try {
			observation = await _provider.ObserveAsync(
				parentPath,
				true,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			throw;
		} catch ( Exception exception ) {
			return new DirectoryStateResult(
				null,
				PathnameExpansionEvent.CreateError(
					operand,
					operandIndex,
					CreateError(
						PathTraversalErrorCode.ObservationFailed,
						parentPath,
						PathTraversalErrorScope.Entry,
						"A parent directory could not be observed.",
						exception
					)
				)
			);
		}

		if ( observation.Kind != FileSystemEntryKind.Directory ) {
			return default;
		}
		if (
			options.FileSystemBoundaryMode == FileSystemBoundaryMode.StayOnRootFileSystem
			&& !CanRemainOnRootFileSystem( rootFileSystemIdentity, observation.FileSystemIdentity )
		) {
			if ( !observation.FileSystemIdentity.IsAvailable ) {
				return new DirectoryStateResult(
					null,
					PathnameExpansionEvent.CreateError(
						operand,
						operandIndex,
						CreateError(
							PathTraversalErrorCode.IdentityUnavailable,
							parentPath,
							PathTraversalErrorScope.Subtree,
							"A filesystem identity is required to enforce pathname-expansion boundaries."
						)
					)
				);
			}
			return new DirectoryStateResult(
				null,
				PathnameExpansionEvent.CreateFileSystemBoundary( operand, operandIndex, parentPath )
			);
		}
		if ( requiresStableDirectoryIdentity && !observation.EntryIdentity.IsAvailable ) {
			return new DirectoryStateResult(
				null,
				PathnameExpansionEvent.CreateError(
					operand,
					operandIndex,
					CreateError(
						PathTraversalErrorCode.IdentityUnavailable,
						parentPath,
						PathTraversalErrorScope.Subtree,
						"A stable directory identity is required for recursive pathname expansion."
					)
				)
			);
		}

		var ancestry = RewindAncestry( state.Ancestry, observation, parentPath );
		return new DirectoryStateResult(
			new ExpansionState(
				parentPath,
				CombineDisplayPath( state.DisplayPath, ".." ),
				state.SegmentIndex + 1,
				checked(state.Depth + 1),
				ancestry,
				true,
				false
			),
			null
		);
	}

	private static IReadOnlyList<ExpansionAncestryEntry> RewindAncestry(
		IReadOnlyList<ExpansionAncestryEntry> ancestry,
		ReadOnlyFileSystemEntry observation,
		string parentPath
	) {
		if ( !observation.EntryIdentity.IsAvailable ) {
			return Array.Empty<ExpansionAncestryEntry>();
		}
		for ( var index = ancestry.Count - 1; index >= 0; index-- ) {
			if ( ancestry[index].Identity == observation.EntryIdentity ) {
				return ancestry.Take( index + 1 ).ToArray();
			}
		}
		return new[] { new ExpansionAncestryEntry( observation.EntryIdentity, parentPath ) };
	}

	private static bool TryFindAncestorPath(
		IReadOnlyList<ExpansionAncestryEntry> ancestry,
		FileSystemEntryIdentity identity,
		out string path
	) {
		for ( var index = ancestry.Count - 1; index >= 0; index-- ) {
			if ( ancestry[index].Identity == identity ) {
				path = ancestry[index].Path;
				return true;
			}
		}
		path = string.Empty;
		return false;
	}

	private async ValueTask<List<ReadOnlyDirectoryEntry>> LoadChildrenAsync(
		string directoryPath,
		PathnameExpansionOptions options,
		CancellationToken cancellationToken
	) {
		var children = new List<ReadOnlyDirectoryEntry>();
		await foreach ( var child in _provider.EnumerateDirectoryAsync(
			directoryPath,
			cancellationToken
		).WithCancellation( cancellationToken ).ConfigureAwait( false ) ) {
			if ( children.Count >= options.MaximumEntriesPerDirectory ) {
				throw new ExpansionDirectoryLimitException( directoryPath, options.MaximumEntriesPerDirectory );
			}
			children.Add( child );
		}

		if ( options.MatchOrder != PathnameExpansionMatchOrder.Provider ) {
			var comparison = options.MatchOrder == PathnameExpansionMatchOrder.Ordinal
				? StringComparer.Ordinal
				: StringComparer.OrdinalIgnoreCase;
			children.Sort( ( left, right ) => {
				var result = comparison.Compare( left.Name, right.Name );
				return result != 0
					? result
					: StringComparer.Ordinal.Compare( left.Name, right.Name );
			} );
		}
		return children;
	}

	private static PathnameExpansionEvent HandleNoMatch(
		string operand,
		int operandIndex,
		long rootOrdinal,
		PathnameExpansionOptions options,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		switch ( options.UnmatchedPatternBehavior ) {
			case UnmatchedPathnamePatternBehavior.PreserveAsLiteral:
				try {
					return PathnameExpansionEvent.CreateRoot( CreateLiteralRoot(
						operand,
						operandIndex,
						rootOrdinal,
						options.BaseDirectory
					) );
				} catch ( Exception exception ) when (
					exception is ArgumentException
						or NotSupportedException
						or PathTooLongException
				) {
					return PathnameExpansionEvent.CreateError(
						operand,
						operandIndex,
						CreateError(
							PathTraversalErrorCode.InvalidPath,
							operand,
							PathTraversalErrorScope.Root,
							"The unmatched operand cannot be represented as a literal pathname.",
							exception
						)
					);
				}
			case UnmatchedPathnamePatternBehavior.ReturnNoMatches:
				return PathnameExpansionEvent.CreateNoMatch( operand, operandIndex );
			case UnmatchedPathnamePatternBehavior.ReportError:
				return PathnameExpansionEvent.CreateError(
					operand,
					operandIndex,
					CreateError(
						PathTraversalErrorCode.NoPatternMatch,
						operand,
						PathTraversalErrorScope.Root,
						"The pathname pattern produced no matches."
					)
				);
			default:
				throw new ArgumentOutOfRangeException( nameof( options ) );
		}
	}

	private static PathTraversalRoot CreateLiteralRoot(
		string operand,
		int operandIndex,
		long rootOrdinal,
		string baseDirectory,
		string? literalPath = null
	) {
		var effectivePath = literalPath ?? operand;
		if ( effectivePath.Length == 0 ) {
			throw new ArgumentException( "A pathname operand cannot be empty.", nameof( operand ) );
		}
		var accessPath = Path.IsPathRooted( effectivePath )
			? Path.GetFullPath( effectivePath )
			: Path.GetFullPath( effectivePath, Path.GetFullPath( baseDirectory ) );
		return new PathTraversalRoot(
			operand,
			operandIndex,
			rootOrdinal,
			accessPath,
			literalPath ?? operand,
			PathTraversalRootKind.Literal
		);
	}

	private static string CombineDisplayPath( string parent, string name ) => parent.Length == 0
		? name
		: Path.Combine( parent, name );

	private static string CreateStateKey( ExpansionState state ) => string.Concat(
		state.SegmentIndex.ToString( System.Globalization.CultureInfo.InvariantCulture ),
		"\0",
		state.AccessPath,
		"\0",
		state.DisplayPath,
		"\0",
		state.AllowTerminalLinkFollow ? "1" : "0"
	);

	private static bool CanRemainOnRootFileSystem(
		FileSystemIdentity root,
		FileSystemIdentity child
	) => root.IsAvailable && child.IsAvailable && root == child;

	private static PathTraversalError CreateError(
		PathTraversalErrorCode code,
		string path,
		PathTraversalErrorScope scope,
		string message,
		Exception? exception = null
	) => new(
		code,
		null,
		path,
		PathTraversalOperationStage.ExpandPattern,
		scope,
		message,
		exception
	);

	private sealed record ExpansionInitialization(
		string StartPath,
		string DisplayPrefix,
		ReadOnlyFileSystemEntry StartObservation
	);

	private sealed record ExpansionState(
		string AccessPath,
		string DisplayPath,
		int SegmentIndex,
		int Depth,
		IReadOnlyList<ExpansionAncestryEntry> Ancestry,
		bool IsKnownDirectory,
		bool AllowTerminalLinkFollow
	);

	private readonly record struct ExpansionAncestryEntry(
		FileSystemEntryIdentity Identity,
		string Path
	);

	private readonly record struct DirectoryStateResult(
		ExpansionState? State,
		PathnameExpansionEvent? Event
	);
}

/// <summary>
/// Reports that pathname expansion exceeded its configured per-directory resource limit.
/// </summary>
internal sealed class ExpansionDirectoryLimitException : Exception {
	/// <summary>
	/// Initializes the exception.
	/// </summary>
	/// <param name="path">The directory pathname.</param>
	/// <param name="limit">The configured limit.</param>
	internal ExpansionDirectoryLimitException( string path, int limit )
		: base( string.Concat( "Directory '", path, "' exceeded the entry limit ", limit, "." ) ) {
	}
}
