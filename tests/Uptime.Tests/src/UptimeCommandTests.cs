using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;
using Xunit;
using Tool = Icod.CoreUtils.Uptime.Command;

namespace Icod.CoreUtils.Uptime.Tests;

public sealed class UptimeCommandTests
{
    [Fact]
    public async Task DefaultOutputContainsUsersAndLoadAverages()
    {
        var output = new StringWriter();
        var context = Context(output);
        var exitCode = await Tool.RunAsync([], context, new FakeProvider());
        Assert.Equal(0, exitCode);
        Assert.Contains("2 users", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("load average: 1.25, 2.50, 3.75", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrettyAndSinceFormatsAreSupported()
    {
        var pretty = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["--pretty"], Context(pretty), new FakeProvider()));
        Assert.Equal(String.Concat("up 1 day, 2 hours, 3 minutes", Environment.NewLine), pretty.ToString());

        var since = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["--since"], Context(since), new FakeProvider()));
        Assert.Equal(String.Concat("2026-01-01 09:57:00", Environment.NewLine), since.ToString());
    }

    [Fact]
    public async Task RawOutputIsMachineReadable()
    {
        var output = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["--raw"], Context(output), new FakeProvider()));
        Assert.Equal(6, output.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task HelpAndVersionWork()
    {
        Assert.Equal(0, await Tool.RunAsync(["--help"], Context(new StringWriter()), new FakeProvider()));
        Assert.Equal(0, await Tool.RunAsync(["--version"], Context(new StringWriter()), new FakeProvider()));
    }

    [Fact]
    public async Task ExtraOperandFails()
    {
        var error = new StringWriter();
        Assert.Equal(1, await Tool.RunAsync(["extra"], Context(new StringWriter(), error), new FakeProvider()));
        Assert.Contains("extra operand", error.ToString(), StringComparison.Ordinal);
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
    ) => new("uptime", TextReader.Null, output, error ?? new StringWriter(), cancellationToken: cancellationToken);

    private sealed class FakeProvider : ISystemMetricsProvider
    {
        public ValueTask<SystemMetricsSnapshot> GetSnapshotAsync(
            bool container,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new SystemMetricsSnapshot(
                new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero),
                new TimeSpan(1, 2, 3, 0),
                2,
                1.25,
                2.5,
                3.75
            ));
        }
    }
}
