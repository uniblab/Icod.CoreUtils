using Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.TransactionalReplacement;

/// <summary>Tests E6 containment and escape rejection.</summary>
public sealed class TransactionalReplacementPathSafetyTests {
	/// <summary>Verifies that a normalized descendant remains inside the transaction root.</summary>
	[Fact]
	public async Task AcceptsContainedDescendant() {
		var root = Path.GetFullPath( Path.Combine( "root", "scope" ) );
		var child = Path.Combine( root, "nested", "file" );
		var safety = new TransactionalReplacementPathSafety( HostComparison() );
		var result = await safety.RequireContainedAsync( root, child );
		Assert.Equal( Path.GetFullPath( child ), result );
	}

	/// <summary>Verifies that lexical parent traversal cannot escape the transaction root.</summary>
	[Fact]
	public async Task RejectsEscapingDestination() {
		var root = Path.GetFullPath( Path.Combine( "root", "scope" ) );
		var escaped = Path.GetFullPath( Path.Combine( root, "..", "outside" ) );
		var safety = new TransactionalReplacementPathSafety( HostComparison() );
		await Assert.ThrowsAsync<InvalidOperationException>(
			async () => {
				_ = await safety.RequireContainedAsync( root, escaped );
			}
		);
	}

	private static StringComparison HostComparison() {
		return OperatingSystem.IsWindows()
			? StringComparison.OrdinalIgnoreCase
			: StringComparison.Ordinal;
	}
}
