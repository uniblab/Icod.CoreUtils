using System.Runtime.CompilerServices;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;
using Xunit;
using Tool = Icod.CoreUtils.Pinky.Command;

namespace Icod.CoreUtils.Pinky.Tests;

public sealed class PinkyCommandTests
{
    [Fact]
    public async Task ShortFormatUsesInjectedSessions()
    {
        var output = new StringWriter();
        var exitCode = await Tool.RunAsync([], Context(output), new FakeProvider());
        Assert.Equal(0, exitCode);
        Assert.Contains("Login name", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("alice", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Alice Example", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("remote.example", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuietShortFormatOmitsHeadingsNameHostAndIdle()
    {
        var output = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["-q", "-f"], Context(output), new FakeProvider()));
        Assert.DoesNotContain("Login name", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Alice Example", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("remote.example", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LookupUsesProvider()
    {
        var output = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["--lookup"], Context(output), new FakeProvider()));
        Assert.Contains("canonical.remote.example", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LongFormatReadsProjectAndPlan()
    {
        var directory = Path.Combine(Path.GetTempPath(), String.Concat("pinky-tests-", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, ".project"), "CoreUtils");
            await File.WriteAllTextAsync(Path.Combine(directory, ".plan"), "Ship Batch 9");
            var output = new StringWriter();
            var provider = new FakeProvider(directory);
            Assert.Equal(0, await Tool.RunAsync(["-l", "alice"], Context(output), provider));
            Assert.Contains("Directory:", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("CoreUtils", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Ship Batch 9", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LongFormatRequiresAUser()
    {
        var error = new StringWriter();
        Assert.Equal(1, await Tool.RunAsync(["-l"], Context(new StringWriter(), error), new FakeProvider()));
        Assert.Contains("no username specified", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HelpAndVersionWork()
    {
        Assert.Equal(0, await Tool.RunAsync(["--help"], Context(new StringWriter()), new FakeProvider()));
        Assert.Equal(0, await Tool.RunAsync(["--version"], Context(new StringWriter()), new FakeProvider()));
    }

    [Fact]
    public async Task MissingLongFormatUserFails()
    {
        var error = new StringWriter();
        Assert.Equal(1, await Tool.RunAsync(["-l", "missing"], Context(new StringWriter(), error), new FakeProvider()));
        Assert.Contains("no such user", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationReturnsConventionalCode()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(130, await Tool.RunAsync([], Context(new StringWriter(), cancellationToken: cancellation.Token), new FakeProvider()));
    }

    private static CommandContext Context(
        TextWriter output,
        TextWriter? error = null,
        CancellationToken cancellationToken = default
    ) => new("pinky", TextReader.Null, output, error ?? new StringWriter(), cancellationToken: cancellationToken);

    private sealed class FakeProvider : IUserInformationProvider
    {
        private readonly string _home;

        public FakeProvider(string? home = null)
        {
            _home = home ?? "/home/alice";
        }

        public ValueTask<IReadOnlyList<UserAccountInfo>> GetAccountsAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<UserAccountInfo>>([
                new UserAccountInfo("alice", "Alice Example", _home, "/bin/sh", "1000", "1000"),
            ]);
        }

        public ValueTask<UserAccountInfo?> FindAccountAsync(
            string userName,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            UserAccountInfo? account = userName == "alice"
                ? new UserAccountInfo("alice", "Alice Example", _home, "/bin/sh", "1000", "1000")
                : null;
            return ValueTask.FromResult(account);
        }

        public async IAsyncEnumerable<LoginSessionInfo> GetLoginSessionsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new LoginSessionInfo(
                "alice",
                "pts/1",
                "remote.example",
                new DateTimeOffset(2026, 7, 27, 9, 0, 0, TimeSpan.Zero),
                TimeSpan.FromMinutes(4),
                42
            );
        }

        public ValueTask<string> ResolveHostAsync(
            string host,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult("canonical.remote.example");
        }
    }
}
