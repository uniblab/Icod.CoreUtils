namespace Icod.CoreUtils.Ptx.Tests;

using Xunit;

/// <summary>Verifies that the command project retains its lowercase native apphost.</summary>
public sealed class NativeAppHostTests {
	/// <summary>Confirms the command assembly name remains exactly <c>ptx</c>.</summary>
	[Fact]
	public void CommandAssemblyNameIsLowercasePtx() {
		Assert.Equal( "ptx", typeof( Command ).Assembly.GetName().Name );
	}
}
