using Path = global::System.IO.Path;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Icod.CoreUtils.Shared.Platform;

/// <summary>
/// Provides the process info implementation.
/// </summary>
public sealed class ProcessInfo
{
    /// <summary>
    /// Gets or sets the pid value.
    /// </summary>
    public required int Pid { get; init; }
    /// <summary>
    /// Gets or sets the parent pid value.
    /// </summary>
    public int ParentPid { get; init; }
    /// <summary>
    /// Gets or sets the process group id value.
    /// </summary>
    public int ProcessGroupId { get; init; }
    /// <summary>
    /// Gets or sets the session id value.
    /// </summary>
    public int SessionId { get; init; }
    /// <summary>
    /// Gets or sets the effective user id value.
    /// </summary>
    public required string EffectiveUserId { get; init; }
    /// <summary>
    /// Gets or sets the real user id value.
    /// </summary>
    public required string RealUserId { get; init; }
    /// <summary>
    /// Gets or sets the effective group id value.
    /// </summary>
    public required string EffectiveGroupId { get; init; }
    /// <summary>
    /// Gets or sets the real group id value.
    /// </summary>
    public required string RealGroupId { get; init; }
    /// <summary>
    /// Gets or sets the effective user name value.
    /// </summary>
    public required string EffectiveUserName { get; init; }
    /// <summary>
    /// Gets or sets the real user name value.
    /// </summary>
    public required string RealUserName { get; init; }
    /// <summary>
    /// Gets or sets the effective group name value.
    /// </summary>
    public required string EffectiveGroupName { get; init; }
    /// <summary>
    /// Gets or sets the real group name value.
    /// </summary>
    public required string RealGroupName { get; init; }
    /// <summary>
    /// Gets or sets the terminal value.
    /// </summary>
    public required string Terminal { get; init; }
    /// <summary>
    /// Gets or sets the state value.
    /// </summary>
    public required string State { get; init; }
    /// <summary>
    /// Gets or sets the command value.
    /// </summary>
    public required string Command { get; set; }
    /// <summary>
    /// Gets or sets the arguments value.
    /// </summary>
    public required string Arguments { get; set; }
    /// <summary>
    /// Gets or sets the environment value.
    /// </summary>
    public string Environment { get; init; } = String.Empty;
    /// <summary>
    /// Gets or sets the start time value.
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }
    /// <summary>
    /// Gets or sets the cpu time value.
    /// </summary>
    public TimeSpan CpuTime { get; init; }
    /// <summary>
    /// Gets or sets the elapsed value.
    /// </summary>
    public TimeSpan Elapsed { get; init; }
    /// <summary>
    /// Gets or sets the working set bytes value.
    /// </summary>
    public long WorkingSetBytes { get; init; }
    /// <summary>
    /// Gets or sets the virtual memory bytes value.
    /// </summary>
    public long VirtualMemoryBytes { get; init; }
    /// <summary>
    /// Gets or sets the thread count value.
    /// </summary>
    public int ThreadCount { get; init; }
    /// <summary>
    /// Gets or sets the priority value.
    /// </summary>
    public int Priority { get; init; }
    /// <summary>
    /// Gets or sets the nice value.
    /// </summary>
    public int Nice { get; init; }
    /// <summary>
    /// Gets or sets the cpu percent value.
    /// </summary>
    public double CpuPercent { get; init; }
    /// <summary>
    /// Gets or sets the memory percent value.
    /// </summary>
    public double MemoryPercent { get; init; }
}

/// <summary>
/// Represents process snapshot.
/// </summary>
/// <param name="Processes">The processes value.</param>
/// <param name="CurrentProcessId">The current process id value.</param>
/// <param name="CurrentUserId">The current user id value.</param>
/// <param name="CurrentUserName">The current user name value.</param>
/// <param name="CurrentTerminal">The current terminal value.</param>
/// <param name="TotalMemoryBytes">The total memory bytes value.</param>
/// <param name="CapturedAt">The captured at value.</param>
public sealed record ProcessSnapshot(
    IReadOnlyList<ProcessInfo> Processes,
    int CurrentProcessId,
    string CurrentUserId,
    string CurrentUserName,
    string CurrentTerminal,
    long TotalMemoryBytes,
    DateTimeOffset CapturedAt
);

