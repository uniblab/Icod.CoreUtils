using Xunit;
namespace Icod.CoreUtils.PathChk.Tests;
using Tool = Icod.CoreUtils.PathChk.Command;
public sealed class PathChkCommandTests {
	[Fact] public async Task PortableNamePasses() => Assert.Equal(0,await Tool.RunAsync(new[]{"-p","abc/DEF_12-3.txt"}));
	[Fact] public async Task NonportableNameFails() { var e=new StringWriter(); Assert.Equal(1,await Tool.RunAsync(new[]{"-p","a b"},stderr:e)); Assert.Contains("nonportable",e.ToString()); }
	[Fact] public async Task LeadingHyphenComponentFails() => Assert.Equal(1,await Tool.RunAsync(new[]{"-P","a/-b"},stderr:new StringWriter()));
	[Fact] public async Task PortabilityCombinesChecks() => Assert.Equal(1,await Tool.RunAsync(new[]{"--portability","a/-bad name"},stderr:new StringWriter()));
	[Fact] public async Task NonexistentDefaultPathIsValid() => Assert.Equal(0,await Tool.RunAsync(new[]{System.IO.Path.Combine( System.IO.Path.GetTempPath(),Guid.NewGuid().ToString("N"),"file")}));
	[Fact] public async Task LargeOperandSetCompletes() { var a=Enumerable.Range(0,5000).Select(i=>$"a{i}").ToArray(); Assert.Equal(0,await Tool.RunAsync(a)); }
	[Fact] public async Task CancellationReturns130() { using var c=new CancellationTokenSource(); c.Cancel(); Assert.Equal(130,await Tool.RunAsync(new[]{"a"},cancellationToken:c.Token)); }
	[Fact] public async Task HelpVersionAndInvalidOption() { Assert.Equal(0,await Tool.RunAsync(new[]{"--help"},stdout:new StringWriter())); Assert.Equal(0,await Tool.RunAsync(new[]{"--version"},stdout:new StringWriter())); Assert.Equal(1,await Tool.RunAsync(new[]{"--no-such-option"},stderr:new StringWriter())); }
}
