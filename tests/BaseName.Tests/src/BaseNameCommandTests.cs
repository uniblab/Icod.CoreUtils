using Xunit;
namespace Icod.CoreUtils.BaseName.Tests;
using Tool = Icod.CoreUtils.BaseName.Command;
public sealed class BaseNameCommandTests {
	[Theory] [InlineData("/usr/bin/sort", "sort")] [InlineData("a//b///", "b")] [InlineData("///", "/")] [InlineData("", "")]
	public async Task BasicForms( string input, string expected ) { var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(new[]{input},stdout:o)); Assert.Equal(expected+Environment.NewLine,o.ToString()); }
	[Fact] public async Task SuffixAndMultipleAndZero() { var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(new[]{"-a","-s",".cs","-z","a/x.cs","b/y.cs"},stdout:o)); Assert.Equal("x\0y\0",o.ToString()); }
	[Fact] public async Task LegacySuffixDoesNotRemoveWholeName() { var o=new StringWriter(); await Tool.RunAsync(new[]{"foo","foo"},stdout:o); Assert.Equal("foo"+Environment.NewLine,o.ToString()); }
	[Fact] public async Task LargeOperandSetStreams() { var a=Enumerable.Range(0,5000).Select(i=>$"/x/n{i}").Prepend("-a").ToArray(); var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(a,stdout:o)); Assert.Equal(5000,o.ToString().Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries).Length); }
	[Fact] public async Task MissingOperandFails() { var e=new StringWriter(); Assert.Equal(1,await Tool.RunAsync(Array.Empty<string>(),stderr:e)); Assert.Contains("missing operand",e.ToString()); }
	[Fact] public async Task CancellationReturns130() { using var c=new CancellationTokenSource(); c.Cancel(); Assert.Equal(130,await Tool.RunAsync(new[]{"x"},cancellationToken:c.Token)); }
	[Fact] public async Task HelpVersionAndInvalidOption() { Assert.Equal(0,await Tool.RunAsync(new[]{"--help"},stdout:new StringWriter())); Assert.Equal(0,await Tool.RunAsync(new[]{"--version"},stdout:new StringWriter())); Assert.Equal(1,await Tool.RunAsync(new[]{"--no-such-option"},stderr:new StringWriter())); }
}