/// <summary>
/// Supplies snapshots of processes visible to the current host.
/// </summary>
public interface IProcessInformationProvider
{
    /// <summary>Asynchronously captures the current process snapshot.</summary>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The captured process snapshot.</returns>
    ValueTask<ProcessSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Provides the system process information provider implementation.
/// </summary>
public sealed class SystemProcessInformationProvider : IProcessInformationProvider
{
    private readonly IUserInformationProvider _users;

    /// <summary>
    /// Performs the system process information provider operation.
    /// </summary>
    public SystemProcessInformationProvider(IUserInformationProvider? users = null)
    {
        _users = users ?? new SystemUserInformationProvider();
    }

    /// <summary>
    /// Gets snapshot async.
    /// </summary>
    public async ValueTask<ProcessSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var capturedAt = DateTimeOffset.Now;
        var totalMemory = await GetTotalMemoryAsync(cancellationToken).ConfigureAwait(false);
        var accounts = await _users.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
        var usersById = accounts
            .GroupBy(account => account.UserId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().UserName, StringComparer.Ordinal);
        var groupsById = await ReadGroupsAsync(cancellationToken).ConfigureAwait(false);
        var processes = new List<ProcessInfo>();

        foreach (var process in Process.GetProcesses().OrderBy(item => item.Id))
        {
            using (process)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = OperatingSystem.IsLinux()
                    ? await ReadLinuxProcessAsync(
                        process,
                        capturedAt,
                        totalMemory,
                        usersById,
                        groupsById,
                        cancellationToken
                    ).ConfigureAwait(false)
                    : ReadPortableProcess(process, capturedAt, totalMemory);
                if (info is not null)
                {
                    processes.Add(info);
                }
            }
        }

