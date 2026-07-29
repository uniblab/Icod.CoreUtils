using System.Globalization;
using System.Text.RegularExpressions;

namespace Icod.CoreUtils.Shared.Time;

/// <summary>
/// Represents date parse result.
/// </summary>
/// <param name="Success">The success value.</param>
/// <param name="Value">The value value.</param>
/// <param name="TimeZone">The time zone value.</param>
/// <param name="Diagnostic">The diagnostic value.</param>
public sealed record DateParseResult(
    bool Success,
    DateTimeOffset Value,
    TimeZoneInfo TimeZone,
    string Diagnostic
);

/// <summary>
/// Provides gnu date parser operations.
/// </summary>
public static partial class GnuDateParser
{
    /// <summary>
    /// Performs the parse operation.
    /// </summary>
    public static DateParseResult Parse(
        string input,
        DateTimeOffset baseTime,
        TimeZoneInfo defaultTimeZone
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(defaultTimeZone);

        var text = input.Trim();
        var timeZone = defaultTimeZone;
        var timeZoneMatch = TimeZonePrefixRegex().Match(text);
        if (timeZoneMatch.Success)
        {
            var zoneName = timeZoneMatch.Groups[2].Value;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(zoneName);
                text = text[timeZoneMatch.Length..].TrimStart();
            }
            catch (TimeZoneNotFoundException)
            {
                return Failure(baseTime, defaultTimeZone, String.Concat("unknown time zone: ", zoneName));
            }
            catch (InvalidTimeZoneException)
            {
                return Failure(baseTime, defaultTimeZone, String.Concat("invalid time zone: ", zoneName));
            }
        }

        var zonedBase = TimeZoneInfo.ConvertTime(baseTime, timeZone);
        if (String.IsNullOrEmpty(text))
        {
            var start = AtLocalTime(zonedBase.Date, timeZone);
            return Success(start, timeZone, "empty date denotes the start of today");
        }

        if (text[0] == '@'
            && Decimal.TryParse(
                text.AsSpan(1),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var epochSeconds))
        {
            try
            {
                var ticks = Decimal.ToInt64(epochSeconds * TimeSpan.TicksPerSecond);
                var value = DateTimeOffset.UnixEpoch.AddTicks(ticks);
                return Success(value, timeZone, "parsed seconds since the Unix epoch");
            }
            catch (OverflowException)
            {
                return Failure(baseTime, timeZone, "epoch value is outside the supported range");
            }
            catch (ArgumentOutOfRangeException)
            {
                return Failure(baseTime, timeZone, "epoch value is outside the supported range");
            }
        }

        switch (text.ToLowerInvariant())
        {
            case "now":
                return Success(baseTime, timeZone, "parsed current date and time");
            case "today":
            case "midnight":
                return Success(AtLocalTime(zonedBase.Date, timeZone), timeZone, "parsed start of today");
            case "noon":
                return Success(AtLocalTime(zonedBase.Date.AddHours(12.0), timeZone), timeZone, "parsed noon today");
            case "yesterday":
                return Success(AtLocalTime(zonedBase.Date.AddDays(-1.0), timeZone), timeZone, "parsed yesterday");
            case "tomorrow":
                return Success(AtLocalTime(zonedBase.Date.AddDays(1.0), timeZone), timeZone, "parsed tomorrow");
        }

        var weekdayMatch = WeekdayRegex().Match(text);
        if (weekdayMatch.Success
            && TryParseWeekday(weekdayMatch.Groups[2].Value, out var weekday))
        {
            var direction = String.Equals(
                weekdayMatch.Groups[1].Value,
                "next",
                StringComparison.OrdinalIgnoreCase
            ) ? 1 : -1;
            var date = zonedBase.Date;
            do
            {
                date = date.AddDays(direction);
            }
            while (date.DayOfWeek != weekday);

            return Success(AtLocalTime(date, timeZone), timeZone, "parsed relative weekday");
        }

        var relativeMatch = RelativeRegex().Match(text);
        if (relativeMatch.Success
            && Int32.TryParse(
                relativeMatch.Groups[1].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            if (relativeMatch.Groups[3].Success)
            {
                amount = -amount;
            }

            try
            {
                var value = ApplyRelative(zonedBase, amount, relativeMatch.Groups[2].Value);
                return Success(value, timeZone, "parsed relative date expression");
            }
            catch (ArgumentOutOfRangeException)
            {
                return Failure(baseTime, timeZone, "relative date is outside the supported range");
            }
        }

        foreach (var culture in new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture })
        {
            if (HasExplicitOffset(text)
                && DateTimeOffset.TryParse(
                    text,
                    culture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var parsedOffset))
            {
                return Success(parsedOffset, timeZone, "parsed calendar date and time with explicit offset");
            }

            if (DateTime.TryParse(
                text,
                culture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDateTime))
            {
                var unspecified = DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Unspecified);
                return Success(AtLocalTime(unspecified, timeZone), timeZone, "parsed local calendar date and time");
            }
        }

        return Failure(baseTime, timeZone, "unrecognized date expression");
    }

    private static DateTimeOffset ApplyRelative(
        DateTimeOffset value,
        int amount,
        string unit
    ) => unit.ToLowerInvariant() switch
    {
        "second" or "seconds" => value.AddSeconds(amount),
        "minute" or "minutes" => value.AddMinutes(amount),
        "hour" or "hours" => value.AddHours(amount),
        "day" or "days" => value.AddDays(amount),
        "week" or "weeks" => value.AddDays(checked(amount * 7.0)),
        "month" or "months" => value.AddMonths(amount),
        "year" or "years" => value.AddYears(amount),
        _ => value,
    };

    private static DateTimeOffset AtLocalTime(DateTime dateTime, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1.0);
        }

        var offset = timeZone.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset);
    }

    private static bool HasExplicitOffset(string value)
    {
        if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var match = ExplicitOffsetRegex().Match(value);
        return match.Success;
    }

    private static bool TryParseWeekday(string value, out DayOfWeek day)
    {
        for (var index = 0; index < 7; index++)
        {
            var candidate = (DayOfWeek)index;
            if (String.Equals(value, candidate.ToString(), StringComparison.OrdinalIgnoreCase)
                || String.Equals(
                    value,
                    CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(candidate),
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                day = candidate;
                return true;
            }
        }

        day = default;
        return false;
    }

    private static DateParseResult Success(
        DateTimeOffset value,
        TimeZoneInfo timeZone,
        string diagnostic
    ) => new(true, value, timeZone, diagnostic);

    private static DateParseResult Failure(
        DateTimeOffset value,
        TimeZoneInfo timeZone,
        string diagnostic
    ) => new(false, value, timeZone, diagnostic);

    [GeneratedRegex("[+-]\\d{2}:?\\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitOffsetRegex();

    [GeneratedRegex("^TZ=(['\"])(.*?)\\1\\s*", RegexOptions.CultureInvariant)]
    private static partial Regex TimeZonePrefixRegex();

    [GeneratedRegex("^(next|last)\\s+([A-Za-z]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WeekdayRegex();

    [GeneratedRegex("^([+-]?\\d+)\\s*(seconds?|minutes?|hours?|days?|weeks?|months?|years?)(?:\\s+(ago))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RelativeRegex();
}
