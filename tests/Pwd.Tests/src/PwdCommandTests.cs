using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Icod.CoreUtils.Pwd.Tests;
using Tool = Icod.CoreUtils.Pwd.Command;
public sealed class PwdCommandTests {
	[Fact] public async Task PhysicalPrintsCurrentDirectory() { var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(new[]{"-P"},stdout:o)); Assert.Equal( System.IO.Path.TrimEndingDirectorySeparator( System.IO.Path.GetFullPath(Directory.GetCurrentDirectory())),o.ToString().TrimEnd()); }
	[Fact] public async Task LogicalUsesValidPwd() { var old=Environment.GetEnvironmentVariable("PWD"); try { var cwd=System.IO.Path.GetFullPath(Directory.GetCurrentDirectory()); Environment.SetEnvironmentVariable("PWD",cwd); var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(new[]{"-L"},stdout:o)); Assert.Equal(cwd,o.ToString().TrimEnd()); } finally { Environment.SetEnvironmentVariable("PWD",old); } }
	[Fact] public async Task InvalidLogicalPwdFallsBack() { var old=Environment.GetEnvironmentVariable("PWD"); try { Environment.SetEnvironmentVariable("PWD","relative/../bad"); var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(new[]{"-L"},stdout:o)); Assert.True( System.IO.Path.IsPathRooted(o.ToString().TrimEnd())); } finally { Environment.SetEnvironmentVariable("PWD",old); } }
	[Fact] public async Task OperandsAreIgnoredLikeGnuPwd() { var o=new StringWriter(); Assert.Equal(0,await Tool.RunAsync(new[]{"ignored"},stdout:o)); Assert.False(string.IsNullOrWhiteSpace(o.ToString())); }
	[Fact] public async Task CancellationReturns130() { using var c=new CancellationTokenSource(); c.Cancel(); Assert.Equal(130,await Tool.RunAsync(Array.Empty<string>(),cancellationToken:c.Token)); }
	[Fact] public async Task HelpVersionAndInvalidOption() { Assert.Equal(0,await Tool.RunAsync(new[]{"--help"},stdout:new StringWriter())); Assert.Equal(0,await Tool.RunAsync(new[]{"--version"},stdout:new StringWriter())); Assert.Equal(1,await Tool.RunAsync(new[]{"--no-such-option"},stderr:new StringWriter())); }
}
