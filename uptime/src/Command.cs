using System.Globalization;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;

namespace Icod.CoreUtils.Uptime;

/// <summary>
/// Implements GNU-compatible <c>uptime</c> and prints system uptime, logged-in-user count, and load averages.
/// </summary>
/// <remarks>
/// System metrics are supplied through an injectable provider and rendered with GNU-compatible pluralization.
/// </remarks>
public static class Command
{
    private const string ProgramName = "uptime";
    private const string Version = "uptime (Icod.CoreUtils) 1.0";

    /// <summary>
    /// Executes <c>uptime</c> synchronously with optional standard-stream substitution.
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
    /// Executes <c>uptime</c> asynchronously with optional injected standard streams.
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
        new SystemMetricsProvider()
    );

    /// <summary>
    /// Executes <c>uptime</c> asynchronously using a complete shared command context.
    /// </summary>
    /// <remarks>
    /// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
    /// </remarks>
    /// <param name="args">The command-line arguments, excluding the executable name.</param>
    /// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
    /// <param name="provider">The injectable system-metrics provider; <see langword="null"/> selects the system implementation when supported by this overload.</param>
    /// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Formats an uptime duration for the <c>--pretty</c> output form.
    /// </summary>
    /// <param name="uptime">The non-negative elapsed uptime.</param>
    /// <returns>A human-readable combination of years, days, hours, and minutes.</returns>
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
