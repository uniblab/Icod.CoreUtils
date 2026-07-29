using System.Globalization;
using System.Text;

namespace Icod.CoreUtils.Shared.Time;

/// <summary>
/// Provides gnu date formatter operations.
/// </summary>
public static class GnuDateFormatter
{
    /// <summary>
    /// Performs the format operation.
    /// </summary>
    public static string Format(
        DateTimeOffset value,
        string format,
        TimeZoneInfo timeZone,
        CultureInfo? culture = null
    )
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(timeZone);
        culture ??= CultureInfo.CurrentCulture;

        var zoned = TimeZoneInfo.ConvertTime(value, timeZone);
        var builder = new StringBuilder(format.Length + 32);
        for (var index = 0; index < format.Length; index++)
        {
            var character = format[index];
            if (character != '%' || index + 1 >= format.Length)
            {
                builder.Append(character);
                continue;
            }

            index++;
            var colonCount = 0;
            while (index < format.Length && format[index] == ':')
            {
                colonCount++;
                index++;
            }

            var flags = String.Empty;
            while (index < format.Length && "-_0+^#".Contains(format[index]))
            {
                flags = String.Concat(flags, format[index]);
                index++;
            }

            var widthStart = index;
            while (index < format.Length && Char.IsAsciiDigit(format[index]))
            {
                index++;
            }

            int? width = null;
            if (index > widthStart
                && Int32.TryParse(
                    format.AsSpan(widthStart, index - widthStart),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsedWidth))
            {
                width = parsedWidth;
            }

            if (index < format.Length && (format[index] == 'E' || format[index] == 'O'))
            {
                index++;
            }

            if (index >= format.Length)
            {
                builder.Append('%');
                break;
            }

            var directive = format[index];
            var text = FormatDirective(zoned, directive, colonCount, timeZone, culture);
            text = ApplyFlags(text, directive, flags, width, culture);
            builder.Append(text);
        }

