# Icod.CoreUtils Architecture and Migration Record

**Status:** Completion Gate G — COMPLETE; executable composition-root boundary — COMPLETE
**Architecture checkpoint:** 2026-08-28
**CoreUtils Gate G baseline:** `main` commit `26d3f4bd9f587eb7d7c32bff03f37fd69138779d`
**Program-host refactor baseline:** `refactor` commit `9e7356535e9d366cced10d0f29543fc729ca5a87`

## Purpose

This document records the repository, package, engine, executable, versioning, release, and interoperability boundaries that remain after Completion Gate G split the former multi-suite `Icod.CoreUtils` incubation repository.

G10A records the intended final architecture. G10B supplied the repository-by-repository dependency and independent-build evidence in `Icod.CoreUtils-G10B-Dependency-Audit.md`. G10C reconciled the final roadmaps against that evidence and closed Completion Gate G.

## Architectural rules

1. A command suite owns its command semantics, presentation, upstream compatibility profile, and suite-specific engines.
2. Cross-repository implementation reuse flows through a published neutral package, never through another command suite's source tree.
3. A suite-specific Shared/engine project remains a repository-local `ProjectReference` dependency unless its owning repository independently establishes a package boundary.
4. `Icod.CoreUtils.Shared` is permanently repository-local and non-packable.
5. Public command, text, file, and archive formats are preferred over runtime suite-to-suite dependencies for interoperability.
6. A neutral foundation must not depend on a command suite.
7. Repository and package versions are independent; the Icod ecosystem is not lockstep-versioned.
8. A cross-repository `ProjectReference` or dependency on a neighboring checkout fails G10B.
9. `Program.cs` owns the executable's operating-process boundary; `Command` owns command semantics and remains independently callable and testable.

## Executable composition-root boundary

The post-Gate-G executable-host refactor establishes a boundary orthogonal to repository and package ownership: `Program.cs` is the composition root where a command is attached to the current operating-system process, while `Command` owns the command itself.

For executable hosts whose command contract depends on process resources, `Program.cs` is responsible for:

- validating the process argument array before other work;
- acquiring current-process standard resources in the representation required by the command;
- translating Ctrl+C or other process-wide host events into the command's cancellation or signal contract;
- installing and removing process-wide handlers with deterministic cleanup; and
- delegating to `Command.RunAsync` with explicit resources and cancellation.

`Command` is responsible for option and operand parsing, diagnostics, command execution policy, and exit-status semantics. It consumes caller-provided resources rather than reacquiring process-global resources when those resources are part of the invocation contract, remains reusable outside an executable process, and never disposes caller-owned standard streams or other borrowed resources.

Standard-resource injection follows command semantics rather than a one-size-fits-all wrapper. Character-oriented commands receive text readers and writers; byte-preserving commands receive raw standard streams; mixed commands may receive both; and terminal or descriptor-control commands use the applicable provider when ordinary stream wrappers would lose required host identity.

This rule does not require every `Program.cs` to have identical syntax. Commands with defining process-level behavior—such as signal forwarding, ignore-interrupt policy, terminal-descriptor identity, or native child standard-handle inheritance—retain explicit composition-root handling rather than being forced through generic Ctrl+C cancellation. Direct `CommandContext` construction in `Program.cs` is valid when it is the clearest way to inject the required process resources.

## Repository ownership

### Neutral foundations

- `Icod.CommandFramework` — <https://github.com/uniblab/Icod.CommandFramework>
- `Icod.Path` — <https://github.com/uniblab/Icod.Path>
- `Icod.Timing` — <https://github.com/uniblab/Icod.Timing>
- `Icod.Host` — <https://github.com/uniblab/Icod.Host>
- `Icod.Processes` — <https://github.com/uniblab/Icod.Processes>
- `Icod.TermInfo` — <https://github.com/uniblab/Icod.TermInfo>
- `Icod.Terminal` — <https://github.com/uniblab/Icod.Terminal>
- `Icod.DCurses` — <https://github.com/uniblab/Icod.DCurses>

These repositories own command-neutral mechanism. Their exact package-version edges are audited mechanically in G10B.

### Command-suite repositories

- `Icod.CoreUtils` — <https://github.com/uniblab/Icod.CoreUtils>
- `Icod.UtilLinux` — <https://github.com/uniblab/Icod.UtilLinux>
- `Icod.Grep` — <https://github.com/uniblab/Icod.Grep>
- `Icod.Tar` — <https://github.com/uniblab/Icod.Tar>
- `Icod.ProcPs` — <https://github.com/uniblab/Icod.ProcPs>
- `Icod.DiffUtils` — <https://github.com/uniblab/Icod.DiffUtils>
- `Icod.LineEditor` — <https://github.com/uniblab/Icod.LineEditor>
- `Icod.Patch` — <https://github.com/uniblab/Icod.Patch>

Each command suite owns its upstream-specific behavior, tests, fixtures, solution, CI, release metadata, and repository-local engines.

## CoreUtils repository boundary

`Icod.CoreUtils.Shared` is the permanent same-repository implementation library for GNU Coreutils/Fileutils/Textutils behavior shared by multiple CoreUtils commands. It is not an independently published package.

