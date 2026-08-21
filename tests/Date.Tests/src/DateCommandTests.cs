using Icod.CommandFramework.Diagnostics;
using Icod.CoreUtils.Shared.Time;
using Xunit;
using Tool = Icod.CoreUtils.Date.Command;

namespace Icod.CoreUtils.Date.Tests;

public sealed class DateCommandTests
{
    [Fact]
    public async Task FormatsInjectedCurrentTime()
    {
        var output = new StringWriter();
        var exitCode = await Tool.RunAsync(
            ["--utc", "+%F %T %z"],
            Context(output),
            new FakeClock()
        );
        Assert.Equal(0, exitCode);
        Assert.Equal(String.Concat("2026-07-27 15:16:17 +0000", Environment.NewLine), output.ToString());
    }

    [Fact]
    public async Task ParsesEpochAndRelativeDates()
    {
        var epoch = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["--utc", "--date=@0", "+%s"], Context(epoch), new FakeClock()));
        Assert.Equal(String.Concat("0", Environment.NewLine), epoch.ToString());

        var relative = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["--utc", "--date=2 days", "+%F"], Context(relative), new FakeClock()));
        Assert.Equal(String.Concat("2026-07-29", Environment.NewLine), relative.ToString());
    }

    [Fact]
    public async Task ReadsDateExpressionsFromStandardInput()
    {
        var output = new StringWriter();
        var input = new StringReader(String.Concat("@0", Environment.NewLine, "@60", Environment.NewLine));
        var context = new CommandContext("date", input, output, new StringWriter());
        Assert.Equal(0, await Tool.RunAsync(["--utc", "--file=-", "+%s"], context, new FakeClock()));
        Assert.Equal(String.Concat("0", Environment.NewLine, "60", Environment.NewLine), output.ToString());
    }

    [Fact]
    public async Task IsoAndRfcFormatsAreSupported()
    {
        var iso = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["--utc", "--iso-8601=seconds"], Context(iso), new FakeClock()));
        Assert.Contains("2026-07-27T15:16:17+00:00", iso.ToString(), StringComparison.Ordinal);

        var rfc = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["--utc", "--rfc-email"], Context(rfc), new FakeClock()));
        Assert.Contains("+0000", rfc.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LargeDateFileStreamsAllRecords()
    {
        var inputText = String.Concat(
            String.Join(Environment.NewLine, Enumerable.Range(0, 2000).Select(index => String.Concat("@", index.ToString()))),
            Environment.NewLine
        );
        var output = new StringWriter();
        var context = new CommandContext("date", new StringReader(inputText), output, new StringWriter());
        Assert.Equal(0, await Tool.RunAsync(["--utc", "--file=-", "+%s"], context, new FakeClock()));
        Assert.Equal(2000, output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task HelpAndVersionWork()
    {
        Assert.Equal(0, await Tool.RunAsync(["--help"], Context(new StringWriter()), new FakeClock()));
        Assert.Equal(0, await Tool.RunAsync(["--version"], Context(new StringWriter()), new FakeClock()));
    }

    [Fact]
    public async Task SetFailureIsReportedCleanly()
    {
        var error = new StringWriter();
        Assert.Equal(1, await Tool.RunAsync(["--set=now"], Context(new StringWriter(), error), new FakeClock()));
        Assert.Contains("cannot set date", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationReturnsConventionalCode()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(130, await Tool.RunAsync([], Context(new StringWriter(), cancellationToken: cancellation.Token), new FakeClock()));
    }

    private static CommandContext Context(
        TextWriter output,
        TextWriter? error = null,
        CancellationToken cancellationToken = default
    ) => new("date", TextReader.Null, output, error ?? new StringWriter(), cancellationToken: cancellationToken);

    private sealed class FakeClock : IDateTimeProvider
    {
        public DateTimeOffset Now => new(2026, 7, 27, 11, 16, 17, TimeSpan.FromHours(-4));
        public DateTimeOffset UtcNow => new(2026, 7, 27, 15, 16, 17, TimeSpan.Zero);

        public ValueTask<bool> TrySetSystemTimeAsync(
            DateTimeOffset value,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(false);
        }
    }
}
