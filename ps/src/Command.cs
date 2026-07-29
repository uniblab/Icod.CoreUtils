using System.Globalization;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;

namespace Icod.CoreUtils.Ps;

/// <summary>
/// Implements GNU-compatible <c>ps</c> and prints process information obtained from the shared platform provider.
/// </summary>
/// <remarks>
/// Process enumeration is supplied through an injectable provider for cross-platform behavior and deterministic tests.
/// </remarks>
public static class Command
{
    private const string ProgramName = "ps";
    private const string Version = "ps (Icod.CoreUtils) 1.0";

    /// <summary>
    /// Executes <c>ps</c> synchronously with optional standard-stream substitution.
    /// </summary>
    /// <remarks>
    /// This compatibility entry point blocks on the TAP implementation. A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream; caller-supplied streams remain caller-owned.
    /// </remarks>
    /// <param name="args">The command-line arguments, excluding the executable name.</param>
    /// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
    /// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
    /// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
    /// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
    public static int Run(
        string[] args,
        TextReader? stdin = null,
        TextWriter? stdout = null,
        TextWriter? stderr = null
    ) => RunAsync(args, stdin, stdout, stderr).GetAwaiter().GetResult();

    /// <summary>
    /// Executes <c>ps</c> asynchronously with optional injected standard streams.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream. Caller-supplied streams remain caller-owned.
    /// </remarks>
    /// <param name="args">The command-line arguments, excluding the executable name.</param>
    /// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
    /// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
    /// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
    /// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
    /// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
    public static Task<int> RunAsync(
        string[] args,
        TextReader? stdin = null,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        CancellationToken cancellationToken = default
    ) => RunAsync(
        args ?? [],
        new CommandContext(
            ProgramName,
            stdin ?? Console.In,
            stdout ?? Console.Out,
            stderr ?? Console.Error,
            cancellationToken: cancellationToken
        ),
        new SystemProcessInformationProvider()
    );

    /// <summary>
    /// Executes <c>ps</c> asynchronously using a complete shared command context.
    /// </summary>
    /// <remarks>
    /// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
    /// </remarks>
    /// <param name="args">The command-line arguments, excluding the executable name.</param>
    /// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
    /// <param name="provider">The injectable process-information provider; <see langword="null"/> selects the system implementation when supported by this overload.</param>
    /// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static async Task<int> RunAsync(
        string[] args,
        CommandContext context,
        IProcessInformationProvider provider
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(provider);

        var normalized = NormalizeArguments(args);
        var parser = CreateParser(
            new OptionDefinition("all", 'A', ["all", "everyone"]),
            new OptionDefinition("all-terminals", 'a'),
            new OptionDefinition("deselect", 'N', ["deselect"]),
            new OptionDefinition("no-leaders", 'd'),
            new OptionDefinition("command", 'C', ["command"], OptionValueArity.Required),
            new OptionDefinition("group", 'G', ["Group"], OptionValueArity.Required),
            new OptionDefinition("pgrp", 'g', ["group"], OptionValueArity.Required),
            new OptionDefinition("pid", 'p', ["pid"], OptionValueArity.Required),
            new OptionDefinition("quick-pid", 'q', ["quick-pid"], OptionValueArity.Required),
            new OptionDefinition("ppid", null, ["ppid"], OptionValueArity.Required),
            new OptionDefinition("sid", 's', ["sid"], OptionValueArity.Required),
            new OptionDefinition("tty", 't', ["tty"], OptionValueArity.Required),
            new OptionDefinition("effective-user", 'u', ["user"], OptionValueArity.Required),
            new OptionDefinition("real-user", 'U', ["User"], OptionValueArity.Required),
            new OptionDefinition("include-no-terminal", 'x'),
            new OptionDefinition("running", 'r'),
            new OptionDefinition("full", 'f'),
            new OptionDefinition("extra-full", 'F'),
            new OptionDefinition("long", 'l'),
            new OptionDefinition("jobs", 'j'),
            new OptionDefinition("format", 'o', ["format"], OptionValueArity.Required),
            new OptionDefinition("predefined-format", 'O', valueArity: OptionValueArity.Required),
            new OptionDefinition("forest", 'H', ["forest"]),
            new OptionDefinition("sort", null, ["sort"], OptionValueArity.Required),
            new OptionDefinition("no-headers", null, ["no-headers", "no-heading"]),
            new OptionDefinition("headers", null, ["headers"]),
            new OptionDefinition("columns", null, ["cols", "columns", "width"], OptionValueArity.Required),
            new OptionDefinition("lines", null, ["lines", "rows"], OptionValueArity.Required),
            new OptionDefinition("bsd-a", null, ["bsd-a"]),
            new OptionDefinition("bsd-x", null, ["bsd-x"]),
            new OptionDefinition("bsd-u", null, ["bsd-u"]),
            new OptionDefinition("bsd-v", null, ["bsd-v"]),
            new OptionDefinition("bsd-j", null, ["bsd-j"]),
            new OptionDefinition("bsd-l", null, ["bsd-l"]),
            new OptionDefinition("bsd-f", null, ["bsd-f"]),
            new OptionDefinition("bsd-c", null, ["bsd-c"]),
            new OptionDefinition("bsd-e", null, ["bsd-e"]),
            new OptionDefinition("bsd-h", null, ["bsd-h"]),
            new OptionDefinition("bsd-w", null, ["bsd-w"]),
            new OptionDefinition("current-terminal", 'T', ["current-terminal"]),
            new OptionDefinition("help", null, ["help"], OptionValueArity.Optional),
            new OptionDefinition("version", null, ["version"])
        );