        return builder.ToString();
    }

    private static string FormatDirective(
        DateTimeOffset value,
        char directive,
        int colonCount,
        TimeZoneInfo timeZone,
        CultureInfo culture
    ) => directive switch
    {
        '%' => "%",
        'a' => culture.DateTimeFormat.GetAbbreviatedDayName(value.DayOfWeek),
        'A' => culture.DateTimeFormat.GetDayName(value.DayOfWeek),
        'b' or 'h' => culture.DateTimeFormat.GetAbbreviatedMonthName(value.Month),
        'B' => culture.DateTimeFormat.GetMonthName(value.Month),
        'c' => value.ToString("F", culture),
        'C' => Numeric(value.Year / 100, 2),
        'd' => Numeric(value.Day, 2),
        'D' => value.ToString("MM/dd/yy", CultureInfo.InvariantCulture),
        'e' => value.Day.ToString(" 0", CultureInfo.InvariantCulture),
        'F' => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        'g' => Numeric(ISOWeek.GetYear(value.DateTime) % 100, 2),
        'G' => ISOWeek.GetYear(value.DateTime).ToString("0000", CultureInfo.InvariantCulture),
        'H' => Numeric(value.Hour, 2),
        'I' => Numeric(value.Hour % 12 == 0 ? 12 : value.Hour % 12, 2),
        'j' => Numeric(value.DayOfYear, 3),
        'k' => value.Hour.ToString(" 0", CultureInfo.InvariantCulture),
        'l' => (value.Hour % 12 == 0 ? 12 : value.Hour % 12).ToString(" 0", CultureInfo.InvariantCulture),
        'm' => Numeric(value.Month, 2),
        'M' => Numeric(value.Minute, 2),
        'n' => Environment.NewLine,
        'N' => String.Concat(
            ((value.Ticks % TimeSpan.TicksPerSecond) * 100L).ToString("000000000", CultureInfo.InvariantCulture)
        ),
        'p' => value.ToString("tt", culture).ToUpper(culture),
        'P' => value.ToString("tt", culture).ToLower(culture),
        'q' => ((value.Month - 1) / 3 + 1).ToString(CultureInfo.InvariantCulture),
        'r' => value.ToString("hh:mm:ss tt", culture),
        'R' => value.ToString("HH:mm", CultureInfo.InvariantCulture),
        's' => value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        'S' => Numeric(value.Second, 2),
        't' => "\t",
        'T' => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        'u' => ((int)value.DayOfWeek == 0 ? 7 : (int)value.DayOfWeek).ToString(CultureInfo.InvariantCulture),
        'U' => GetWeekNumber(value.DateTime, DayOfWeek.Sunday).ToString("00", CultureInfo.InvariantCulture),
        'V' => ISOWeek.GetWeekOfYear(value.DateTime).ToString("00", CultureInfo.InvariantCulture),
        'w' => ((int)value.DayOfWeek).ToString(CultureInfo.InvariantCulture),
        'W' => GetWeekNumber(value.DateTime, DayOfWeek.Monday).ToString("00", CultureInfo.InvariantCulture),
        'x' => value.ToString("d", culture),
        'X' => value.ToString("T", culture),
        'y' => Numeric(value.Year % 100, 2),
        'Y' => value.Year.ToString("0000", CultureInfo.InvariantCulture),
        'z' => FormatOffset(value.Offset, colonCount),
        'Z' => GetZoneName(value, timeZone),
        _ => String.Concat('%', directive),
    };

    private static string ApplyFlags(
        string text,
        char directive,
        string flags,
        int? width,
        CultureInfo culture
    )
    {
        if (flags.Contains('^'))
        {
            text = text.ToUpper(culture);
        }
        else if (flags.Contains('#'))
        {
            text = SwapCase(text, culture);
        }

        if (flags.Contains('-'))
        {
            text = text.TrimStart('0', ' ');
            return text.Length == 0 ? "0" : text;
        }

        if (directive == 'N' && width.HasValue && width.Value >= 0 && width.Value < text.Length)
        {
            return text[..width.Value];
        }

        if (!width.HasValue || text.Length >= width.Value)
        {
            return text;
        }

        var padding = flags.Contains('_') ? ' ' : '0';
        var numeric = "CdeGgHIkjlmMqSUuVwWyY".Contains(directive);
        return numeric
            ? text.PadLeft(width.Value, padding)
            : text.PadRight(width.Value, ' ');
    }

    private static string SwapCase(string value, CultureInfo culture)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(Char.IsUpper(character)
                ? Char.ToLower(character, culture)
                : Char.IsLower(character)
                    ? Char.ToUpper(character, culture)
                    : character);
        }

        return builder.ToString();
    }

    private static string Numeric(int value, int width) =>
        value.ToString(String.Concat("D", width.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    private static string FormatOffset(TimeSpan offset, int colonCount)
    {
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        offset = offset.Duration();
        var hours = ((int)offset.TotalHours).ToString("00", CultureInfo.InvariantCulture);
        var minutes = offset.Minutes.ToString("00", CultureInfo.InvariantCulture);
        var seconds = offset.Seconds.ToString("00", CultureInfo.InvariantCulture);
        return colonCount switch
        {
            0 => String.Concat(sign, hours, minutes),
            1 => String.Concat(sign, hours, ':', minutes),
            2 => String.Concat(sign, hours, ':', minutes, ':', seconds),
            _ when offset.Seconds != 0 => String.Concat(sign, hours, ':', minutes, ':', seconds),
            _ when offset.Minutes != 0 => String.Concat(sign, hours, ':', minutes),
            _ => String.Concat(sign, hours),
        };
    }

    private static string GetZoneName(DateTimeOffset value, TimeZoneInfo timeZone)
    {
        if (timeZone.Equals(TimeZoneInfo.Utc))
        {
            return "UTC";
        }

        return timeZone.IsDaylightSavingTime(value.DateTime)
            ? timeZone.DaylightName
            : timeZone.StandardName;
    }

    private static int GetWeekNumber(DateTime value, DayOfWeek firstDay)
    {
        var first = new DateTime(value.Year, 1, 1);
        var daysToFirst = ((int)firstDay - (int)first.DayOfWeek + 7) % 7;
        var firstWeekStart = first.AddDays(daysToFirst);
        if (value.Date < firstWeekStart.Date)
        {
            return 0;
        }

        return (value.Date - firstWeekStart.Date).Days / 7 + 1;
    }
}
