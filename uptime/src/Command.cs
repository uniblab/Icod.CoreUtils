using System.Globalization;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;

namespace Icod.CoreUtils.Uptime;

public static class Command
{
    private const string ProgramName = "uptime";
    private const string Version = "uptime (Icod.CoreUtils) 1.0";

    public static int Run(
        string[] args,
        TextReader? stdin = null,
        TextWriter? stdout = null,
        TextWriter? stderr = null
    ) => RunAsync(args, stdin, stdout, stderr).GetAwaiter().GetResult();

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
        new SystemMetricsProvider()
    );

    public static async Task<int> RunAsync(
        string[] args,
        CommandContext context,
        ISystemMetricsProvider provider
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(provider);

        var parser = CreateParser(
            new OptionDefinition("container", 'c', ["container"]),
            new OptionDefinition("pretty", 'p', ["pretty"]),
            new OptionDefinition("raw", 'r', ["raw"]),
            new OptionDefinition("since", 's', ["since"]),
            new OptionDefinition("help", 'h', ["help"]),
            new OptionDefinition("version", 'V', ["version"])
        );

        try
        {
            var result = parser.Parse(args);
            if (await WriteParseErrorsAsync(result, context).ConfigureAwait(false))
            {
                return CommandExitCodes.Failure;
            }

            if (result.HasOption("help"))
            {
                const string help = """
Usage: uptime [OPTION]...
Show how long the system has been running.
  -c, --container  show container uptime
  -p, --pretty     show uptime in a human-readable form
  -r, --raw        show raw values
  -s, --since      system up since, in yyyy-mm-dd HH:MM:SS format
  -h, --help       display this help and exit
  -V, --version    output version information and exit
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
                    String.Concat("extra operand '", result.Operands[0], "'"),
                    context.CancellationToken
                ).ConfigureAwait(false);
                return CommandExitCodes.Failure;
            }

            var container = result.HasOption("container")
                || IsTruthy(Environment.GetEnvironmentVariable("PROCPS_CONTAINER"));
            var snapshot = await provider.GetSnapshotAsync(
                container,
                context.CancellationToken
            ).ConfigureAwait(false);

            string output;
            if (result.HasOption("since"))
            {
                output = (snapshot.CurrentTime - snapshot.Uptime)
                    .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
            else if (result.HasOption("pretty"))
            {
                output = String.Concat("up ", FormatPretty(snapshot.Uptime));
            }
            else if (result.HasOption("raw"))
            {
                output = String.Concat(
                    snapshot.CurrentTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    " ",
                    snapshot.Uptime.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture),
                    " ",
                    snapshot.UserCount.ToString(CultureInfo.InvariantCulture),
                    " ",
                    snapshot.LoadAverageOneMinute.ToString("0.00", CultureInfo.InvariantCulture),
                    " ",
                    snapshot.LoadAverageFiveMinutes.ToString("0.00", CultureInfo.InvariantCulture),
                    " ",
                    snapshot.LoadAverageFifteenMinutes.ToString("0.00", CultureInfo.InvariantCulture)
                );
            }
            else
            {
                output = FormatStandard(snapshot);
            }

            await context.StandardOutput.WriteLineAsync(
                output.AsMemory(),
                context.CancellationToken
            ).ConfigureAwait(false);
            return CommandExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return CommandExitCodes.Canceled;
        }
    }

    internal static string FormatPretty(TimeSpan uptime)
    {
        var parts = new List<string>();
        var totalDays = (int)Math.Floor(uptime.TotalDays);
        var weeks = totalDays / 7;
        var days = totalDays % 7;
        if (weeks > 0)
        {
            parts.Add(String.Concat(weeks.ToString(CultureInfo.InvariantCulture), weeks == 1 ? " week" : " weeks"));
        }
        if (days > 0)
        {
            parts.Add(String.Concat(days.ToString(CultureInfo.InvariantCulture), days == 1 ? " day" : " days"));
        }
        if (uptime.Hours > 0)
        {
            parts.Add(String.Concat(uptime.Hours.ToString(CultureInfo.InvariantCulture), uptime.Hours == 1 ? " hour" : " hours"));
        }
        if (uptime.Minutes > 0 || parts.Count == 0)
        {
            parts.Add(String.Concat(uptime.Minutes.ToString(CultureInfo.InvariantCulture), uptime.Minutes == 1 ? " minute" : " minutes"));
        }

        return String.Join(", ", parts);
    }

    private static string FormatStandard(SystemMetricsSnapshot snapshot)
    {
        var totalDays = (int)Math.Floor(snapshot.Uptime.TotalDays);
        string uptime;
        if (totalDays > 0)
        {
            uptime = String.Concat(
                totalDays.ToString(CultureInfo.InvariantCulture),
                totalDays == 1 ? " day, " : " days, ",
                snapshot.Uptime.Hours.ToString(CultureInfo.InvariantCulture),
                ":",
                snapshot.Uptime.Minutes.ToString("00", CultureInfo.InvariantCulture)
            );
        }
        else if (snapshot.Uptime.TotalHours >= 1.0)
        {
            uptime = String.Concat(
                snapshot.Uptime.Hours.ToString(CultureInfo.InvariantCulture),
                ":",
                snapshot.Uptime.Minutes.ToString("00", CultureInfo.InvariantCulture)
            );
        }
        else
        {
            uptime = String.Concat(
                snapshot.Uptime.Minutes.ToString(CultureInfo.InvariantCulture),
                snapshot.Uptime.Minutes == 1 ? " min" : " mins"
            );
        }

        return String.Concat(
            snapshot.CurrentTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            " up ",
            uptime,
            ",  ",
            snapshot.UserCount.ToString(CultureInfo.InvariantCulture),
            snapshot.UserCount == 1 ? " user,  load average: " : " users,  load average: ",
            snapshot.LoadAverageOneMinute.ToString("0.00", CultureInfo.InvariantCulture),
            ", ",
            snapshot.LoadAverageFiveMinutes.ToString("0.00", CultureInfo.InvariantCulture),
            ", ",
            snapshot.LoadAverageFifteenMinutes.ToString("0.00", CultureInfo.InvariantCulture)
        );
    }

    private static bool IsTruthy(string? value) =>
        !String.IsNullOrEmpty(value)
        && !String.Equals(value, "0", StringComparison.Ordinal)
        && !String.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
        && !String.Equals(value, "no", StringComparison.OrdinalIgnoreCase);

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
}
