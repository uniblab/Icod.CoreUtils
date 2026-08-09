# Batch 65 — ProcPs terminal load display

Batch 65 adds `Icod.ProcPs.Tload` with assembly name `tload` and establishes the first focused consumer of the ProcPs full-screen refresh foundation.

## Compatibility surface

The command follows procps-ng 4.0.6 for its user-facing option surface:

- `-d`, `--delay <secs>` selects a positive refresh delay in seconds; the default is five seconds.
- `-s`, `--scale <num>` selects the vertical graph scale. Zero retains automatic scaling and negative values are rejected.
- `-h`, `--help` and `-V`, `--version` exit without opening a terminal.
- One optional terminal operand selects the output terminal; more than one operand is an error.

The graph uses the one-minute load average for vertical plotting, overlays the current one-, five-, and fifteen-minute values, retains procps-style horizontal scale ticks, and automatically reduces the vertical scale when the current load would exceed the display height.

## Shared full-screen runtime

`Icod.ProcPs.Shared/src/FullScreen.cs` introduces reusable contracts for later full-screen ProcPs tools:

- writable terminal endpoints with current geometry, frame-home writes, and explicit restoration;
- a terminal factory that can wrap standard output or open a selected terminal path;
- standard-output attachment and geometry through the existing cross-suite `SystemTerminalDeviceProvider`, plus native tty geometry for an explicitly selected terminal;
- polling of current terminal geometry so resize changes are visible to the refresh loop;
- POSIX `SIGWINCH`, `SIGCONT`, and `SIGTSTP` coordination;
- interactive termination cancellation for POSIX signals and Windows console cancellation;
- restoration before POSIX suspension and restoration in the command's `finally` path;
- caller-owned output streams are not disposed by the terminal wrapper.

The runtime deliberately leaves command-specific key handling, graph layout, and option parsing in the consuming command projects.

## Platform boundary

`tload` consumes the neutral `ProcLoadAverages` observation from `Icod.ProcPs.Shared`. Linux obtains authoritative procfs load averages and macOS uses its native load-average provider. Hosts without a defensible load-average observation receive a controlled failure. The command does not manufacture a Windows analogue for Unix load average.

When no explicit terminal operand is supplied, redirected standard output is rejected. This prevents an infinite full-screen cursor-control stream from being silently written into a pipe or ordinary file. An explicitly selected terminal remains permitted.

## Tests

`Icod.ProcPs.Tload.Tests` uses injected output/error streams, a fake terminal endpoint and terminal factory, a fake signal source, a fake system-metrics provider, and `ProcSampler` over an injectable monotonic clock/fixed scheduler. Coverage includes:

- default rendering and five-second cadence;
- delay and scale parsing;
- selected terminal routing;
- resize re-layout;
- redirected-output policy;
- unavailable load-average handling;
- cancellation and terminal restoration;
- controlled frame-write failures with terminal restoration;
- suspension restoration and resume re-entry;
- invalid options, help, and version.

The tests do not write directly to the test runner's standard output or standard error.

## Validation

The patch is structured for the repository's `Debug`, `Staging`, and `Release` configurations and `net10.0`. Full `dotnet clean`, restore, build, and test validation on `windows-latest`, `ubuntu-latest`, and `macos-latest` remains the repository closure gate.
