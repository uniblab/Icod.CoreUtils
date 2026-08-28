/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using Path = global::System.IO.Path;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Icod.CoreUtils.Shared.Platform;

/// <summary>
/// Represents user account info.
/// </summary>
/// <param name="UserName">The user name value.</param>
/// <param name="FullName">The full name value.</param>
/// <param name="HomeDirectory">The home directory value.</param>
/// <param name="Shell">The shell value.</param>
/// <param name="UserId">The user id value.</param>
/// <param name="GroupId">The group id value.</param>
public sealed record UserAccountInfo(
    string UserName,
    string FullName,
    string HomeDirectory,
    string Shell,
    string UserId,
    string GroupId
);

/// <summary>
/// Represents login session info.
/// </summary>
/// <param name="UserName">The user name value.</param>
/// <param name="Terminal">The terminal value.</param>
/// <param name="Host">The host value.</param>
/// <param name="LoginTime">The login time value.</param>
/// <param name="IdleTime">The idle time value.</param>
/// <param name="ProcessId">The process id value.</param>
public sealed record LoginSessionInfo(
    string UserName,
    string Terminal,
    string Host,
    DateTimeOffset LoginTime,
    TimeSpan? IdleTime,
    int ProcessId
);

/// <summary>
/// Supplies user-account and login-session information.
/// </summary>
public interface IUserInformationProvider
{
    /// <summary>Asynchronously reads the known user accounts.</summary>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The known user accounts.</returns>
    ValueTask<IReadOnlyList<UserAccountInfo>> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Asynchronously finds an account by user name.</summary>
    /// <param name="userName">The user name to locate.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The matching account, or <see langword="null"/> when no account matches.</returns>
    ValueTask<UserAccountInfo?> FindAccountAsync(
        string userName,
        CancellationToken cancellationToken = default
    );

