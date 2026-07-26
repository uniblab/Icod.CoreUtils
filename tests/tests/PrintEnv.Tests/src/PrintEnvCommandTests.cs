using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Icod.CoreUtils.PrintEnv.Tests;
using Tool = Icod.CoreUtils.PrintEnv.Command;
public sealed class PrintEnvCommandTests {
	[Fact] public async Task NamedValuesAndMissingStatus() { var n="ICOD_TEST_"+Guid.NewGuid().ToString("N"); Environment.SetEnvironmentVariable(n,"value"); try { var o=new StringWriter(); Assert.Equal(1,await Tool.RunAsync(new[]{n,n+"_MISSING"},stdout:o)); Assert.Equal("value"+Environment.NewLine,o.ToString()); } finally { Environment.SetEnvironmentVariable(n,null); } }
	[Fact] public async Task NullTermination() { var n="ICOD_TEST_"+Guid.NewGuid().ToString("N"); Environment.SetEnvironmentVariable(n,"v"); try { var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(new[]{"-0",n},stdout:o)); Assert.Equal("v\0",o.ToString()); } finally { Environment.SetEnvironmentVariable(n,null); } }
	[Fact] public async Task NoOperandsPrintsAssignments() { var n="ICOD_TEST_"+Guid.NewGuid().ToString("N"); Environment.SetEnvironmentVariable(n,"v"); try { var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(Array.Empty<string>(),stdout:o)); Assert.Contains(n+"=v",o.ToString()); } finally { Environment.SetEnvironmentVariable(n,null); } }
	[Fact] public async Task LargeNamedSetStreams() { var n="ICOD_TEST_"+Guid.NewGuid().ToString("N"); Environment.SetEnvironmentVariable(n,new string('x',1024)); try { var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(Enumerable.Repeat(n,2000).ToArray(),stdout:o)); Assert.True(o.ToString().Length>2_000_000); } finally { Environment.SetEnvironmentVariable(n,null); } }
	[Fact] public async Task CancellationReturns130() { using var c=new CancellationTokenSource(); c.Cancel(); Assert.Equal(130,await Tool.RunAsync(Array.Empty<string>(),cancellationToken:c.Token)); }
	[Fact] public async Task HelpVersionAndInvalidOption() { Assert.Equal(0,await Tool.RunAsync(new[]{"--help"},stdout:new StringWriter())); Assert.Equal(0,await Tool.RunAsync(new[]{"--version"},stdout:new StringWriter())); Assert.Equal(1,await Tool.RunAsync(new[]{"--no-such-option"},stderr:new StringWriter())); }
}
