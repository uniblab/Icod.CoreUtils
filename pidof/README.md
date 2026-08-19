# Icod.ProcPs.PidOf

`Icod.ProcPs.PidOf` implements the procps-ng 4.0.6 `pidof` profile over the
shared ProcPs process-observation and path-identity layer.

Linux `/proc` is the authoritative compatibility profile. Windows and macOS use
their native process executable observations where those semantics are
defensible. Linux-only root-namespace and lightweight-task behavior remains
explicitly capability-dependent rather than being synthesized on other hosts.