        var current = processes.FirstOrDefault(item => item.Pid == Environment.ProcessId);
        return new ProcessSnapshot(
            processes,
            Environment.ProcessId,
            current?.EffectiveUserId ?? Environment.UserName,
            current?.EffectiveUserName ?? Environment.UserName,
            current?.Terminal ?? GetTerminal(Environment.ProcessId),
            totalMemory,
            capturedAt
        );
    }

    private static async ValueTask<ProcessInfo?> ReadLinuxProcessAsync(
        Process process,
        DateTimeOffset capturedAt,
        long totalMemory,
        IReadOnlyDictionary<string, string> usersById,
        IReadOnlyDictionary<string, string> groupsById,
        CancellationToken cancellationToken
    )
    {
        var pid = process.Id;
        var directory = String.Concat("/proc/", pid.ToString(CultureInfo.InvariantCulture));
        try
        {
            var stat = await File.ReadAllTextAsync(Path.Combine(directory, "stat"), cancellationToken)
                .ConfigureAwait(false);
            if (!TryParseStat(stat, out var parsed))
            {
                return null;
            }

            var status = File.Exists(Path.Combine(directory, "status"))
                ? await File.ReadAllLinesAsync(Path.Combine(directory, "status"), cancellationToken)
                    .ConfigureAwait(false)
                : [];
            var ids = ParseStatus(status);
            var commandLine = await ReadNullDelimitedFileAsync(
                Path.Combine(directory, "cmdline"),
                cancellationToken
            ).ConfigureAwait(false);
            var environment = await ReadNullDelimitedFileAsync(
                Path.Combine(directory, "environ"),
                cancellationToken
            ).ConfigureAwait(false);
            var command = parsed.Command;
            if (String.IsNullOrEmpty(commandLine))
            {
                commandLine = String.Concat('[', command, ']');
            }

            DateTimeOffset? startTime = null;
            try
            {
                startTime = new DateTimeOffset(process.StartTime);
            }
            catch
            {
            }

            var cpuTime = TimeSpan.FromSeconds((parsed.UserTicks + parsed.SystemTicks) / 100.0);
            var elapsed = startTime.HasValue ? capturedAt - startTime.Value : TimeSpan.Zero;
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            var cpuPercent = elapsed.TotalSeconds <= 0.0
                ? 0.0
                : cpuTime.TotalSeconds / elapsed.TotalSeconds * 100.0;
            var memoryPercent = totalMemory <= 0L
                ? 0.0
                : ids.WorkingSetBytes * 100.0 / totalMemory;
            var realUserId = ids.RealUserId;
            var effectiveUserId = ids.EffectiveUserId;
            var realGroupId = ids.RealGroupId;
            var effectiveGroupId = ids.EffectiveGroupId;

            return new ProcessInfo
            {
                Pid = pid,
                ParentPid = parsed.ParentPid,
                ProcessGroupId = parsed.ProcessGroupId,
                SessionId = parsed.SessionId,
                EffectiveUserId = effectiveUserId,
                RealUserId = realUserId,
                EffectiveGroupId = effectiveGroupId,
                RealGroupId = realGroupId,
                EffectiveUserName = LookupName(usersById, effectiveUserId),
                RealUserName = LookupName(usersById, realUserId),
                EffectiveGroupName = LookupName(groupsById, effectiveGroupId),
                RealGroupName = LookupName(groupsById, realGroupId),
                Terminal = GetTerminal(pid),
                State = parsed.State.ToString(),
                Command = command,
                Arguments = commandLine,
                Environment = environment,
                StartTime = startTime,
                CpuTime = cpuTime,
                Elapsed = elapsed,
                WorkingSetBytes = ids.WorkingSetBytes,
                VirtualMemoryBytes = ids.VirtualMemoryBytes,
                ThreadCount = ids.ThreadCount > 0 ? ids.ThreadCount : parsed.ThreadCount,
                Priority = parsed.Priority,
                Nice = parsed.Nice,
                CpuPercent = cpuPercent,
                MemoryPercent = memoryPercent,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static ProcessInfo? ReadPortableProcess(
        Process process,
        DateTimeOffset capturedAt,
        long totalMemory
    )
    {
        try
        {
            DateTimeOffset? start = null;
            try
            {
                start = new DateTimeOffset(process.StartTime);
            }
            catch
            {
            }

            TimeSpan cpu = TimeSpan.Zero;
            try
            {
                cpu = process.TotalProcessorTime;
            }
            catch
            {
            }

            var elapsed = start.HasValue ? capturedAt - start.Value : TimeSpan.Zero;
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            long workingSet = 0L;
            long virtualMemory = 0L;
            int threadCount = 0;
            int priority = 0;
            try
            {
                workingSet = process.WorkingSet64;
                virtualMemory = process.VirtualMemorySize64;
                threadCount = process.Threads.Count;
                priority = process.BasePriority;
            }
            catch
            {
            }

            var user = Environment.UserName;
            return new ProcessInfo
            {
                Pid = process.Id,
                EffectiveUserId = user,
                RealUserId = user,
                EffectiveGroupId = String.Empty,
                RealGroupId = String.Empty,
                EffectiveUserName = user,
                RealUserName = user,
                EffectiveGroupName = String.Empty,
                RealGroupName = String.Empty,
                Terminal = Console.IsInputRedirected ? "?" : "console",
                State = process.HasExited ? "Z" : "S",
                Command = process.ProcessName,
                Arguments = process.ProcessName,
                StartTime = start,
                CpuTime = cpu,
                Elapsed = elapsed,
                WorkingSetBytes = workingSet,
                VirtualMemoryBytes = virtualMemory,
                ThreadCount = threadCount,
                Priority = priority,
                CpuPercent = elapsed.TotalSeconds <= 0.0
                    ? 0.0
                    : cpu.TotalSeconds / elapsed.TotalSeconds * 100.0,
                MemoryPercent = totalMemory <= 0L ? 0.0 : workingSet * 100.0 / totalMemory,
            };
        }
        catch
        {
            return null;
        }
    }

    private static async ValueTask<long> GetTotalMemoryAsync(CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsLinux() && File.Exists("/proc/meminfo"))
        {
            try
            {
                var lines = await File.ReadAllLinesAsync("/proc/meminfo", cancellationToken)
                    .ConfigureAwait(false);
                foreach (var line in lines)
                {
                    if (!line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length >= 2
                        && Int64.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kibibytes))
                    {
                        return checked(kibibytes * 1024L);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
    }

    private static async ValueTask<Dictionary<string, string>> ReadGroupsAsync(
        CancellationToken cancellationToken
    )
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists("/etc/group"))
        {
            return result;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync("/etc/group", cancellationToken)
                .ConfigureAwait(false);
            foreach (var line in lines)
            {
                var fields = line.Split(':');
                if (fields.Length >= 3 && !String.IsNullOrEmpty(fields[0]))
                {
                    result.TryAdd(fields[2], fields[0]);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }

        return result;
    }

    private static async ValueTask<string> ReadNullDelimitedFileAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        if (!File.Exists(path))
        {
            return String.Empty;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return String.Empty;
            }

            var text = Encoding.UTF8.GetString(bytes);
            return text.Replace('\0', ' ').TrimEnd();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return String.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return String.Empty;
        }
    }

    private static string GetTerminal(int pid)
    {
        if (!OperatingSystem.IsLinux())
        {
            return Console.IsInputRedirected ? "?" : "console";
        }

        try
        {
            var link = new FileInfo(
                String.Concat("/proc/", pid.ToString(CultureInfo.InvariantCulture), "/fd/0")
            ).LinkTarget;
            if (String.IsNullOrEmpty(link) || !link.StartsWith("/dev/", StringComparison.Ordinal))
            {
                return "?";
            }

            return link[5..];
        }
        catch
        {
            return "?";
        }
    }

    private static string LookupName(IReadOnlyDictionary<string, string> names, string id) =>
        names.TryGetValue(id, out var name) ? name : id;

    private static bool TryParseStat(string stat, out ParsedStat parsed)
    {
        parsed = default;
        var open = stat.IndexOf('(');
        var close = stat.LastIndexOf(')');
        if (open < 0 || close <= open || close + 2 >= stat.Length)
        {
            return false;
        }

        var command = stat[(open + 1)..close];
        var fields = stat[(close + 2)..]
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 20
            || fields[0].Length == 0
            || !Int32.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentPid)
            || !Int32.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var processGroupId)
            || !Int32.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sessionId)
            || !Int64.TryParse(fields[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out var userTicks)
            || !Int64.TryParse(fields[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out var systemTicks)
            || !Int32.TryParse(fields[15], NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority)
            || !Int32.TryParse(fields[16], NumberStyles.Integer, CultureInfo.InvariantCulture, out var nice)
            || !Int32.TryParse(fields[17], NumberStyles.Integer, CultureInfo.InvariantCulture, out var threadCount))
        {
            return false;
        }

        parsed = new ParsedStat(
            command,
            fields[0][0],
            parentPid,
            processGroupId,
            sessionId,
            userTicks,
            systemTicks,
            priority,
            nice,
            threadCount
        );
        return true;
    }

    private static ParsedStatus ParseStatus(IEnumerable<string> lines)
    {
        var realUserId = "0";
        var effectiveUserId = "0";
        var realGroupId = "0";
        var effectiveGroupId = "0";
        var threadCount = 0;
        var workingSet = 0L;
        var virtualMemory = 0L;

        foreach (var line in lines)
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2)
            {
                continue;
            }

            if (line.StartsWith("Uid:", StringComparison.Ordinal) && fields.Length >= 3)
            {
                realUserId = fields[1];
                effectiveUserId = fields[2];
            }
            else if (line.StartsWith("Gid:", StringComparison.Ordinal) && fields.Length >= 3)
            {
                realGroupId = fields[1];
                effectiveGroupId = fields[2];
            }
            else if (line.StartsWith("Threads:", StringComparison.Ordinal))
            {
                Int32.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out threadCount);
            }
            else if (line.StartsWith("VmRSS:", StringComparison.Ordinal))
            {
                if (Int64.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kibibytes))
                {
                    workingSet = kibibytes * 1024L;
                }
            }
            else if (line.StartsWith("VmSize:", StringComparison.Ordinal))
            {
                if (Int64.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kibibytes))
                {
                    virtualMemory = kibibytes * 1024L;
                }
            }
        }

        return new ParsedStatus(
            realUserId,
            effectiveUserId,
            realGroupId,
            effectiveGroupId,
            threadCount,
            workingSet,
            virtualMemory
        );
    }

    private readonly record struct ParsedStat(
        string Command,
        char State,
        int ParentPid,
        int ProcessGroupId,
        int SessionId,
        long UserTicks,
        long SystemTicks,
        int Priority,
        int Nice,
        int ThreadCount
    );

    private readonly record struct ParsedStatus(
        string RealUserId,
        string EffectiveUserId,
        string RealGroupId,
        string EffectiveGroupId,
        int ThreadCount,
        long WorkingSetBytes,
        long VirtualMemoryBytes
    );
}
