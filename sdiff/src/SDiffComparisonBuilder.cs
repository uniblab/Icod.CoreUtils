namespace Icod.DiffUtils.SDiff;

using Icod.DiffUtils.Shared.Edits;
using Icod.DiffUtils.Shared.Lines;

/// <summary>Builds command-local interactive groups from the shared two-way edit script.</summary>
internal static class SDiffComparisonBuilder {
	/// <summary>Builds aligned groups while applying blank-line and matching-line suppression.</summary>
	/// <param name="left">The materialized left input.</param>
	/// <param name="right">The materialized right input.</param>
	/// <param name="script">The shared two-way edit script.</param>
	/// <param name="options">The validated command options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The aligned command-local comparison.</returns>
	public static SDiffComparison Build(
		SDiffInput left,
		SDiffInput right,
		EditScript script,
		SDiffOptions options,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( left );
		ArgumentNullException.ThrowIfNull( right );
		ArgumentNullException.ThrowIfNull( script );
		ArgumentNullException.ThrowIfNull( options );
		var groups = new List<SDiffGroup>();
		var operations = script.Operations;
		var operationIndex = 0;
		var differenceIndex = 0;
		while ( operationIndex < operations.Count ) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( EditOperationKind.Equal == operations[operationIndex].Kind ) {
				var first = operationIndex;
				while ( operationIndex < operations.Count && EditOperationKind.Equal == operations[operationIndex].Kind ) {
					cancellationToken.ThrowIfCancellationRequested();
					operationIndex++;
				}
				groups.Add( BuildEqualGroup( left, right, operations, first, operationIndex, cancellationToken ) );
				continue;
			}

			var start = operationIndex;
			while ( operationIndex < operations.Count && EditOperationKind.Equal != operations[operationIndex].Kind ) {
				cancellationToken.ThrowIfCancellationRequested();
				operationIndex++;
			}
			var difference = script.Differences[differenceIndex++];
			var ignored = IsIgnored( difference, options, cancellationToken );
			groups.Add( BuildDifferenceGroup( left, right, operations, start, operationIndex, !ignored, cancellationToken ) );
		}
		return new SDiffComparison( groups.AsReadOnly() );
	}

	private static SDiffGroup BuildEqualGroup(
		SDiffInput left,
		SDiffInput right,
		IReadOnlyList<EditOperation> operations,
		int start,
		int end,
		CancellationToken cancellationToken
	) {
		var leftLines = new List<ComparisonLine>( end - start );
		var rightLines = new List<ComparisonLine>( end - start );
		var rows = new List<SDiffRow>( end - start );
		for ( var index = start; index < end; index++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			var operation = operations[index];
			var leftLine = left.Document.Lines[operation.OldIndex!.Value];
			var rightLine = right.Document.Lines[operation.NewIndex!.Value];
			leftLines.Add( leftLine );
			rightLines.Add( rightLine );
			rows.Add( new SDiffRow( leftLine, ' ', rightLine, true ) );
		}
		return new SDiffGroup(
			operations[start].OldIndex!.Value,
			operations[start].NewIndex!.Value,
			leftLines.AsReadOnly(),
			rightLines.AsReadOnly(),
			rows.AsReadOnly(),
			false
		);
	}

	private static SDiffGroup BuildDifferenceGroup(
		SDiffInput left,
		SDiffInput right,
		IReadOnlyList<EditOperation> operations,
		int start,
		int end,
		bool isDifferent,
		CancellationToken cancellationToken
	) {
		var deletes = new List<ComparisonLine>();
		var inserts = new List<ComparisonLine>();
		var leftStart = 0;
		var rightStart = 0;
		var hasLeftStart = false;
		var hasRightStart = false;
		for ( var index = start; index < end; index++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			var operation = operations[index];
			if ( EditOperationKind.Delete == operation.Kind ) {
				leftStart = hasLeftStart ? leftStart : operation.OldIndex!.Value;
				hasLeftStart = true;
				deletes.Add( left.Document.Lines[operation.OldIndex!.Value] );
			} else {
				rightStart = hasRightStart ? rightStart : operation.NewIndex!.Value;
				hasRightStart = true;
				inserts.Add( right.Document.Lines[operation.NewIndex!.Value] );
			}
		}
		if ( !hasLeftStart ) {
			leftStart = FindOldPosition( operations, start );
		}
		if ( !hasRightStart ) {
			rightStart = FindNewPosition( operations, start );
		}
		var rows = new List<SDiffRow>( Math.Max( deletes.Count, inserts.Count ) );
		var count = Math.Max( deletes.Count, inserts.Count );
		for ( var index = 0; index < count; index++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			var leftLine = index < deletes.Count ? deletes[index] : null;
			var rightLine = index < inserts.Count ? inserts[index] : null;
			var marker = isDifferent
				? GetDifferenceMarker( leftLine, rightLine )
				: null == leftLine ? ')' : null == rightLine ? '(' : ' ';
			rows.Add( new SDiffRow( leftLine, marker, rightLine, !isDifferent ) );
		}
		return new SDiffGroup(
			leftStart,
			rightStart,
			deletes.AsReadOnly(),
			inserts.AsReadOnly(),
			rows.AsReadOnly(),
			isDifferent
		);
	}

	private static bool IsIgnored(
		DifferenceBlock difference,
		SDiffOptions options,
		CancellationToken cancellationToken
	) {
		if ( !options.IgnoreBlankLines && 0 == options.IgnoredLinePatterns.Count ) {
			return false;
		}
		var changed = difference.Operations
			.Where( operation => EditOperationKind.Equal != operation.Kind )
			.Select( operation => operation.Line )
			.ToArray();
		if ( 0 == changed.Length ) {
			return false;
		}
		foreach ( var line in changed ) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( options.IgnoreBlankLines && LineNormalizer.IsBlank( line, options.ComparisonPolicy ) ) {
				continue;
			}
			var matched = false;
			foreach ( var expression in options.IgnoredLinePatterns ) {
				var result = expression.Match( GetComparableOutput( line, options ), cancellationToken: cancellationToken );
				if ( !result.IsSuccess ) {
					throw new IOException( result.Diagnostic?.Message ?? "regular-expression matching failed" );
				}
				if ( result.IsMatch ) {
					matched = true;
					break;
				}
			}
			if ( !matched ) {
				return false;
			}
		}
		return true;
	}

	private static string GetComparableOutput( ComparisonLine line, SDiffOptions options ) {
		var value = line.Content;
		return options.ComparisonPolicy.StripTrailingCarriageReturn && value.EndsWith( '\r' )
			? value[..^1]
			: value;
	}

	private static char GetDifferenceMarker( ComparisonLine? left, ComparisonLine? right ) {
		if ( null == left ) {
			return '>';
		}
		if ( null == right ) {
			return '<';
		}
		if ( left.HasLineTerminator == right.HasLineTerminator ) {
			return '|';
		}
		return left.HasLineTerminator ? '/' : '\\';
	}

	private static int FindOldPosition( IReadOnlyList<EditOperation> operations, int endExclusive ) {
		var position = 0;
		for ( var index = 0; index < endExclusive; index++ ) {
			if ( operations[index].OldIndex.HasValue ) {
				position++;
			}
		}
		return position;
	}

	private static int FindNewPosition( IReadOnlyList<EditOperation> operations, int endExclusive ) {
		var position = 0;
		for ( var index = 0; index < endExclusive; index++ ) {
			if ( operations[index].NewIndex.HasValue ) {
				position++;
			}
		}
		return position;
	}
}
