namespace Icod.DiffUtils.Shared.Edits;

using Icod.DiffUtils.Shared.Lines;

/// <summary>Builds shortest line edit scripts with Myers' difference algorithm.</summary>
public static class LineDiffEngine {
	/// <summary>Compares two line sequences under the supplied normalization policy.</summary>
	public static EditScript Compare(
		IReadOnlyList<ComparisonLine> oldLines,
		IReadOnlyList<ComparisonLine> newLines,
		LineComparisonPolicy? policy = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( oldLines );
		ArgumentNullException.ThrowIfNull( newLines );
		policy ??= new LineComparisonPolicy();
		var oldKeys = oldLines.Select( line => LineNormalizer.Normalize( line, policy ) ).ToArray();
		var newKeys = newLines.Select( line => LineNormalizer.Normalize( line, policy ) ).ToArray();
		var operations = BuildOperations( oldLines, newLines, oldKeys, newKeys, cancellationToken );
		return new EditScript( operations.AsReadOnly(), BuildBlocks( operations ).AsReadOnly() );
	}

	/// <summary>Builds context-expanded hunks from an edit script.</summary>
	public static IReadOnlyList<DifferenceHunk> BuildHunks( EditScript script, int contextLines ) {
		ArgumentNullException.ThrowIfNull( script );
		ArgumentOutOfRangeException.ThrowIfNegative( contextLines );
		if ( !script.HasDifferences ) {
			return Array.Empty<DifferenceHunk>();
		}
		var operations = script.Operations;
		var includedOperations = new HashSet<EditOperation>( ReferenceEqualityComparer.Instance );
		foreach ( var difference in script.Differences ) {
			foreach ( var operation in difference.Operations ) {
				includedOperations.Add( operation );
			}
		}
		var changed = new List<int>();
		for ( var index = 0; index < operations.Count; index++ ) {
			if ( includedOperations.Contains( operations[index] ) ) {
				changed.Add( index );
			}
		}
		var spans = new List<(int Start, int End)>();
		foreach ( var index in changed ) {
			var start = Math.Max( 0, index - contextLines );
			var end = Math.Min( operations.Count, index + contextLines + 1 );
			if ( 0 == spans.Count || spans[^1].End < start ) {
				spans.Add( (start, end) );
			} else {
				spans[^1] = (spans[^1].Start, Math.Max( spans[^1].End, end ));
			}
		}
		var hunks = new List<DifferenceHunk>( spans.Count );
		foreach ( var span in spans ) {
			var slice = operations.Skip( span.Start ).Take( span.End - span.Start ).ToArray();
			var oldIndices = slice.Where( operation => operation.OldIndex.HasValue ).Select( operation => operation.OldIndex!.Value ).ToArray();
			var newIndices = slice.Where( operation => operation.NewIndex.HasValue ).Select( operation => operation.NewIndex!.Value ).ToArray();
			var oldStart = 0 < oldIndices.Length ? oldIndices[0] : GetOldPositionBefore( operations, span.Start );
			var newStart = 0 < newIndices.Length ? newIndices[0] : GetNewPositionBefore( operations, span.Start );
			hunks.Add( new DifferenceHunk(
				oldStart,
				oldIndices.Length,
				newStart,
				newIndices.Length,
				Array.AsReadOnly( slice )
			) );
		}
		return hunks.AsReadOnly();
	}

	private static List<EditOperation> BuildOperations(
		IReadOnlyList<ComparisonLine> oldLines,
		IReadOnlyList<ComparisonLine> newLines,
		IReadOnlyList<string> oldKeys,
		IReadOnlyList<string> newKeys,
		CancellationToken cancellationToken
	) {
		var oldCount = oldKeys.Count;
		var newCount = newKeys.Count;
		var maximum = checked( oldCount + newCount );
		if ( 0 == maximum ) {
			return new List<EditOperation>();
		}
		var offset = maximum + 1;
		var frontier = new int[( maximum * 2 ) + 3];
		var trace = new List<int[]>( maximum + 1 );
		frontier[offset + 1] = 0;
		for ( var distance = 0; distance <= maximum; distance++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			trace.Add( (int[])frontier.Clone() );
			for ( var diagonal = -distance; diagonal <= distance; diagonal += 2 ) {
				var index = offset + diagonal;
				var x = diagonal == -distance || ( diagonal != distance && frontier[index - 1] <= frontier[index + 1] )
					? frontier[index + 1]
					: frontier[index - 1] + 1;
				var y = x - diagonal;
				while ( x < oldCount && y < newCount && string.Equals( oldKeys[x], newKeys[y], StringComparison.Ordinal ) ) {
					x++;
					y++;
				}
				frontier[index] = x;
				if ( oldCount <= x && newCount <= y ) {
					return Backtrack( oldLines, newLines, trace, distance, offset, cancellationToken );
				}
			}
		}
		throw new InvalidOperationException( "The line difference engine failed to construct an edit path." );
	}

