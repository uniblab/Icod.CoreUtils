# Icod.ProcPs.Sysctl source

This directory contains the procps-ng-compatible `sysctl` command implementation for the Icod ProcPs suite.

- `Command.cs` owns command-line parsing, procps-compatible output/status behavior, configuration parsing, glob expansion, and `--system` ordering.
- `SysctlBackend.cs` defines the injectable kernel/configuration boundary and the production Linux `/proc/sys` implementation.

The production backend is intentionally Linux-centric. It does not reinterpret unrelated Windows, macOS, or BSD settings as Linux sysctl keys. Help and version reporting remain portable, while kernel-parameter operations return a controlled unsupported-platform diagnostic when Linux `/proc/sys` is unavailable.
