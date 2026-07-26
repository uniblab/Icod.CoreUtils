using Xunit;
namespace Icod.CoreUtils.HostName.Tests;
using System.Net;
using Tool = Icod.CoreUtils.HostName.Command;
public sealed class HostNameCommandTests {
	[Fact] public async Task DefaultPrintsDnsHostName() { var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(Array.Empty<string>(),stdout:o)); Assert.Equal(Dns.GetHostName(),o.ToString().TrimEnd()); }
	[Fact] public async Task ShortPrintsFirstLabel() { var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(new[]{"-s"},stdout:o)); Assert.Equal(Dns.GetHostName().Split('.',2)[0],o.ToString().TrimEnd()); }
	[Fact] public async Task SettingFailsCleanly() { var e=new StringWriter(); Assert.Equal(1,await Tool.RunAsync(new[]{"newname"},stderr:e)); Assert.Contains("not supported",e.ToString()); }
	[Fact] public async Task FileSettingReadsAsynchronouslyThenFails() { var file=Path.GetTempFileName(); try { await File.WriteAllTextAsync(file,"host-from-file\n"); Assert.Equal(1,await Tool.RunAsync(new[]{"-F",file},stderr:new StringWriter())); } finally { File.Delete(file); } }
	[Fact] public async Task NisFailsCleanly() => Assert.Equal(1,await Tool.RunAsync(new[]{"-y"},stderr:new StringWriter()));
	[Fact] public async Task HelpAndVersionWork() { Assert.Equal(0,await Tool.RunAsync(new[]{"--help"},stdout:new StringWriter())); Assert.Equal(0,await Tool.RunAsync(new[]{"--version"},stdout:new StringWriter())); }
	[Fact] public async Task CancellationReturns130() { using var c=new CancellationTokenSource(); c.Cancel(); Assert.Equal(130,await Tool.RunAsync(Array.Empty<string>(),cancellationToken:c.Token)); }
}
