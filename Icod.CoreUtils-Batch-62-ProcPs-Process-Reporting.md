# Batch 62 — ProcPs process reporting

## Scope

Batch 62 introduces the suite-correct `Icod.ProcPs.Ps` project and a dedicated `tests/ProcPs.Ps.Tests` project. The executable assembly remains lowercase `ps`. In accordance with the roadmap, the historical `ps` project and tests remain available alongside the replacement until the new implementation is green on the required validation path; they should then be retired as the final migration step.

The command is a presentation and compatibility engine over `Icod.ProcPs.Shared`. It does not introduce another process-enumeration foundation.

## Shared-provider use

`Icod.ProcPs.Ps` consumes:

- `IProcProcessProvider` for process snapshots;
- `IProcSystemMetricsProvider` for system memory, CPU, and uptime inputs used by calculated fields;
- `IProcMatchSupplementProvider` for elapsed time, environment data, and Linux lightweight tasks;
- `ProcPersonalityResolver` for procps personality selection;
- `ProcReportFieldCatalog` for the reusable ps/top-family process-reporting field and alias vocabulary; and
- ProcPs account, namespace, container, terminal, command-line, CPU, memory, and identity observations already carried by `ProcProcessSnapshot`.

Batch 62 extends `ProcMatchSupplement` with two reporting observations that are deliberately not promoted into the general process snapshot:

- parsed Linux `/proc/PID/status` fields, used for signal masks and capability sets; and
- the Linux `/proc/PID/attr/current` security label.

Both retain explicit observation availability and provenance. Non-Linux hosts do not fabricate Linux signal/capability/security values.

## Implemented command surface

The Batch 62 `ps` profile covers the compatibility areas required by the roadmap:

- default selection and explicit all/PID/quick-PID/parent/group/session/terminal/user/group/command selectors;
- BSD selection modifiers, running-only selection, and deselection;
- Linux/POSIX/BSD/SunOS/Digital/HP/AIX personality resolution through the shared resolver;
- standard, full, extra-full, long, jobs, BSD user, memory, and thread-oriented field sets;
- user-defined `-o`/`--format` fields, aliases, custom headings, sorting, and heading suppression/forcing;
- process forests and Linux lightweight-thread presentation;
- terminal, state, priority, thread-count, RSS/VSZ/size, command, environment, elapsed, start, CPU-time, CPU-percent, and memory-percent fields;
- cgroup/container and namespace reporting;
- Linux security label, pending/blocked/ignored/caught signal masks, and inheritable/permitted/effective/bounding/ambient capability sets;
- output-width controls and host-native generated line endings;
- deterministic help, version, syntax-error, and cancellation paths.

Fields that the shared providers cannot observe defensibly retain explicit placeholder behavior rather than inventing host semantics.

## Test-stream policy audit

Batch 62 also applies the repository test-stream convention to `tests/Timeout.Tests`. Tests that previously called `timeout` without explicit output/error destinations now provide `Stream.Null` or a capture stream. The child-process timeout case does not require inherited test-runner console handles and is isolated as well.

The new `ps` tests likewise inject both standard streams on every command invocation. They cover default and explicit selection, BSD/personality behavior, formats/headings, sort order, quick PID order, metrics, containers/namespaces, forests, threads, security/signal/capability fields, width handling, large enumerations, help/version, and cancellation. Shared tests cover Linux procfs status/security-label fixture parsing.

## Validation status

The implementation is ready for repository validation. This working environment does not provide the .NET SDK, so `dotnet clean`, build, and test could not be executed here. Required-runner validation remains necessary before Batch 62 is fully closed, and the historical `ps` project/tests are intentionally retained until that green validation permits their retirement.
