using System.Globalization;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;

namespace Icod.CoreUtils.Pinky;

public static class Command
{
    private const string ProgramName = "pinky";
    private const string Version = "pinky (Icod.CoreUtils) 1.0";

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
        new SystemUserInformationProvider()
    );

    public static async Task<int> RunAsync(
        string[] args,
        CommandContext context,
        IUserInformationProvider provider
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(provider);

        var parser = CreateParser(
            new OptionDefinition("long", 'l', ["long-format"]),
            new OptionDefinition("omit-home-shell", 'b'),
            new OptionDefinition("omit-project", 'h'),
            new OptionDefinition("omit-plan", 'p'),
            new OptionDefinition("short", 's', ["short-format"]),
            new OptionDefinition("omit-headings", 'f'),
            new OptionDefinition("omit-full-name", 'w'),
            new OptionDefinition("omit-full-name-host", 'i'),
            new OptionDefinition("omit-full-name-host-idle", 'q'),
            new OptionDefinition("lookup", null, ["lookup"]),
            new OptionDefinition("help", null, ["help"]),
            new OptionDefinition("version", null, ["version"])
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
Usage: pinky [OPTION]... [USER]...
A lightweight finger program.
  -l                 produce long format output
  -b                 omit the user's home directory and shell in long format
  -h                 omit the user's project file in long format
  -p                 omit the user's plan file in long format
  -s                 produce short format output
  -f                 omit the line of column headings in short format
  -w                 omit the user's full name in short format
  -i                 omit the user's full name and remote host in short format
  -q                 omit the user's full name, remote host, and idle time
      --lookup       attempt to canonicalize host names via DNS
      --help         display this help and exit
      --version      output version information and exit
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

            var longFormat = GetLastFormatIsLong(result);
            if (longFormat && result.Operands.Count == 0)
            {
                await context.Diagnostics.ErrorAsync(
                    "no username specified; at least one must be specified when using -l",
                    context.CancellationToken
                ).ConfigureAwait(false);
                return CommandExitCodes.Failure;
            }

            return longFormat
                ? await WriteLongAsync(result, context, provider).ConfigureAwait(false)
                : await WriteShortAsync(result, context, provider).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return CommandExitCodes.Canceled;
        }
    }

    private static async Task<int> WriteShortAsync(
        OptionParseResult result,
        CommandContext context,
        IUserInformationProvider provider
    )
    {
        var accounts = await provider.GetAccountsAsync(context.CancellationToken).ConfigureAwait(false);
        var accountsByName = accounts.ToDictionary(account => account.UserName, StringComparer.Ordinal);
        var sessions = new List<LoginSessionInfo>();
        await foreach (var session in provider.GetLoginSessionsAsync(context.CancellationToken))
        {
            if (result.Operands.Count == 0
                || result.Operands.Contains(session.UserName, StringComparer.Ordinal))
            {
                sessions.Add(session);
            }
        }

        var showFullName = !result.HasOption("omit-full-name")
            && !result.HasOption("omit-full-name-host")
            && !result.HasOption("omit-full-name-host-idle");
        var showHost = !result.HasOption("omit-full-name-host")
            && !result.HasOption("omit-full-name-host-idle");
        var showIdle = !result.HasOption("omit-full-name-host-idle");

        if (!result.HasOption("omit-headings"))
        {
            var headings = new List<string> { "Login name" };
            if (showFullName)
            {
                headings.Add("Real name");
            }
            headings.Add("TTY");
            if (showIdle)
            {
                headings.Add("Idle");
            }
            headings.Add("When");
            if (showHost)
            {
                headings.Add("Where");
            }
            await context.StandardOutput.WriteLineAsync(
                String.Join("  ", headings).AsMemory(),
                context.CancellationToken
            ).ConfigureAwait(false);
        }

        foreach (var session in sessions)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var fields = new List<string> { session.UserName };
            if (showFullName)
            {
                fields.Add(accountsByName.TryGetValue(session.UserName, out var account)
                    ? account.FullName
                    : session.UserName);
            }
            fields.Add(session.Terminal);
            if (showIdle)
            {
                fields.Add(FormatIdle(session.IdleTime));
            }
            fields.Add(session.LoginTime == DateTimeOffset.MinValue
                ? "?"
                : session.LoginTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture));
            if (showHost)
            {
                var host = result.HasOption("lookup")
                    ? await provider.ResolveHostAsync(session.Host, context.CancellationToken).ConfigureAwait(false)
                    : session.Host;
                fields.Add(host);
            }

            await context.StandardOutput.WriteLineAsync(
                String.Join("  ", fields).AsMemory(),
                context.CancellationToken
            ).ConfigureAwait(false);
        }

        return CommandExitCodes.Success;
    }

    private static async Task<int> WriteLongAsync(
        OptionParseResult result,
        CommandContext context,
        IUserInformationProvider provider
    )
    {
        var userNames = result.Operands;
        var failed = false;

        foreach (var userName in userNames)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var account = await provider.FindAccountAsync(userName, context.CancellationToken)
                .ConfigureAwait(false);
            if (account is null)
            {
                await context.Diagnostics.ErrorAsync(
                    String.Concat("no such user: ", userName),
                    context.CancellationToken
                ).ConfigureAwait(false);
                failed = true;
                continue;
            }

            await context.StandardOutput.WriteLineAsync(
                String.Concat(
                    "Login name: ", account.UserName,
                    "        In real life: ", account.FullName
                ).AsMemory(),
                context.CancellationToken
            ).ConfigureAwait(false);

            if (!result.HasOption("omit-home-shell"))
            {
                await context.StandardOutput.WriteLineAsync(
                    String.Concat(
                        "Directory: ", account.HomeDirectory,
                        "        Shell: ", account.Shell
                    ).AsMemory(),
                    context.CancellationToken
                ).ConfigureAwait(false);
            }

            if (!result.HasOption("omit-project"))
            {
                await WriteOptionalFileAsync(
                    "Project",
                    Path.Combine(account.HomeDirectory, ".project"),
                    context
                ).ConfigureAwait(false);
            }

            if (!result.HasOption("omit-plan"))
            {
                await WriteOptionalFileAsync(
                    "Plan",
                    Path.Combine(account.HomeDirectory, ".plan"),
                    context
                ).ConfigureAwait(false);
            }
        }

        return failed ? CommandExitCodes.Failure : CommandExitCodes.Success;
    }

    private static async Task WriteOptionalFileAsync(
        string label,
        string path,
        CommandContext context
    )
    {
        if (!File.Exists(path))
        {
            await context.StandardOutput.WriteLineAsync(
                String.Concat(label, ":").AsMemory(),
                context.CancellationToken
            ).ConfigureAwait(false);
            return;
        }

        try
        {
            var text = await File.ReadAllTextAsync(path, context.CancellationToken).ConfigureAwait(false);
            await context.StandardOutput.WriteLineAsync(
                String.Concat(label, ":").AsMemory(),
                context.CancellationToken
            ).ConfigureAwait(false);
            if (!String.IsNullOrEmpty(text))
            {
                var normalized = text.ReplaceLineEndings(Environment.NewLine);
                await context.StandardOutput.WriteAsync(
                    normalized.AsMemory(),
                    context.CancellationToken
                ).ConfigureAwait(false);
                if (!normalized.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                {
                    await context.StandardOutput.WriteLineAsync(
                        ReadOnlyMemory<char>.Empty,
                        context.CancellationToken
                    ).ConfigureAwait(false);
                }
            }
        }
        catch (IOException exception)
        {
            await context.Diagnostics.WarningAsync(
                String.Concat("cannot read '", path, "': ", exception.Message),
                context.CancellationToken
            ).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException exception)
        {
            await context.Diagnostics.WarningAsync(
                String.Concat("cannot read '", path, "': ", exception.Message),
                context.CancellationToken
            ).ConfigureAwait(false);
        }
    }

    private static bool GetLastFormatIsLong(OptionParseResult result)
    {
        var last = result.Options
            .Where(option => option.Definition.Key is "long" or "short")
            .LastOrDefault();
        return last?.Definition.Key == "long";
    }

    private static string FormatIdle(TimeSpan? idle)
    {
        if (!idle.HasValue)
        {
            return "?";
        }
        if (idle.Value < TimeSpan.FromMinutes(1.0))
        {
            return "     ";
        }
        if (idle.Value < TimeSpan.FromDays(1.0))
        {
            return String.Concat(
                ((int)idle.Value.TotalHours).ToString("00", CultureInfo.InvariantCulture),
                ":",
                idle.Value.Minutes.ToString("00", CultureInfo.InvariantCulture)
            );
        }

        return String.Concat(
            ((int)idle.Value.TotalDays).ToString(CultureInfo.InvariantCulture),
            "d"
        );
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
}
