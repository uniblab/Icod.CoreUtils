# Icod.CoreUtils Batch 61 — ProcPs Process Memory Maps

## Scope

Batch 61 adds `Icod.ProcPs.Pmap` and its dedicated xUnit project, and extends
`Icod.ProcPs.Shared` with the reuse-aware process-memory-map contract required
by later ProcPs consumers such as `ps`.

The compatibility baseline is procps-ng 4.0.6 `pmap`.

## Shared provider boundary

`IProcMemoryMapProvider` observes a previously captured `ProcProcessSnapshot`.
`SystemProcMemoryMapProvider` dispatches to `LinuxProcMemoryMapProvider` on Linux and to an explicit unsupported provider elsewhere. `LinuxProcMemoryMapProvider` checks the process identity before and after the map read so a PID-reuse race cannot silently associate a new process's address space with the old snapshot. The Linux provider is separately injectable, allowing procfs fixtures and race behavior to be tested on Windows and macOS without pretending those hosts expose `/proc`.

On Linux:

- basic and device reports read `/proc/PID/maps`;
- `-x`, `-X`, and `-XX` read `/proc/PID/smaps`;
- map ranges, permissions, offsets, device IDs, inode values, UTF-8 mapping
  names, numeric detail fields, and `VmFlags` are preserved;
- observations carry `LinuxProcfs` provenance and exact fidelity.

Windows and macOS deliberately return `Unsupported` for this complete
Linux-equivalent map contract. Native module lists, working-set summaries, and
partial region APIs are not silently promoted to `/proc/PID/maps` semantics.
This preserves the roadmap rule that unsupported platform capabilities receive
controlled diagnostics rather than fabricated values.

## pmap profile

The command implements the Batch 61 public surface from procps-ng 4.0.6:

- basic mapping output and totals;
- `-x` extended RSS/dirty output from `smaps`;
- `-X` compact kernel-detail output and `-XX` full detail output;
- `-d` device/offset output;
- `-q` header/footer suppression while retaining the PID/command banner;
- `-p` full mapping paths and `-k` kernel pseudo-names;
- `-A LOW[,HIGH]` hexadecimal address-range filtering;
- private/shared permission presentation, device IDs, offsets, and names;
- multiple PID operands, including `/proc/PID` forms;
- UTF-8 mapping-name preservation;
- procps-ng's missing-requested-process status bit `42`;
- controlled denied, malformed, unavailable, reused, and unsupported
  diagnostics;
- host-correct generated line endings without CRLF doubling on Windows.

The historical SunOS `-r` option is accepted with the upstream compatibility
warning. Procps-ng's rc-file options (`-c`, `-C`, `-n`, `-N`) are recognized so
they do not fail as unknown options, but Batch 61 returns a controlled
unsupported diagnostic because rc-file creation/filtering is outside the
roadmap's pmap scope.

## Tests

Deterministic command tests cover basic, device, extended, dynamic extended,
quiet, range, path/kernel-name, UTF-8, vanished-process, unsupported-capability,
and newline behavior. Parser fixtures exercise CRLF input, ordered `smaps` metrics, `VmFlags`, and UTF-8 names. Shared-provider fixtures run on every host, verify invalid UTF-8 replacement and PID-reuse rejection, while a live provider test verifies exact map availability on Linux and explicit unsupported capability on Windows/macOS.

## Validation status

Source/project/solution structure is validated in the delivery environment.
The .NET SDK is not available there, so Debug/Release build and xUnit execution
on `windows-latest`, `ubuntu-latest`, and `macos-latest` remain the closure gate.
