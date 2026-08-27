# Icod.CoreUtils Architecture and Migration Record

**Status:** Completion Gate G — G10A architecture record
**Architecture checkpoint:** 2026-08-27
**CoreUtils baseline:** `main` commit `26d3f4bd9f587eb7d7c32bff03f37fd69138779d`

## Purpose

This document records the repository, package, engine, executable, versioning, release, and interoperability boundaries that remain after Completion Gate G split the former multi-suite `Icod.CoreUtils` incubation repository.

G10A records the intended final architecture. G10B supplies exhaustive dependency and independent-build evidence. G10C closes Completion Gate G only after that evidence is complete.

## Architectural rules

1. A command suite owns its command semantics, presentation, upstream compatibility profile, and suite-specific engines.
2. Cross-repository implementation reuse flows through a published neutral package, never through another command suite's source tree.
3. A suite-specific Shared/engine project remains a repository-local `ProjectReference` dependency unless its owning repository independently establishes a package boundary.
4. `Icod.CoreUtils.Shared` is permanently repository-local and non-packable.
5. Public command, text, file, and archive formats are preferred over runtime suite-to-suite dependencies for interoperability.
6. A neutral foundation must not depend on a command suite.
7. Repository and package versions are independent; the Icod ecosystem is not lockstep-versioned.
8. A cross-repository `ProjectReference` or dependency on a neighboring checkout fails G10B.

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

G10B must mechanically enumerate production package/project edges, reject cross-repository source references and command-suite runtime cycles, and confirm current independent restore/build/test evidence for every final repository.

### G10C — Final closure

G10C reconciles both roadmaps against G10B evidence, records any required corrections, and marks Completion Gate G complete.
