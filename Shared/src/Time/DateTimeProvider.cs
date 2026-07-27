using System.Runtime.InteropServices;

namespace Icod.CoreUtils.Shared.Time;

public interface IDateTimeProvider
{
    DateTimeOffset Now { get; }

    DateTimeOffset UtcNow { get; }

    ValueTask<bool> TrySetSystemTimeAsync(
        DateTimeOffset value,
        CancellationToken cancellationToken = default
    );
}

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    private const int ClockRealtime = 0;
    public DateTimeOffset Now => DateTimeOffset.Now;

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

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
        public long Seconds;
        public long Nanoseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Timeval
    {
        public long Seconds;
        public long Microseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Milliseconds;
    }
}
