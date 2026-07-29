# Icod.CoreUtils Audit and Refactor Roadmap

## Living status

| Item | Status |
|---|---|
| Completed command batches | For completion status, see list of batches below |
| Current engineering gate | For completion status, see list of batches below |
| Next infrastructure dependency | For completion status, see list of batches below |
| Current target framework | `net10.0` |
| Required CI runners | `windows-latest`, `ubuntu-latest`, `macos-latest` |

## Scope

`Icod.CoreUtils` is a cross-platform .NET implementation of GNU Coreutils. Its scope expressly includes the file-manipulation and text-processing command families historically distributed as **GNU Fileutils** and **GNU Textutils**. These are natural Coreutils inclusions rather than unrelated extensions: GNU combined `fileutils`, `sh-utils`, and `textutils` into the unified `coreutils` package in 2003. The modern GNU Coreutils project remains the basic file, shell, and text manipulation suite.

Historical references:

- [GNU Coreutils FAQ — Fileutils, shellutils and textutils](https://www.gnu.org/software/coreutils/faq/coreutils-faq.html#Fileutils-shellutils-and-textutils)
- [GNU Coreutils 5.0 release announcement](https://lists.gnu.org/archive/html/coreutils-announce/2003-04/msg00000.html)

The primary supported CI targets are `windows-latest`, `ubuntu-latest`, and `macos-latest`. BSD support remains a best-effort target. The implementation is therefore not a Unix-only port: platform-independent behavior is preferred, native behavior is implemented per supported ABI where required, and unsupported platform capabilities receive controlled diagnostics.

The repository and solution will also serve as the **temporary development home** for projects that ultimately belong to other upstream suites:

- `Icod.DiffUtils.Shared`, `Icod.DiffUtils.Cmp`, `Icod.DiffUtils.Diff`, `Icod.DiffUtils.Diff3`, and `Icod.DiffUtils.SDiff`;
- `Icod.Grep`;
- `Icod.Patch`;
- `Icod.Ed.Shared`, `Icod.Ed.Ed`, and `Icod.Ed.Red`;
- `Icod.Sed`;
- `Icod.Tar`;
- `Icod.ProcPs.Shared` and the complete procps-ng command family.

These projects will use their **final suite-correct project names and namespaces from the beginning**, but they will remain in `Icod.CoreUtils.sln` and this repository until the final architectural extraction. Developing the suites together provides real consumers for the current `Shared` APIs, makes cross-suite duplication visible, and supplies the evidence needed to decide which APIs belong in `Icod.CommandFramework`, which remain in `Icod.CoreUtils.Shared`, and which belong in a suite-specific Shared library.

No separate `[` project will be added. The existing `test` project remains the condition evaluator.

## Development architecture

The present repository is deliberately a **multi-suite incubation workspace**. It is not yet the final repository layout.

The current `Shared` project continues to incubate both:

1. functionality that may ultimately become cross-suite `Icod.CommandFramework` APIs; and
2. functionality that may remain specific to GNU Coreutils, Fileutils, and Textutils in `Icod.CoreUtils.Shared`.

The project's present physical name does not establish permanent ownership. The common argument processor is the existing example: it resides in the current `Icod.CoreUtils.Shared` project during incubation, but use by independent suites makes it a likely `Icod.CommandFramework` candidate. The same rule applies to host, processor-resource, process-identity, process-targeting, process-launch, waiting, signal, priority, clock, and terminal mechanics that are consumed by both Coreutils and ProcPs or by other suites.

The suite projects developed here may add their own Shared libraries when the reuse is genuinely suite-specific:

- `Icod.DiffUtils.Shared`;
- `Icod.Ed.Shared`;
- `Icod.ProcPs.Shared`;
- other suite-local engine projects only when a concrete reuse or testability need is demonstrated.

During the incubation phase, projects use `ProjectReference` relationships inside the solution. The intended dependency direction is:

```text
Current Shared incubation project
        ↓
Suite-specific Shared or engine project, when justified
        ↓
Individual command projects
```

The suite-specific projects must use their final namespace families now:

```text
Icod.CoreUtils.*
Icod.DiffUtils.*
Icod.Grep
Icod.Patch
Icod.Ed.*
Icod.Sed
Icod.Tar
Icod.ProcPs.*
```

The solution should use matching solution folders and suite directories so source ownership is clear before repository extraction.

### Co-resident executable-name collisions

Some suites contain commands with the same executable name. In particular, procps-ng supplies names such as `kill` and `uptime` that may overlap commands already implemented under the Coreutils roadmap.

During the co-resident phase:

- every executable retains the lowercase command assembly name required by its upstream suite;
- projects with colliding output names use suite-specific output directories;
- tests and packaging identify the suite explicitly rather than assuming one repository-wide path per executable name;
- no implementation is silently discarded merely to avoid a build-output collision;
- the final repository and packaging split resolves which commands may be installed together and how any aliases or umbrella distributions are composed.

### Ultimate architecture

At the end of the implementation roadmap, Completion Gate G performs an evidence-based classification and extraction.

The intended final architecture has three layers:

1. **`Icod.CommandFramework`** — contracts demonstrated to be common across independent command suites;
2. **suite-specific Shared libraries** — functionality shared only inside one upstream family, such as `Icod.CoreUtils.Shared`, `Icod.DiffUtils.Shared`, `Icod.Ed.Shared`, or `Icod.ProcPs.Shared`;
3. **individual command projects** — thin command front ends over the applicable framework and suite engine.

The final dependency direction is:

```text
Icod.CommandFramework
        ↓
Suite-specific Shared project, when required
        ↓
Command projects
```

At that point:

- `Icod.CommandFramework` becomes its own solution, repository, and versioned NuGet package;
- `Icod.CoreUtils.Shared` is retained and extracted only if the consumer audit shows meaningful Coreutils/Fileutils/Textutils-only reuse;
- the suite projects are separated into their own solutions and repositories;
- cross-repository dependencies become versioned NuGet `PackageReference` entries;
- project references remain appropriate within each extracted suite unless an additional package boundary is independently justified.

`Icod.CoreUtils` must not acquire permanent production dependencies on sibling command suites. Interoperability should normally occur through documented command-line behavior and textual formats. Unified diffs flow from `Icod.DiffUtils` to `Icod.Patch`, and ed scripts flow from `Icod.DiffUtils` to `Icod.Ed`, without requiring runtime references between their suite-specific Shared libraries.

### Icod.CommandFramework

`Icod.CommandFramework` will be extracted only after the co-resident suite work has supplied enough actual consumers to reveal stable boundaries. Candidate responsibilities include:

- command contexts and injected standard streams;
- common argument-processing foundations;
- diagnostics, quoting, and exit-status support;
- cancellation, broken-pipe, and disposal behavior;
- high-performance cross-platform file I/O;
- byte, text, record, delimiter, locale, and display-width abstractions;
- secure temporary-object and workspace infrastructure;
- general filesystem capability, traversal, and metadata abstractions;
- host identity, processor-resource availability, affinity, quota, and provenance abstractions;
- process identity, PID-reuse-aware targeting, process groups, sessions, lifetime, liveness, and waiting abstractions;
- argument-safe process launch, environment construction, standard-stream forwarding, signal, priority, exit-status, and termination-reason abstractions;
- monotonic clocks, cancellation-aware delay, periodic scheduling, terminal control, and platform-capability abstractions.
An API moves to `Icod.CommandFramework` because multiple independent suites use the same contract, not merely because it currently resides in `Shared`.

### Icod.CoreUtils.Shared

`Icod.CoreUtils.Shared` may remain after framework extraction. Its purpose is narrower: behavior shared among Coreutils, Fileutils, and Textutils commands that is not a suitable cross-suite framework contract.

During incubation, code common to Coreutils and ProcPs may still be implemented physically in the current `Icod.CoreUtils.Shared` project because that is the available shared foundation. Such code must be marked as a provisional `Icod.CommandFramework` candidate and must not be treated as permanently Coreutils-owned merely because of its temporary project location.

Likely eventual `Icod.CoreUtils.Shared` examples include Coreutils-specific option combinations, backup and overwrite policies, block-size conventions, ownership and mode presentation, listing models, copy/move/install policies, and other engines reused by multiple Coreutils commands but not by Diffutils, Grep, Patch, Ed, Sed, Tar, or ProcPs.

The final dependency would be:

```text
Icod.CoreUtils command
        ↓
Icod.CoreUtils.Shared
        ↓
Icod.CommandFramework
```

### Suite-specific projects

- `Icod.DiffUtils.Shared` owns comparison, differencing, hunk construction, two-way and three-way merge, and difference-output behavior reused by `cmp`, `diff`, `diff3`, and `sdiff`.
- `Icod.Grep` owns grep-specific pattern sources, matcher orchestration, recursive selection, binary-input policy, context grouping, and output formatting.
- `Icod.Patch` owns patch parsing, hunk application, fuzz and offset matching, reversal detection, rejects, backups, and transactional application.
- `Icod.Ed.Shared` owns address parsing, editor commands, mutable line buffers, substitutions, global commands, undo, file operations, shell integration, and restricted-mode enforcement for `ed` and `red`.
- `Icod.Sed` owns its parser, addresses, pattern and hold spaces, substitutions, branching, command cycle, and in-place-editing semantics.
- `Icod.Tar` owns archive formats, entry models, sparse-file archive behavior, selection and exclusion rules, compression integration, and extraction security.
- `Icod.ProcPs.Shared` owns procps-ng-specific process enumeration, Linux `/proc` parsing and equivalent observation providers, selection grammar, detailed snapshots, field definitions, sorting, personalities, terminal association, CPU and memory metric interpretation, kernel-data models, and full-screen process-tool support. It consumes rather than duplicates the general processor-resource, process-identity, target, launch, wait, signal, priority, clock, and terminal contracts incubated in the current Shared project.

### Co-resident suite incubation policy

Each non-Coreutils suite developed in this repository must:

- use its final suite-correct project filename and namespace immediately;
- preserve the lowercase executable assembly name;
- live in a clearly identified suite directory and solution folder;
- have command-specific and suite-Shared test projects;
- reproduce the established `net10.0`, C# 13, Debug/Staging/Release, UTF-8/LF, XML documentation, and three-runner CI policies;
- use project references during co-resident development;
- keep suite-specific state out of the general Shared incubation project;
- consume existing cross-suite abstractions rather than recreating parallel processor, process, signal, priority, waiting, timing, or terminal contracts inside a suite-specific Shared project;
- classify every new shared API provisionally as cross-suite, Coreutils-specific, suite-specific, or command-local;
- establish textual compatibility fixtures where public formats cross suite boundaries;
- document output-path handling for duplicate executable names;
- defer solution and repository extraction until Completion Gate G;
- distinguish completion of an implementation batch from completion of the final repository/package split.

## Authoritative Source

For GNU Coreutils commands—including the natural historical GNU Fileutils and GNU Textutils command families—the conformance baseline is the pinned GNU Coreutils manual and source. The separate Fileutils and Textutils packages are historical provenance, not competing modern specifications.

For the co-resident sibling-suite projects, use the corresponding upstream project as the primary authority.

| Program family or command | Project family | Primary authority |
|---|---|---|
| GNU Coreutils, including historical GNU Fileutils and GNU Textutils families | `Icod.CoreUtils` | GNU Coreutils manual and source |
| `sed` | `Icod.Sed` | GNU sed |
| `grep` | `Icod.Grep` | GNU grep 3.12 |
| `cmp`, `diff`, `diff3`, `sdiff` | `Icod.DiffUtils` | GNU Diffutils 3.12 |
| `patch` | `Icod.Patch` | GNU patch 2.8 |
| `ed`, `red` | `Icod.Ed` | GNU ed 1.22.5 |
| procps-ng command family | `Icod.ProcPs` | procps-ng 4.0.6, with an explicitly documented portability profile |
| `tar` | `Icod.Tar` | GNU tar |

Man7 pages are useful synopses and secondary references, but they must not replace the authoritative upstream manual.

## Current repository audit

### What is working well

- The completed batches established shared command-line, diagnostics, streaming, numeric, platform, identity, process, date/time, and block-I/O abstractions.
- Batches 1 through 16 have command-specific tests.
- The complete test suite has been exercised on Windows, Ubuntu, and macOS during Batch 10 stabilization.
- Source projects consistently reference `Shared` where common behavior is appropriate; that project currently incubates both future `Icod.CommandFramework` APIs and Coreutils-specific `Icod.CoreUtils.Shared` APIs while co-resident sibling suites supply additional real consumers.
- Recent projects use asynchronous entry points, injected streams, cancellation, and provider abstractions more consistently than the original implementations.
- Cross-platform test failures have exposed and corrected line-ending assumptions, test-vector defects, and native ABI assumptions that Windows- or Linux-only validation would have missed.

### Defects and risks that remain

1. **Several implementations still silently accept unsupported behavior.**  
   Unknown or unsupported options are ignored by some commands. Unsupported platform behavior sometimes returns success. Both patterns are incompatible with a conformance-oriented port.

2. **Several commands still throw unhandled `NotImplementedException`.**  
   The existing `chown`, `chgrp`, `runcon`, and `chroot` implementations are examples. Unsupported operations must produce a controlled diagnostic and documented nonzero status.

3. **Some commands delegate their defining operation to an installed native utility.**  
   Examples include portions of `link`, `chcon`, `install`, `sync`, and `stdbuf`. Production implementations must not obtain apparent compatibility by invoking the same host utility. Native utilities may be used only by optional differential tests.

4. **Some implementations are not yet the command they claim to be.**
   - The existing `diff` implementation is not a complete difference algorithm and does not yet implement the required result-status model; it will be migrated into `Icod.DiffUtils.Diff` and corrected during the consecutive Diffutils batches.
   - The existing `patch` implementation handles a private simplified format rather than normal, context, and unified patches; it will be migrated into the co-resident `Icod.Patch` project and re-audited there.
   - `link` behaves like a partial `ln` front end rather than the simple two-operand hard-link command.
   - `stat` substitutes creation time where inode-change time is required.
   - `chmod` does not yet implement GNU/POSIX numeric and symbolic mode semantics correctly.
   - `tar` needs correct entry typing, metadata handling, and extraction-path safety.
   - `stdbuf` cannot silently run a child without applying the requested buffering mode.

5. **Several text commands use the wrong data model.**  
   Common problems include ordinal comparison instead of locale collation, UTF-16 `char` processing where bytes or locale characters are required, line-based processing for commands that must transform delimiters, and whole-input buffering where bounded memory or temporary spill files are required.

6. **Several recursive filesystem commands lack a common traversal policy.**  
   Symlink traversal, hard-link identity, cycles, mount boundaries, sparse files, metadata preservation, and destination-inside-source detection must be centralized before `rm`, `cp`, `mv`, `du`, `ls`, and `tar` are considered conformant.

7. **Injected standard streams are not consistently respected or owned correctly.**  
   A command must use the supplied `stdin`, `stdout`, and `stderr`, and must never dispose a caller-owned standard stream.

## Project conventions

These conventions apply to every existing project that is altered and every project that is added:

1. Project filenames and namespaces use conventional PascalCase and the correct suite family, such as `Icod.CoreUtils.BaseName.csproj`, `Icod.DiffUtils.Diff.csproj`, `Icod.Ed.Ed.csproj`, or `Icod.ProcPs.Ps.csproj`.
2. `<AssemblyName>` remains the short lowercase command name exactly matching the tool directory, such as `basename`.
3. All source and project text files are UTF-8.
4. The first `<PropertyGroup>` of every altered or added `.csproj` contains `<LangVersion>13.0</LangVersion>`, and every project retains the established Debug, Staging, and Release conditional property groups.  Example:
```xml
    <PropertyGroup>
		<LangVersion>13.0</LangVersion>
		<OutputType>Exe</OutputType>
		<TargetFramework>net10.0</TargetFramework>
		<Nullable>enable</Nullable>
		<ImplicitUsings>enable</ImplicitUsings>
		<GenerateDocumentationFile>true</GenerateDocumentationFile>
		<OutputPath>..\bin\$(Configuration)\</OutputPath>
		<AssemblyName>pwd</AssemblyName>
		<RootNamespace>Icod.CoreUtils.Pwd</RootNamespace>
	</PropertyGroup>
	<PropertyGroup Condition=" '$(PlatformTarget)' == '' ">
		<PlatformTarget>AnyCPU</PlatformTarget>
	</PropertyGroup>
	<ItemGroup>
		<ProjectReference Include="..\Shared\Icod.CoreUtils.Shared.csproj" />
	</ItemGroup>
	<PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
		<ErrorReport>prompt</ErrorReport>
		<WarningLevel>2</WarningLevel>
		<DebugSymbols>true</DebugSymbols>
		<DebugType>full</DebugType>
		<Optimize>false</Optimize>
		<DefineConstants>DEBUG;TRACE</DefineConstants>
		<SignAssembly>false</SignAssembly>
		<TreatWarningsAsErrors>false</TreatWarningsAsErrors>
	</PropertyGroup>
	<PropertyGroup Condition=" '$(Configuration)' == 'Staging' ">
		<ErrorReport>prompt</ErrorReport>
		<WarningLevel>3</WarningLevel>
		<DebugSymbols>true</DebugSymbols>
		<DebugType>full</DebugType>
		<Optimize>false</Optimize>
		<DefineConstants>TRACE</DefineConstants>
		<SignAssembly>false</SignAssembly>
		<TreatWarningsAsErrors>false</TreatWarningsAsErrors>
	</PropertyGroup>
	<PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
		<ErrorReport>prompt</ErrorReport>
		<WarningLevel>4</WarningLevel>
		<DebugType>pdbonly</DebugType>
		<Optimize>true</Optimize>
		<SignAssembly>false</SignAssembly>
		<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
		<WarningsNotAsErrors>CS1591</WarningsNotAsErrors>
	</PropertyGroup>
```
5. Commands use `CommandContext`, the current Shared option/argument processor, Shared diagnostics, and injectable providers where behavior is platform- or environment-dependent. After the final framework extraction, cross-suite forms of these APIs are supplied by `Icod.CommandFramework`.
6. Every command exposes cancellation-aware `RunAsync(..., CancellationToken)`, retains a synchronous compatibility wrapper, and uses an asynchronous `Main` where appropriate. Naturally asynchronous I/O and child-process waits use TAP directly rather than `Task.Run`.
7. Literal newline escapes such as `\n` and `\r\n` are permitted only when they are part of the utility’s data semantics, escape grammar, or documented byte transformation. They are never used as the host platform’s generated line separator.
8. Generated line endings use `WriteLine`, `WriteLineAsync`, or `Environment.NewLine`. Line-oriented input uses `ReadLine`, `ReadLineAsync`, and `Environment.NewLine` as appropriate. Code must not hard-code `\n` or `\r\n` for host line-reading or line-writing semantics.
9. When multiple strings are sent to `WriteAsync`, `WriteLineAsync`, or related output methods, combine them with `System.String.Concat` rather than the `+` operator.
10. Each command has its own dedicated xUnit test project following the established `tests/<Tool>.Tests` pattern.
11. Each Program.cs file must have class-level XML documentation whose `<summary>` includes the command usage, plus a dedicated usage-printing or usage-writing function.
12. Every member or type which is declared public, protected, or internal must have XML documentation consisting of valid <summary>, <param>, <returns>, <value>, and <exception> details as appropriate.  These cannot be stubs merely to silence CS1591.
13. Any directory with more than one source code file must have a README.md file describing the contents and purposes of that directory.
14. Every new project is added to the solution, all required configuration mappings, the appropriate suite solution folder, and every local and CI build/test entry point.
15. Co-resident projects whose lowercase executable names collide use suite-specific output directories. Tests and packaging identify the suite explicitly; an assembly name is not changed merely to avoid an incubation-time path collision.
16. The supported CI platform targets are explicitly `windows-latest`, `ubuntu-latest`, and `macos-latest`. Platform-specific tests may be conditional, but every runner must build the full solution and execute the complete applicable test suite.
17. Do not use `Assert.True` to check for substrings. Use `Assert.StartsWith`, `Assert.EndsWith` instead. (https://xunit.net/xunit.analyzers/rules/xUnit2009).
18. Do not use `Assert.Equal` to check for boolean conditions. Use `Assert.True` instead. (https://xunit.net/xunit.analyzers/rules/xUnit2004)
19. The eventual extracted repositories retain these conventions unless their own roadmap records a deliberate exception.
20. Cross-suite compatibility is tested at the public command-line or textual-format boundary unless a dependency has been deliberately classified as cross-suite infrastructure. During the current roadmap such APIs may be incubated in `Icod.CoreUtils.Shared`; their permanent public home is `Icod.CommandFramework` after the final extraction audit.

## Repository-wide engineering rules

1. Unknown options, missing operands, conflicting options, invalid numeric values, and unsupported modes receive deterministic diagnostics and documented nonzero statuses.
2. No production command invokes the same native utility to perform its defining operation.
3. Platform limitations use capability checks and controlled failure. They never throw an unhandled `NotImplementedException` and never silently report success.
4. Caller-owned standard streams are never disposed.
5. Byte-oriented commands operate on streams; character-oriented commands explicitly define their locale and encoding behavior.
6. Commands that can process unbounded input must stream or spill to secure temporary storage. Whole-input buffering requires an explicit, tested justification.
7. Pathname operands support centralized expansion where appropriate: `*` and `?` for segment matching and `**` for transitive recursive matching.
8. Recursive operations share one traversal engine with explicit symlink, cycle, mount-boundary, and error-continuation policies.
9. File mutation is race-aware. Temporary files are created securely and exclusively; replacement is atomic where the command promises it.
10. Security-sensitive extraction, deletion, overwrite, ownership, mode, and root/context operations receive adversarial tests.
11. Exit statuses are part of the public contract and are tested independently from displayed output.
12. `--help` and `--version` behavior, write failures, broken pipes, cancellation, and disposal are tested consistently.
13. Native structures and calls are defined per supported operating-system ABI; a Linux structure declaration must not be assumed valid on macOS or Windows.
14. The exact upstream package and version used as the conformance baseline is recorded for every batch.
15. `Icod.CoreUtils` command projects do not take production dependencies on `Icod.DiffUtils`, `Icod.Grep`, `Icod.Patch`, `Icod.Ed`, `Icod.ProcPs`, `Icod.Sed`, or `Icod.Tar`.
16. Co-resident suite projects remain isolated by namespace, solution folder, tests, and output paths; Completion Gate G performs the final solution, repository, packaging, and CI extraction.
17. Until the final framework audit, every substantial Shared API records a provisional classification: cross-suite `Icod.CommandFramework` candidate, Coreutils-only `Icod.CoreUtils.Shared` candidate, suite-specific Shared candidate, or command-local implementation. The classification may change when real consumers provide better evidence.
18. A type's current assembly is not proof of final ownership. Cross-suite process and processor mechanics may reside temporarily in the current `Icod.CoreUtils.Shared` project, while ProcPs-specific enumeration, `/proc` parsing, field catalogs, selection grammar, metrics, and screen state remain in `Icod.ProcPs.Shared`.
19. Suite-specific projects must not introduce parallel abstractions for processor availability, process identity, targets, launching, waiting, signals, priorities, clocks, or terminals when the current Shared incubation project already provides the required cross-suite contract.

## Engineering completion gates

These gates are repository milestones rather than command batches. They do not alter the historical numbering.

## Batch-size policy

- A batch groups commands only when they share an implementation engine or directly validate the same new infrastructure.
- A complex parser, state machine, security boundary, or platform layer may receive a one-command batch.
- A pair or trio is preferred over a superficially thematic five- or six-command batch when the larger group would hide unrelated risk.
- A suite-specific Shared library may receive its own batch before the consecutive command batches that validate it.
- Co-resident suite blocks remain consecutive once begun unless an explicit completion gate is required between their projects.
- Each batch must leave shared infrastructure in a reusable, documented, and tested state.
- A batch is complete only after the full solution builds and all applicable tests pass on `windows-latest`, `ubuntu-latest`, and `macos-latest`, subject to explicit platform capability expectations.

## Refactor order

### Historical sequence — completed

### Batch 0 — Foundation and repository hygiene

- [x] Refactor `Shared`
- [x] add automated tests and CI
- [x] centralize option/stream/process/platform helpers
- [x] and remove generated artifacts from review archives.

### Batch 1 — Stabilize shared line/byte readers (2 tools)

- [x] `head`
- [x] `tail`

### Batch 2 — Stabilize the stream editor (1 tool)

- [x] `sed`

### Batch 3 — Core streaming byte and record I/O (3 tools)

- [x] `cat`
- [x] `tee`
- [x] `wc`

### Batch 4 — Base encoding family (3 tools)

- [x] `base32`
- [x] `base64`
- [x] `basenc`

### Batch 5 — Checksum and digest family (9 tools)

- [x] `b2sum`
- [x] `cksum`
- [x] `md5sum`
- [x] `sha1sum`
- [x] `sha224sum`
- [x] `sha256sum`
- [x] `sha384sum`
- [x] `sha512sum`
- [x] `sum`

### Batch 6 — Small deterministic commands (7 tools)

- [x] `true`
- [x] `false`
- [x] `echo`
- [x] `yes`
- [x] `sleep`
- [x] `seq`
- [x] `factor`

### Batch 7 — Path and basic host-information commands (7 tools)

- [x] `basename`
- [x] `dirname`
- [x] `pathchk`
- [x] `pwd`
- [x] `printenv`
- [x] `arch`
- [x] `hostname`

### Batch 8 — Identity and login-information commands (7 tools)

- [x] `uname`
- [x] `whoami`
- [x] `logname`
- [x] `groups`
- [x] `id`
- [x] `users`
- [x] `who`

### Batch 9 — Platform and process-information commands (4 tools)

- [x] `pinky`
- [x] `ps`
- [x] `uptime`
- [x] `date`

`ps` and `uptime` were implemented and stabilized as part of this historical batch. Their completed history remains recorded here; Batches 56 and 62 later migrate useful code and tests into the suite-correct `Icod.ProcPs` projects without rewriting history.

### Batch 10 — Block-oriented copy and conversion (1 tool)

- [x] `dd`

### Completion Gate A — before closing Batch 11

- [x] Change and retain local scripts and CI so they build and test the entire solution rather than a selected test project.
- [x] Migrate every project, build script, and CI workflow from `net9.0` to `net10.0` LTS.
- [x] Retain `<LangVersion>13.0</LangVersion>` and the established Debug, Staging, and Release configuration policy during the framework migration.
- ~~[x] Add and require a Release build in CI in addition to the Debug build and test run.~~
- [x] Run the full applicable solution test suite on all three required runners:
  - [x] `windows-latest`
  - [x] `ubuntu-latest`
  - [x] `macos-latest`
- [x] Verify that every test project is included in the solution and is actually discovered on every applicable runner.
- ~~[ ] Add a repository check for UTF-8 text files and forbidden generated artifacts.~~
- ~~[ ] Add repository checks for lowercase command assembly names and required project configuration blocks.~~
- [x] Record the exact authoritative upstream version used for every completed and future batch.
- [x] Update the living status section after Batch 11 is merged.
- [x] Recommended local verification sequence after migration:
```text
dotnet clean Icod.CoreUtils.sln -c Debug
dotnet restore Icod.CoreUtils.sln
dotnet build Icod.CoreUtils.sln -c Debug --no-restore
dotnet test Icod.CoreUtils.sln -c Debug --no-build --no-restore
```

## Revised sequence beginning with Batch 11

### Completion Gate B — before Batch 12

* [x] Add `Shared.FileSystem` flush and allocation capability abstractions needed by `dd`, `truncate`, and `sync`:

  * [x] data-only versus data-and-metadata flush;
  * [x] file-specific versus filesystem-wide flush;
  * [x] sparse extension and allocated-range capability reporting;
  * [x] controlled platform diagnostics where equivalent semantics are unavailable;
  * [x] injectable abstractions and platform-specific integration tests.

### Batch 11 — File-size manipulation (1 tool)

- [x] `truncate`

Implement the complete GNU size operand grammar, `--reference`, `--io-blocks`, creation policy, relative modifiers, rounding modifiers, overflow checks, sparse extension, and precise diagnostics. Reuse the safe size and file-position infrastructure introduced by `dd`.

### Batch 12 — Filesystem flushing (1 tool)

- [x] `sync`

Replace native-command delegation and no-op success with an explicit platform implementation. Support file-specific data and filesystem flushing where the platform permits it, and produce a controlled diagnostic for semantics that cannot be represented.

### Batch 13 — Binary formatting (1 tool)

- [x] `od`

For `od`, build a reusable binary-formatting engine covering address radices, type strings, byte order, duplicate suppression, skip/read limits, string extraction, and nonseekable standard input. The command establishes reusable formatting and width infrastructure for later consumers.

### Batch 14 — Formatted and human-readable numeric output (2 tools)

- [x] `printf`
- [x] `numfmt`

Create shared format-string, escape, numeric, grouping, padding, precision, and human-suffix components. These will later be reused by `stat`, `sort -h`, `ls -h`, `df`, and `du`.

### Batch 15 — Secure temporary objects (1 tool)

- [x] `mktemp`

Add secure, exclusive file and directory creation, template validation, `TMPDIR` handling, suffix and directory modes, cleanup tests, and resistance to race and symlink attacks. This infrastructure is required before external sort, reverse processing, sibling diff/patch/editor work, transactional replacement, and archive testing.

### Completion Gate C1 — before Batch 16

* [x] Add the shared regular-expression foundation:

  * [x] GNU basic regular-expression syntax and matching policy;
  * [x] leftmost-longest matching behavior;
  * [x] anchoring, captures, and back-references;
  * [x] locale-aware character-class abstraction;
  * [x] deterministic compilation and matching diagnostics;
  * [x] explicit documentation of differences from `System.Text.RegularExpressions`;
  * [x] injectable and testable matching providers.

This gate prevents `expr` from introducing an isolated regular-expression implementation. The same foundation remains the CoreUtils basis for `csplit` and is a candidate for eventual extraction into `Icod.CommandFramework` for reuse by `Icod.Grep`, `Icod.Sed`, and `Icod.Ed`.

### Batch 16 — Expression language (1 tool)

- [x] `expr`

Implement a real precedence-aware expression parser with arithmetic, relations, Boolean operators, string operations, regular expressions, overflow behavior, quoting rules, and GNU exit statuses. Do not pair it with `test`; their grammars and result models are materially different.

### Completion Gate C2 — before Batch 17

* [x] Add the shared text-unit and display-column model:

  * [x] byte iteration;
  * [x] decoded Unicode-scalar iteration;
  * [x] explicit invalid-encoding policy;
  * [x] display-column width calculation;
  * [x] tab-stop grammar and repeated tab intervals;
  * [x] backspace and carriage-return column behavior;
  * [X] injectable width and locale providers.

This gate provides only the facilities needed by `expand`, `unexpand`, `fold`, and the later page-layout commands. It does not prematurely introduce sorting or external-storage behavior.

### Batch 17 — Tabs and display columns (3 tools)

- [x] `expand`
- [x] `unexpand`
- [x] `fold`

Use the shared display-column and tab-stop model. Cover tab lists, repeated tab intervals, initial/all modes, byte versus character behavior, word-boundary folding, backspaces, carriage returns, wide characters, and invalid multibyte input.

### Batch 18 — Paragraph and line-number formatting (2 tools)

- [ ] `fmt`
- [ ] `nl`

Implement paragraph recognition, sentence spacing, crown/tagged modes, logical-page delimiters, numbering styles, header/body separation, and standard-stream ownership. Share line-layout and display-width components without forcing one combined execution engine.

### Completion Gate C3 — before Batch 19

* [ ] Add the shared record, range, and escape model:

  * [ ] configurable line-delimited and NUL-delimited record readers and writers;
  * [ ] byte, character, field, and general range-list parsing;
  * [ ] complement and open-ended range handling;
  * [ ] delimiter and separator abstractions;
  * [ ] documented escape-sequence parsing;
  * [ ] deterministic behavior for malformed ranges and escapes.

This gate directly supports `cut` and `paste`, then remains available to `tr`, `sort`, `split`, and related Coreutils and Textutils commands. The genuinely cross-suite record and delimiter contracts are candidates for eventual extraction into `Icod.CommandFramework` for use by `Icod.Grep` and `Icod.Sed`.

### Batch 19 — Field and record extraction (2 tools)

- [ ] `cut`
- [ ] `paste`

Implement complete byte/character/field list grammar, complement and output delimiters, NUL records, delimiter suppression, serial paste, delimiter escape cycles, multiple input streams, and correct behavior on multibyte input.

### Completion Gate D — before Batch 20

* [ ] Extend the secure temporary-object infrastructure established by `mktemp` with the shared external-ordering model:

  * [ ] locale-aware collation;
  * [ ] reusable sort-key parsing and comparison;
  * [ ] stable comparison and original-order tracking;
  * [ ] bounded-memory sorted runs;
  * [ ] stable external merge;
  * [ ] temporary-workspace lifecycle management;
  * [ ] deterministic cleanup on success, failure, and cancellation.

This gate is intentionally not split because its components jointly form the execution foundation required by `sort`.

### Batch 20 — External ordering (1 tool)

- [ ] `sort`

For `sort`, implement key specifications, locale collation, stable and unique modes, numeric families, month/version/human-numeric comparison, checking and merging, secure temporary runs, bounded-memory external merge, zero-terminated records, and exact exit statuses.

### Batch 21 — External randomization (1 tool)

- [ ] `shuf`

`shuf` reuses the temporary-storage and record infrastructure established by earlier batches while retaining its own randomized execution engine.

### Batch 22 — Sorted-stream consumers (3 tools)

- [ ] `comm`
- [ ] `join`
- [ ] `uniq`

Reuse the collation and record model established by `sort`. Preserve duplicate-key Cartesian behavior in `join`, order checking, header and output formatting, field selection, skip/check options, counting, zero records, and streaming without loading complete inputs.

### Batch 23 — Character transformation (1 tool)

- [ ] `tr`

Implement the full `tr` set-expression grammar, including ranges, escapes, repetition, character classes, equivalence classes, complement, delete, squeeze, locale behavior, and delimiter bytes.

### Batch 24 — Graph ordering (1 tool)

- [ ] `tsort`

Implement `tsort` tokenization, deterministic ordering, stable diagnostics, and cycle reporting.

### Batch 25 — Permuted indexing (1 tool)

- [ ] `ptx`

Reuse the established text, locale, tokenization, ordering, and spill-storage primitives without coupling `ptx` to the execution engines of earlier commands.

### Completion Gate E1 — before Batch 26

* [ ] Add the shared read-only pathname traversal model:

  * [ ] centralize pathname expansion policy for eligible operands;
  * [ ] recursive directory enumeration;
  * [ ] symlink and reparse-point traversal policy;
  * [ ] file identity sufficient for cycle detection;
  * [ ] mount-boundary policy;
  * [ ] include and exclude matching support;
  * [ ] deterministic error-continuation behavior;
  * [ ] injectable filesystem-enumeration providers.

This gate is completed before the co-resident `Icod.Grep` batch so recursive search consumes a stable read-only traversal contract. It also supports later Coreutils directory listing and filesystem accounting, recursive directory comparison in `Icod.DiffUtils`, and archive traversal in `Icod.Tar`. The cross-suite portions remain candidates for `Icod.CommandFramework`.

### Batch 26 — `Icod.Grep` search engine (1 tool)

- [ ] `Icod.Grep`

Create the suite-correct `Icod.Grep` project inside the current solution, migrate the existing grep seed implementation and tests into that namespace, and implement the documented GNU grep 3.12 option and pattern model. Cover multiple pattern sources, basic/extended/fixed/Perl-mode policy, recursive traversal, include/exclude rules, binary policy, context, filename and line metadata, counts, quiet/list modes, NUL behavior, and the required 0/1/2 status distinction.

`Icod.Grep` consumes the current regular-expression, record, diagnostic, and read-only traversal abstractions through project references during incubation. Grep-specific matcher orchestration, binary-input policy, context grouping, and output formatting remain in the grep project or a repository-local engine if later testing justifies one. Completion Gate G will move genuine cross-suite dependencies to `Icod.CommandFramework` and extract `Icod.Grep` into its own solution and repository.

### Batch 27 — Splitting and reversing (2 tools)

- [ ] `split`
- [ ] `tac`

Repair split-output rotation and support nonseekable input, line/byte/chunk modes, suffix alphabets, filters, additional suffixes, numeric suffixes, and exact file-creation cleanup.
Implement `tac` with backward file scanning or secure temporary spooling rather than whole-input memory loading.

### Batch 28 — Pattern-directed splitting (1 tool)

- [ ] `csplit`

Reuse the regular-expression policy established by Completion Gate C1 for `csplit`, including numeric and regex addresses, offsets, repetition, suppression, prefix/suffix grammar, keep-files behavior, exact byte counts, and cleanup after failure or cancellation. Do not introduce a runtime dependency on `Icod.Grep`.

### Batch 29 — Page presentation (1 tool)

- [ ] `pr`

For `pr`, implement columns, page geometry, headers and footers, form feeds, dates, numbering, merge modes, separators, and terminal-independent output.

### Batch 30 — `Icod.DiffUtils.Shared` foundation (1 library)

- [ ] `Icod.DiffUtils.Shared`

Create the suite-specific Shared project inside the current solution and add its dedicated test project. Record GNU Diffutils 3.12 as the authoritative baseline. Establish comparison inputs, byte and line normalization, edit scripts, ranges, hunks, output-format models, temporary-workspace use, directory-comparison coordination, three-way merge models, and side-by-side layout primitives only where they are genuinely shared by two or more Diffutils commands.

`Icod.DiffUtils.Shared` uses a project reference to the current Shared incubation project. It must not absorb general command, filesystem, text, locale, process, or platform behavior merely because those APIs have not yet been extracted into `Icod.CommandFramework`.

### Batch 31 — `Icod.DiffUtils.Cmp` byte comparison (1 tool)

- [ ] `Icod.DiffUtils.Cmp`

Create the suite-correct project and test project with lowercase assembly name `cmp`. Implement byte-oriented comparison, silent mode, all-differences reporting, byte and line numbering, skip and limit operands, EOF diagnostics, binary-safe standard input, cancellation, and exact statuses for equality, difference, and error.

This batch validates the byte-comparison and result-status contracts without requiring the line-difference engine.

### Batch 32 — `Icod.DiffUtils.Diff` difference engine (1 tool)

- [ ] `Icod.DiffUtils.Diff`

Create the suite-correct project, migrate the existing `diff` seed code and tests into the new namespace, and replace the simplified implementation with a real sequence-difference engine. Implement normal, context, unified, ed, and other in-scope formats; whitespace and case policies; labels; function context; binary handling; incomplete-line behavior; recursive directory comparison; absent-file policy; and statuses 0 for no differences, 1 for differences, and greater than 1 for errors.

The existing `Icod.CoreUtils.Diff` project is retired only after the new project and tests pass throughout the solution. Textual fixtures must be independently consumable by GNU patch, `Icod.Patch`, GNU ed, and `Icod.Ed`.

### Batch 33 — `Icod.DiffUtils.Diff3` three-way comparison (1 tool)

- [ ] `Icod.DiffUtils.Diff3`

Create the suite-correct project and test project with lowercase assembly name `diff3`. Implement three-file comparison, common-ancestor modes, overlap classification, merge output, conflict markers, ed scripts, labels, input validation, and exact statuses. Reuse the proven two-way data model without forcing `diff3` semantics into the `diff` front end.

### Batch 34 — `Icod.DiffUtils.SDiff` side-by-side comparison (1 tool)

- [ ] `Icod.DiffUtils.SDiff`

Create the suite-correct project and test project with lowercase assembly name `sdiff`. Implement side-by-side layout, width and display-column handling, common-line suppression, left-column behavior, tab expansion, interactive merge commands, editor invocation without unsafe shell interpolation, transactional output, nonterminal behavior, and exact status propagation.

Completion of Batches 30 through 34 leaves the complete GNU Diffutils family implemented and tested inside the current solution. Repository extraction remains deferred until Completion Gate G.

### In-solution suite incubation milestone — `Icod.Patch`

This milestone does not alter command-batch numbering.

- [ ] Create `Icod.Patch` and `Icod.Patch.Tests` in the current solution.
- [ ] Migrate the existing patch seed implementation and relevant tests into the correct namespace.
- [ ] Record GNU patch 2.8 as the authoritative baseline.
- [ ] Establish independent normal, context, and unified patch corpora, including output from GNU Diffutils and `Icod.DiffUtils`.
- [ ] Keep the production boundary textual: `Icod.Patch` must not reference `Icod.DiffUtils.Shared` merely to consume patch files.
- [ ] Keep patch parsing, hunk application, fuzz and offset matching, reversal detection, rejects, backups, and application state inside `Icod.Patch` or a repository-local engine.
- [ ] Consume transactional replacement capabilities after Completion Gate E6 rather than duplicating them.
- [ ] Preserve the lowercase assembly name `patch` and use a suite-specific solution folder.

Detailed GNU patch conformance continues under the `Icod.Patch` roadmap while the project remains co-resident. Final solution and repository extraction occurs in Completion Gate G.

### In-solution suite incubation milestone — `Icod.Ed`

This milestone does not alter command-batch numbering.

- [ ] Create `Icod.Ed.Shared`, `Icod.Ed.Ed`, `Icod.Ed.Red`, and their test projects in the current solution.
- [ ] Migrate the existing editor seed implementation and relevant tests into the correct namespace family.
- [ ] Record GNU ed 1.22.5 as the authoritative baseline.
- [ ] Keep address parsing, command parsing, mutable line buffers, marks, substitutions, global commands, undo state, file operations, shell integration, and restricted-mode enforcement in `Icod.Ed.Shared`.
- [ ] Establish textual compatibility tests for ed scripts emitted by GNU Diffutils and `Icod.DiffUtils`.
- [ ] Consume common regular-expression, text, process, and transactional replacement contracts through project references during incubation.
- [ ] Preserve lowercase assembly names `ed` and `red`.

Detailed conformance follows the `Icod.Ed` roadmap while the projects remain in the same solution. Final solution and repository extraction occurs in Completion Gate G.

### In-solution suite incubation milestone — `Icod.Sed`

This milestone preserves completed historical Batch 2 while correcting project ownership without rewriting history.

- [ ] Create or rename the command project as `Icod.Sed` with lowercase assembly name `sed`.
- [ ] Migrate the completed seed implementation, tests, and namespace into the suite-correct project inside the current solution.
- [ ] Record the pinned GNU sed release as the authoritative baseline.
- [ ] Keep the sed parser, address model, pattern and hold spaces, substitution engine, branching, command cycle, and in-place-editing state inside `Icod.Sed` or its repository-local engine.
- [ ] Consume common regular-expression, text-record, temporary-file, filesystem, and replacement contracts through project references during incubation.
- [ ] Keep sed-specific execution state out of the general Shared incubation project.

Further GNU sed conformance follows the `Icod.Sed` roadmap. Final solution and repository extraction occurs in Completion Gate G.

### Completion Gate E2 — before Batch 35

* [ ] Complete the shared canonical-path model:

  * [ ] lexical path normalization;
  * [ ] physical path resolution;
  * [ ] symbolic-link and reparse-point inspection;
  * [ ] missing-component policies;
  * [ ] loop detection;
  * [ ] relative-path calculation;
  * [ ] platform root, volume, and separator semantics;
  * [ ] deterministic failure without returning unresolved input as success.

This gate supplies the defining infrastructure for `readlink` and `realpath`.

### Batch 35 — Symbolic-link and canonical-path resolution (2 tools)

- [ ] `readlink`
- [ ] `realpath`

Implement lexical versus physical resolution, missing-component policies, canonicalization modes, delimiters, quiet/verbose behavior, relative output, symlink loops, reparse points, and deterministic failures. Never return the unresolved input as a false success.

### Completion Gate E3 — before Batch 36

* [ ] Add the authoritative shared filesystem-metadata model:

  * [ ] file type and size;
  * [ ] link count and link identity;
  * [ ] mode, ownership, and group information;
  * [ ] access, modification, inode-change, and birth timestamps;
  * [ ] device, inode, and platform-equivalent identity;
  * [ ] allocated-block accounting;
  * [ ] filesystem information;
  * [ ] timestamp mutation capabilities;
  * [ ] explicit reporting of unavailable platform metadata.

This gate supports `stat`, `touch`, and the file predicates subsequently required by `test`.

### Batch 36 — File metadata and timestamps (2 tools)

- [ ] `stat`
- [ ] `touch`

Build the authoritative metadata adapter and format-string engine. Distinguish access, modification, inode-change, and birth times where available; expose controlled platform gaps; support dereference policies, filesystems, reference files, date parsing, selective timestamps, no-create, and directories.

### Batch 37 — Condition evaluator (1 tool)

- [ ] `test`

Implement the complete GNU/POSIX operand-count grammar, file type and characteristic predicates, access checks, string and numeric comparisons, connectives, precedence, ambiguity rules, and statuses 0, 1, and 2. **Do not create a separate `[` project.**

### Completion Gate E4 — before Batch 38

* [ ] Add shared mode and basic pathname-mutation infrastructure:

  * [ ] numeric mode parsing;
  * [ ] symbolic mode-clause parsing;
  * [ ] umask application;
  * [ ] basic directory, file, link, FIFO, and device-node capability providers;
  * [ ] no-follow and dereference policies;
  * [ ] race-aware single-path mutation;
  * [ ] controlled privilege and platform diagnostics.

This gate supports `mkdir`, `rmdir`, `unlink`, `link`, `ln`, `mkfifo`, `mknod`, and the later permission commands.

### Batch 38 — Basic directory and name removal (3 tools)

- [ ] `mkdir`
- [ ] `rmdir`
- [ ] `unlink`

Implement modes, parents, verbose/context policy, ignore-fail behavior, parent removal, exact operand rules, and deterministic handling of files versus directories. These commands validate the new filesystem adapter without yet introducing recursive deletion.

### Batch 39 — Hard and symbolic links (2 tools)

- [ ] `link`
- [ ] `ln`

Make `link` the documented two-operand hard-link command. Build `ln` as a separate front end over shared link primitives, covering symbolic/physical/logical behavior, targets, directories, relative links, backups, force/interactive modes, and platform capability diagnostics. Do not invoke native `ln`.

### Batch 40 — Special file creation (2 tools)

- [ ] `mkfifo`
- [ ] `mknod`

Add the missing GNU projects. Implement modes, FIFO creation, block/character device operands, major/minor validation, umask behavior, and controlled privilege/platform failure. Never emulate success by creating an ordinary file.

### Completion Gate E5 — before Batch 41

* [ ] Extend the traversal model for recursive mutation and copying:

  * [ ] mutation-safe recursive traversal;
  * [ ] preserve-root protection;
  * [ ] one-filesystem boundaries;
  * [ ] race-aware no-follow operations;
  * [ ] hard-link identity tracking;
  * [ ] sparse-file detection and preservation;
  * [ ] metadata-preservation policy;
  * [ ] destination-inside-source detection;
  * [ ] partial-failure and cleanup policy;
  * [ ] integration points for the later transactional replacement and backup model.

This gate supports recursive `chmod`, `chown`, `chgrp`, `rm`, `cp`, `mv`, `install`, `du`, and `tar`.

### Batch 41 — Permission modes (1 tool)

- [ ] `chmod`

Implement octal parsing correctly, symbolic clauses, omitted-who/umask behavior, recursive traversal, reference mode, symlink policy, preserve-root, verbose/change reporting, and Windows capability mapping without pretending that the read-only attribute is a complete Unix mode.

### Batch 42 — Ownership and group mutation (2 tools)

- [ ] `chown`
- [ ] `chgrp`

Replace `NotImplementedException` with real Unix ownership operations and controlled non-Unix diagnostics. Implement names and numeric IDs, reference files, dereference policies, recursive traversal, from-filtering, preserve-root, and verbose/change reporting.

### Batch 43 — Recursive removal (1 tool)

- [ ] `rm`

Use the shared traversal engine. Implement interactive modes, recursive directory handling, force, one-file-system, preserve-root, empty-directory removal, symlink safety, write-protected prompts, race-aware deletion, glob expansion policy, and error continuation.

### Completion Gate E6 — before Batch 44

* [ ] Add shared transactional file-replacement infrastructure:

  * [ ] secure sibling temporary files;
  * [ ] atomic replacement where supported;
  * [ ] backup-name generation and retention policy;
  * [ ] rollback behavior after partial failure;
  * [ ] pathname-containment and escape checks;
  * [ ] deterministic cleanup after success, failure, and cancellation;
  * [ ] explicit diagnostics where atomic replacement is unavailable;
  * [ ] integration with the recursive traversal and metadata-preservation contracts established by Completion Gate E5.

This gate is placed immediately before `cp` and `mv`, the first remaining Coreutils/Fileutils commands that require the complete replacement, backup, and rollback model. During incubation, the co-resident `Icod.Patch`, `Icod.Ed`, `Icod.Sed`, and `Icod.Tar` projects may consume these contracts through project references; their genuinely cross-suite portions are candidates for `Icod.CommandFramework`.

### Batch 44 — Copy and move engine (2 tools)

- [ ] `cp`
- [ ] `mv`

Implement source/destination classification, recursive copy, symlink and hard-link policy, metadata preservation, sparse files, reflink/copy-file-range opportunities, backup and overwrite modes, update rules, atomic replacement, cross-filesystem moves, destination-inside-source prevention, and partial-failure cleanup.

### Batch 45 — Installation engine (1 tool)

- [ ] `install`

Build on `mkdir`, `cp`, `chmod`, and `chown` primitives rather than invoking external utilities. Implement directory creation, modes, owners/groups, stripping policy, backups, compare mode, timestamps, SELinux-context policy, and atomic destination replacement.

### Completion Gate F1 — before Batch 46

* [ ] Add shared terminal-aware presentation capabilities:

  * [ ] terminal-versus-redirected stream detection;
  * [ ] terminal width and height discovery;
  * [ ] color-capability policy;
  * [ ] quoting and control-character presentation policy;
  * [ ] environment and terminal-name inputs used by `dircolors`;
  * [ ] injectable providers for deterministic tests;
  * [ ] controlled fallback when terminal information is unavailable.

This gate provides only the presentation capabilities needed by `dircolors`, `ls`, `dir`, and `vdir`.

### Batch 46 — Color database and directory listing family (4 tools)

- [ ] `dircolors`
- [ ] `ls`
- [ ] `dir`
- [ ] `vdir`

Implement the documented `dircolors` database grammar, terminal selectors, file-extension rules, shell-specific output, built-in database, print-database mode, and diagnostics. Produce a reusable `LS_COLORS` parser for the listing engine.

Create one listing engine with three thin entry profiles. Implement locale sorting, quoting, color, columns, widths, recursion with cycle protection, symlink policy, inode/block/owner/group/mode metadata, human sizes, time styles, indicators, classification, dereference modes, and terminal-sensitive defaults. Remove independent simplified `dir` and `vdir` implementations.

### Batch 47 — Filesystem usage reporting (2 tools)

- [ ] `df`
- [ ] `du`

Use real allocated-block and filesystem data where available. Implement block-size environment rules, human/SI formats, inode reporting, filesystem types, exclusions, totals, apparent size, hard-link deduplication, symlink and mount policies, depth and summarize modes, NUL input, and controlled platform differences.

### Batch 48 — Data destruction (1 tool)

- [ ] `shred`

Implement pass selection, random sources, exact-size handling, synchronization, removal and renaming policy, device/file distinctions, progress, and failure recovery. Document and test the limits of overwriting on SSDs, copy-on-write filesystems, snapshots, journaling, and remapped storage.

### Completion Gate F2 — shared host and processor-resource foundation before Batch 49

* [ ] Add shared host and processor-resource capabilities to the current Shared incubation project:

  * [ ] host-identifier retrieval and normalization;
  * [ ] configured processor count;
  * [ ] installed processor count;
  * [ ] online processor count;
  * [ ] processors available to the current process;
  * [ ] current-process affinity inspection;
  * [ ] container, job-object, processor-set, and cgroup quota inspection where available;
  * [ ] optional processor topology and NUMA descriptors where supported;
  * [ ] capability and data-provenance reporting;
  * [ ] injectable providers and deterministic tests;
  * [ ] controlled and documented platform differences.

These factual provider contracts are provisionally classified as `Icod.CommandFramework` candidates even though they are implemented physically in the current `Icod.CoreUtils.Shared` project during incubation. They support `hostid` and `nproc` immediately and later supply processor-resource facts to `Icod.ProcPs.Shared`, `ps`, `top`, `vmstat`, and other consumers.

GNU `nproc` interpretation of `OMP_NUM_THREADS`, `OMP_THREAD_LIMIT`, `--all`, `--ignore`, minimum-result policy, diagnostics, and exit statuses remains command-specific and must not be embedded in the general processor provider.

### Batch 49 — Host and processor context (2 tools)

- [ ] `hostid`
- [ ] `nproc`

Add the missing projects. Define reproducible host-ID behavior. Build `nproc` as the first consumer of the shared processor-resource provider, then apply GNU-specific environment overrides, `--all`, `--ignore`, minimum-result, affinity, quota, diagnostics, and exit-status policy in the command project.

### Completion Gate F3 — before Batch 50

* [ ] Add shared terminal-identification and terminal-control capabilities:

  * [ ] terminal pathname discovery;
  * [ ] terminal attachment inspection for selected file descriptors;
  * [ ] terminal-mode retrieval and mutation;
  * [ ] input and output speed reporting;
  * [ ] control-character representation;
  * [ ] machine-readable mode serialization and restoration;
  * [ ] explicit Unix and Windows capability boundaries.

This gate supports `tty` and `stty` without requiring child-process or signal infrastructure prematurely.

### Batch 50 — Terminal identification (1 tool)

- [ ] `tty`

Add the missing project. Implement silent mode, terminal-name reporting, correct standard-input inspection, and statuses for terminal versus nonterminal input across supported platforms.

### Batch 51 — Terminal characteristics (1 tool)

- [ ] `stty`

Add the missing project as a dedicated platform batch. Implement reading and changing terminal modes, sane/raw profiles, control characters, speed, machine-readable save/restore form, selected device handling, and a documented Windows capability boundary.

### Completion Gate F4 — shared process execution and control foundation before Batch 52

* [ ] Add cross-suite process execution and control primitives to the current Shared incubation project:

  * [ ] process identity with optional PID-reuse detection tokens where supported;
  * [ ] process, process-group, and session target models;
  * [ ] executable lookup;
  * [ ] argument-safe process launching without shell interpolation;
  * [ ] working-directory and environment construction;
  * [ ] asynchronous standard-stream forwarding;
  * [ ] child-process lifetime, cancellation, cleanup, and orphan policy;
  * [ ] process liveness and deterministic vanished-process results;
  * [ ] child-process waiting;
  * [ ] arbitrary-process waiting capability contracts for later `pidwait` use;
  * [ ] signal-name and signal-number parsing;
  * [ ] signal listing, translation, disposition, and delivery;
  * [ ] process-priority retrieval and mutation;
  * [ ] monotonic-clock, cancellation-aware delay, and periodic-scheduling contracts;
  * [ ] exit-status, signal-termination, timeout, and other termination-reason translation;
  * [ ] controlled Windows substitutions where semantics are defensible;
  * [ ] injectable providers and cross-platform integration tests.

These contracts are provisionally classified as `Icod.CommandFramework` candidates even though they are implemented physically in the current `Icod.CoreUtils.Shared` project during incubation.

This gate supports `env` and `nohup`, allows Coreutils `kill` to validate signal parsing, targets, and delivery in Batch 53, allows `nice` and `timeout` to validate priority, waiting, process groups, clocks, termination, and status propagation in Batch 54, and supplies the common mechanics consumed immediately by the ProcPs block and later by `Icod.Tar`, `Icod.Ed`, and other suites.

### Batch 52 — Environment and hangup-independent execution (2 tools)

- [ ] `env`
- [ ] `nohup`

Build the shared child-process launch environment. Implement environment clearing/removal, split-string parsing, working directory, `argv0`, signal policy, NUL output, command lookup, `nohup` redirection rules, diagnostics, and asynchronous stream forwarding.

### Batch 53 — Signal control (1 tool)

* [ ] `kill`

Implement signal-name and signal-number parsing, signal listing and translation, process and process-group targets, queued values where supported and in scope, exact diagnostics and exit statuses, and Windows substitutions only where they are semantically defensible.

### Batch 54 — Priority and time-bounded execution (2 tools)

* [ ] `nice`
* [ ] `timeout`

Implement priority adjustment without child-start races. Parse the complete duration grammar and support signal selection, kill-after behavior, foreground and process-group handling, status preservation, verbose diagnostics, exact exit-status propagation, and explicit platform-capability handling.

### Completion Gate P1 — ProcPs classification and provider foundation before Batch 55

- [ ] Establish the co-resident procps-ng suite foundation:

  - [ ] pin procps-ng 4.0.6 and audit its exact command, launcher, alias, and install inventory;
  - [ ] resolve the pinned-baseline relationship between `pidwait` and `pwait`;
  - [ ] audit every processor- and process-related API created by Completion Gates F2 through F4 and record its actual Coreutils, ProcPs, Tar, Ed, and other suite consumers;
  - [ ] keep genuinely cross-suite processor-resource, process-identity, target, launch, wait, signal, priority, clock, scheduler, status, and terminal contracts in the current Shared incubation project and classify them as future `Icod.CommandFramework` candidates;
  - [ ] prohibit `Icod.ProcPs.Shared` from introducing duplicate abstractions for those common mechanics;
  - [ ] define Linux `/proc` as the canonical ProcPs observation provider;
  - [ ] define Windows, macOS, and BSD process and system observation capabilities;
  - [ ] attach provenance to every field that is exact, equivalent, approximated, synthesized, or unavailable;
  - [ ] define the boundary between general process mechanics and ProcPs-specific enumeration, detailed snapshots, selection grammar, fields, personalities, metrics, sorting, and presentation;
  - [ ] define process lifetime races, permissions, namespaces, containers, affinity, and quota interpretation for ProcPs observations without redefining the underlying common identity and resource contracts;
  - [ ] define memory, swap, CPU activity, load, uptime, virtual-memory, process-map, slab, hugepage, user-session, and kernel-parameter provider boundaries;
  - [ ] consume the shared monotonic clock and periodic scheduler while defining ProcPs-specific sampling intervals, counter deltas, wraparound, and refresh semantics;
  - [ ] consume shared terminal primitives while defining testable ProcPs screen models, interaction, sorting, filtering, and configuration behavior;
  - [ ] establish suite-specific output directories for command names that collide with existing Coreutils projects;
  - [ ] establish fixture-driven `/proc` parsing and injectable ProcPs provider tests.

Linux behavior remains authoritative for procps-ng. Other supported platforms must expose honest capability and provenance information rather than fabricated Linux fields, misleading zero values, or silent success.

This gate follows Batches 52 through 54 so ProcPs can consume already tested process launch, identity, targets, waiting, signals, process groups, status propagation, priorities, clocks, and timeout foundations. It precedes the ProcPs block so those cross-suite facilities are not rediscovered independently by each procps-ng command and so ProcPs-specific observation begins at the correct architectural layer.

### Batch 55 — `Icod.ProcPs.Shared` provider foundation (1 library)

- [ ] `Icod.ProcPs.Shared`

Create the suite-specific Shared project and its dedicated test project inside the current solution. Implement the procps-ng-specific observation and domain foundation:

- process enumeration and detailed snapshots built on the common process-identity model;
- parent and child relationships, sessions, groups, users, terminals, namespaces, containers, and lifetime-race interpretation required by procps-ng;
- Linux `/proc` parsing and equivalent Windows, macOS, and BSD observation providers;
- field provenance and capability reporting;
- the procps-ng process-selection grammar and adapters over the common signal, arbitrary-wait, priority, and target providers;
- memory, swap, CPU activity, load, uptime, virtual-memory, map, slab, hugepage, and user-session metrics;
- counter-delta, sampling-window, wraparound, and refresh calculations over the common monotonic clock and scheduler;
- field catalogs, sorting, personalities, display policy, configuration, and reusable ProcPs screen models.

`Icod.ProcPs.Shared` uses project references to the current Shared incubation project during development. It consumes common processor-resource, process identity, targets, launching, waiting, signals, priorities, clocks, scheduling, statuses, and terminal primitives from that project. Procps-specific enumeration, fields, selection, personalities, `/proc` parsers, kernel models, metric interpretation, and screen state remain here. Neither layer should duplicate the other merely because `Icod.CommandFramework` has not yet been extracted.

### Batch 56 — Basic system summaries (2 tools)

- [ ] `Icod.ProcPs.Uptime`
- [ ] `Icod.ProcPs.Free`

Create the suite-correct projects and dedicated test projects with lowercase assembly names `uptime` and `free`.

For `uptime`, migrate useful code and tests from historical Batch 9 and implement the procps-ng profile, including pretty and since output, load averages, user counts where required, container-aware behavior, exact diagnostics, and platform data provenance.

For `free`, implement physical and swap memory reporting, units, human-readable and SI forms, totals, wide, low/high, committed-memory forms, repeated sampling, exact rounding, and controlled platform limitations.

These commands validate the narrowest uptime, load, memory, unit, and provenance contracts before more complicated sampled or per-process consumers are introduced. Because a Coreutils-profile `uptime` may also exist, use the ProcPs suite output directory during incubation and keep the conformance profiles separately testable until final packaging policy is decided.

### Batch 57 — Sampled system statistics (1 tool)

- [ ] `Icod.ProcPs.Vmstat`

Create the project and tests with assembly name `vmstat`. Implement process, memory, paging, block I/O, system, CPU, disk, partition, slab, forks, statistics, timestamps, units, wide mode, repeated sampling, counter wraparound, cancellation, and exact platform capability reporting.

This batch validates deterministic clocks, interval sampling, counter deltas, units, wraparound, and partial provider availability before the full-screen tools depend on them.

### Batch 58 — Process selection, signaling, and waiting (4 projects)

- [ ] `Icod.ProcPs.Pgrep`
- [ ] `Icod.ProcPs.Pkill`
- [ ] `Icod.ProcPs.PidWait`
- [ ] `Icod.ProcPs.PWait`

Implement one shared process-selection grammar and apply it consistently across selection, signal delivery, and waiting.

For `pgrep`, cover names and regular expressions; IDs; ancestry; sessions; groups; users; terminals; namespaces; ages; environment; signal handlers; pidfiles; newest and oldest rules; counts; delimiters; shell quoting; and exact no-match and error statuses.

For `pkill`, reuse the same selection model and implement signal delivery, queued values, echoing, newest and oldest behavior, process release where supported, partial failure, and exact statuses.

For `pidwait`, implement the pinned procps-ng waiting behavior using pidfd or an equivalent provider where available, including selection, vanished processes, permissions, cancellation, and exact statuses. Upstream renamed `pwait` to `pidwait`; retain `pwait` as a compatibility launcher only when confirmed by the pinned suite compatibility policy. Both launchers must use one engine rather than drift into separate implementations.

### Batch 59 — Process lookup and working directories (2 tools)

- [ ] `Icod.ProcPs.PidOf`
- [ ] `Icod.ProcPs.Pwdx`

For `pidof`, implement program-name matching, scripts, roots, omission lists, separators, single-result behavior, kernel-thread and zombie policy, namespace and container effects, and deterministic no-match behavior.

For `pwdx`, implement one or more process targets, permission and vanished-process behavior, process-root and namespace effects, path reporting, exact diagnostics, and controlled platform limitations.

These commands provide focused validation of executable identity, process roots, namespace-aware path resolution, and short-lived-process races before the larger process-reporting engine.

### Batch 60 — Direct and legacy process control (3 tools)

- [ ] `Icod.ProcPs.Kill`
- [ ] `Icod.ProcPs.Skill`
- [ ] `Icod.ProcPs.Snice`

For ProcPs `kill`, implement the procps-ng option and signal model, process targets, queued values where supported, listing and translation, exact diagnostics, and platform substitutions only where semantically defensible.

For `skill`, implement the pinned obsolete selection and signaling interface faithfully, reuse the shared process-selection and signal-delivery models, issue upstream-compatible warnings where applicable, and do not silently substitute `pkill` argument semantics.

For `snice`, implement the pinned obsolete selection and priority grammar, process targeting, privilege failures, partial success, diagnostics, and platform capability mapping.

This batch follows Coreutils `kill`, `nice`, and `timeout` so common signal, target, priority, and child-process behavior has already been exercised. Because a Coreutils-profile `kill` may coexist, use the ProcPs suite output directory and keep both conformance profiles separately testable until Completion Gate G.

### Batch 61 — Process memory maps (1 tool)

- [ ] `Icod.ProcPs.Pmap`

Create the project and tests with assembly name `pmap`. Implement basic, extended, device, quiet, range, totals, permissions, offsets, mappings, names, UTF-8 handling, vanished-process behavior, privilege diagnostics, and explicit capability reporting when a platform cannot supply Linux-equivalent maps.

### Batch 62 — Process reporting engine (1 tool)

- [ ] `Icod.ProcPs.Ps`

Create the suite-correct project, migrate useful implementation and tests from historical Batch 9, and retire the old CoreUtils-namespace project only after the new project is green.

Implement procps-ng personalities, selection forms, field catalogs, aliases, custom formats, sorting, threads, forests, security labels, terminals, widths, headers, environment and command data, containers, namespaces, signals, capabilities, start and elapsed times, CPU and memory calculations, and exact output formatting.

`ps` is deliberately scheduled after several smaller consumers have validated process enumeration, identity, selection, races, namespaces, maps, signals, and metrics. It is a presentation and compatibility engine over `Icod.ProcPs.Shared`, not the provider foundation itself.

### Batch 63 — User and session reporting (1 tool)

- [ ] `Icod.ProcPs.W`

Create the project and tests with assembly name `w`. Implement logged-in users, terminals, origins, login and idle times, current processes, JCPU and PCPU, load and uptime headings, short and long forms, container behavior, utmp or equivalent provider limitations, and exact diagnostics.

### Batch 64 — Kernel parameter control (1 tool)

- [ ] `Icod.ProcPs.Sysctl`

Create the project and tests with assembly name `sysctl`. Implement name and value reads, writes, patterns, exclusions, configuration-file ordering, system mode, deprecated-key forms, privilege behavior, exact statuses, and an explicit Linux-centric capability boundary.

Do not pretend that unrelated Windows, macOS, or BSD settings are Linux sysctl keys. Equivalent native functionality may be exposed only where the mapping is documented and semantically defensible.

### Batch 65 — Load display (1 tool)

- [ ] `Icod.ProcPs.Tload`

Create the project and tests with assembly name `tload`. Implement terminal load graphs, scale and delay controls, resize handling, selected terminal output, deterministic sampling clocks, redirected-output policy, cancellation, suspension and resume, and reliable terminal restoration.

`tload` is the first focused validation of the ProcPs full-screen refresh foundation.

### Batch 66 — Periodic command display (1 tool)

- [ ] `Icod.ProcPs.Watch`

Create the project and tests with assembly name `watch`. Implement periodic argument-safe child execution, interval and precision, differences and color, headers, beep, equexit, chgexit and error-exit behavior, terminal resizing, visible-change semantics, command-status propagation, cancellation, suspension and resume, and terminal restoration.

This batch combines the shared terminal refresh model with the child-process infrastructure already established by Completion Gate F4 and Batches 52 through 54.

### Batch 67 — Specialized kernel-memory displays (2 tools)

- [ ] `Icod.ProcPs.HugeTop`
- [ ] `Icod.ProcPs.SlabTop`

For `hugetop`, implement system and per-process hugepage reporting, sorting, refresh, batch and interactive modes, terminal behavior, and controlled unsupported diagnostics where the platform exposes no equivalent data.

For `slabtop`, implement slab-cache metrics, sorting, human-readable sizes, refresh, batch and full-screen behavior, resizing, and controlled unsupported diagnostics outside platforms exposing equivalent kernel data.

These commands share the terminal runtime but validate distinct Linux kernel-memory providers. They must not fabricate plausible-looking data on unsupported platforms.

### Batch 68 — Interactive process monitor (1 tool)

- [ ] `Icod.ProcPs.Top`

Create the project and tests with assembly name `top`. Implement dynamic sampling, process fields, sorting, filtering, forests, threads, CPU and memory summaries, configuration, colors, windows, interactive commands, line editing, signals, batch mode, terminal resize, suspension and resume, and reliable terminal restoration.

Keep the screen model independently testable from rendering. `top` is last in the ProcPs block because it combines almost every provider, metric, process-selection, formatting, signal, sampling, configuration, and terminal capability established by the preceding batches.

Completion of Batches 55 through 68 leaves the complete command family installed by the pinned procps-ng baseline implemented as suite-correct projects inside the current solution. The exact inventory is verified in Completion Gate P1; any discrepancy is corrected in the roadmap rather than silently omitted. Final solution, repository, package, launcher-alias, and executable-collision policy is resolved in Completion Gate G.

### Batch 69 — Root-directory execution (1 tool)

- [ ] `chroot`

Replace `NotImplementedException` with a real Unix implementation and controlled diagnostics elsewhere. Implement users and groups, supplementary-group initialization, skip-chdir policy, command lookup after root change, privilege handling, and process execution without unsafe shell interpolation.

### Batch 70 — SELinux context operations (2 tools)

- [ ] `chcon`
- [ ] `runcon`

Treat these as Linux and SELinux capability commands. Use native APIs or stable libraries rather than invoking external commands. Implement reference and component contexts, dereference and recursion policy, preserve-root, computed and process context behavior, privilege failures, and explicit diagnostics when SELinux is unavailable.

### Batch 71 — Standard-stream buffering control (1 tool)

- [ ] `stdbuf`

Begin with a documented feasibility decision. The current silent fallback is unacceptable. Implement supported preload or native-shim semantics where reliable; otherwise report controlled unsupported behavior for affected commands and platforms. Test child startup, environment injection, buffering modes, stream behavior, and exit-status propagation.

### Batch 72 — `Icod.Tar` archive engine (1 tool)

- [ ] `Icod.Tar`

Create the suite-correct project and test project inside the current solution, migrate the existing tar seed implementation into the `Icod.Tar` namespace, and record the pinned GNU tar release as the authoritative baseline.

Implement the selected GNU, ustar, and POSIX/pax formats; correct archive entry typing; links; sparse files; metadata; streaming create, list, and extract behavior; member selection and exclusions; compression integration; and every in-scope archive operation. Consume the mature traversal, canonical-path, metadata, temporary-workspace, transactional-replacement, child-process, signal, and platform-capability abstractions already developed in the solution.

Extraction is a security boundary. Add adversarial tests for absolute paths, `..`, platform-root tricks, symlink and hard-link escapes, device creation, metadata restoration, case-folding collisions, overwrite races, malformed sparse maps, integer overflow, archive bombs, decompression failures, cancellation, and resource exhaustion. Archive-format and archive-state behavior remains in `Icod.Tar`, not in the general Shared incubation project.

The project remains co-resident until Completion Gate G, when it is moved into its own solution and repository and its cross-suite dependencies are converted to `Icod.CommandFramework` package references.


### Completion Gate G — final classification, package extraction, and repository split

This gate is deliberately last. By this point the Coreutils, Diffutils, Grep, Patch, Ed, Sed, Tar, and ProcPs projects have been developed together in one solution, providing the consumer evidence needed to choose stable API and repository boundaries.

- [ ] Inventory every public, protected, and internal API in the current Shared incubation project and record its actual consumers by project and suite.
- [ ] Inventory every API in `Icod.DiffUtils.Shared`, `Icod.Ed.Shared`, `Icod.ProcPs.Shared`, and any other suite engine to detect duplication or misplaced cross-suite contracts.
- [ ] Classify each API as:
  - [ ] cross-suite and suitable for `Icod.CommandFramework`;
  - [ ] shared only by Coreutils/Fileutils/Textutils and suitable for `Icod.CoreUtils.Shared`;
  - [ ] shared only within another suite and suitable for that suite's Shared library;
  - [ ] command-local and unsuitable for a public package.
- [ ] Review namespace design, accessibility, XML documentation, binary compatibility, trimming/AOT behavior, native ABI boundaries, and dependency direction before freezing public contracts.
- [ ] Create the `Icod.CommandFramework` solution and repository with independent Windows, Ubuntu, and macOS CI.
- [ ] Publish `Icod.CommandFramework` as a versioned NuGet package with symbols, SourceLink, deterministic builds, package documentation, and a Semantic Versioning policy.
- [ ] Move only demonstrated cross-suite functionality into `Icod.CommandFramework`.
- [ ] Retain and extract `Icod.CoreUtils.Shared` only where meaningful Coreutils/Fileutils/Textutils-only reuse remains; make it depend on `Icod.CommandFramework` rather than duplicate framework behavior.
- [ ] Publish `Icod.CoreUtils.Shared` as its own versioned NuGet package when retained.
- [ ] Convert individual Coreutils command projects to `PackageReference` dependencies on the published `Icod.CoreUtils.Shared` binary, or directly on `Icod.CommandFramework` when no Coreutils-specific layer is required.
- [ ] Split the co-resident suite projects into their final solutions and repositories:
  - [ ] `Icod.DiffUtils`;
  - [ ] `Icod.Grep`;
  - [ ] `Icod.Patch`;
  - [ ] `Icod.Ed`;
  - [ ] `Icod.Sed`;
  - [ ] `Icod.Tar`;
  - [ ] `Icod.ProcPs`.
- [ ] Preserve relevant history, project identities, test corpora, documentation, and CI policy during each extraction.
- [ ] Convert every extracted suite to versioned `PackageReference` dependencies on `Icod.CommandFramework`.
- [ ] Retain project references within each extracted suite for its own Shared or engine projects unless a separate package boundary is independently justified.
- [ ] Resolve duplicate executable names and define suite packages, umbrella distributions, aliases, and installation-path policy.
- [ ] Remove stale project, solution-folder, output-path, packaging, CI, and inventory references from the original repository.
- [ ] Eliminate circular dependencies and ensure `Icod.CommandFramework` has no production dependency on any command suite.
- [ ] Build and test every final repository against published NuGet binaries rather than unpublished source-tree references.
- [ ] Publish an architecture and migration document explaining the final package boundaries, repository split, executable ownership, and replacement of transitional Shared dependencies.

Completion of this gate establishes `Icod.CommandFramework` as the neutral cross-platform command foundation and completes the repository split. It does not require `Icod.CoreUtils.Shared` to disappear; that package remains appropriate for behavior genuinely specific to the Coreutils, Fileutils, and Textutils family.

## Why the tools are scheduled this way

- `Icod.CoreUtils` retains GNU Coreutils together with the historical GNU Fileutils and GNU Textutils command families because those packages were merged into GNU Coreutils and now form one natural upstream suite.

- The repository remains a single multi-suite incubation workspace until the end. Creating the suite-correct projects now avoids another namespace migration later, while co-resident development exposes actual API reuse and prevents premature extraction of an unstable `Icod.CommandFramework`.

- Batches 0 through 16 remain the completed historical and infrastructure foundation. They establish command contexts, argument processing, diagnostics, streams, cancellation, numeric operands, temporary objects, regular expressions, platform capabilities, and raw-file behavior used by later Coreutils commands and the co-resident suites.

- Completion Gates C2 and C3 establish text units, display columns, records, ranges, delimiters, and escapes before the remaining Textutils-family commands and before suites such as Grep and Sed depend more deeply on those contracts.

- Completion Gate D and `sort` establish locale-aware comparison, bounded-memory ordering, spill files, stable merging, and cleanup before sorted-stream consumers and before other suites can claim the same infrastructure is genuinely reusable.

- Completion Gate E1 establishes read-only traversal before `Icod.Grep` and `Icod.DiffUtils.Diff` require recursive directory work. This gives both suites the same cycle, symlink, mount-boundary, include/exclude, and error-continuation foundation without placing search- or diff-specific policy in the general Shared project.

- Batch 26 implements `Icod.Grep` directly in its final namespace while retaining the one-solution development model. Grep-specific pattern orchestration, binary policy, context grouping, and output semantics stay in the grep project; only proven cross-suite contracts are candidates for the final framework.

- `split`, `tac`, `csplit`, and `pr` remain between Grep and Diffutils because they complete the pending Coreutils/Textutils streaming and presentation work that depends on the same record, regular-expression, temporary-storage, and display-width foundations.

- Batches 30 through 34 are consecutive because the complete GNU Diffutils family should be developed as one cohesive suite. `Icod.DiffUtils.Shared` is established first; `cmp` validates byte comparison and status contracts; `diff` establishes the two-way engine and textual formats; `diff3` adds three-way comparison and merging; and `sdiff` adds side-by-side and interactive behavior.

- `Icod.DiffUtils` remains independent of `Icod.Patch` and `Icod.Ed` at runtime. Unified, context, normal, and ed-script text are the compatibility contracts, ensuring the implementations can interoperate with GNU and third-party tools rather than only with each other.

- `Icod.Patch`, `Icod.Ed`, and `Icod.Sed` are created in their final namespace families inside the current solution, but their detailed conformance work continues under their own roadmaps. Transactional file replacement, process execution, and other cross-suite dependencies are consumed through project references as those capabilities mature.

- Completion Gates E2 through E6 remain focused on Coreutils/Fileutils path resolution, metadata, modes, mutation, recursive traversal, and transactional replacement. The co-resident sibling suites provide additional consumers, helping distinguish framework contracts from Coreutils-specific policy.

- Completion Gates F1 through F4 establish cross-suite terminal presentation and control, host and processor-resource facts, process identity and targets, argument-safe launch, child and arbitrary-process waiting contracts, signals, process groups, priorities, monotonic timing, status translation, and timeout behavior before the most platform-intensive suite begins. These APIs live physically in the current Shared incubation project but are provisionally classified as `Icod.CommandFramework` candidates.

- Completion Gate P1 follows Coreutils `env`, `nohup`, `kill`, `nice`, and `timeout`, audits those shared processor and process APIs against their first large sibling-suite consumer, prohibits duplicate ProcPs abstractions, and then defines the procps-ng-specific observation and provenance layer. Linux `/proc` remains the authoritative semantic source; Windows, macOS, and BSD providers must identify exact, equivalent, approximated, and unavailable data honestly.

- Batches 55 through 68 are consecutive so `Icod.ProcPs.Shared` and the complete pinned procps-ng family evolve together. The order progresses from narrow system summaries, through sampled statistics and process targeting, to process maps and `ps`, then to user, kernel, terminal, specialized-memory, and finally full-screen process monitoring.

- The architectural boundary is facts and mechanics versus suite interpretation: common processor availability, identity, targeting, launch, wait, signal, priority, clock, status, and terminal contracts remain in the current Shared incubation project, while ProcPs owns `/proc` parsing, process enumeration, detailed snapshots, selection grammar, field catalogs, metric interpretation, personalities, sorting, and screen behavior.

- `uptime` and `free` validate the narrowest load and memory providers; `vmstat` validates interval sampling and counter deltas; `pgrep`, `pkill`, and `pidwait` establish one selection engine; and `pidof`, `pwdx`, `kill`, `skill`, and `snice` exercise identity, path, signal, and priority behavior before `ps` consumes the complete process model.

- `ps` is deliberately not the ProcPs foundation. It follows smaller provider consumers so process races, fields, namespaces, maps, metrics, and selection behavior can be corrected before they are hidden inside its large personality and formatting surface.

- `tload`, `watch`, `hugetop`, and `slabtop` progressively validate the refresh and terminal runtime. `top` remains last because it combines process snapshots, sampled CPU and memory, fields, sorting, filtering, configuration, signals, interactive input, rendering, resizing, suspension, and terminal restoration.

- The historical `ps` and `uptime` work remains recorded in Batch 9. Batches 56 and 62 migrate useful implementation and tests into the correct ProcPs namespace without rewriting history. ProcPs `kill` and `uptime` coexist with any Coreutils-profile commands through suite-specific output directories until final package ownership is decided.

- Obsolete or compatibility procps-ng tools such as `skill`, `snice`, and `pwait` remain in scope because the stated goal is the complete pinned suite. Their deprecation or alias status must be documented rather than used as a reason for silent omission.

- `chroot`, the SELinux commands, and `stdbuf` follow ProcPs because they are specialized privilege, security-context, or preload concerns and provide no foundational provider capability required by the ProcPs family.

- `Icod.Tar` remains the final major suite before Completion Gate G. Archive correctness depends on the mature filesystem foundation and also benefits from the completed process, signal, terminal, provider, and capability work. Tar-specific archive formats and extraction state stay outside the general Shared project.

- Completion Gate G is deliberately last. Only after all suites have supplied real consumers can the project reliably separate `Icod.CommandFramework`, any remaining `Icod.CoreUtils.Shared`, suite-specific Shared libraries, and command-local code.

- The final repository split occurs together with the framework/package extraction. This avoids maintaining multiple repositories against unstable shared APIs during the heaviest refactoring period, while still ensuring that every project already has its final suite namespace and a clean ownership boundary.

- Complex parsers, state machines, security boundaries, platform-specialized commands, and shared-library foundations receive focused batches. Commands are grouped only where they share an actual engine or directly validate the same new infrastructure.

## Per-batch workflow

1. Pin the authoritative upstream package and version.
2. Record synopsis, options, operands, environment variables, locale effects, signals, output grammar, exit statuses, and platform-dependent behavior.
3. Produce a conformance matrix marking each item as required, intentionally deferred, platform-limited, or not applicable.
4. Compare the current implementation and tests against that matrix.
5. Design or extend shared infrastructure before adding command-local duplicates, and record whether each new abstraction is provisionally cross-suite, suite-specific, or command-local.
6. Implement BCL behavior first, then focused native interop where required for semantics.
7. Add synchronous and asynchronous unit tests using injected streams.
8. Add CLI integration tests through `ProcessTestHost`.
9. Add differential tests against the pinned upstream utility where licensing and runner availability permit.
10. Add large-input, bounded-memory, cancellation, broken-pipe, standard-stream, multiple-file, invalid-input, and cleanup tests.
11. Add platform capability and native-ABI tests for `windows-latest`, `ubuntu-latest`, and `macos-latest`.
12. Run Debug and Release builds, then the entire applicable solution test suite on all three required runners.
13. Verify UTF-8 encoding and LF line endings, lowercase assembly names, required project configuration, and absence of generated artifacts.
14. Update this roadmap’s living status and record any deliberately deferred behavior.
15. For a co-resident suite milestone, verify the final namespace, solution folder, output path, test coverage, suite-specific Shared boundary, transitional project references, and public-format compatibility.
16. For a ProcPs milestone, verify that common processor, process, signal, priority, waiting, timing, status, and terminal mechanics are consumed from the current Shared incubation project rather than duplicated in `Icod.ProcPs.Shared`.
17. For Completion Gate G, verify every extracted repository against the published `Icod.CommandFramework` and other applicable NuGet packages before declaring the architecture stable.

## Batch completion checklist

A batch is complete only when:

- every scheduled command has a complete option/operand matrix;
- all required behavior is implemented or explicitly documented as platform-limited;
- no unknown option is silently ignored;
- no unsupported operation throws `NotImplementedException`;
- no production path delegates to the same native utility;
- all command, suite-specific Shared, and applicable framework tests pass;
- large inputs satisfy the stated memory strategy;
- cancellation and broken-pipe behavior are deterministic;
- exit statuses match the upstream contract;
- `windows-latest`, `ubuntu-latest`, and `macos-latest` CI expectations are green;
- the full solution builds in Debug and Release;
- source encoding and line-ending checks pass;
- lowercase assembly names and PascalCase project/namespace conventions are preserved;
- the target framework and project configuration satisfy the current completion gate;
- roadmap status and documentation are updated;
- co-resident suite batches preserve suite-correct namespaces, isolated output paths, tests, and dependency direction; Completion Gate G leaves no stale solution, packaging, CI, or inventory references after extraction;
- ProcPs batches consume the shared processor and process foundation without duplicating its identities, targets, launch, wait, signal, priority, timing, status, or terminal contracts;
- Completion Gate G leaves `Icod.CommandFramework` free of suite dependencies, preserves `Icod.CoreUtils.Shared` only where Coreutils/Fileutils/Textutils-specific reuse remains, and verifies all consumers against published packages.
