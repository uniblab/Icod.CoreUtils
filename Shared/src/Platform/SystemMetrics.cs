using System.Diagnostics;
using System.Globalization;

namespace Icod.CoreUtils.Shared.Platform;

/// <summary>
/// Represents system metrics snapshot.
/// </summary>
/// <param name="CurrentTime">The current time value.</param>
/// <param name="Uptime">The uptime value.</param>
/// <param name="UserCount">The user count value.</param>
/// <param name="LoadAverageOneMinute">The load average one minute value.</param>
/// <param name="LoadAverageFiveMinutes">The load average five minutes value.</param>
/// <param name="LoadAverageFifteenMinutes">The load average fifteen minutes value.</param>
public sealed record SystemMetricsSnapshot(
    DateTimeOffset CurrentTime,
    TimeSpan Uptime,
    int UserCount,
    double LoadAverageOneMinute,
    double LoadAverageFiveMinutes,
    double LoadAverageFifteenMinutes
);

/// <summary>
/// Supplies host and container-aware system metrics.
/// </summary>
public interface ISystemMetricsProvider
{
    /// <summary>Asynchronously captures the current system metrics.</summary>
    /// <param name="container">Whether container-aware metrics should be preferred.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The captured system metrics.</returns>
    ValueTask<SystemMetricsSnapshot> GetSnapshotAsync(
        bool container,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Provides the system metrics provider implementation.
/// </summary>
public sealed class SystemMetricsProvider : ISystemMetricsProvider
{
    private readonly IUserInformationProvider _users;

    /// <summary>
    /// Performs the system metrics provider operation.
    /// </summary>
    public SystemMetricsProvider(IUserInformationProvider? users = null)
    {
        _users = users ?? new SystemUserInformationProvider();
    }

    /// <summary>
    /// Gets snapshot async.
    /// </summary>
    public async ValueTask<SystemMetricsSnapshot> GetSnapshotAsync(
        bool container,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.Now;
        var uptime = container
            ? GetContainerUptime(now)
            : await GetSystemUptimeAsync(cancellationToken).ConfigureAwait(false);
        var loads = await GetLoadAveragesAsync(cancellationToken).ConfigureAwait(false);

        var userCount = 0;
        await foreach (var _ in _users.GetLoginSessionsAsync(cancellationToken))
        {
            checked
            {
                userCount++;
            }
        }

        return new SystemMetricsSnapshot(
            now,
            uptime,
            userCount,
            loads.One,
            loads.Five,
            loads.Fifteen
        );
    }

    private static TimeSpan GetContainerUptime(DateTimeOffset now)
    {
        try
        {
            using var process = Process.GetProcessById(1);
            var started = new DateTimeOffset(process.StartTime);
            var elapsed = now - started;
            return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }
        catch
        {
            return TimeSpan.FromMilliseconds(Math.Max(0L, Environment.TickCount64));
        }
    }

    private static async ValueTask<TimeSpan> GetSystemUptimeAsync(
        CancellationToken cancellationToken
    )
    {
        if (OperatingSystem.IsLinux() && File.Exists("/proc/uptime"))
        {
            try
            {
                var text = await File.ReadAllTextAsync("/proc/uptime", cancellationToken)
                    .ConfigureAwait(false);
                var token = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
                if (Double.TryParse(
                    token,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var seconds
                ))
                {
                    return TimeSpan.FromSeconds(Math.Max(0.0, seconds));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return TimeSpan.FromMilliseconds(Math.Max(0L, Environment.TickCount64));
    }

    private static async ValueTask<(double One, double Five, double Fifteen)> GetLoadAveragesAsync(
        CancellationToken cancellationToken
    )
    {
        if (OperatingSystem.IsLinux() && File.Exists("/proc/loadavg"))
        {
            try
            {
                var text = await File.ReadAllTextAsync("/proc/loadavg", cancellationToken)
                    .ConfigureAwait(false);
                var fields = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length >= 3
                    && Double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var one)
                    && Double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var five)
                    && Double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var fifteen))
                {
                    return (one, five, fifteen);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return (0.0, 0.0, 0.0);
    }
}
