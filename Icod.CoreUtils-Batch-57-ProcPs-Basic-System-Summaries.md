# Batch 57 — ProcPs Basic System Summaries

## Scope and upstream baseline

Batch 57 implements the ProcPs-owned `uptime` and `free` profiles against **procps-ng 4.0.6**. The historical Batch 9 `Icod.CoreUtils.Uptime` implementation is retired and replaced by `Icod.ProcPs.Uptime`; there is only one live `uptime` executable. To preserve existing repository packaging/build expectations, the replacement `uptime` keeps the historical `bin/<Configuration>/` output location and lowercase assembly name. `free` uses the ProcPs suite output directory.

Upstream references used for the compatibility matrix:

- `src/uptime.c` from procps-ng 4.0.6
- `library/uptime.c` from procps-ng 4.0.6
- `src/free.c` from procps-ng 4.0.6
- `local/units.c` from procps-ng 4.0.6
- the Batch 56 `Icod.ProcPs.Shared` observation/provenance contracts

## `Icod.ProcPs.Uptime`

The solution reuses the former uptime project/test GUIDs while changing their names and paths to the ProcPs-owned projects. The obsolete `uptime/` and `tests/Uptime.Tests/` files are deleted.

Implemented forms include default, `--container`, `--pretty`, `--raw`, `--since`, help/version, and `PROCPS_CONTAINER`.

Compatibility details include GNU `getopt_long` operand permutation; raw mode deliberately using system uptime; procps-ng strict uptime decomposition boundaries; DST-correct historical local time for `--since`; exact Linux `/proc/uptime` provenance; Linux container uptime derived from PID 1 procfs start ticks and libc `_SC_CLK_TCK`; explicit derived provenance; procps raw numeric layout; and controlled failures for unavailable provider data.

## `Icod.ProcPs.Free`

Implemented forms include byte/binary/decimal units, human/`--si`, low/high, line, total, committed, wide, repeat/count, help/version, and GNU-style long-option abbreviations.

Compatibility details include mutually exclusive unit options; byte-domain command scaling equivalent to procps-ng's kB-to-byte conversion; truncating fixed-unit output; procps human-fit rules through petabytes; `MemAvailable` fallback; `Cached + SReclaimable`; low-memory fallback; nonnegative swap/high/low used values; procps-style unsigned committed-memory subtraction; repeat separator placement before delay; decimal-point/comma repeat intervals converted through single-precision microseconds; exact Linux `/proc/meminfo` provenance; and controlled unsupported-platform behavior.

## Tests added

Dedicated xUnit projects cover representative exact output layouts, version/help/operand behavior, GNU option permutation, DST-sensitive `--since`, unavailable users, controlled provider limitations, Linux procfs/derived provenance, unit conversion, `MemAvailable` fallback, repeat/count sampling, decimal-comma intervals, and ambiguous long options.

## Validation status

The source, project XML, solution registration, repository-relative paths, and LF/UTF-8 text layout were statically validated for this delivery. The implementation environment does **not** contain `dotnet`, `csc`, `msbuild`, or another .NET build toolchain. Consequently, the roadmap's required Debug/Release builds, complete xUnit run, differential CLI fixtures, and `windows-latest` / `ubuntu-latest` / `macos-latest` CI checks were not executed here and remain the merge/closure gate.
