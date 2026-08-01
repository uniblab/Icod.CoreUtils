namespace Icod.DiffUtils.Shared.Merge;

using Icod.DiffUtils.Shared.Edits;
using Icod.DiffUtils.Shared.Lines;

/// <summary>Builds GNU-compatible connected regions from two comparisons onto one common input.</summary>
public static class ThreeWayMergeEngine {
	private sealed class PairwiseBlock {
		/// <summary>Initializes one pairwise changed block.</summary>
		public PairwiseBlock( int sideStart, int sideLength, int commonStart, int commonLength ) {
			ArgumentOutOfRangeException.ThrowIfNegative( sideStart );
			ArgumentOutOfRangeException.ThrowIfNegative( sideLength );
			ArgumentOutOfRangeException.ThrowIfNegative( commonStart );
			ArgumentOutOfRangeException.ThrowIfNegative( commonLength );
			this.SideStart = sideStart;
			this.SideLength = sideLength;
			this.CommonStart = commonStart;
			this.CommonLength = commonLength;
		}

		/// <summary>Gets the zero-based side start.</summary>
		public int SideStart { get; }
		/// <summary>Gets the side length.</summary>
		public int SideLength { get; }
		/// <summary>Gets the zero-based common start.</summary>
		public int CommonStart { get; }
		/// <summary>Gets the common length.</summary>
		public int CommonLength { get; }
		/// <summary>Gets the one-based inclusive first side line.</summary>
		public int SideLow => checked( this.SideStart + 1 );
		/// <summary>Gets the one-based inclusive final side line, or the line before <see cref="SideLow"/> for an empty range.</summary>
		public int SideHigh => checked( this.SideStart + this.SideLength );
		/// <summary>Gets the one-based inclusive first common line.</summary>
		public int CommonLow => checked( this.CommonStart + 1 );
		/// <summary>Gets the one-based inclusive final common line, or the line before <see cref="CommonLow"/> for an empty range.</summary>
		public int CommonHigh => checked( this.CommonStart + this.CommonLength );
	}

	/// <summary>Compares descendants against the second input as their common ancestor.</summary>
	public static ThreeWayComparison Compare(
		IReadOnlyList<ComparisonLine> mineLines,
		IReadOnlyList<ComparisonLine> olderLines,
		IReadOnlyList<ComparisonLine> yoursLines,
		LineComparisonPolicy? policy = null,
		CancellationToken cancellationToken = default
	) {
		return Compare(
			mineLines,
			olderLines,
			yoursLines,
			ThreeWayCommonInput.Second,
			policy,
			cancellationToken
		);
	}

