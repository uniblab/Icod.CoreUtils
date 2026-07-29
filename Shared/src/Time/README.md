# Time

The `Icod.CoreUtils.Shared.Time` namespace contains date parsing, date formatting, and injectable clock services.

## Responsibilities

- Supply local and UTC current-time values.
- Attempt controlled system-clock updates through platform-specific APIs.
- Parse GNU-compatible date operands relative to an explicit base time and time zone.
- Format GNU date conversion specifications consistently across commands.

## Portability policy

BCL date, time-zone, and formatting APIs are preferred. Native clock-setting calls are isolated behind `IDateTimeProvider`; unsupported hosts return a controlled unsuccessful result rather than exposing a platform exception.