    /// <summary>Asynchronously enumerates current login sessions.</summary>
    /// <param name="cancellationToken">A token that can cancel the enumeration.</param>
    /// <returns>The asynchronous login-session sequence.</returns>
    IAsyncEnumerable<LoginSessionInfo> GetLoginSessionsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>Asynchronously resolves a host name for display.</summary>
    /// <param name="host">The host identifier to resolve.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The resolved host name.</returns>
    ValueTask<string> ResolveHostAsync(
        string host,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Provides the system user information provider implementation.
/// </summary>
public sealed class SystemUserInformationProvider : IUserInformationProvider
{
    private const int UtmpRecordSize = 384;
    private const short UserProcess = 7;

    /// <summary>
    /// Gets accounts async.
    /// </summary>
    public async ValueTask<IReadOnlyList<UserAccountInfo>> GetAccountsAsync(
        CancellationToken cancellationToken = default
    )
    {
        if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && File.Exists("/etc/passwd"))
        {
            try
            {
                var lines = await File.ReadAllLinesAsync("/etc/passwd", cancellationToken).ConfigureAwait(false);
                var accounts = new List<UserAccountInfo>(lines.Length);
                foreach (var line in lines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (TryParsePasswdLine(line, out var account))
                    {
                        accounts.Add(account);
                    }
                }

                return accounts;
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

        var currentName = Environment.UserName;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return
        [
            new UserAccountInfo(
                currentName,
                currentName,
                home,
                Environment.GetEnvironmentVariable("SHELL") ?? String.Empty,
                currentName,
                String.Empty
            ),
        ];
    }

    /// <summary>
    /// Performs the find account async operation.
    /// </summary>
    public async ValueTask<UserAccountInfo?> FindAccountAsync(
        string userName,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var accounts = await GetAccountsAsync(cancellationToken).ConfigureAwait(false);
        return accounts.FirstOrDefault(
            account => String.Equals(account.UserName, userName, StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// Gets login sessions async.
    /// </summary>
    public async IAsyncEnumerable<LoginSessionInfo> GetLoginSessionsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var path = GetUtmpPath();
        if (path is null)
        {
            if (OperatingSystem.IsWindows() && Environment.UserInteractive)
            {
                var uptime = TimeSpan.FromMilliseconds(Math.Max(0L, Environment.TickCount64));
                yield return new LoginSessionInfo(
                    Environment.UserName,
                    "console",
                    Environment.MachineName,
                    DateTimeOffset.Now - uptime,
                    null,
                    Environment.ProcessId
                );
            }

            yield break;
        }

        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                UtmpRecordSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            );
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        if (stream is null)
        {
            yield break;
        }

        await using var ownedStream = stream;
        var buffer = new byte[UtmpRecordSize];
        while (true)
        {
                var hasRecord = false;
                var readFailed = false;
                try
                {
                    hasRecord = await ReadFullRecordAsync(ownedStream, buffer, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (IOException)
                {
                    readFailed = true;
                }
                catch (UnauthorizedAccessException)
                {
                    readFailed = true;
                }

                if (readFailed || !hasRecord)
                {
                    yield break;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (ReadInt16(buffer.AsSpan(0, 2)) != UserProcess)
                {
                    continue;
                }

                var user = ReadCString(buffer.AsSpan(44, 32));
                var terminal = ReadCString(buffer.AsSpan(8, 32));
                if (String.IsNullOrEmpty(user) || String.IsNullOrEmpty(terminal))
                {
                    continue;
                }

                var host = ReadCString(buffer.AsSpan(76, 256));
                var processId = ReadInt32(buffer.AsSpan(4, 4));
                var seconds = ReadInt32(buffer.AsSpan(340, 4));
                DateTimeOffset loginTime;
                try
                {
                    loginTime = DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime();
                }
                catch (ArgumentOutOfRangeException)
                {
                    loginTime = DateTimeOffset.MinValue;
                }

                yield return new LoginSessionInfo(
                    user,
                    terminal,
                    host,
                    loginTime,
                    GetIdleTime(terminal),
                    processId
                );
        }
    }

    /// <summary>
    /// Resolves host async.
    /// </summary>
    public async ValueTask<string> ResolveHostAsync(
        string host,
        CancellationToken cancellationToken = default
    )
    {
        if (String.IsNullOrWhiteSpace(host))
        {
            return host;
        }

        var normalized = host;
        var colon = normalized.LastIndexOf(':');
        if (colon > 0 && normalized.Count(character => character == ':') == 1)
        {
            normalized = normalized[..colon];
        }

        try
        {
            var entry = await System.Net.Dns.GetHostEntryAsync(normalized)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return String.IsNullOrWhiteSpace(entry.HostName) ? host : entry.HostName;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return host;
        }
    }

    private static bool TryParsePasswdLine(string line, out UserAccountInfo account)
    {
        account = null!;
        if (String.IsNullOrEmpty(line) || line[0] == '#')
        {
            return false;
        }

        var fields = line.Split(':');
        if (fields.Length < 7 || String.IsNullOrEmpty(fields[0]))
        {
            return false;
        }

        var fullName = fields[4];
        var comma = fullName.IndexOf(',');
        if (comma >= 0)
        {
            fullName = fullName[..comma];
        }

        account = new UserAccountInfo(
            fields[0],
            String.IsNullOrEmpty(fullName) ? fields[0] : fullName,
            fields[5],
            fields[6],
            fields[2],
            fields[3]
        );
        return true;
    }

    private static string? GetUtmpPath()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        foreach (var candidate in new[] { "/run/utmp", "/var/run/utmp" })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static async ValueTask<bool> ReadFullRecordAsync(
        FileStream stream,
        byte[] buffer,
        CancellationToken cancellationToken
    )
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(offset, buffer.Length - offset),
                cancellationToken
            ).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    private static TimeSpan? GetIdleTime(string terminal)
    {
        try
        {
            var path = System.IO.Path.Combine("/dev", terminal);
            var lastAccess = File.GetLastAccessTimeUtc(path);
            if (lastAccess == DateTime.MinValue)
            {
                return null;
            }

            var idle = DateTime.UtcNow - lastAccess;
            return idle < TimeSpan.Zero ? TimeSpan.Zero : idle;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadCString(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        if (end >= 0)
        {
            bytes = bytes[..end];
        }

        return Encoding.UTF8.GetString(bytes).Trim();
    }

    private static short ReadInt16(ReadOnlySpan<byte> bytes) =>
        BitConverter.IsLittleEndian
            ? BinaryPrimitives.ReadInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadInt16BigEndian(bytes);

    private static int ReadInt32(ReadOnlySpan<byte> bytes) =>
        BitConverter.IsLittleEndian
            ? BinaryPrimitives.ReadInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadInt32BigEndian(bytes);
}