	/// <summary>Compares three external inputs using the selected common input for pairwise alignment.</summary>
	public static ThreeWayComparison Compare(
		IReadOnlyList<ComparisonLine> mineLines,
		IReadOnlyList<ComparisonLine> olderLines,
		IReadOnlyList<ComparisonLine> yoursLines,
		ThreeWayCommonInput commonInput,
		LineComparisonPolicy? policy = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( mineLines );
		ArgumentNullException.ThrowIfNull( olderLines );
		ArgumentNullException.ThrowIfNull( yoursLines );
		if ( commonInput is not ThreeWayCommonInput.Second and not ThreeWayCommonInput.Third ) {
			throw new ArgumentOutOfRangeException( nameof( commonInput ) );
		}
		policy ??= new LineComparisonPolicy();

		IReadOnlyList<ComparisonLine>[] external = { mineLines, olderLines, yoursLines };
		var commonExternal = ThreeWayCommonInput.Second == commonInput ? 1 : 2;
		int[] sideExternal = ThreeWayCommonInput.Second == commonInput ? new[] { 0, 2 } : new[] { 0, 1 };
		var commonLines = external[commonExternal];
		var threads = new[] {
			BuildPairwiseBlocks( external[sideExternal[0]], commonLines, policy, cancellationToken ),
			BuildPairwiseBlocks( external[sideExternal[1]], commonLines, policy, cancellationToken )
		};
		var positions = new int[2];
		var lastHigh = new int[3];
		var regions = new List<ThreeWayChangeRegion>();

		while ( positions[0] < threads[0].Count || positions[1] < threads[1].Count ) {
			cancellationToken.ThrowIfCancellationRequested();
			var current0 = positions[0] < threads[0].Count ? threads[0][positions[0]] : null;
			var current1 = positions[1] < threads[1].Count ? threads[1][positions[1]] : null;
			var baseThread = null == current0
				? 1
				: null == current1
					? 0
					: current0.CommonLow > current1.CommonLow ? 1 : 0;
			var highThread = baseThread;
			var firstBlock = threads[baseThread][positions[baseThread]++];
			var highWaterMark = firstBlock.CommonHigh;
			var usingBlocks = new[] { new List<PairwiseBlock>(), new List<PairwiseBlock>() };
			usingBlocks[baseThread].Add( firstBlock );

			var otherThread = highThread ^ 1;
			while ( positions[otherThread] < threads[otherThread].Count
				&& threads[otherThread][positions[otherThread]].CommonLow <= checked( highWaterMark + 1 ) ) {
				var block = threads[otherThread][positions[otherThread]++];
				usingBlocks[otherThread].Add( block );
				if ( highWaterMark < block.CommonHigh ) {
					highThread ^= 1;
					highWaterMark = block.CommonHigh;
				}
				otherThread = highThread ^ 1;
			}

			var commonLow = usingBlocks[baseThread][0].CommonLow;
			var commonHigh = usingBlocks[highThread][^1].CommonHigh;
			var sideLow = new int[2];
			var sideHigh = new int[2];
			for ( var thread = 0; thread < 2; thread++ ) {
				if ( 0 < usingBlocks[thread].Count ) {
					var first = usingBlocks[thread][0];
					var last = usingBlocks[thread][^1];
					sideLow[thread] = checked( commonLow - first.CommonLow + first.SideLow );
					sideHigh[thread] = checked( commonHigh - last.CommonHigh + last.SideHigh );
				} else {
					sideLow[thread] = checked( commonLow - lastHigh[2] + lastHigh[thread] );
					sideHigh[thread] = checked( commonHigh - lastHigh[2] + lastHigh[thread] );
				}
			}

			var starts = new int[3];
			var lengths = new int[3];
			IReadOnlyList<ComparisonLine>[] lines = {
				Array.Empty<ComparisonLine>(),
				Array.Empty<ComparisonLine>(),
				Array.Empty<ComparisonLine>()
			};
			for ( var thread = 0; thread < 2; thread++ ) {
				var externalIndex = sideExternal[thread];
				starts[externalIndex] = checked( sideLow[thread] - 1 );
				lengths[externalIndex] = checked( sideHigh[thread] - sideLow[thread] + 1 );
				lines[externalIndex] = Slice( external[externalIndex], starts[externalIndex], lengths[externalIndex] );
			}
			starts[commonExternal] = checked( commonLow - 1 );
			lengths[commonExternal] = checked( commonHigh - commonLow + 1 );
			lines[commonExternal] = Slice( commonLines, starts[commonExternal], lengths[commonExternal] );

			int? oddExternal;
			if ( 0 == usingBlocks[0].Count ) {
				oddExternal = sideExternal[1];
			} else if ( 0 == usingBlocks[1].Count ) {
				oddExternal = sideExternal[0];
			} else if ( AreEquivalent( lines[sideExternal[0]], lines[sideExternal[1]], policy ) ) {
				oddExternal = commonExternal;
			} else {
				oddExternal = null;
			}
			regions.Add( new ThreeWayChangeRegion(
				Classify( oddExternal ),
				starts[0],
				lines[0],
				starts[1],
				lines[1],
				starts[2],
				lines[2]
			) );
			lastHigh[0] = sideHigh[0];
			lastHigh[1] = sideHigh[1];
			lastHigh[2] = commonHigh;
		}
		return new ThreeWayComparison( regions.AsReadOnly() );
	}

	private static IReadOnlyList<PairwiseBlock> BuildPairwiseBlocks(
		IReadOnlyList<ComparisonLine> sideLines,
		IReadOnlyList<ComparisonLine> commonLines,
		LineComparisonPolicy policy,
		CancellationToken cancellationToken
	) {
		var script = LineDiffEngine.Compare( sideLines, commonLines, policy, cancellationToken );
		var sideChanged = new bool[checked( sideLines.Count + 2 )];
		var commonChanged = new bool[checked( commonLines.Count + 2 )];
		foreach ( var operation in script.Operations ) {
			if ( EditOperationKind.Delete == operation.Kind ) {
				sideChanged[operation.OldIndex!.Value + 1] = true;
			} else if ( EditOperationKind.Insert == operation.Kind ) {
				commonChanged[operation.NewIndex!.Value + 1] = true;
			}
		}
		var sideKeys = sideLines.Select( line => LineNormalizer.Normalize( line, policy ) ).ToArray();
		var commonKeys = commonLines.Select( line => LineNormalizer.Normalize( line, policy ) ).ToArray();
		ShiftBoundaries( sideChanged, commonChanged, sideKeys, commonKeys, cancellationToken );
		return BuildBlocks( sideChanged, commonChanged, sideLines.Count, commonLines.Count );
	}

