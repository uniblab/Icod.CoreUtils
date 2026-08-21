using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CommandFramework.FileSystem.Traversal;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.RecursiveMutation;

/// <summary>Tests repeated E1 identity tracking.</summary>
public sealed class HardLinkIdentityTrackerTests {
	/// <summary>Verifies that the first path is returned for a repeated identity.</summary>
	[Fact]
	public void RetainsFirstPathForRepeatedIdentity() {
		var tracker = new HardLinkIdentityTracker();
		var identity = new FileSystemEntryIdentity( "test", "42" );
		Assert.Equal(
			HardLinkIdentityDisposition.First,
			tracker.Track( identity, "first-source", "first-destination", out var first )
		);
		Assert.Null( first );
		Assert.Equal(
			HardLinkIdentityDisposition.Repeated,
			tracker.Track( identity, "second-source", "second-destination", out first )
		);
		Assert.Equal( "first-source", first!.SourcePath );
		Assert.Equal( "first-destination", first.DestinationPath );
		Assert.Equal( 1, tracker.Count );
	}

	/// <summary>Verifies that unavailable identities are never retained.</summary>
	[Fact]
	public void DoesNotRetainUnavailableIdentity() {
		var tracker = new HardLinkIdentityTracker();
		Assert.Equal(
			HardLinkIdentityDisposition.Unavailable,
			tracker.Track( FileSystemEntryIdentity.Unavailable, "entry", null, out var first )
		);
		Assert.Null( first );
		Assert.Equal( 0, tracker.Count );
	}
}
