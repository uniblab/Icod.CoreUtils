# Icod.CoreUtils.Shared.Time

This namespace contains the Coreutils-specific wall-clock and GNU date policy
retained after the cross-suite monotonic timing substrate moved to the
standalone `Icod.Timing` package.

## Responsibilities

- Supply local and UTC current-time values for Coreutils commands.
- Attempt controlled system-clock updates through platform-specific APIs.
- Parse GNU-compatible date operands relative to an explicit base time and time zone.
- Format GNU date conversion specifications consistently across commands.

The retained production types are:

- `IDateTimeProvider`
- `SystemDateTimeProvider`
- `GnuDateParser`
- `GnuDateFormatter`

## Neutral timing ownership

Cross-suite monotonic timing and scheduling are owned by `Icod.Timing`,
including:

- `IMonotonicClock`
- `SystemMonotonicClock`
- `IPeriodicScheduler`
- `MonotonicPeriodicScheduler`
- `PeriodicTick`

Coreutils commands or co-resident suites that need timeout, elapsed-time,
sampling, or refresh-cadence behavior consume those published timing contracts
directly.

## Portability policy

BCL date, time-zone, and formatting APIs are preferred. Native clock-setting calls remain isolated behind `IDateTimeProvider`; unsupported hosts return a controlled unsuccessful result rather than exposing a platform exception.

GNU date parsing and formatting remain in `Icod.CoreUtils.Shared.Time` because they encode Coreutils-specific command semantics rather than neutral scheduling infrastructure.
