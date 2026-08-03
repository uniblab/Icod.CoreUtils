using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.RecursiveMutation;
/// <summary>Tests requested-versus-required E3 metadata policy.</summary>
public sealed class RecursiveMetadataPreservationPlanTests {
	/// <summary>Verifies that unavailable required ownership is reported without inventing values.</summary>
	[Fact]
	public void ReportsMissingRequiredMetadata() {
		var metadata = new FileSystemMetadata(
			"entry",
			FileSystemEntryKind.File,
			false,
			false,
			new FileSystemEntryIdentity( "test", "entry-1" ),
			new FileSystemIdentity( "test", "filesystem-1" )
		) {
			Mode = FileSystemMetadataValue<uint>.Available( 0x1A4 )
		};
		var required = RecursiveMetadataFields.Mode | RecursiveMetadataFields.Ownership;
		var plan = RecursiveMetadataPreservationPlan.Create(
			metadata,
			required,
			required
		);
		Assert.Equal( required, plan.Required );
		Assert.True( plan.Available.HasFlag( RecursiveMetadataFields.Mode ) );
		Assert.False( plan.Available.HasFlag( RecursiveMetadataFields.Ownership ) );
		Assert.Equal( RecursiveMetadataFields.Ownership, plan.MissingRequired );
		Assert.False( plan.CanProceed );
	}
	/// <summary>Verifies that requiring all timestamps reports each unavailable E3 timestamp independently.</summary>
	[Fact]
	public void ReportsIndividualMissingTimestamps() {
		var metadata = new FileSystemMetadata(
			"entry",
			FileSystemEntryKind.File,
			false,
			false,
			new FileSystemEntryIdentity( "test", "entry-2" ),
			new FileSystemIdentity( "test", "filesystem-1" )
		) {
			ModificationTime = FileSystemMetadataValue<DateTimeOffset>.Available( DateTimeOffset.UnixEpoch )
		};
		var plan = RecursiveMetadataPreservationPlan.Create(
			metadata,
			RecursiveMetadataFields.Timestamps,
			RecursiveMetadataFields.Timestamps
		);
		Assert.Equal( RecursiveMetadataFields.Timestamps, plan.Required );
		Assert.True( plan.Available.HasFlag( RecursiveMetadataFields.ModificationTime ) );
		Assert.Equal(
			RecursiveMetadataFields.AccessTime | RecursiveMetadataFields.BirthTime,
			plan.MissingRequired
		);
		Assert.False( plan.CanProceed );
	}
}
