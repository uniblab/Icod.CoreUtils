using Xunit;
namespace Icod.CoreUtils.Arch.Tests;
using Tool = Icod.CoreUtils.Arch.Command;
public sealed class ArchCommandTests {
	[Fact] public async Task PrintsArchitecture() { var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(Array.Empty<string>(),stdout:o)); Assert.False(string.IsNullOrWhiteSpace(o.ToString())); Assert.Single(o.ToString().Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries)); }
	[Fact] public async Task ExtraOperandFails() => Assert.Equal(1,await Tool.RunAsync(new[]{"x"},stderr:new StringWriter()));
	[Fact] public async Task HelpAndVersionWork() { Assert.Equal(0,await Tool.RunAsync(new[]{"--help"},stdout:new StringWriter())); Assert.Equal(0,await Tool.RunAsync(new[]{"--version"},stdout:new StringWriter())); }
	[Fact] public async Task CancellationReturns130() { using var c=new CancellationTokenSource(); c.Cancel(); Assert.Equal(130,await Tool.RunAsync(Array.Empty<string>(),cancellationToken:c.Token)); }
}
