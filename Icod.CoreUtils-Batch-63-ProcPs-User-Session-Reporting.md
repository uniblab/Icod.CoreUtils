# Batch 63 — ProcPs `w` user/session reporting

Batch 63 adds the suite-correct `Icod.ProcPs.W` executable with assembly name `w` and a dedicated `tests/ProcPs.W.Tests` project. The command is a presentation layer over `Icod.ProcPs.Shared`: login/session accounting, process observation, uptime/load metrics, and account matching remain shared provider responsibilities.

## Shared session observations

`Icod.ProcPs.Shared/src/UserSessions.cs` introduces `ProcLoginSession`, `IProcLoginSessionProvider`, and `SystemProcLoginSessionProvider`.

- Linux reads libc `utmpx` user-process records. Terminal idle time is derived from the controlling terminal device's last-access time when that observation is permitted. Login PID, terminal, host origin, numeric `ut_addr_v6` origin, and login time retain native accounting semantics.
- macOS reads Darwin `utmpx`. The record layout is the same one already used by the ProcPs system-metrics provider for logged-in-user counts; detailed observations are marked equivalent because Darwin accounting semantics are not identical to procps-ng on Linux.
- Windows uses Terminal Services APIs. `WTSEnumerateSessionsW` and `WTSQuerySessionInformationW` supply the interactive user, station name, client name/address, logon time, last-input time, and platform session identifier. These are explicitly marked equivalent native session observations rather than being described as `utmp` records.
- Unsupported hosts return a controlled capability observation instead of fabricated session data.

## `w` compatibility surface

The command implements the procps-ng 4.0.6 Batch 63 surface: default long output, `-s` short output, `-h` suppression of both heading lines, `-f` FROM toggling, `-u` current-user-filter suppression, `-p` login/current PID prefixing beside WHAT, `-o` legacy idle formatting, `-t` terminal enumeration supplementing accounting records, `-c`/`--container`, `-i`/`--ip-addr`, help/version handling, and the optional user operand. `PROCPS_CONTAINER`, `PROCPS_USERLEN`, `PROCPS_FROMLEN`, and `COLUMNS` are honored with procps-compatible width bounds.

JCPU is accumulated from all processes associated with the login session. On Linux, the shared procfs snapshot now retains the terminal foreground process-group identifier (`tpgid`); on macOS, the libproc provider carries Darwin’s native terminal foreground process-group observation. PCPU and WHAT therefore prefer the newest process in the foreground group on both platforms. Where a platform cannot expose that concept, selection falls back to the newest associated process. Unless `-u` is supplied, candidates are restricted to the login user's real or effective UID when that UID can be resolved. Association uses native platform-session IDs first, controlling-terminal identity second, and the login process's POSIX session ID as a fallback.

The heading combines the shared uptime observation, login-session count, and cross-platform load averages. A platform that can report uptime but cannot expose defensible Unix-style load averages prints `n/a` load values rather than inventing them. Container uptime is delegated to `IProcSystemMetricsProvider.GetUptimeAsync`; unsupported container semantics therefore remain an explicit provider diagnostic.

The `--ip-addr` form prefers a numeric address supplied by the native accounting provider: Linux uses `ut_addr_v6` and Windows uses the WTS client-address observation. The command does not perform a second DNS lookup merely to manufacture an address; macOS or other providers that expose only a client name fall back to that defensible native value.

## Validation

The Batch 63 tests use injected `stdout` and `stderr` streams throughout. The shared Linux procfs fixture also asserts `tpgid` parsing at the provider boundary. They cover long/short forms, headings, user filtering, FROM toggling, foreground-group JCPU/PCPU and WHAT selection, PID output, `-u`, terminal supplementation, container delegation, valid and invalid ProcPs width environment variables, numeric-origin mode, controlled missing-accounting diagnostics, help/version/errors, and cancellation.

Repository build/test and the required `windows-latest`, `ubuntu-latest`, and `macos-latest` runner validation remain the closure step.