        try
        {
            var result = parser.Parse(normalized);
            if (await WriteParseErrorsAsync(result, context).ConfigureAwait(false))
            {
                return CommandExitCodes.Failure;
            }

            if (result.HasOption("help"))
            {
                const string help = """
Usage: ps [OPTION]...
Report a snapshot of current processes.
Selection by list:
  -A, -e, --all           select all processes
  -a                      select processes with a terminal except session leaders
  -d                      select all except session leaders
  -N, --deselect          negate the selection
  -C, --command=LIST      select by command name
  -G, --Group=LIST        select by real group name or ID
  -g, --group=LIST        select by process group or session
  -p, --pid=LIST          select by process ID
  -q, --quick-pid=LIST    select by process ID and preserve list order
      --ppid=LIST         select by parent process ID
  -s, --sid=LIST          select by session ID
  -t, --tty=LIST          select by terminal
  -u, --user=LIST         select by effective user name or ID
  -U, --User=LIST         select by real user name or ID
  -x                      include processes without a controlling terminal
  -r                      select only running processes
  -T                      select processes on this terminal
Output formats:
  -f, -F, -l, -j          full, extra-full, long, or jobs format
  -o, --format=FORMAT     user-defined format
  -O FORMAT               predefined columns plus FORMAT
  -H, --forest            show process hierarchy
      --sort=SPEC         sort by comma-separated keys; prefix with - to reverse
      --no-headers        suppress headings
      --headers           repeat headings
      --help[=SECTION]    display this help and exit
      --version           output version information and exit
BSD-style option groups such as ax, aux, j, l, and v are also accepted.
""";
                await context.StandardOutput.WriteAsync(
                    help.ReplaceLineEndings(Environment.NewLine).AsMemory(),
                    context.CancellationToken
                ).ConfigureAwait(false);
                return CommandExitCodes.Success;
            }

            if (result.HasOption("version"))
            {
                await context.StandardOutput.WriteLineAsync(
                    Version.AsMemory(),
                    context.CancellationToken
                ).ConfigureAwait(false);
                return CommandExitCodes.Success;
            }

            if (result.Operands.Count > 0)
            {
                await context.Diagnostics.ErrorAsync(
                    String.Concat("unsupported operand '", result.Operands[0], "'"),
                    context.CancellationToken
                ).ConfigureAwait(false);
                return CommandExitCodes.Failure;
            }

            var snapshot = await provider.GetSnapshotAsync(context.CancellationToken)
                .ConfigureAwait(false);
            var processes = SelectProcesses(snapshot, result).ToList();
            if (result.HasOption("quick-pid"))
            {
                processes = OrderQuick(processes, result.GetOccurrences("quick-pid")).ToList();
            }
            else
            {
                processes = SortProcesses(processes, result).ToList();
            }

            if (result.HasOption("bsd-c"))
            {
                foreach (var process in processes)
                {
                    process.Arguments = process.Command;
                }
            }
            if (result.HasOption("bsd-e"))
            {
                foreach (var process in processes)
                {
                    if (!String.IsNullOrEmpty(process.Environment))
                    {
                        process.Arguments = String.Concat(
                            process.Arguments,
                            " ",
                            process.Environment
                        ).Trim();
                    }
                }
            }
            if (result.HasOption("forest") || result.HasOption("bsd-f"))
            {
                ApplyForest(processes);
            }

            var columns = ResolveColumns(result);
            var rows = processes.Select(process => columns.Select(column => column.GetValue(process, snapshot)).ToArray()).ToList();
            var widths = CalculateWidths(columns, rows);
            var noHeaders = result.HasOption("no-headers")
                || result.HasOption("bsd-h")
                || columns.All(column => String.IsNullOrEmpty(column.Header));

            if (!noHeaders)
            {
                await WriteRowAsync(
                    columns.Select(column => column.Header).ToArray(),
                    columns,
                    widths,
                    context
                ).ConfigureAwait(false);
            }

            foreach (var row in rows)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                await WriteRowAsync(row, columns, widths, context).ConfigureAwait(false);
            }

