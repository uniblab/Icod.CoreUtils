using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;
using Xunit;
using Tool = Icod.CoreUtils.Ps.Command;

namespace Icod.CoreUtils.Ps.Tests;

public sealed class PsCommandTests
{
    [Fact]
    public async Task DefaultSelectionUsesCurrentUserAndTerminal()
    {
        var output = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync([], Context(output), new FakeProvider()));
        Assert.Contains("101", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("202", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllAndCustomFormatAreSupported()
    {
        var output = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["-A", "-o", "pid=PROCESS,ppid,user,args"], Context(output), new FakeProvider()));
        Assert.Contains("PROCESS", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("101", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("202", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("worker --jobs", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BsdAuxAndSortingAreSupported()
    {
        var output = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["aux", "--sort=-pid"], Context(output), new FakeProvider()));
        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("USER", lines[0], StringComparison.Ordinal);
        Assert.Contains("202", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuickPidPreservesRequestedOrder()
    {
        var output = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["-q", "202,101", "-o", "pid="], Context(output), new FakeProvider()));
        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("202", lines[0].Trim());
        Assert.Equal("101", lines[1].Trim());
    }

    [Fact]
    public async Task SelectionByCommandAndUserWorks()
    {
        var commandOutput = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["-C", "worker", "-o", "pid="], Context(commandOutput), new FakeProvider()));
        Assert.Equal(String.Concat("202", Environment.NewLine), commandOutput.ToString());

        var userOutput = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["-u", "alice", "-o", "pid="], Context(userOutput), new FakeProvider()));
        Assert.Contains("101", userOutput.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LargeProcessSetIsWrittenWithoutBufferingFailures()
    {
        var output = new StringWriter();
        Assert.Equal(0, await Tool.RunAsync(["-A", "-o", "pid="], Context(output), new LargeProvider(3000)));
        Assert.Equal(3000, output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task HelpAndVersionWork()
    {
        Assert.Equal(0, await Tool.RunAsync(["--help"], Context(new StringWriter()), new FakeProvider()));
        Assert.Equal(0, await Tool.RunAsync(["--version"], Context(new StringWriter()), new FakeProvider()));
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
    ) => new("ps", TextReader.Null, output, error ?? new StringWriter(), cancellationToken: cancellationToken);

    private sealed class LargeProvider : IProcessInformationProvider
    {
        private readonly int _count;

        public LargeProvider(int count)
        {
            _count = count;
        }

        public ValueTask<ProcessSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var captured = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
            var processes = Enumerable.Range(1, _count).Select(pid => new ProcessInfo
            {
                Pid = pid,
                ParentPid = 0,
                EffectiveUserId = "1000",
                RealUserId = "1000",
                EffectiveGroupId = "1000",
                RealGroupId = "1000",
                EffectiveUserName = "alice",
                RealUserName = "alice",
                EffectiveGroupName = "users",
                RealGroupName = "users",
                Terminal = "pts/1",
                State = "S",
                Command = "worker",
                Arguments = "worker",
            }).ToArray();
            return ValueTask.FromResult(new ProcessSnapshot(processes, 1, "1000", "alice", "pts/1", 1L, captured));
        }
    }

    private sealed class FakeProvider : IProcessInformationProvider
    {
        public ValueTask<ProcessSnapshot> GetSnapshotAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            var captured = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
            return ValueTask.FromResult(new ProcessSnapshot(
                [
                    CreateProcess(101, 1, "alice", "1000", "pts/1", "shell", "shell -l", captured.AddHours(-2)),
                    CreateProcess(202, 101, "bob", "1001", "?", "worker", "worker --jobs", captured.AddMinutes(-30)),
                ],
                101,
                "1000",
                "alice",
                "pts/1",
                8L * 1024L * 1024L * 1024L,
                captured
            ));
        }

        private static ProcessInfo CreateProcess(
            int pid,
            int parentPid,
            string user,
            string userId,
            string terminal,
            string command,
            string arguments,
            DateTimeOffset start
        ) => new()
        {
            Pid = pid,
            ParentPid = parentPid,
            ProcessGroupId = parentPid == 1 ? pid : parentPid,
            SessionId = parentPid == 1 ? pid : 101,
            EffectiveUserId = userId,
            RealUserId = userId,
            EffectiveGroupId = userId,
            RealGroupId = userId,
            EffectiveUserName = user,
            RealUserName = user,
            EffectiveGroupName = user,
            RealGroupName = user,
            Terminal = terminal,
            State = "S",
            Command = command,
            Arguments = arguments,
            StartTime = start,
            CpuTime = TimeSpan.FromSeconds(pid),
            Elapsed = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero) - start,
            WorkingSetBytes = pid * 1024L,
            VirtualMemoryBytes = pid * 4096L,
            ThreadCount = 2,
            Priority = 20,
            Nice = 0,
            CpuPercent = pid / 100.0,
            MemoryPercent = pid / 1000.0,
        };
    }
}
