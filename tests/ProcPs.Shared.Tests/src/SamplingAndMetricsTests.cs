namespace Icod.ProcPs.Shared.Tests;

using Icod.ProcPs.Shared;
using Xunit;

public sealed class SamplingAndMetricsTests {
	[Fact]
	public void CounterDeltaHandlesWraparound() {
		Assert.Equal( 11UL, ProcCounterMath.Delta( 250, 5, 8 ) );
	}

	[Fact]
	public void CpuBusyPercentExcludesIdleDelta() {
		var before = new ProcCpuTimes( 10, 0, 10, 80, 0, 0, 0, 0, 0, 0 );
		var after = new ProcCpuTimes( 20, 0, 20, 160, 0, 0, 0, 0, 0, 0 );
		Assert.Equal( 20d, ProcCpuMath.BusyPercent( before, after ), 6 );
	}

	[Fact]
	public void LoadAverageParserReadsRunnableAndLastPid() {
		var load = LinuxProcSystemMetricsProvider.ParseLoadAverage( "0.10 0.20 0.30 2/100 4321\n" );
		Assert.Equal( 0.10, load.OneMinute );
		Assert.Equal( 2, load.Runnable );
		Assert.Equal( 100, load.TotalEntities );
		Assert.Equal( 4321, load.LastProcessId );
	}

	[Fact]
	public void CpuParserDoesNotDoubleCountGuestFieldsInTotal() {
		var cpu = LinuxProcSystemMetricsProvider.ParseCpu( "cpu 1 2 3 4 5 6 7 8 9 10\n" );
		Assert.Equal( 36UL, cpu.Total );
		Assert.Equal( 9UL, cpu.Guest );
		Assert.Equal( 10UL, cpu.GuestNice );
	}

	[Fact]
	public async Task LinuxProviderObservesUserSessionsWhenRunningOnLinux() {
		if ( !OperatingSystem.IsLinux() ) return;
		var provider = new LinuxProcSystemMetricsProvider();
		Assert.True( provider.Capabilities.HasFlag( ProcSystemCapabilities.UserSessions ) );
		var snapshot = await provider.GetSnapshotAsync();
		Assert.True( snapshot.UserSessions.HasValue, snapshot.UserSessions.Diagnostic );
		Assert.True( 0 <= snapshot.UserSessions.Value.Count );
	}
}
