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

using System.Runtime.InteropServices;

namespace Icod.CoreUtils.Shared.Time;

/// <summary>
/// Supplies current time values and controlled system-clock updates.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Gets the current local time.</summary>
    DateTimeOffset Now { get; }

    /// <summary>Gets the current Coordinated Universal Time.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Asynchronously attempts to set the system clock.</summary>
    /// <param name="value">The time to set.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns><see langword="true"/> when the system clock was set; otherwise <see langword="false"/>.</returns>
    ValueTask<bool> TrySetSystemTimeAsync(
        DateTimeOffset value,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Provides system time through the BCL and platform clock-setting APIs.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    private const int ClockRealtime = 0;
    /// <summary>
    /// Gets the current local time.
    /// </summary>
    public DateTimeOffset Now => DateTimeOffset.Now;

    /// <summary>
    /// Gets the current Coordinated Universal Time.
    /// </summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <summary>
    /// Asynchronously attempts to set the system clock.
    /// </summary>
    /// <param name="value">The time to set.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns><see langword="true"/> when the system clock was set; otherwise <see langword="false"/>.</returns>
    public ValueTask<bool> TrySetSystemTimeAsync(
        DateTimeOffset value,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
        {
            var utc = value.UtcDateTime;
            var systemTime = new SystemTime
            {
                Year = checked((ushort)utc.Year),
                Month = checked((ushort)utc.Month),
                DayOfWeek = checked((ushort)utc.DayOfWeek),
                Day = checked((ushort)utc.Day),
                Hour = checked((ushort)utc.Hour),
                Minute = checked((ushort)utc.Minute),
                Second = checked((ushort)utc.Second),
                Milliseconds = checked((ushort)utc.Millisecond),
            };
            return ValueTask.FromResult(SetSystemTime(ref systemTime));
        }

        if (OperatingSystem.IsLinux())
        {
            var utcTicks = value.UtcDateTime.Ticks;
            var time = new Timespec
            {
                Seconds = value.ToUnixTimeSeconds(),
                Nanoseconds = checked((utcTicks % TimeSpan.TicksPerSecond) * 100L),
            };
            return ValueTask.FromResult(ClockSetTime(ClockRealtime, ref time) == 0);
        }

        if (OperatingSystem.IsMacOS())
        {
            var utcTicks = value.UtcDateTime.Ticks;
            var time = new Timeval
            {
                Seconds = value.ToUnixTimeSeconds(),
                Microseconds = checked((utcTicks % TimeSpan.TicksPerSecond) / 10L),
            };
            return ValueTask.FromResult(SetTimeOfDay(ref time, IntPtr.Zero) == 0);
        }

        return ValueTask.FromResult(false);
    }

    [DllImport("libc", EntryPoint = "clock_settime", SetLastError = true)]
    private static extern int ClockSetTime(int clockId, ref Timespec time);

    [DllImport("libc", EntryPoint = "settimeofday", SetLastError = true)]
    private static extern int SetTimeOfDay(ref Timeval time, IntPtr timeZone);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSystemTime(ref SystemTime systemTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec
    {
        /// <summary>
        /// Stores the seconds value.
        /// </summary>
        public long Seconds;
        /// <summary>
        /// Stores the nanoseconds value.
        /// </summary>
        public long Nanoseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Timeval
    {
        /// <summary>
        /// Stores the seconds value.
        /// </summary>
        public long Seconds;
        /// <summary>
        /// Stores the microseconds value.
        /// </summary>
        public long Microseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        /// <summary>
        /// Stores the year value.
        /// </summary>
        public ushort Year;
        /// <summary>
        /// Stores the month value.
        /// </summary>
        public ushort Month;
        /// <summary>
        /// Stores the day of week value.
        /// </summary>
        public ushort DayOfWeek;
        /// <summary>
        /// Stores the day value.
        /// </summary>
        public ushort Day;
        /// <summary>
        /// Stores the hour value.
        /// </summary>
        public ushort Hour;
        /// <summary>
        /// Stores the minute value.
        /// </summary>
        public ushort Minute;
        /// <summary>
        /// Stores the second value.
        /// </summary>
        public ushort Second;
        /// <summary>
        /// Stores the milliseconds value.
        /// </summary>
        public ushort Milliseconds;
    }
}
