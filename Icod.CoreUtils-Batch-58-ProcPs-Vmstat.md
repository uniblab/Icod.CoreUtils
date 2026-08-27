# Batch 58 — ProcPs `vmstat`

## Baseline

Batch 58 implements `Icod.ProcPs.Vmstat` against the procps-ng 4.0.6 `vmstat` command profile and extends `Icod.ProcPs.Shared` with the observations needed by sampled system-statistics consumers.

The Linux implementation remains the authoritative compatibility profile. Windows and macOS report only native observations that have a defensible semantic correspondence; missing Linux-specific observations are represented explicitly rather than fabricated.

## Command surface

The new executable has assembly name `vmstat` and supports:

- default process, memory, swap, block-I/O, system, and CPU reporting;
- `-a` / `--active` active/inactive memory columns;
- `-f` / `--forks` cumulative fork reporting;
- `-m` / `--slabs` slab reporting;
- `-n` / `--one-header` header suppression during repeated reports;
- `-s` / `--stats` cumulative statistics;
- `-d` / `--disk` disk statistics;
- `-D` / `--disk-sum` disk-summary statistics;
- `-p` / `--partition` partition-specific statistics;
- `-S` / `--unit` with the procps-ng `b`, `B`, `k`, `K`, `m`, and `M` first-character behavior;
- `-w` / `--wide`;
- `-t` / `--timestamp` where procps-ng applies timestamps;
- `-y` / `--no-first`;
- GNU-style long-option abbreviation and ordinary-operand permutation behavior used elsewhere in the ProcPs ports;
- `[delay [count]]` repeated sampling;
- cancellation with status 130;
- procps-ng 4.0.6 help/version identity.

## Sampling semantics

The default report distinguishes the first row from later rows:

- the first row reports cumulative/since-boot quantities using the procps-ng 4.0.6 divisors;
- subsequent rows use counter deltas over the requested interval;
- `--no-first` captures a baseline, prints the header, waits one interval, and then emits delta rows;
- swap counters are converted to the selected unit before interval-rate rounding, matching procps-ng;
- page-I/O and system-event counters retain their procps-ng units;
- CPU percentages use integer half-up rounding rather than floating-point rounding;
- guest ticks remain in the CPU divisor before being subtracted from displayed user time, matching procps-ng;
- Linux backward-moving idle ticks use the same debt/carry-forward treatment as procps-ng instead of being mistaken for unsigned counter wrap;
- neutral CPU counters honor the provider's counter width, including Darwin's 32-bit Mach CPU counters.

Two procps-ng 4.0.6 behaviors that look unusual but are intentionally preserved are:

1. the initial context-switch value is divided by the cumulative CPU-tick divisor, while initial interrupts are divided by uptime;
2. disk `cur` and `sec` presentation retains procps-ng's `/1000` conversion.

## Shared provider additions

`Icod.ProcPs.Shared` adds `IProcVmstatProvider` and native dispatch through `SystemProcVmstatProvider`.

### Linux

`LinuxProcVmstatProvider` supplies the complete Batch 58 profile:

- process queues, interrupts, context switches, boot time, and fork count from `/proc/stat`;
- paging and swap counters from `/proc/vmstat`;
- memory, CPU, uptime, slab, and raw VM counters from the existing Linux system provider;
- disks and partitions from `/proc/diskstats`;
- partition classification through `/sys/dev/block/<major>:<minor>/partition`.

These observations retain Linux procfs provenance and exact fidelity.

### Windows

`WindowsProcVmstatProvider` exposes the native neutral memory and aggregate CPU observations already implemented by the Windows ProcPs provider correction. Linux-only queue, Unix paging, `/proc` VM-event, Linux diskstats, slab, and fork-summary semantics remain explicitly unavailable rather than being synthesized from unrelated Windows counters.

The default report therefore renders known Windows memory/CPU fields and `-` placeholders for observations without a defensible equivalent. Linux-specific specialized report modes return controlled unsupported diagnostics.

### macOS

`MacOsProcVmstatProvider` exposes neutral memory and aggregate CPU observations plus Mach paging counters. The Darwin memory observation now retains active/inactive pages, page-ins/page-outs, swap-ins/swap-outs, and page size in source-specific fields so `vmstat` can consume them without introducing Linux field names.

The default report renders the defensible Darwin memory/CPU/paging subset and explicit placeholders for unavailable Linux process-queue/system-event categories. Linux diskstats/slab/statistics-summary modes remain controlled unsupported operations.

## Specialized report compatibility

Linux specialized modes retain the procps-ng 4.0.6 presentation rules, including:

- disk and slab sorting/filtering behavior;
- disk and disk-summary counter fields;
- partition header/data widths and `/dev/` stripping;
- slab cache-name ascending order;
- raw user/nice ticks in `--stats` rather than guest-subtracted display values;
- partition and slab modes ignoring `--timestamp` as upstream does;
- `--one-header` behavior for repeated disk/slab output.

## Tests

The Batch 58 test projects cover:

- default headers and active/wide/timestamp variants;
- unit conversion;
- first-row since-boot rates;
- procps-ng's initial context-switch divisor;
- guest accounting;
- backward Linux idle-tick debt and recovery;
- `--no-first` and interval deltas;
- delay/count behavior;
- partial-platform placeholders;
- unsupported specialized modes;
- forks, disk, partition, slab, and statistics modes;
- one-header behavior;
- timestamp exceptions for partition/slab;
- statistics raw CPU ticks;
- option conflicts, long/short parsing, help, and version;
- Linux `/proc/stat` and `/proc/diskstats` fixtures;
- counter wraparound;
- cancellation;
- OS-gated native provider capability/provenance checks.

## Roadmap status

The roadmap is advanced through completed/merged Batch 57 and the validated three-platform ProcPs provider correction. Batch 58 is marked implemented and ready for repository/runner validation. Batch 59 remains the next engineering batch after Batch 58 validation.

## Validation note

The implementation environment used to prepare this batch does not contain a .NET SDK, so `dotnet build` and the xUnit suite could not be executed locally. The delivered sources were statically checked for solution/project structure, XML validity, source delimiter balance, and repository text-format compliance; the repository's current authoritative text-format policy is defined by `.editorconfig`. Final closure still requires the repository's Debug/Release and `windows-latest`, `ubuntu-latest`, and `macos-latest` test runs.
