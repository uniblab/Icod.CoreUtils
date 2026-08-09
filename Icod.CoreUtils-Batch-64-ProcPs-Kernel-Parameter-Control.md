# Batch 64 — ProcPs kernel parameter control

Batch 64 adds `Icod.ProcPs.Sysctl` with assembly name `sysctl` and a dedicated `Icod.ProcPs.Sysctl.Tests` project.

The implementation follows the procps-ng 4.0.6 command surface for direct reads and writes, dot/slash key forms, `-a`, `--deprecated`, `--dry-run`, `-b`, `-e`, `-N`, `-n`, `-p`/`-f`, `--system`, `-r`, `-q`, and `-w`. Configuration loading supports ignored failures, glob assignments and exclusions, multiple preload files, standard input, and the procps `sysctl.d` precedence/order model followed by `/etc/sysctl.conf`.

The production backend is intentionally Linux-specific and reads/writes `/proc/sys`. Windows and macOS are not given synthetic Linux key mappings; help/version remain available and operational requests fail with a controlled capability diagnostic when `/proc/sys` is unavailable.

Repository build and runner validation remain the completion gate after application of this patch.
