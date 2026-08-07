# Time

The `Icod.CoreUtils.Shared.Time` namespace contains date parsing, date formatting, wall-clock services, and monotonic scheduling contracts.

## Responsibilities

- Supply local and UTC current-time values.
- Attempt controlled system-clock updates through platform-specific APIs.
- Parse GNU-compatible date operands relative to an explicit base time and time zone.
- Format GNU date conversion specifications consistently across commands.
- Supply injectable monotonic timestamps and cancellation-aware delays.
- Produce drift-resistant fixed-rate periodic ticks without depending on wall-clock adjustments.

## Portability policy

BCL date, time-zone, formatting, `Stopwatch`, and task-delay APIs are preferred. Native clock-setting calls are isolated behind `IDateTimeProvider`; unsupported hosts return a controlled unsuccessful result rather than exposing a platform exception. Timeout and refresh logic must use `IMonotonicClock` rather than local or UTC wall time. Tests may inject a deterministic clock so timeout and periodic behavior do not depend on CI runner load.