	private static List<EditOperation> Backtrack(
		IReadOnlyList<ComparisonLine> oldLines,
		IReadOnlyList<ComparisonLine> newLines,
		IReadOnlyList<int[]> trace,
		int finalDistance,
		int offset,
		CancellationToken cancellationToken
	) {
		var operations = new List<EditOperation>( oldLines.Count + newLines.Count );
		var x = oldLines.Count;
		var y = newLines.Count;
		for ( var distance = finalDistance; 0 < distance; distance-- ) {
			cancellationToken.ThrowIfCancellationRequested();
			var frontier = trace[distance];
			var diagonal = x - y;
			var previousDiagonal = diagonal == -distance || ( diagonal != distance && frontier[offset + diagonal - 1] <= frontier[offset + diagonal + 1] )
				? diagonal + 1
				: diagonal - 1;
			var previousX = frontier[offset + previousDiagonal];
			var previousY = previousX - previousDiagonal;
			while ( previousX < x && previousY < y ) {
				x--;
				y--;
				operations.Add( new EditOperation( EditOperationKind.Equal, x, y, oldLines[x] ) );
			}
			if ( x == previousX ) {
				y--;
				operations.Add( new EditOperation( EditOperationKind.Insert, null, y, newLines[y] ) );
			} else {
				x--;
				operations.Add( new EditOperation( EditOperationKind.Delete, x, null, oldLines[x] ) );
			}
		}
		while ( 0 < x && 0 < y ) {
			x--;
			y--;
			operations.Add( new EditOperation( EditOperationKind.Equal, x, y, oldLines[x] ) );
		}
		while ( 0 < x ) {
			x--;
			operations.Add( new EditOperation( EditOperationKind.Delete, x, null, oldLines[x] ) );
		}
		while ( 0 < y ) {
			y--;
			operations.Add( new EditOperation( EditOperationKind.Insert, null, y, newLines[y] ) );
		}
		operations.Reverse();
		return operations;
	}

	private static List<DifferenceBlock> BuildBlocks( IReadOnlyList<EditOperation> operations ) {
		var blocks = new List<DifferenceBlock>();
		var oldPosition = 0;
		var newPosition = 0;
		var index = 0;
		while ( index < operations.Count ) {
			var operation = operations[index];
			if ( EditOperationKind.Equal == operation.Kind ) {
				oldPosition++;
				newPosition++;
				index++;
				continue;
			}
			var oldStart = oldPosition;
			var newStart = newPosition;
			var blockOperations = new List<EditOperation>();
			while ( index < operations.Count && EditOperationKind.Equal != operations[index].Kind ) {
				operation = operations[index++];
				blockOperations.Add( operation );
				if ( EditOperationKind.Delete == operation.Kind ) {
					oldPosition++;
				} else {
					newPosition++;
				}
			}
			blocks.Add( new DifferenceBlock(
				oldStart,
				oldPosition - oldStart,
				newStart,
				newPosition - newStart,
				blockOperations.AsReadOnly()
			) );
		}
		return blocks;
	}

	private static int GetOldPositionBefore( IReadOnlyList<EditOperation> operations, int endExclusive ) {
		var count = 0;
		for ( var index = 0; index < endExclusive; index++ ) {
			if ( operations[index].OldIndex.HasValue ) {
				count++;
			}
		}
		return count;
	}

	private static int GetNewPositionBefore( IReadOnlyList<EditOperation> operations, int endExclusive ) {
		var count = 0;
		for ( var index = 0; index < endExclusive; index++ ) {
			if ( operations[index].NewIndex.HasValue ) {
				count++;
			}
		}
		return count;
	}
}
