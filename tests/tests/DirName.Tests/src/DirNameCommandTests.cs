using Xunit;
namespace Icod.CoreUtils.DirName.Tests;
using Tool = Icod.CoreUtils.DirName.Command;
public sealed class DirNameCommandTests {
	[Theory] [InlineData("/usr/bin/sort", "/usr/bin")] [InlineData("a//b///", "a")] [InlineData("//a/b", "//a")] [InlineData("a", ".")] [InlineData("///", "/")]
	public async Task BasicForms( string input, string expected ) { var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(new[]{input},stdout:o)); Assert.Equal(expected+Environment.NewLine,o.ToString()); }
	[Fact] public async Task ZeroTerminatedMultiple() { var o=new StringWriter(); await Tool.RunAsync(new[]{"-z","a/b","c/d"},stdout:o); Assert.Equal("a\0c\0",o.ToString()); }
	[Fact] public async Task LargeOperandSetStreams() { var a=Enumerable.Range(0,5000).Select(i=>$"/x/n{i}").ToArray(); var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(a,stdout:o)); Assert.Equal(5000,o.ToString().Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries).Length); }
	[Fact] public async Task MissingOperandFails() { Assert.Equal(1,await Tool.RunAsync(Array.Empty<string>(),stderr:new StringWriter())); }
	[Fact] public async Task CancellationReturns130() { using var c=new CancellationTokenSource(); c.Cancel(); Assert.Equal(130,await Tool.RunAsync(new[]{"x"},cancellationToken:c.Token)); }
	[Fact] public async Task HelpVersionAndInvalidOption() { Assert.Equal(0,await Tool.RunAsync(new[]{"--help"},stdout:new StringWriter())); Assert.Equal(0,await Tool.RunAsync(new[]{"--version"},stdout:new StringWriter())); Assert.Equal(1,await Tool.RunAsync(new[]{"--no-such-option"},stderr:new StringWriter())); }
}
