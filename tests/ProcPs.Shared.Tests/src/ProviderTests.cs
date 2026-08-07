namespace Icod.ProcPs.Shared.Tests;

using Icod.ProcPs.Shared;
using System.Globalization;
using Icod.CoreUtils.Shared.Processes;
using Xunit;

public sealed class ProviderTests {
	[Fact]
	public async Task FixtureProcRootProducesDetailedSnapshot() {
		var root = Path.Combine( Path.GetTempPath(), "icod-procps-" + Guid.NewGuid().ToString( "N" ) );
		var pid = Environment.ProcessId;
		var processRoot = Path.Combine( root, pid.ToString( CultureInfo.InvariantCulture ) );
		Directory.CreateDirectory( Path.Combine( processRoot, "ns" ) );
		Directory.CreateDirectory( Path.Combine( processRoot, "fd" ) );
		try {
			await File.WriteAllTextAsync( Path.Combine( processRoot, "stat" ), $"{pid} (fixture) S 1 {pid} {pid} 0 0 0 0 0 0 0 10 20 0 0 20 5 3 0 777 4096 2" );
			await File.WriteAllTextAsync( Path.Combine( processRoot, "status" ), "Uid:\t1000\t1001\t1002\t1003\nGid:\t2000\t2001\t2002\t2003\nNSpid:\t42\t7\n" );
			await File.WriteAllBytesAsync( Path.Combine( processRoot, "cmdline" ), new byte[] { (byte)'a', 0, (byte)'b', 0 } );
			await File.WriteAllTextAsync( Path.Combine( processRoot, "cgroup" ), "0::/system.slice/docker-0123456789abcdef.scope\n" );
			await File.WriteAllTextAsync( Path.Combine( processRoot, "maps" ), "00400000-00452000 r-xp 00000000 08:02 123 /tmp/file\n" );
			var provider = new LinuxProcProcessProvider( SystemProcessInspector.Instance, root );
			var observed = await provider.GetProcessAsync( pid );
			Assert.True( observed.HasValue, observed.Diagnostic );
			Assert.Equal( "fixture", observed.Value.CommandName.Value );
			Assert.Equal( new[] { "a", "b" }, observed.Value.CommandLineArguments.Value );
			Assert.Equal( 1001U, observed.Value.EffectiveUserId.Value );
			Assert.Equal( "0123456789abcdef", observed.Value.Container.Value.ContainerId );
			Assert.True( observed.Value.LifetimeStable.Value );
			var maps = await provider.GetMemoryMapsAsync( pid );
			Assert.True( maps.HasValue );
			Assert.Single( maps.Value );
		} finally {
			Directory.Delete( root, true );
		}
	}

	[Fact]
	public async Task SystemProviderCanObserveCurrentProcess() {
		var provider = SystemProcProcessProvider.Instance;
		var observed = await provider.GetProcessAsync( Environment.ProcessId );
		Assert.True( observed.HasValue, observed.Diagnostic );
		Assert.Equal( Environment.ProcessId, observed.Value.ProcessId );
		Assert.True( observed.Value.CommandName.HasValue );
	}
}