            return CommandExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return CommandExitCodes.Canceled;
        }
    }

    private static IEnumerable<ProcessInfo> SelectProcesses(
        ProcessSnapshot snapshot,
        OptionParseResult result
    )
    {
        var explicitSelection = result.Options.Any(option => SelectionKeys.Contains(option.Definition.Key));
        IEnumerable<ProcessInfo> selected;
        if (!explicitSelection)
        {
            var hasBsdStyle = result.Options.Any(option =>
                option.Definition.Key.StartsWith("bsd-", StringComparison.Ordinal)
            );
            selected = snapshot.Processes.Where(process =>
                String.Equals(process.EffectiveUserId, snapshot.CurrentUserId, StringComparison.Ordinal)
                && (hasBsdStyle
                    ? process.Terminal != "?"
                    : String.Equals(process.Terminal, snapshot.CurrentTerminal, StringComparison.Ordinal))
            );
        }
        else
        {
            var predicates = BuildSelectionPredicates(snapshot, result);
            selected = snapshot.Processes.Where(process => predicates.Any(predicate => predicate(process)));
        }

        if (result.HasOption("deselect"))
        {
            var chosen = selected.Select(process => process.Pid).ToHashSet();
            selected = snapshot.Processes.Where(process => !chosen.Contains(process.Pid));
        }

        if (result.HasOption("running"))
        {
            selected = selected.Where(process => process.State.StartsWith("R", StringComparison.Ordinal));
        }

        return selected;
    }

    private static IReadOnlyList<Func<ProcessInfo, bool>> BuildSelectionPredicates(
        ProcessSnapshot snapshot,
        OptionParseResult result
    )
    {
        var predicates = new List<Func<ProcessInfo, bool>>();
        if (result.HasOption("all") || result.HasOption("bsd-a") && result.HasOption("bsd-x"))
        {
            predicates.Add(_ => true);
        }
        if (result.HasOption("all-terminals") || result.HasOption("bsd-a"))
        {
            predicates.Add(process => process.Terminal != "?" && process.Pid != process.SessionId);
        }
        if (result.HasOption("no-leaders"))
        {
            predicates.Add(process => process.Pid != process.SessionId);
        }
        if ((result.HasOption("bsd-x") || result.HasOption("include-no-terminal"))
            && !(result.HasOption("bsd-a") && result.HasOption("bsd-x")))
        {
            predicates.Add(process => String.Equals(
                process.EffectiveUserId,
                snapshot.CurrentUserId,
                StringComparison.Ordinal
            ));
        }
        if (result.HasOption("current-terminal"))
        {
            predicates.Add(process => String.Equals(
                process.Terminal,
                snapshot.CurrentTerminal,
                StringComparison.Ordinal
            ));
        }

        AddStringPredicate(result, "command", predicates, process => process.Command);
        AddStringPredicate(result, "group", predicates, process => process.RealGroupName, process => process.RealGroupId);
        AddIntPredicate(result, "pgrp", predicates, process => process.ProcessGroupId);
        AddIntPredicate(result, "pid", predicates, process => process.Pid);
        AddIntPredicate(result, "quick-pid", predicates, process => process.Pid);
        AddIntPredicate(result, "ppid", predicates, process => process.ParentPid);
        AddIntPredicate(result, "sid", predicates, process => process.SessionId);
        AddStringPredicate(result, "tty", predicates, process => NormalizeTerminal(process.Terminal));
        AddStringPredicate(result, "effective-user", predicates, process => process.EffectiveUserName, process => process.EffectiveUserId);
        AddStringPredicate(result, "real-user", predicates, process => process.RealUserName, process => process.RealUserId);
        return predicates;
    }

    private static void AddIntPredicate(
        OptionParseResult result,
        string key,
        ICollection<Func<ProcessInfo, bool>> predicates,
        Func<ProcessInfo, int> selector
    )
    {
        var values = ParseList(result.GetOccurrences(key).Select(option => option.Value))
            .Select(value => Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : Int32.MinValue)
            .ToHashSet();
        if (values.Count > 0)
        {
            predicates.Add(process => values.Contains(selector(process)));
        }
    }

    private static void AddStringPredicate(
        OptionParseResult result,
        string key,
        ICollection<Func<ProcessInfo, bool>> predicates,
        params Func<ProcessInfo, string>[] selectors
    )
    {
        var values = ParseList(result.GetOccurrences(key).Select(option => option.Value))
            .ToHashSet(StringComparer.Ordinal);
        if (values.Count > 0)
        {
            predicates.Add(process => selectors.Any(selector => values.Contains(selector(process))));
        }
    }

    private static IEnumerable<string> ParseList(IEnumerable<string?> values) => values
        .Where(value => value is not null)
        .SelectMany(value => value!.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static IEnumerable<ProcessInfo> OrderQuick(
        IEnumerable<ProcessInfo> processes,
        IEnumerable<OptionOccurrence> occurrences
    )
    {
        var order = ParseList(occurrences.Select(option => option.Value))
            .Select((value, index) => new { value, index })
            .Where(item => Int32.TryParse(item.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .GroupBy(item => Int32.Parse(item.value, CultureInfo.InvariantCulture))
            .ToDictionary(group => group.Key, group => group.First().index);
        return processes.OrderBy(process => order.TryGetValue(process.Pid, out var index) ? index : Int32.MaxValue);
    }

    private static IEnumerable<ProcessInfo> SortProcesses(
        IEnumerable<ProcessInfo> processes,
        OptionParseResult result
    )
    {
        var specification = String.Join(",", result.GetOccurrences("sort").Select(option => option.Value));
        if (String.IsNullOrWhiteSpace(specification))
        {
            return processes.OrderBy(process => process.Pid);
        }

        IOrderedEnumerable<ProcessInfo>? ordered = null;
        foreach (var rawKey in specification.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var descending = rawKey.StartsWith('-');
            var key = rawKey.TrimStart('+', '-');
            Func<ProcessInfo, IComparable> selector = key switch
            {
                "pid" => process => process.Pid,
                "ppid" => process => process.ParentPid,
                "pgid" or "pgrp" => process => process.ProcessGroupId,
                "sid" or "sess" => process => process.SessionId,
                "user" or "euser" => process => process.EffectiveUserName,
                "uid" or "euid" => process => process.EffectiveUserId,
                "group" or "egroup" => process => process.EffectiveGroupName,
                "tty" or "tname" => process => process.Terminal,
                "start" or "start_time" => process => process.StartTime ?? DateTimeOffset.MinValue,
                "time" or "cputime" => process => process.CpuTime,
                "pcpu" or "%cpu" => process => process.CpuPercent,
                "pmem" or "%mem" => process => process.MemoryPercent,
                "rss" => process => process.WorkingSetBytes,
                "vsz" or "vsize" => process => process.VirtualMemoryBytes,
                "comm" or "ucmd" => process => process.Command,
                _ => process => process.Pid,
            };

            ordered = ordered is null
                ? descending
                    ? processes.OrderByDescending(selector)
                    : processes.OrderBy(selector)
                : descending
                    ? ordered.ThenByDescending(selector)
                    : ordered.ThenBy(selector);
        }

        return ordered ?? processes.OrderBy(process => process.Pid);
    }

    private static List<Column> ResolveColumns(OptionParseResult result)
    {
        var custom = result.GetOccurrences("format").Select(option => option.Value).Where(value => value is not null).ToArray();
        if (custom.Length > 0)
        {
            return ParseColumns(String.Join(",", custom));
        }

        var predefined = result.GetLastValue("predefined-format");
        if (predefined is not null)
        {
            var columns = ParseColumns("pid,tty,time,cmd");
            columns.InsertRange(1, ParseColumns(predefined));
            return columns;
        }

        if (result.HasOption("bsd-u"))
        {
            return ParseColumns("user,pid,pcpu,pmem,vsz,rss,tty,stat,start,time,args");
        }
        if (result.HasOption("bsd-v"))
        {
            return ParseColumns("pid,tty,stat,time,vsz,rss,pmem,args");
        }
        if (result.HasOption("jobs") || result.HasOption("bsd-j"))
        {
            return ParseColumns("pid,pgid,sid,tty,time,cmd");
        }
        if (result.HasOption("long") || result.HasOption("bsd-l"))
        {
            return ParseColumns("stat,uid,pid,ppid,pcpu,pri,ni,vsz,rss,tty,time,cmd");
        }
        if (result.HasOption("extra-full"))
        {
            return ParseColumns("uid,pid,ppid,pcpu,sz,rss,psr,stime,tty,time,args");
        }
        if (result.HasOption("full"))
        {
            return ParseColumns("uid,pid,ppid,pcpu,stime,tty,time,args");
        }

        return ParseColumns("pid,tty,time,cmd");
    }

    private static List<Column> ParseColumns(string specification)
    {
        var output = new List<Column>();
        foreach (var token in specification.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = token.IndexOf('=');
            var nameAndWidth = equals >= 0 ? token[..equals] : token;
            var header = equals >= 0 ? token[(equals + 1)..] : null;
            var colon = nameAndWidth.LastIndexOf(':');
            int? width = null;
            if (colon > 0
                && Int32.TryParse(nameAndWidth.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedWidth))
            {
                width = parsedWidth;
                nameAndWidth = nameAndWidth[..colon];
            }

            output.Add(CreateColumn(nameAndWidth, header, width));
        }

        if (output.Count == 0)
        {
            output.Add(CreateColumn("pid", null, null));
        }
        return output;
    }

    private static Column CreateColumn(string name, string? header, int? width)
    {
        var key = name.TrimStart('%').ToLowerInvariant();
        var column = key switch
        {
            "pid" => Numeric("PID", width, (p, _) => p.Pid.ToString(CultureInfo.InvariantCulture)),
            "ppid" => Numeric("PPID", width, (p, _) => p.ParentPid.ToString(CultureInfo.InvariantCulture)),
            "pgid" or "pgrp" => Numeric("PGID", width, (p, _) => p.ProcessGroupId.ToString(CultureInfo.InvariantCulture)),
            "sid" or "sess" => Numeric("SID", width, (p, _) => p.SessionId.ToString(CultureInfo.InvariantCulture)),
            "user" or "euser" => Text("USER", width, (p, _) => p.EffectiveUserName),
            "ruser" => Text("RUSER", width, (p, _) => p.RealUserName),
            "uid" or "euid" => Numeric("UID", width, (p, _) => p.EffectiveUserId),
            "ruid" => Numeric("RUID", width, (p, _) => p.RealUserId),
            "group" or "egroup" => Text("GROUP", width, (p, _) => p.EffectiveGroupName),
            "gid" or "egid" => Numeric("GID", width, (p, _) => p.EffectiveGroupId),
            "tty" or "tname" => Text("TTY", width, (p, _) => NormalizeTerminal(p.Terminal)),
            "stat" => Text("STAT", width, (p, _) => p.State),
            "state" or "s" => Text("S", width, (p, _) => p.State.Length == 0 ? "?" : p.State[..1]),
            "time" or "cputime" => Numeric("TIME", width, (p, _) => FormatCpuTime(p.CpuTime)),
            "etime" => Numeric("ELAPSED", width, (p, _) => FormatElapsed(p.Elapsed)),
            "etimes" => Numeric("ELAPSED", width, (p, _) => ((long)p.Elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture)),
            "start" => Text("START", width, (p, s) => FormatStart(p.StartTime, s.CapturedAt)),
            "stime" => Text("STIME", width, (p, _) => p.StartTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "?"),
            "lstart" => Text("STARTED", width, (p, _) => p.StartTime?.ToString("ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture) ?? "?"),
            "comm" or "ucmd" or "cmd" => Text(key == "cmd" ? "CMD" : "COMMAND", width, (p, _) => p.Command, last: true),
            "args" or "command" => Text("COMMAND", width, (p, _) => p.Arguments, last: true),
            "pcpu" => Numeric("%CPU", width, (p, _) => p.CpuPercent.ToString("0.0", CultureInfo.InvariantCulture)),
            "pmem" => Numeric("%MEM", width, (p, _) => p.MemoryPercent.ToString("0.0", CultureInfo.InvariantCulture)),
            "rss" => Numeric("RSS", width, (p, _) => (p.WorkingSetBytes / 1024L).ToString(CultureInfo.InvariantCulture)),
            "vsz" or "vsize" or "sz" => Numeric("VSZ", width, (p, _) => (p.VirtualMemoryBytes / 1024L).ToString(CultureInfo.InvariantCulture)),
            "nlwp" or "thcount" => Numeric("NLWP", width, (p, _) => p.ThreadCount.ToString(CultureInfo.InvariantCulture)),
            "pri" => Numeric("PRI", width, (p, _) => p.Priority.ToString(CultureInfo.InvariantCulture)),
            "ni" or "nice" => Numeric("NI", width, (p, _) => p.Nice.ToString(CultureInfo.InvariantCulture)),
            "psr" => Numeric("PSR", width, (_, _) => "-"),
            "label" => Text("LABEL", width, (_, _) => "-"),
            _ => Text(header ?? name.ToUpperInvariant(), width, (_, _) => "?"),
        };
        return column with { Header = header ?? GetDefaultHeader(key) };
    }

    private static string GetDefaultHeader(string key) => key switch
    {
        "pid" => "PID",
        "ppid" => "PPID",
        "pgid" or "pgrp" => "PGID",
        "sid" or "sess" => "SID",
        "user" or "euser" => "USER",
        "ruser" => "RUSER",
        "uid" or "euid" => "UID",
        "ruid" => "RUID",
        "group" or "egroup" => "GROUP",
        "gid" or "egid" => "GID",
        "tty" or "tname" => "TTY",
        "stat" => "STAT",
        "state" or "s" => "S",
        "time" or "cputime" => "TIME",
        "etime" or "etimes" => "ELAPSED",
        "start" or "stime" => "START",
        "lstart" => "STARTED",
        "comm" or "ucmd" => "COMMAND",
        "cmd" => "CMD",
        "args" or "command" => "COMMAND",
        "pcpu" => "%CPU",
        "pmem" => "%MEM",
        "rss" => "RSS",
        "vsz" or "vsize" or "sz" => "VSZ",
        "nlwp" or "thcount" => "NLWP",
        "pri" => "PRI",
        "ni" or "nice" => "NI",
        "psr" => "PSR",
        "label" => "LABEL",
        _ => key.ToUpperInvariant(),
    };

    private static Column Numeric(string header, int? width, Func<ProcessInfo, ProcessSnapshot, string> value) =>
        new(header, width, true, false, value);

    private static Column Text(
        string header,
        int? width,
        Func<ProcessInfo, ProcessSnapshot, string> value,
        bool last = false
    ) => new(header, width, false, last, value);

    private static int[] CalculateWidths(IReadOnlyList<Column> columns, IReadOnlyList<string[]> rows)
    {
        var widths = new int[columns.Count];
        for (var index = 0; index < columns.Count; index++)
        {
            widths[index] = columns[index].Width ?? columns[index].Header.Length;
            foreach (var row in rows)
            {
                widths[index] = Math.Max(widths[index], row[index].Length);
            }
        }
        return widths;
    }

    private static async Task WriteRowAsync(
        IReadOnlyList<string> values,
        IReadOnlyList<Column> columns,
        IReadOnlyList<int> widths,
        CommandContext context
    )
    {
        var fields = new string[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            if (columns[index].IsLast)
            {
                fields[index] = values[index];
            }
            else
            {
                fields[index] = columns[index].RightAlign
                    ? values[index].PadLeft(widths[index])
                    : values[index].PadRight(widths[index]);
            }
        }

        await context.StandardOutput.WriteLineAsync(
            String.Join(' ', fields).TrimEnd().AsMemory(),
            context.CancellationToken
        ).ConfigureAwait(false);
    }

    private static void ApplyForest(IReadOnlyList<ProcessInfo> processes)
    {
        var selected = processes.Select(process => process.Pid).ToHashSet();
        var byPid = processes.ToDictionary(process => process.Pid);
        foreach (var process in processes)
        {
            var depth = 0;
            var parent = process.ParentPid;
            var seen = new HashSet<int>();
            while (selected.Contains(parent) && seen.Add(parent) && byPid.TryGetValue(parent, out var parentProcess))
            {
                depth++;
                parent = parentProcess.ParentPid;
            }

            if (depth > 0)
            {
                var prefix = String.Concat(new string(' ', depth * 2), "\\_ ");
                process.Arguments = String.Concat(prefix, process.Arguments);
                process.Command = String.Concat(prefix, process.Command);
            }
        }
    }

    private static string NormalizeTerminal(string value) => value switch
    {
        "?" or "" => "?",
        _ when value.StartsWith("/dev/", StringComparison.Ordinal) => value[5..],
        _ => value,
    };

    private static string FormatCpuTime(TimeSpan value) => String.Concat(
        ((int)value.TotalHours).ToString("00", CultureInfo.InvariantCulture),
        ":",
        value.Minutes.ToString("00", CultureInfo.InvariantCulture),
        ":",
        value.Seconds.ToString("00", CultureInfo.InvariantCulture)
    );

    private static string FormatElapsed(TimeSpan value)
    {
        var time = String.Concat(
            ((int)value.TotalHours % 24).ToString("00", CultureInfo.InvariantCulture),
            ":",
            value.Minutes.ToString("00", CultureInfo.InvariantCulture),
            ":",
            value.Seconds.ToString("00", CultureInfo.InvariantCulture)
        );
        return value.TotalDays >= 1.0
            ? String.Concat(((int)value.TotalDays).ToString(CultureInfo.InvariantCulture), "-", time)
            : time;
    }

    private static string FormatStart(DateTimeOffset? start, DateTimeOffset now)
    {
        if (!start.HasValue)
        {
            return "?";
        }
        return (now - start.Value).TotalHours < 24.0
            ? start.Value.ToString("HH:mm", CultureInfo.InvariantCulture)
            : start.Value.ToString("MMMdd", CultureInfo.InvariantCulture);
    }

    private static string[] NormalizeArguments(IReadOnlyList<string> args)
    {
        var output = new List<string>();
        foreach (var argument in args)
        {
            if (argument == "-e")
            {
                output.Add("--all");
                continue;
            }
            if (argument.Length > 0 && argument.All(character => "axuvjlfrcehwT".Contains(character)))
            {
                foreach (var character in argument)
                {
                    output.Add(character switch
                    {
                        'a' => "--bsd-a",
                        'x' => "--bsd-x",
                        'u' => "--bsd-u",
                        'v' => "--bsd-v",
                        'j' => "--bsd-j",
                        'l' => "--bsd-l",
                        'f' => "--bsd-f",
                        'r' => "--running",
                        'c' => "--bsd-c",
                        'e' => "--bsd-e",
                        'h' => "--bsd-h",
                        'w' => "--bsd-w",
                        'T' => "--current-terminal",
                        _ => argument,
                    });
                }
                continue;
            }
            if (Int32.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                output.Add(String.Concat("--pid=", argument));
                continue;
            }
            if (argument.Length > 1
                && argument[0] == '+'
                && Int32.TryParse(argument.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                output.Add(String.Concat("--sid=", argument[1..]));
                continue;
            }
            if (argument.Length > 1
                && argument[0] == '-'
                && Int32.TryParse(argument.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                output.Add(String.Concat("--pgrp=", argument[1..]));
                continue;
            }

            output.Add(argument);
        }
        return [.. output];
    }

    private static OptionParser CreateParser(params OptionDefinition[] options) => new(
        options,
        new OptionParserSettings
        {
            AllowLongOptionAbbreviations = true,
            Ordering = OptionOrdering.Permute,
        }
    );

    private static async Task<bool> WriteParseErrorsAsync(
        OptionParseResult result,
        CommandContext context
    )
    {
        if (result.IsSuccess)
        {
            return false;
        }
        foreach (var error in result.Errors)
        {
            await context.StandardError.WriteLineAsync(
                OptionDiagnosticFormatter.Format(context.ProgramName, error).AsMemory(),
                context.CancellationToken
            ).ConfigureAwait(false);
        }
        return true;
    }

    private static readonly HashSet<string> SelectionKeys =
    [
        "all",
        "all-terminals",
        "no-leaders",
        "command",
        "group",
        "pgrp",
        "pid",
        "quick-pid",
        "ppid",
        "sid",
        "tty",
        "effective-user",
        "real-user",
        "include-no-terminal",
        "bsd-a",
        "bsd-x",
        "current-terminal",
    ];

    private sealed record Column(
        string Header,
        int? Width,
        bool RightAlign,
        bool IsLast,
        Func<ProcessInfo, ProcessSnapshot, string> GetValue
    );
}
