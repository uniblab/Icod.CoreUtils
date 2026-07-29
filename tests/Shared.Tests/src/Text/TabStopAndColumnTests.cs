namespace Icod.CoreUtils.Shared.Tests.Text;

using Icod.CoreUtils.Shared.Text;
using Xunit;

/// <summary>Tests tab-stop lookup and checked display-column movement.</summary>
public sealed class TabStopAndColumnTests {
	/// <summary>Verifies the default recurring eight-column model.</summary>
	[Fact]
	public void DefaultStopsRepeatEveryEightColumns() {
		Assert.Equal<ulong?>( 8UL, TabStopSet.Default.GetNextStop( 0 ) );
		Assert.Equal<ulong?>( 8UL, TabStopSet.Default.GetNextStop( 7 ) );
		Assert.Equal<ulong?>( 16UL, TabStopSet.Default.GetNextStop( 8 ) );
		Assert.Equal( 8UL, TabStopSet.Default.MaximumDistance );
	}

	/// <summary>Verifies that an explicit list without continuation can be exhausted.</summary>
	[Fact]
	public void ExplicitStopsCanBeExhausted() {
		var stops = TabStopSet.Create(
			new ulong[] { 4, 8, 12 },
			TabStopContinuation.None
		);
		Assert.Equal<ulong?>( 4UL, stops.GetNextStop( 0 ) );
		Assert.Equal<ulong?>( 8UL, stops.GetNextStop( 4 ) );
		Assert.Equal<ulong?>( 12UL, stops.GetNextStop( 11 ) );
		Assert.Null( stops.GetNextStop( 12 ) );
		Assert.Equal( 4UL, stops.MaximumDistance );
	}

	/// <summary>Verifies that the maximum distance includes explicit gaps and recurrence.</summary>
	[Fact]
	public void MaximumDistanceCoversExplicitAndRecurringStops() {
		var stops = TabStopSet.Create(
			new ulong[] { 3, 11 },
			TabStopContinuation.Relative( 5 )
		);
		Assert.Equal( 8UL, stops.MaximumDistance );
	}

	/// <summary>Verifies that a tab-stop model defensively copies its explicit input.</summary>
	[Fact]
	public void ExplicitStopInputIsDefensivelyCopied() {
		var values = new ulong[] { 4, 8 };
		var stops = TabStopSet.Create(
			values,
			TabStopContinuation.None
		);
		values[0] = 2;
		Assert.Equal( 4UL, stops.ExplicitStops[0] );
	}

	/// <summary>Verifies validation of directly constructed tab-stop models.</summary>
	[Fact]
	public void DirectConstructionValidatesExplicitStops() {
		Assert.Throws<ArgumentException>(
			() => TabStopSet.Create(
				Array.Empty<ulong>(),
				TabStopContinuation.None
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TabStopSet.Create(
				new ulong[] { 0 },
				TabStopContinuation.None
			)
		);
		Assert.Throws<ArgumentException>(
			() => TabStopSet.Create(
				new ulong[] { 8, 4 },
				TabStopContinuation.None
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TabStopContinuation.Absolute( 0 )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TabStopContinuation.Relative( 0 )
		);
	}

	/// <summary>Verifies relative recurrence from column zero without explicit stops.</summary>
	[Fact]
	public void RelativeContinuationWithoutExplicitStopsUsesColumnZero() {
		var stops = TabStopSet.Create(
			Array.Empty<ulong>(),
			TabStopContinuation.Relative( 6 )
		);
		Assert.Equal<ulong?>( 6UL, stops.GetNextStop( 0 ) );
		Assert.Equal<ulong?>( 12UL, stops.GetNextStop( 6 ) );
		Assert.Equal( 6UL, stops.MaximumDistance );
	}

	/// <summary>Verifies checked overflow for an unrepresentable recurring stop.</summary>
	[Fact]
	public void RecurringStopOverflowIsControlled() {
		var stops = TabStopSet.Every( ulong.MaxValue );
		Assert.Equal<ulong?>( ulong.MaxValue, stops.GetNextStop( 0 ) );
		Assert.Throws<OverflowException>(
			() => stops.GetNextStop( ulong.MaxValue )
		);
		var state = new DisplayColumnState( ulong.MaxValue );
		Assert.Throws<OverflowException>(
			() => state.TryAdvanceToNextTabStop( stops )
		);
		Assert.Equal( ulong.MaxValue, state.Column );
	}

	/// <summary>Verifies forward, backspace, carriage-return, and reset behavior.</summary>
	[Fact]
	public void ColumnStateAppliesReusableMovementOperations() {
		var state = new DisplayColumnState();
		state.Advance( 6 );
		Assert.Equal( 6UL, state.Column );
		state.Backspace();
		Assert.Equal( 5UL, state.Column );
		state.Backspace( 3 );
		Assert.Equal( 2UL, state.Column );
		state.Backspace( 10 );
		Assert.Equal( 0UL, state.Column );
		state.Reset( 17 );
		state.CarriageReturn();
		Assert.Equal( 0UL, state.Column );
	}

	/// <summary>Verifies that column advancement uses checked arithmetic.</summary>
	[Fact]
	public void ColumnAdvanceUsesCheckedArithmetic() {
		var state = new DisplayColumnState( ulong.MaxValue );
		Assert.Throws<OverflowException>(
			() => state.Advance( 1UL )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new DisplayColumnState().Advance( -1 )
		);
	}

	/// <summary>Verifies advancing to a configured tab stop and preserving state after exhaustion.</summary>
	[Fact]
	public void ColumnStateAdvancesOnlyWhenTabStopExists() {
		var state = new DisplayColumnState( 3 );
		var stops = TabStopSet.Create(
			new ulong[] { 4, 8 },
			TabStopContinuation.None
		);
		Assert.True( state.TryAdvanceToNextTabStop( stops ) );
		Assert.Equal( 4UL, state.Column );
		Assert.True( state.TryAdvanceToNextTabStop( stops ) );
		Assert.Equal( 8UL, state.Column );
		Assert.False( state.TryAdvanceToNextTabStop( stops ) );
		Assert.Equal( 8UL, state.Column );
		Assert.Throws<ArgumentNullException>(
			() => state.TryAdvanceToNextTabStop( null! )
		);
	}
}
