namespace Icod.CoreUtils.Shared.Tests.Ranges;

using Icod.CoreUtils.Shared.Ranges;
using Xunit;

/// <summary>Tests normalized range sets, complement calculation, and sequential cursors.</summary>
public sealed class RangeSetTests {

	/// <summary>Verifies overlap merging without adjacent-range merging.</summary>
	[Fact]
	public void SetMergesOnlyOverlaps() {
		var set = new RangeSet(
			new[] {
				new InclusiveRange( 5, 8 ),
				new InclusiveRange( 1, 2 ),
				new InclusiveRange( 2, 4 ),
				new InclusiveRange( 9, 10 )
			}
		);
		Assert.Equal(
			new[] {
				new InclusiveRange( 1, 4 ),
				new InclusiveRange( 5, 8 ),
				new InclusiveRange( 9, 10 )
			},
			set.Ranges
		);
	}

	/// <summary>Verifies binary membership and explicit range starts.</summary>
	[Fact]
	public void SetReportsMembershipAndBoundaries() {
		var set = new RangeSet(
			new[] {
				new InclusiveRange( 2, 3 ),
				new InclusiveRange( 5, null )
			}
		);
		Assert.False( set.Contains( 1 ) );
		Assert.True( set.Contains( 2 ) );
		Assert.True( set.Contains( ulong.MaxValue ) );
		Assert.True( set.IsRangeStart( 2 ) );
		Assert.True( set.IsRangeStart( 5 ) );
		Assert.False( set.IsRangeStart( 3 ) );
	}

	/// <summary>Verifies bounded and unbounded complement calculation.</summary>
	[Fact]
	public void SetComputesComplements() {
		var set = new RangeSet(
			new[] {
				new InclusiveRange( 2, 4 ),
				new InclusiveRange( 7, 8 )
			}
		);
		Assert.Equal(
			new[] {
				new InclusiveRange( 1, 1 ),
				new InclusiveRange( 5, 6 ),
				new InclusiveRange( 9, null )
			},
			set.Complement( 1 ).Ranges
		);
		Assert.Equal(
			new[] {
				new InclusiveRange( 1, 1 ),
				new InclusiveRange( 5, 6 ),
				new InclusiveRange( 9, 10 )
			},
			set.Complement( 1, 10 ).Ranges
		);
	}

	/// <summary>Verifies that an open range exhausts the complement.</summary>
	[Fact]
	public void OpenRangeTerminatesComplement() {
		var set = new RangeSet( new[] { new InclusiveRange( 3, null ) } );
		Assert.Equal(
			new[] { new InclusiveRange( 1, 2 ) },
			set.Complement( 1 ).Ranges
		);
	}

	/// <summary>Verifies increasing and backward cursor movement.</summary>
	[Fact]
	public void CursorMatchesSequentialAndResetPositions() {
		var cursor = new RangeSet(
			new[] {
				new InclusiveRange( 2, 3 ),
				new InclusiveRange( 5, 5 )
			}
		).CreateCursor();
		Assert.False( cursor.Match( 1 ).IsSelected );
		var two = cursor.Match( 2 );
		Assert.True( two.IsSelected );
		Assert.True( two.IsRangeStart );
		Assert.True( cursor.Contains( 3 ) );
		Assert.False( cursor.Contains( 4 ) );
		Assert.True( cursor.IsRangeStart( 5 ) );
		Assert.True( cursor.IsRangeStart( 2 ) );
		cursor.Reset();
		Assert.True( cursor.Contains( 3 ) );
	}

	/// <summary>Verifies range and complement construction validation.</summary>
	[Fact]
	public void ModelsValidateEndpoints() {
		Assert.Throws<ArgumentOutOfRangeException>( () => new InclusiveRange( 2, 1 ) );
		Assert.Throws<ArgumentException>( () => new RangeMatch( false, true ) );
		var set = RangeSet.Empty;
		Assert.Throws<ArgumentOutOfRangeException>( () => set.Complement( 2, 1 ) );
		Assert.Empty( set.Ranges );
	}

}