The G9-frozen CoreUtils external dependency boundary is:

```text
Icod.CoreUtils.<command>
    -> ProjectReference: Icod.CoreUtils.Shared
       where Coreutils-family shared behavior is required

Icod.CoreUtils.Timeout
    -> ProjectReference: Icod.CoreUtils.Shared
    -> PackageReference: Icod.Timing 1.0.0

Icod.CoreUtils.Shared
    -> PackageReference: Icod.CommandFramework 1.1.0
    -> PackageReference: Icod.Path 1.0.0
```

A newer neutral package release does not implicitly change this validated boundary. Updating a package major or minor line is a deliberate consumer-repository migration.

## Repository-local engine boundaries

### DiffUtils

`Icod.DiffUtils.Shared` owns comparison and differencing behavior reused by `cmp`, `diff`, `diff3`, and `sdiff`. Command-to-engine edges remain repository-local project references.

### LineEditor

`Icod.LineEditor.Ed.Shared` owns the mutable editor engine shared by `ed` and `red`. `sed` remains a separate execution engine. The LE9 decision not to create a general `Icod.LineEditor.Shared` layer remains authoritative.

### ProcPs

`Icod.ProcPs.Shared` owns procps-ng-specific process/system observations, Linux `/proc` parsing, selection and field models, metric interpretation, personalities, sorting, and reusable screen state. General process, timing, host, and terminal mechanism belongs in neutral packages.

### Other suites

`Icod.Grep`, `Icod.Tar`, `Icod.UtilLinux`, and `Icod.Patch` retain suite- or command-specific behavior in their own repositories and consume published neutral foundations where cross-repository mechanism is required.

## Executable ownership

The final architecture has one authoritative suite owner for each extracted executable family:

- CoreUtils owns executables still present as `Icod.CoreUtils.*` command projects.
- UtilLinux owns `kill` and `renice`.
- Grep owns `grep`.
- Tar owns `tar`.
- Patch owns `patch`.
- DiffUtils owns `cmp`, `diff`, `diff3`, and `sdiff`.
- LineEditor owns `ed`, `red`, and `sed`.
- ProcPs owns its selected procps-ng command family, including the replacement `ps` and `uptime` profiles and the post-extraction terminal tools.

Historical co-resident command names do not establish current ownership. Once a validated replacement is transferred to its final suite repository, the obsolete CoreUtils source is not retained merely to preserve history.

## Dependency direction

The architectural direction is:

```text
published neutral package
        ↓ PackageReference
repository-local Shared / engine, where required
        ↓ ProjectReference
command project / repository-local router
```

A command may reference a neutral package directly when no suite-local engine is required.

Forbidden edges are:

```text
neutral foundation -> command suite
command suite A     -> command suite B runtime package
repository A        -> ProjectReference into repository B
repository A        -> neighboring source checkout required for restore/build
```

G10B enumerates every actual production edge and proves the graph is acyclic.

## Versioning policy

The ecosystem uses independent repository/package version lines.

- Every repository owns its own SemVer line.
- Consumers pin explicit compatible package versions.
- A neutral package release does not force unrelated repositories to move in lockstep.
- A deliberate migration to a new major dependency line is validated in the consuming repository.
- Repository-local Shared/engine projects do not require independent package versions merely because several projects consume them.
- Suite routers and suite packages may advance independently from internal engine metadata when their repository's release policy requires it.

## Release and CI ownership

Every repository owns its own solution, tests, CI workflows, release metadata, and intentionally published artifacts.

For CoreUtils, G9 froze the steady-state validation policy as:

```text
pull request: Staging clean / restore / build / test
main push:    Release clean / restore / build / test
runners:      windows-latest, ubuntu-latest, macos-latest
```

The temporary Debug/Staging/Release × three-runner PR matrix used during G9 was validation evidence, not the permanent workflow.

Publishing is a separate release action. Build/test CI does not imply that packages are being published.

## Textual interoperability

Runtime assembly dependencies are unnecessary when a public format is the contract.

- DiffUtils emits normal, context, unified, and ed-script text.
- Patch consumes compatible patch text without a runtime dependency on DiffUtils.
- LineEditor Ed consumes ed scripts without a runtime dependency on DiffUtils.
- Fixtures may cross repositories to validate textual compatibility without creating production assembly references.
- Standard input/output, ordinary files, archive formats, exit statuses, and documented command behavior remain public interoperability surfaces.

This preserves interoperability with GNU and third-party tools instead of coupling the Icod implementations only to one another.

## Gate G closure sequence

### G10A — Architecture record

Complete with this document. Ownership, dependency direction, versioning, release policy, and textual interoperability are written down against the post-G9 CoreUtils state.

### G10B — Cross-repository dependency and isolation verification

Complete. `Icod.CoreUtils-G10B-Dependency-Audit.md` records the audited production package/project edges, repository-local engine boundaries, successful current `main` CI evidence, acyclic graph proof, and the conclusion that no G10-required version-pin migration exists.

### G10C — Final closure

Complete. G10C reconciled both roadmaps against the G10B evidence and marked Completion Gate G complete.

**Completion Gate G is complete.**