	private static void ShiftBoundaries(
		bool[] sideChanged,
		bool[] commonChanged,
		IReadOnlyList<string> sideKeys,
		IReadOnlyList<string> commonKeys,
		CancellationToken cancellationToken
	) {
		bool[][] changed = { sideChanged, commonChanged };
		IReadOnlyList<string>[] keys = { sideKeys, commonKeys };
		for ( var file = 0; file < 2; file++ ) {
			var ownChanged = changed[file];
			var otherChanged = changed[1 - file];
			var ownKeys = keys[file];
			var position = 0;
			var otherPosition = 0;
			var end = ownKeys.Count;
			while ( true ) {
				cancellationToken.ThrowIfCancellationRequested();
				while ( position < end && !IsChanged( ownChanged, position ) ) {
					while ( IsChanged( otherChanged, otherPosition ) ) {
						otherPosition++;
					}
					position++;
					otherPosition++;
				}
				if ( position == end ) {
					break;
				}
				var start = position++;
				while ( IsChanged( ownChanged, position ) ) {
					position++;
				}
				while ( IsChanged( otherChanged, otherPosition ) ) {
					otherPosition++;
				}

				int corresponding;
				int runLength;
				do {
					runLength = position - start;
					while ( 0 < start && string.Equals( ownKeys[start - 1], ownKeys[position - 1], StringComparison.Ordinal ) ) {
						SetChanged( ownChanged, --start, true );
						SetChanged( ownChanged, --position, false );
						while ( IsChanged( ownChanged, start - 1 ) ) {
							start--;
						}
						do {
							otherPosition--;
						} while ( IsChanged( otherChanged, otherPosition ) );
					}
					corresponding = IsChanged( otherChanged, otherPosition - 1 ) ? position : end;
					while ( position != end
						&& string.Equals( ownKeys[start], ownKeys[position], StringComparison.Ordinal ) ) {
						SetChanged( ownChanged, start++, false );
						SetChanged( ownChanged, position++, true );
						while ( IsChanged( ownChanged, position ) ) {
							position++;
						}
						otherPosition++;
						while ( IsChanged( otherChanged, otherPosition ) ) {
							corresponding = position;
							otherPosition++;
						}
					}
				} while ( runLength != position - start );

				while ( corresponding < position ) {
					SetChanged( ownChanged, --start, true );
					SetChanged( ownChanged, --position, false );
					do {
						otherPosition--;
					} while ( IsChanged( otherChanged, otherPosition ) );
				}
			}
		}
	}

	private static IReadOnlyList<PairwiseBlock> BuildBlocks(
		bool[] sideChanged,
		bool[] commonChanged,
		int sideCount,
		int commonCount
	) {
		var blocks = new List<PairwiseBlock>();
		var sidePosition = 0;
		var commonPosition = 0;
		while ( sidePosition < sideCount || commonPosition < commonCount ) {
			if ( IsChanged( sideChanged, sidePosition ) || IsChanged( commonChanged, commonPosition ) ) {
				var sideStart = sidePosition;
				var commonStart = commonPosition;
				while ( sidePosition < sideCount && IsChanged( sideChanged, sidePosition ) ) {
					sidePosition++;
				}
				while ( commonPosition < commonCount && IsChanged( commonChanged, commonPosition ) ) {
					commonPosition++;
				}
				blocks.Add( new PairwiseBlock(
					sideStart,
					sidePosition - sideStart,
					commonStart,
					commonPosition - commonStart
				) );
			}
			if ( sidePosition < sideCount ) {
				sidePosition++;
			}
			if ( commonPosition < commonCount ) {
				commonPosition++;
			}
		}
		return blocks.AsReadOnly();
	}

	private static bool AreEquivalent(
		IReadOnlyList<ComparisonLine> first,
		IReadOnlyList<ComparisonLine> second,
		LineComparisonPolicy policy
	) {
		if ( first.Count != second.Count ) {
			return false;
		}
		for ( var index = 0; index < first.Count; index++ ) {
			if ( !string.Equals(
				LineNormalizer.Normalize( first[index], policy ),
				LineNormalizer.Normalize( second[index], policy ),
				StringComparison.Ordinal
			) ) {
				return false;
			}
		}
		return true;
	}

	private static ThreeWayChangeKind Classify( int? oddExternal ) {
		return oddExternal switch {
			0 => ThreeWayChangeKind.MineOnly,
			1 => ThreeWayChangeKind.OlderOnly,
			2 => ThreeWayChangeKind.YoursOnly,
			_ => ThreeWayChangeKind.Overlap
		};
	}

	private static IReadOnlyList<ComparisonLine> Slice(
		IReadOnlyList<ComparisonLine> lines,
		int start,
		int length
	) {
		if ( start < 0 || length < 0 || lines.Count < checked( start + length ) ) {
			throw new InvalidOperationException( "The three-way line mapping produced an invalid range." );
		}
		return Array.AsReadOnly( lines.Skip( start ).Take( length ).ToArray() );
	}

	private static bool IsChanged( bool[] changed, int logicalIndex ) {
		var storageIndex = logicalIndex + 1;
		return 0 <= storageIndex && storageIndex < changed.Length && changed[storageIndex];
	}

	private static void SetChanged( bool[] changed, int logicalIndex, bool value ) {
		changed[logicalIndex + 1] = value;
	}
}
