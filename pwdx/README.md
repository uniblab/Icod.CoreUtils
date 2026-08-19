# Icod.ProcPs.Pwdx

`Icod.ProcPs.Pwdx` implements the procps-ng 4.0.6 `pwdx` profile over the shared
reuse-aware process-path provider.

Linux reads `/proc/PID/cwd`, macOS uses Darwin `libproc`, and Windows reports a
controlled unsupported diagnostic because Windows does not expose another
process's current working directory through a stable documented API.
