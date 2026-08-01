namespace Icod.DiffUtils.Shared.Tests;

using Icod.DiffUtils.Shared.Edits;
using Icod.DiffUtils.Shared.Lines;
using Icod.DiffUtils.Shared.Merge;
using Xunit;

/// <summary>Exercises reusable GNU Diffutils three-way comparison behavior.</summary>
public sealed class ThreeWayMergeTests {
	/// <summary>Equal inputs produce no connected change regions.</summary>
	[Fact]
	public void EqualInputsProduceNoRegions() {
		var lines = Lines( "same" );
		var comparison = ThreeWayMergeEngine.Compare( lines, lines, lines );
		Assert.Empty( comparison.Regions );
		Assert.False( comparison.HasConflicts );
	}

	/// <summary>One-line inputs exercise all four three-way classifications.</summary>
	[Theory]
	[InlineData( "mine", "base", "base", ThreeWayChangeKind.MineOnly )]
	[InlineData( "same", "older", "same", ThreeWayChangeKind.OlderOnly )]
	[InlineData( "base", "base", "yours", ThreeWayChangeKind.YoursOnly )]
	[InlineData( "mine", "older", "yours", ThreeWayChangeKind.Overlap )]
	public void ClassifiesConnectedRegion(
		string mine,
		string older,
		string yours,
		ThreeWayChangeKind expected
	) {
		var comparison = ThreeWayMergeEngine.Compare( Lines( mine ), Lines( older ), Lines( yours ) );
		var region = Assert.Single( comparison.Regions );
		Assert.Equal( expected, region.Kind );
	}

	/// <summary>The historical normal-report mapping can use the third input as common.</summary>
	[Fact]
	public void ThirdCommonInputAlignsRepeatedLinesLikeGnuDiff3() {
		var comparison = ThreeWayMergeEngine.Compare(
			Lines( "A" ),
			Lines( "A", "A" ),
			Lines( "B", "A" ),
			ThreeWayCommonInput.Third
		);
		var region = Assert.Single( comparison.Regions );
		Assert.Equal( ThreeWayChangeKind.Overlap, region.Kind );
		Assert.Equal( 0, region.MineStart );
		Assert.Empty( region.MineLines );
		Assert.Equal( 0, region.OlderStart );
		Assert.Equal( new[] { "A" }, region.OlderLines.Select( line => line.Content ) );
		Assert.Equal( 0, region.YoursStart );
		Assert.Equal( new[] { "B" }, region.YoursLines.Select( line => line.Content ) );
	}

	/// <summary>Interacting changes are coalesced into one overlap region.</summary>
	[Fact]
	public void CoalescesInteractingChanges() {
		var comparison = ThreeWayMergeEngine.Compare(
			Lines( "one", "mine-a", "mine-b", "four" ),
			Lines( "one", "old-a", "old-b", "four" ),
			Lines( "one", "yours-a", "yours-b", "four" )
		);
		var region = Assert.Single( comparison.Regions );
		Assert.Equal( ThreeWayChangeKind.Overlap, region.Kind );
		Assert.Equal( 1, region.MineStart );
		Assert.Equal( 2, region.MineLines.Count );
		Assert.True( comparison.HasConflicts );
		Assert.True( comparison.HasOverlaps );
	}

	/// <summary>Comparison policies are honored during both alignment and classification.</summary>
	[Fact]
	public void StripTrailingCarriageReturnCanMakeInputsEquivalent() {
		var comparison = ThreeWayMergeEngine.Compare(
			Lines( "same\r" ),
			Lines( "same" ),
			Lines( "same" ),
			new LineComparisonPolicy { StripTrailingCarriageReturn = true }
		);
		Assert.Empty( comparison.Regions );
	}

	/// <summary>The shared two-way tie-break follows GNU's repeated-line alignment.</summary>
	[Fact]
	public void RepeatedLineTieBreakPrefersLeadingDeletion() {
		var script = LineDiffEngine.Compare( Lines( "A", "B", "B" ), Lines( "B", "A", "B" ) );
		Assert.Collection(
			script.Operations,
			operation => {
				Assert.Equal( EditOperationKind.Delete, operation.Kind );
				Assert.Equal( 0, operation.OldIndex );
			},
			operation => Assert.Equal( EditOperationKind.Equal, operation.Kind ),
			operation => {
				Assert.Equal( EditOperationKind.Insert, operation.Kind );
				Assert.Equal( 1, operation.NewIndex );
			},
			operation => Assert.Equal( EditOperationKind.Equal, operation.Kind )
		);
	}

	/// <summary>Pre-cancellation is observed before comparison work begins.</summary>
	[Fact]
	public void PreCanceledComparisonThrows() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		Assert.Throws<OperationCanceledException>( () => ThreeWayMergeEngine.Compare(
			Lines( "mine" ),
			Lines( "old" ),
			Lines( "yours" ),
			cancellationToken: cancellation.Token
		) );
	}

	private static IReadOnlyList<ComparisonLine> Lines( params string[] values ) {
		return values.Select( value => new ComparisonLine( value, true ) ).ToArray();
	}
}
