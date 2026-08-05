# Icod.CoreUtils Audit and Refactor Roadmap

## Living status

| Item | Status |
|---|---|
| Completed command batches | `0` through `45` |
| Current engineering milestone | LineEditor Phase LE7 complete; proceed to Phase LE8 — implement restricted `Icod.LineEditor.Red` over the shared engine |
| Completed infrastructure milestone | Completion Gates E2 through E6 — canonical paths, authoritative metadata, pathname-indirection characterization, mode expressions, single-path mutation, recursive mutation/copy planning, and transactional replacement |
| Active infrastructure dependency | LE7 now hosts GNU ed 1.22.5 invocation and session policy over the reusable engine; LE8 selects the same immutable restricted profile for `red` and `ed --restricted` |
| Next command batch | Completion Gate F1 and Batch 46 remain next after the contiguous LineEditor incubation sequence |
| Current target framework | `net10.0` |
| Required CI runners | `windows-latest`, `ubuntu-latest`, `macos-latest` |

## Scope

`Icod.CoreUtils` is a cross-platform .NET implementation of GNU Coreutils. Its scope expressly includes the file-manipulation and text-processing command families historically distributed as **GNU Fileutils** and **GNU Textutils**. These are natural Coreutils inclusions rather than unrelated extensions: GNU combined `fileutils`, `sh-utils`, and `textutils` into the unified `coreutils` package in 2003. The modern GNU Coreutils project remains the basic file, shell, and text manipulation suite.

Historical references:

- [GNU Coreutils FAQ — Fileutils, shellutils and textutils](https://www.gnu.org/software/coreutils/faq/coreutils-faq.html#Fileutils-shellutils-and-textutils)
- [GNU Coreutils 5.0 release announcement](https://lists.gnu.org/archive/html/coreutils-announce/2003-04/msg00000.html)

The primary supported CI targets are `windows-latest`, `ubuntu-latest`, and `macos-latest`. BSD support remains a best-effort target. The implementation is therefore not a Unix-only port: platform-independent behavior is preferred, native behavior is implemented per supported ABI where required, and unsupported platform capabilities receive controlled diagnostics.

The repository and solution will also serve as the **temporary development home** for projects that ultimately belong to other upstream suites and for cross-suite neutral foundations proven by those consumers:

- `Icod.DiffUtils.Shared`, `Icod.DiffUtils.Cmp`, `Icod.DiffUtils.Diff`, `Icod.DiffUtils.Diff3`, and `Icod.DiffUtils.SDiff`;
- `Icod.Grep`;
- `Icod.Patch`;
- `Icod.Path`, the cross-suite neutral canonical-path foundation;
- `Icod.LineEditor.Ed.Shared`, `Icod.LineEditor.Ed`, `Icod.LineEditor.Red`, and `Icod.LineEditor.Sed`;
- an optional `Icod.LineEditor.Shared` only if the completed Ed and Sed engines later demonstrate cohesive family-specific reuse that is neither cross-suite framework material nor specific to one engine;
- `Icod.Tar`;
- `Icod.ProcPs.Shared` and the complete procps-ng command family.

These projects will use their **final ownership-correct project names and namespaces from the beginning**, but they will remain in `Icod.CoreUtils.sln` and this repository until the final architectural extraction. Developing the suites together provides real consumers for the current `Shared` APIs, makes cross-suite duplication visible, and supplies the evidence needed to decide which APIs belong in `Icod.CommandFramework`, which remain in `Icod.CoreUtils.Shared`, and which belong in a suite-specific Shared library.

No separate `[` project will be added. The existing `test` project remains the condition evaluator.

## Development architecture

The present repository is deliberately a **multi-suite incubation workspace**. It is not yet the final repository layout.

The current `Shared` project continues to incubate both:

1. functionality that may ultimately become cross-suite `Icod.CommandFramework` APIs; and
2. functionality that may remain specific to GNU Coreutils, Fileutils, and Textutils in `Icod.CoreUtils.Shared`.

The project's present physical name does not establish permanent ownership. The common argument processor is the existing example: it resides in the current `Icod.CoreUtils.Shared` project during incubation, but use by independent suites makes it a likely `Icod.CommandFramework` candidate. The same rule applies to host, processor-resource, process-identity, process-targeting, process-launch, waiting, signal, priority, clock, and terminal mechanics that are consumed by both Coreutils and ProcPs or by other suites.

The suite projects developed here may add their own Shared libraries when the reuse is genuinely suite-specific:

- `Icod.DiffUtils.Shared`;
- `Icod.LineEditor.Ed.Shared`;
- `Icod.ProcPs.Shared`;
- an optional `Icod.LineEditor.Shared` only after an evidence-based Ed/Sed consumer audit;
- other suite-local engine projects only when a concrete reuse or testability need is demonstrated.

During the incubation phase, projects use `ProjectReference` relationships inside the solution. The intended dependency direction is:

```text
Current Shared incubation project
        ↓
Suite-specific Shared or engine project, when justified
        ↓
Individual command projects
```

`Icod.Path` is a parallel neutral foundation rather than a suite-specific Shared library. It has no dependency on an individual command or on the current CoreUtils Shared incubation project; commands and suite engines reference it directly when canonical-path behavior is required.

The suite-specific and neutral projects must use their final namespace families now:

```text
Icod.CoreUtils.*
Icod.DiffUtils.*
Icod.Grep
Icod.Patch
Icod.Path
Icod.LineEditor.Ed
Icod.LineEditor.Red
Icod.LineEditor.Sed
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
2. **suite-specific Shared libraries** — functionality shared only inside one upstream family, such as `Icod.CoreUtils.Shared`, `Icod.DiffUtils.Shared`, `Icod.LineEditor.Ed.Shared`, or `Icod.ProcPs.Shared`; a general `Icod.LineEditor.Shared` exists only if completed Ed and Sed implementations prove a cohesive residual family layer;
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

`Icod.CoreUtils` must not acquire permanent production dependencies on sibling command suites. Interoperability should normally occur through documented command-line behavior and textual formats. Unified diffs flow from `Icod.DiffUtils` to `Icod.Patch`, and ed scripts flow from `Icod.DiffUtils` to `Icod.LineEditor.Ed`, without requiring runtime references between their suite-specific Shared libraries.

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
- `Icod.LineEditor.Ed.Shared` owns Ed/Red address parsing, command parsing, mutable line buffers, marks, substitutions, global commands, undo, file operations, shell integration, and restricted-mode enforcement. `Icod.LineEditor.Ed` and `Icod.LineEditor.Red` are thin executable profiles over that one engine.
- `Icod.LineEditor.Sed` owns Sed-specific script parsing, address and range state, pattern and hold spaces, branching, command-cycle behavior, substitutions, sandbox policy, and in-place-editing semantics.
- A general `Icod.LineEditor.Shared` project is created only if completed Ed and Sed implementations demonstrate cohesive editor-family reuse that is neither cross-suite `Icod.CommandFramework` material nor specific to one engine. It must not be created merely to wrap the regular-expression, record, diagnostic, process, temporary, filesystem, or text APIs already incubating in the current Shared project.
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
- do not create `Icod.LineEditor.Shared` as a prerequisite or use it to wrap APIs already supplied by the current Shared incubation project; create it only after the completed Sed and Ed engines leave a cohesive, evidence-based family-specific remainder;
- consume existing cross-suite abstractions rather than recreating parallel regular-expression, record, diagnostic, process, temporary, filesystem, processor, signal, priority, waiting, timing, or terminal contracts inside a suite-specific Shared project;
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
| `sed` | `Icod.LineEditor.Sed` | GNU sed 4.10 |
| `grep` | `Icod.Grep` | GNU grep 3.12 |
| `cmp`, `diff`, `diff3`, `sdiff` | `Icod.DiffUtils` | GNU Diffutils 3.12 |
| `patch` | `Icod.Patch` | GNU patch 2.8 |
| `ed`, `red` | `Icod.LineEditor.Ed` | GNU ed 1.22.5 |
| procps-ng command family | `Icod.ProcPs` | procps-ng 4.0.6, with an explicitly documented portability profile |
| `tar` | `Icod.Tar` | GNU tar |

Man7 pages are useful synopses and secondary references, but they must not replace the authoritative upstream manual.

## Current repository audit

### What is working well

- The completed batches established shared command-line, diagnostics, streaming, numeric, platform, identity, process, date/time, and block-I/O abstractions.
- Batches 1 through 29 have dedicated command-specific tests, together with focused Shared tests for the infrastructure introduced by their completion gates.
- The complete applicable solution remains subject to the required `windows-latest`, `ubuntu-latest`, and `macos-latest` build-and-test contract; completed batches must remain green on all three runners.
- Source projects consistently reference `Shared` where common behavior is appropriate; that project currently incubates both future `Icod.CommandFramework` APIs and Coreutils-specific `Icod.CoreUtils.Shared` APIs while co-resident sibling suites supply additional real consumers.
- `Icod.LineEditor.Sed` has already completed its project and namespace migration: it uses assembly name `sed`, root namespace and public command class `Icod.LineEditor.Sed.Command`, an asynchronous entry point, a dedicated test identity, and a project reference to the current Shared incubation project.
- Recent projects use asynchronous entry points, injected streams, cancellation, and provider abstractions more consistently than the original implementations.
- Cross-platform test failures have exposed and corrected line-ending assumptions, test-vector defects, and native ABI assumptions that Windows- or Linux-only validation would have missed.

### Defects and risks that remain

1. **Several implementations still silently accept unsupported behavior.**
   Unknown or unsupported options are ignored by some commands. Unsupported platform behavior sometimes returns success. Both patterns are incompatible with a conformance-oriented port.

2. **Several commands still throw unhandled `NotImplementedException`.**
   The existing `runcon` and `chroot` implementations are examples. Unsupported operations must produce a controlled diagnostic and documented nonzero status.

3. **Some commands delegate their defining operation to an installed native utility.**
   Examples include portions of `link`, `chcon`, `install`, and `stdbuf`. Production implementations must not obtain apparent compatibility by invoking the same host utility. Native utilities may be used only by optional differential tests.

4. **Some implementations are not yet the command they claim to be.**
   - The existing `diff` implementation is not a complete difference algorithm and does not yet implement the required result-status model; it will be migrated into `Icod.DiffUtils.Diff` and corrected during the consecutive Diffutils batches.
   - The historical `patch` seed handled a private simplified format rather than GNU patch syntax. Patch Waves A, B1, B2, and C, Phases P0–P8 with P9 started, have retired that format, normalized the co-resident `Icod.Patch` project, added byte-preserving detection and complete parsers, supplied a pure indexed application/matching engine with offsets, fuzz, reversal, prerequisites, and merge output, added secure canonical multi-file planning over `Icod.Path`, implemented explicit backup/reject/output/dry-run/prompt/metadata policy, and placed committed mutation behind a provisional injected transaction boundary. Final E6 atomicity and P9/P10 closure remain scheduled for later waves.
   - `link` behaves like a partial `ln` front end rather than the simple two-operand hard-link command.
   - `chmod` does not yet implement GNU/POSIX numeric and symbolic mode semantics correctly.
   - `tar` needs correct entry typing, metadata handling, and extraction-path safety.
   - `stdbuf` cannot silently run a child without applying the requested buffering mode.

5. **Several text commands use the wrong data model.**
   Common problems include ordinal comparison instead of locale collation, UTF-16 `char` processing where bytes or locale characters are required, line-based processing for commands that must transform delimiters, and whole-input buffering where bounded memory or temporary spill files are required.

6. **Several recursive filesystem commands lack a common traversal policy.**
   Symlink traversal, hard-link identity, cycles, mount boundaries, sparse files, metadata preservation, and destination-inside-source detection must be centralized before `rm`, `cp`, `mv`, `du`, `ls`, and `tar` are considered conformant.

7. **Injected standard streams are not consistently respected or owned correctly.**
   A command must use the supplied `stdin`, `stdout`, and `stderr`, and must never dispose a caller-owned standard stream.

8. **Sed's remaining risks are record fidelity, capability enforcement, and replacement safety.**
   LE1 exposed the implementation as responsibility-focused partial modules, LE3 replaced the private .NET regex translator with Sed-specific policy over the Shared managed GNU BRE/ERE provider, and LE4 replaced decoded-line I/O with byte-preserving Shared LF/NUL records and explicit termination. The command still combines script fragments with a host-generated line ending and performs shell, external-file, sandbox, and in-place replacement work through command-local capabilities. LE5 and the later LineEditor phases must harden those effects and migrate replacement mechanics to the applicable shared contracts without moving Sed's pattern-space and command-cycle state into a general library.

## Project conventions

These conventions apply to every existing project that is altered and every project that is added:

1. Project filenames and namespaces use conventional PascalCase and the correct suite family, such as `Icod.CoreUtils.BaseName.csproj`, `Icod.DiffUtils.Diff.csproj`, `Icod.LineEditor.Ed.csproj`, or `Icod.ProcPs.Ps.csproj`.
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
15. `Icod.CoreUtils` command projects do not take production dependencies on `Icod.DiffUtils`, `Icod.Grep`, `Icod.Patch`, `Icod.LineEditor.Ed`, `Icod.LineEditor.Red`, `Icod.ProcPs`, `Icod.LineEditor.Sed`, or `Icod.Tar`.
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

This historical completion remains authoritative. The command has since moved to the suite-correct `Icod.LineEditor.Sed` project and public `Icod.LineEditor.Sed.Command` identity. The later LineEditor incubation sequence decomposes and re-audits the implementation against GNU sed 4.10 without rewriting Batch 2 history.

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

This gate prevents `expr` from introducing an isolated regular-expression implementation. The same foundation remains the CoreUtils basis for `csplit` and is a candidate for eventual extraction into `Icod.CommandFramework` for reuse by `Icod.Grep`, `Icod.LineEditor.Sed`, and `Icod.LineEditor.Ed`.

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

- [x] `fmt`
- [x] `nl`

Implement paragraph recognition, sentence spacing, crown/tagged modes, logical-page delimiters, numbering styles, header/body separation, and standard-stream ownership. Share line-layout and display-width components without forcing one combined execution engine.

### Completion Gate C3 — before Batch 19

* [x] Add the shared record, range, and escape model:

  * [x] configurable line-delimited and NUL-delimited record readers and writers;
  * [x] byte, character, field, and general range-list parsing;
  * [x] complement and open-ended range handling;
  * [x] delimiter and separator abstractions;
  * [x] documented escape-sequence parsing;
  * [x] deterministic behavior for malformed ranges and escapes.

This gate directly supports `cut` and `paste`, then remains available to `tr`, `sort`, `split`, and related Coreutils and Textutils commands. The genuinely cross-suite record and delimiter contracts are candidates for eventual extraction into `Icod.CommandFramework` for use by `Icod.Grep` and `Icod.LineEditor.Sed`.

### Batch 19 — Field and record extraction (2 tools)

- [x] `cut`
- [x] `paste`

Implement complete byte/character/field list grammar, complement and output delimiters, NUL records, delimiter suppression, serial paste, delimiter escape cycles, multiple input streams, and correct behavior on multibyte input.

### Completion Gate D — before Batch 20

* [x] Extend the secure temporary-object infrastructure established by `mktemp` with the shared external-ordering model:

  * [x] locale-aware collation;
  * [x] reusable sort-key parsing and comparison;
  * [x] stable comparison and original-order tracking;
  * [x] bounded-memory sorted runs;
  * [x] stable external merge;
  * [x] temporary-workspace lifecycle management;
  * [x] deterministic cleanup on success, failure, and cancellation.

This gate is intentionally not split because its components jointly form the execution foundation required by `sort`. The implementation resides entirely in `Icod.CoreUtils.Shared.Ordering` and `Icod.CoreUtils.Shared.Temporary`; it introduces no dependencies between individual command projects.

### Batch 20 — External ordering (1 tool)

- [x] `sort`

For `sort`, implement key specifications, locale collation, stable and unique modes, numeric families, month/version/human-numeric comparison, checking and merging, secure temporary runs, bounded-memory external merge, zero-terminated records, and exact exit statuses.

The completed implementation consumes the locale, key-syntax, external-run, stable-merge, and secure-workspace contracts in `Icod.CoreUtils.Shared`. GNU `sort`-specific key extraction and comparison policy remain command-local, and no individual command project references another tool. The behavior was audited against GNU Coreutils 9.11.

### Batch 21 — External randomization (1 tool)

- [x] `shuf`

The completed implementation reuses Shared command-line, diagnostic, byte-record, segmented-record, byte-output, and temporary-spool infrastructure. GNU `shuf`-specific random-source handling, unbiased bounded selection, partial Fisher-Yates permutation, range sampling, repeat policy, and its private external index remain command-local. No individual command project references another tool. The behavior was audited against GNU Coreutils 9.11.

### Batch 22 — Sorted-stream consumers (3 tools)

- [x] `comm`
- [x] `join`
- [x] `uniq`

The completed implementation adds a reusable byte-record collation adapter to `Icod.CoreUtils.Shared.Ordering`. `comm` performs a constant-memory two-way merge, and `join` buffers only one equal-key group from each input to preserve duplicate-key Cartesian products. `uniq` remains an adjacent-record streaming state machine with exact selected-byte comparison, locale-aware case folding, counting, grouping, skip/check character handling, NUL records, and safe input/output aliasing. Each command references only `Icod.CoreUtils.Shared`; no individual tool depends on another tool. The behavior was audited against GNU Coreutils 9.11.

### Batch 23 — Character transformation (1 tool)

- [x] `tr`

Implement the full `tr` set-expression grammar, including ranges, escapes, repetition, character classes, equivalence classes, complement, delete, squeeze, locale behavior, and delimiter bytes.

### Batch 24 — Graph ordering (1 tool)

- [x] `tsort`

The completed implementation uses a reusable asynchronous byte-token reader in `Icod.CoreUtils.Shared.IO`, preserves GNU Coreutils 9.11 space/tab/line-feed tokenization, and keeps all graph state command-local. It reproduces GNU's bytewise seed ordering, FIFO release order, reverse relation traversal, equal-pair node declarations, duplicate relations, stable loop-member diagnostics, one-edge loop breaking, continued output, and final failure status for cyclic input. The command uses `CommandContext`, cancellation-aware TAP I/O, normalized Debug/Staging/Release project settings, and a dedicated test project based on the GNU 9.11 fixtures. No individual command project references another command project.

### Batch 25 — Permuted indexing (1 tool)

- [x] `ptx`

Reuse the established text, locale, tokenization, ordering, and spill-storage primitives without coupling `ptx` to the execution engines of earlier commands.

The completed implementation pins GNU Coreutils 9.11 and replaces the former simplified tabular indexer with the GNU and traditional invocation forms. It supports break, ignore, and only files; automatic and input references; right-side references; ASCII case folding; width, gap, macro, truncation, sentence-regexp, and word-regexp controls; and dumb, roff, and TeX output. Input processing and output use cancellation-aware TAP APIs and `CommandContext`. Context bytes are stored once in a secure Shared temporary workspace, while lightweight occurrences are stably ordered through `Icod.CoreUtils.Shared.Ordering.ExternalOrderingEngine<T>` with bounded run memory and merge fan-in. All command-specific state remains inside `ptx`, and no command project references another command project. A dedicated test project covers the GNU 9.11 command surface, structured formats, parameter files, references, cancellation, ownership, and build identity.

### Completion Gate E1 — shared read-only pathname traversal before Batch 26

* [x] Add a shared read-only pathname-expansion and traversal foundation to the current Shared incubation project. Keep three responsibilities separable even when they share low-level types:

  * [x] pathname-pattern parsing, matching, and eligible-operand expansion;
  * [x] low-level read-only filesystem observation through an injectable provider;
  * [x] traversal-policy orchestration over the provider.

* [x] Define centralized pathname expansion for eligible pathname operands:

  * [x] support `*` and `?` within a pathname segment, bracket expressions, and explicit quoting or escaping of metacharacters;
  * [x] define `**` as matching zero or more complete pathname segments; `**` never independently changes symbolic-link or reparse-point traversal policy;
  * [x] define platform separator, root, drive, volume, UNC, and device-path behavior without treating Windows and Unix path forms as interchangeable;
  * [x] define leading-period and case-sensitivity policy explicitly rather than inheriting accidental host defaults;
  * [x] provide command-selectable unmatched-pattern behavior, including preservation as a literal operand, no-match results, and deterministic error reporting;
  * [x] preserve original operand order, define match-order policy, and never silently deduplicate repeated explicit operands or independently reached roots.

* [x] Keep operand expansion distinct from traversal filtering:

  * [x] provide separate decisions for whether an entry is yielded and whether a directory is descended into;
  * [x] support basename, root-relative path, whole-path, and matching-name-suffix scopes where a consumer requires them;
  * [x] support ordered include and exclude rules, including last-matching-rule behavior where requested by the consumer;
  * [x] prune excluded directories before enumerating their children;
  * [x] keep grep-, diff-, listing-, accounting-, and archive-specific selection semantics outside the general traversal engine.

* [x] Preserve root and entry provenance throughout expansion and traversal:

  * [x] identify the original operand and root ordinal that produced each result;
  * [x] distinguish literal roots, expanded roots, and descendants;
  * [x] report traversal depth;
  * [x] retain a user-facing display path separately from the operational access path;
  * [x] expose the path relative to the traversal root and the entry basename without forcing consumers to reconstruct them.

* [x] Implement one-directory-level observation through an injectable provider and iterative asynchronous traversal above it:

  * [x] do not rely on a recursive BCL enumeration mode as the policy engine;
  * [x] avoid managed call-stack recursion for deep directory trees;
  * [x] expose enough traversal phases to represent roots, directory entry, ordinary entries, directory exit, errors, cycles, and filesystem-boundary decisions;
  * [x] include a directory-exit or equivalent post-order phase so later filesystem-accounting consumers can aggregate directories without creating a second traversal engine;
  * [x] define root ordering and configurable child ordering explicitly;
  * [x] support maximum-depth and other bounded-resource policies;
  * [x] tolerate entries that disappear or change type between observation steps through deterministic structured results;
  * [x] use TAP and cancellation-aware asynchronous enumeration where naturally asynchronous; do not wrap traversal in `Task.Run` merely to expose an asynchronous signature.

* [x] Define symbolic-link and Windows reparse-point traversal as an explicit policy with at least these semantic modes:

  * [x] never follow directory links during traversal;
  * [x] follow links supplied as command-line or expanded roots, but not links encountered below those roots;
  * [x] follow all eligible links;
  * [x] expose available link or reparse-point classification rather than silently treating every directory-like target as an ordinary directory;
  * [x] keep link-chain canonicalization and complete target resolution in Completion Gate E2.

* [x] Add the minimum identity model required for safe read-only traversal:

  * [x] file or directory identity sufficient to recognize a followed directory already present in the active ancestry;
  * [x] filesystem, device, or volume identity sufficient to enforce a root-relative mount-boundary policy;
  * [x] explicit capability results when a stable equivalent identity is unavailable on a platform;
  * [x] active-ancestry cycle detection rather than global identity deduplication, so repeated operands and independently reached paths remain observable;
  * [x] structured cycle and boundary results that prevent unsafe descent without terminating unrelated roots.

* [x] Define structured error and continuation behavior:

  * [x] associate each error with its root, path, operation stage, and continuation scope;
  * [x] distinguish failures that skip one entry, one subtree, one root, or the complete traversal;
  * [x] return structured errors to the consumer rather than printing command-specific diagnostics inside Shared;
  * [x] permit consumers to implement quiet, warning, continue, and fail-fast policies without replacing the traversal engine;
  * [x] observe cancellation before root inspection, between directory entries, before link following, before descent, and before yielding results;
  * [x] never dispose caller-owned providers, streams, or other injected resources unless ownership was transferred explicitly.

* [x] Validate the provider and traversal contracts independently:

  * [x] use deterministic synthetic providers for expansion, ordering, pruning, identity, cycle, boundary, disappearance, error, and cancellation cases;
  * [x] add real-filesystem integration tests for ordinary directories, inaccessible entries, broken links, link-to-file and link-to-directory behavior, Unix symbolic-link cycles, Windows junctions and reparse points, repeated hard links, and filesystem-boundary behavior where the platform permits it;
  * [x] run the complete applicable test suite on `windows-latest`, `ubuntu-latest`, and `macos-latest` before marking the gate complete.

The Gate E1 implementation is prepared on the `Gate_e1` branch under `Icod.CoreUtils.Shared.FileSystem.Traversal`. It includes the shared pathname-pattern and expansion layer, injectable one-level provider, Windows/Linux/macOS identity providers, iterative event traversal, selectors, policies, XML documentation, README files, deterministic synthetic tests, and conditional host-filesystem integration tests. The gate remains open until the complete applicable Debug, Staging, and Release test suites pass on `windows-latest`, `ubuntu-latest`, and `macos-latest`.

This gate remains read-only. Completion Gate E2 owns canonical path construction, complete symbolic-link-chain resolution, missing-component policies, and resolution-loop semantics. Completion Gate E3 enriches E1's minimal entry and filesystem identities into the authoritative metadata model. Completion Gate E3R characterizes pathname indirection and Windows reparse points consistently across E1, E2, and E3. Completion Gate E5 extends E1's provenance, event, identity, cycle, boundary, and error contracts for race-aware recursive mutation and copying rather than introducing a second incompatible traversal model.

E1 is completed before Batch 26 so co-resident `Icod.Grep` consumes stable root-only and full-link traversal modes, directory pruning, matching scopes, provenance, cycle handling, mount boundaries, and error continuation. The same contract later supports Coreutils directory listing and filesystem accounting, recursive directory comparison in `Icod.DiffUtils`, and archive traversal in `Icod.Tar`. Search, comparison, listing, accounting, and archive policy remain in their respective command or suite engines. The cross-suite portions are provisional `Icod.CommandFramework` candidates.

### Completion Gate R1 — shared BRE/ERE foundation before Batch 26

- [x] Complete the current Shared regular-expression foundation for its first full cross-suite search consumer:
  - [x] add an explicit GNU/POSIX Basic-versus-Extended syntax profile while preserving Basic as the source-compatible default;
  - [x] implement ERE directly in the managed parser and leftmost-longest matcher rather than translating patterns to `System.Text.RegularExpressions`;
  - [x] cover alternation, grouping, repetition, intervals, brackets, captures, locale classification, diagnostics, cancellation, resource limits, and leftmost-longest behavior;
  - [x] define how byte-preserving input, decoded text, match and capture offsets, invalid input, locale profiles, and replacement output relate;
  - [x] retain injectable provider contracts and deterministic tests;
  - [x] update Shared documentation and provisional ownership classification.

This gate advances LineEditor Phase LE2 before the rest of the LineEditor sequence because `Icod.Grep` is the first remaining command that requires both BRE and ERE. The capability does not depend on Sed's internal decomposition, and implementing it here prevents Grep from creating a second regular-expression engine. Later Sed and Ed phases consume and further validate the same cross-suite contract.

### Batch 26 — `Icod.Grep` search engine (1 tool)

- [x] `grep`

Implement the documented GNU grep 3.12 option and pattern model. Cover multiple pattern sources, basic/extended/fixed/Perl-mode policy, recursive traversal, include/exclude rules, binary policy, context, filename and line metadata, counts, quiet/list modes, NUL behavior, and the required 0/1/2 status distinction.

`Icod.Grep` consumes the Gate R1 BRE/ERE provider and the current record, diagnostic, and read-only traversal abstractions through project references during incubation. Grep-specific matcher orchestration, binary-input policy, context grouping, and output formatting remain in the grep project or a repository-local engine if later testing justifies one. Completion Gate G will move genuine cross-suite dependencies to `Icod.CommandFramework` and extract `Icod.Grep` into its own solution and repository.

Batch 26 replaced the legacy synchronous `System.Text.RegularExpressions` prototype with an asynchronous, byte-preserving command implementation. The completed command uses Shared option parsing and diagnostics, Gate R1 BRE/ERE compilation and byte offsets, Gate E1 recursive traversal and pruning, fixed-string matching, multiple expression and pattern-file sources, NUL-delimited records, binary policies, context groups, filename/line/byte metadata, counts, quiet and file-list modes, forced color output, cancellation, and GNU grep's distinct 0/1/2 statuses. Perl-compatible mode receives an explicit controlled status-two diagnostic because no managed PCRE provider is present. A dedicated `Icod.Grep.Tests` project adds 35 focused command tests covering the implemented policy and cross-platform control paths.

### Batch 27 — Splitting and reversing (2 tools)

- [x] `split`
- [x] `tac`

Repair split-output rotation and support nonseekable input, line/byte/chunk modes, suffix alphabets, filters, additional suffixes, numeric suffixes, and exact file-creation cleanup.
Implement `tac` with backward file scanning or secure temporary spooling rather than whole-input memory loading.

Batch 27 replaced both legacy synchronous prototypes with asynchronous, byte-preserving implementations. `split` now supports streaming line, byte, line-byte, balanced-byte, byte-balanced whole-record, selected-chunk, and round-robin modes; alphabetic, decimal, and hexadecimal suffixes with fixed or GNU-style automatic width growth; additional suffixes; custom byte record separators; filters with `$FILE`, output forwarding, and exit-status propagation; verbose and unbuffered policy; nonseekable input spooling where random access is required; input-overwrite prevention through Gate E1 identity; cancellation; and GNU preserve-on-failure file-creation semantics. `tac` now preserves arbitrary bytes, processes each operand independently, supports literal and Gate R1 GNU Emacs regular-expression separators plus before-separator mode, indexes record extents in secure temporary storage, scans seekable inputs without whole-file loading, and securely spools nonseekable input. Dedicated `Icod.CoreUtils.Split.Tests` and `Icod.CoreUtils.Tac.Tests` projects cover rotation, chunk allocation, suffix growth and exhaustion, filters, input protection, binary data, separator policy, nonseekable input, diagnostics, continuation, and cancellation.

### Batch 28 — Pattern-directed splitting (1 tool)

- [x] `csplit`

Reuse the regular-expression policy established by Completion Gate R1 for `csplit`, including numeric and regex addresses, offsets, repetition, suppression, prefix/suffix grammar, keep-files behavior, exact byte counts, and cleanup after failure or cancellation. Do not introduce a runtime dependency on `Icod.Grep`.

Batch 28 replaced the legacy decoded-text and `System.Text.RegularExpressions` prototype with an asynchronous, byte-preserving implementation audited against GNU Coreutils 9.11. The completed command supports absolute numeric line addresses, Gate R1 GNU BRE addresses, signed line offsets, finite and unlimited repetition, percent-delimited suppression, `--suppress-matched`, configurable prefixes, numeric suffix widths and validated suffix formats, exact byte-count reporting, empty-file elision, keep-files behavior, nonseekable standard input, cancellation, input-overwrite protection, and GNU cleanup semantics after failure. Input bytes and line extents are held in secure temporary spools so the command does not load the entire input into managed memory. A dedicated `Icod.CoreUtils.CSplit.Tests` project covers numeric and regular-expression control flow, offsets, repetitions, suppression, binary and malformed-byte inputs, naming, counts, cleanup, diagnostics, nonseekable input, help, version, and cancellation. No runtime dependency on `Icod.Grep` was introduced.

### Batch 29 — Page presentation (1 tool)

- [x] `pr`

For `pr`, implement columns, page geometry, headers and footers, form feeds, dates, numbering, merge modes, separators, and terminal-independent output.

Batch 29 replaced the legacy two-option synchronous prototype with an asynchronous implementation audited against GNU Coreutils 9.11. The completed command supports balanced down-column and across-column layouts, parallel file merging, selected page ranges, configurable page length and width, deterministic headers and trailers, date formats, form-feed and omitted-pagination policies, line numbering and explicit first numbers, margins, input and output tab policies, control and nonprinting notation, separator and separator-string modes, joined full lines, file-warning suppression, cancellation, and injected text streams. Historical `-COLUMN` and `+FIRST[:LAST]` spellings are handled through the Shared parser's eligibility-aware token-rewrite rules so required negative option values remain intact. Page processing uses bounded per-page buffers and a streaming cursor that recognizes physical form feeds without loading whole inputs. A dedicated `Icod.CoreUtils.Pr.Tests` project covers layouts, page geometry, form-feed boundaries, numbering, separators, widths, controls, diagnostics, help, version, and cancellation.

### Batch 30 — `Icod.DiffUtils.Shared` foundation (1 library)

- [x] `Icod.DiffUtils.Shared`

Create the suite-specific Shared project inside the current solution and add its dedicated test project. Record GNU Diffutils 3.12 as the authoritative baseline. Establish comparison inputs, byte and line normalization, edit scripts, ranges, hunks, output-format models, temporary-workspace use, directory-comparison coordination, three-way merge models, and side-by-side layout primitives only where they are genuinely shared by two or more Diffutils commands.
This library is intended to store that code which is shared between `cmp`, `diff`, `diff3`, and `sdiff` which would not go in `Icod.CoreUtils.Shared` because no other programs or tools use it; i.e., code specific to GNU Diffutils only.

`Icod.DiffUtils.Shared` uses a project reference to the current Shared incubation project. It must not absorb general command, filesystem, text, locale, process, or platform behavior merely because those APIs have not yet been extracted into `Icod.CommandFramework`.

### Batch 31 — `Icod.DiffUtils.Cmp` byte comparison (1 tool)

- [x] `cmp`

Implement byte-oriented comparison, silent mode, all-differences reporting, byte and line numbering, skip and limit operands, EOF diagnostics, binary-safe standard input, cancellation, and exact statuses for equality, difference, and error.

This batch validates the byte-comparison and result-status contracts without requiring the line-difference engine.

Batch 31 replaced the project-template placeholder with a bounded asynchronous byte-comparison implementation audited against GNU Diffutils 3.12. The completed command supports default first-difference reports, visible byte notation, all-differences output, quiet mode, independent and additive skips, bounded comparisons, C-style decimal/octal/hexadecimal quantities with GNU multipliers, binary standard input, EOF diagnostics, cancellation, and statuses 0 for equality, 1 for differences, and 2 for trouble. General radix-aware quantity parsing was added to `Icod.CoreUtils.Shared`; suite-specific comparison inputs and the common result-status contract reside in `Icod.DiffUtils.Shared`. Dedicated `Icod.DiffUtils.Cmp.Tests` and `Icod.DiffUtils.Shared.Tests` projects cover command behavior and the shared contracts. No tool-to-tool project dependency was introduced, and the line-difference engine remains deferred to Batch 32.

### Batch 32 — `Icod.DiffUtils.Diff` difference engine (1 tool)

- [x] `diff`

Implement normal, context, unified, ed, and other in-scope formats; whitespace and case policies; labels; function context; binary handling; incomplete-line behavior; recursive directory comparison; absent-file policy; and statuses 0 for no differences, 1 for differences, and greater than 1 for errors.

Textual fixtures must be independently consumable by our implementations of GNU `patch` and GNU `ed`.

Batch 32 replaced the legacy front end with an asynchronous line-oriented implementation audited against GNU Diffutils 3.12. The command now supports normal, context, unified, reverse and forward `ed`, RCS, side-by-side, brief, and conditional-merge output; labels and function context; whitespace, case, blank-line, matching-line, tab, and carriage-return policies; binary classification and forced-text comparison; incomplete final lines; standard input; recursive directory comparison; exclusions, start points, filename-case policy, symlink policy, absent-file modes, fixed `--from-file`/`--to-file` comparisons, cancellation, and statuses 0, 1, and 2. Reusable UTF-8 comparison documents, line normalization, Myers edit scripts, changed blocks, context hunks, and logical side-by-side rows reside in `Icod.DiffUtils.Shared`; command parsing, directory coordination, and all output syntax remain in `Icod.DiffUtils.Diff`. Dedicated command and shared-engine tests define patch/ed-consumable fixtures and the contracts required by Batches 33 and 34. No tool-to-tool project dependency was introduced.

### Batch 33 — `Icod.DiffUtils.Diff3` three-way comparison (1 tool)

- [x] `diff3`

Implement three-file comparison, common-ancestor modes, overlap classification, merge output, conflict markers, `ed` scripts, labels, input validation, and exact statuses. Reuse the proven two-way data model without forcing `diff3` semantics into the `diff` front end.

Batch 33 replaced the scaffold with an asynchronous three-file implementation audited against GNU Diffutils 3.12 and differentially exercised against the available GNU `diff3`. The command supports the historical normal report, second- or third-input common-file mapping, ancestor-relative region classification, direct merge output, two- and three-way conflict markers, `-e`, `-3`, `-x`, `-E`, `-X`, and `-A` edit policies, reverse-order `ed` scripts, labels, leading-period protection, System V `w`/`q`, incomplete final lines, binary/text policy, trailing-carriage-return normalization, one standard-input operand, cancellation, controlled input diagnostics, and statuses 0, 1, and 2. GNU-style boundary shifting, connected three-way regions, common-input selection, and overlap classification reside in `Icod.DiffUtils.Shared`; parsing, merge policy, reports, markers, and `ed` serialization remain in `Icod.DiffUtils.Diff3`. The command neither references nor invokes the `diff` project for its defining operation. Dedicated command and shared-engine tests cover the contracts required by Batch 34 without forcing `diff3` semantics into the two-way front end.

### Batch 34 — `Icod.DiffUtils.SDiff` side-by-side comparison (1 tool)

- [x] `sdiff`

Implement side-by-side layout, width and display-column handling, common-line suppression, left-column behavior, tab expansion, interactive merge commands, editor invocation without unsafe shell interpolation, transactional output, nonterminal behavior, and exact status propagation.

Completion of Batches 30 through 34 leaves the complete GNU Diffutils family implemented and tested inside the current solution. Repository extraction remains deferred until Completion Gate G.

### In-solution Patch and E-series partial-order workstream

This workstream does not alter command-batch numbering. The detailed Patch architecture, conformance matrix, security model, test requirements, and phase checklists remain in [`Icod.Patch-Development-Roadmap.md`](Icod.Patch-Development-Roadmap.md); this roadmap records the repository-wide ordering constraints and the points at which Patch consumes the E-series infrastructure.

Patch is no longer scheduled as one isolated milestone completed before the E series. Its parser and pure application engine can proceed independently, while its path, metadata, mode, symlink, backup, reject, and replacement behavior must consume the corresponding shared gates as soon as they exist. The schedule is therefore a partial order:

```text
Patch P0 → P1 → P2 → P3/P4 → P5 → P6
                    │                    │
                    │                    └──────────────┐
                    │                                   │
Completion Gate E2 ─┴──────────────→ Patch P7          │
Completion Gates E3, E3R, and E4 ─────→ Patch P8          │
Completion Gate E4 ─────────────────────────────────→ Patch P9
Patch P7 + P8 + P9 + E2/E3/E3R/E4 ─→ Patch P10
Completion Gate E6 + Patch P10 ─────→ Patch P11A
Patch P11A + Batches 44 and 45 ─────→ Patch P11B → Patch P12
```

The hard ordering rules are:

- Patch Phase P2 and Completion Gate E2 must both complete before Phase P7.
- Phase P7 and Completion Gates E3, E3R, and E4 must complete before Phase P8 is closed.
- Phase P6 and Completion Gate E4 must complete before the E6-facing filesystem work in Phase P9 can be closed.
- Phases P7 through P9 and Completion Gates E2, E3, E3R, and E4 must complete before the Phase P10 conformance checkpoint.
- Completion Gate E6 and Phase P10 must complete before initial transaction integration in Phase P11A.
- Phase P11A and the independent `cp`, `mv`, and `install` validation in Batches 44 and 45 must complete before Phase P11B and final Patch conformance in Phase P12.
- Completion Gate E5 is not a direct Patch feature prerequisite. Patch does not need recursive-copy traversal, but E6 may consume the metadata, containment, identity, and cleanup portions of E5 that are needed by the shared transaction contract.

The production boundary with Diffutils remains textual. `Icod.Patch` consumes normal, context, unified, and ed-script patch text and must not reference `Icod.DiffUtils.Shared`.

#### Patch Wave A — Phases P0 through P4

This wave follows Batch 34 and may proceed while Completion Gates E2 and E3 are being designed. It contains no live target-file mutation dependency.

- [x] **P0:** normalized the `Icod.Patch` project and solution family, added C# 13 and dedicated tests/fixtures/docs, pinned GNU patch 2.8 and its complete source-defined option inventory, and retired the private seed syntax; full-checkout build/CI execution remains pending.
- [x] **P1:** implemented shared command context, asynchronous invocation, compatibility wrappers, declarative parsing over the complete GNU 2.8 option-name inventory, explicit later-phase option rejection, standard-input/`-i`/operand source selection, diagnostics, prompt ownership, and exit-status accumulation.
- [x] **P2:** implemented a spill-backed byte-preserving source map, LF/CRLF/CR/incomplete-record preservation, multiple-section and surrounding-text recognition, unified/context/normal/ed candidate detection, directive hardening, fuzz coverage, and resource limits.
- [x] **P3:** implemented complete unified and context file headers, ranges, hunks, change markers, immutable common models, exact source retention for rejects, and `/dev/null` creation/deletion forms.
- [x] **P4:** implemented normal append/change/delete commands and the minimal GNU-compatible ed-script grammar internally, including GNU Diffutils single-dot protection, without invoking native `ed` or referencing the Ed implementation.
- [x] Preserved independent fixtures from GNU Diffutils, `Icod.DiffUtils`, third-party producers, and hand-authored patches.
- [x] Kept all parser and model work independent from target filesystem mutation.

Wave A implementation is complete, with full-checkout Debug/Release and three-runner validation pending after integration. The command intentionally performs no live target mutation. Wave B1 supplies pure virtual application and matching, and the completed Wave B2 P7 layer now supplies canonical filename selection and multi-file planning without committed mutation.

Completion of all of Wave A establishes the patch syntax and source model required by every later Patch phase. It does not claim path, metadata, mode, backup, reject, or transaction conformance.

#### Patch Wave B1 — Phases P5 and P6

P5 and P6 depend on the parsed hunk models from P3 and P4, but not on canonical paths or live filesystem mutation. They should proceed while E2 is completed rather than waiting behind it.

- [x] **P5:** implemented the exact, byte-preserving application engine with indexed in-memory and owner-private spill-backed target content, exact hunk verification, multi-hunk state, ed operation interpretation, virtual file creation/deletion, immutable independently owned results, cancellation, resource limits, and property/invariant coverage.
- [x] **P6:** implemented forward-first nearby offset search, configurable fuzz, canonical horizontal-blank matching, reverse and already-applied detection, force/forward/batch/interactive policy, prerequisite checks, GNU-compatible merge/diff3 conflict output, adversarial candidate limits, and opt-in GNU patch 2.8 differential coverage.
- [x] Kept both phases pure: they operate on parsed patch documents and immutable virtual target content, not canonical paths or committed filesystem mutations.

Wave B1 implementation is complete. The managed project and focused test suite still require full-checkout Debug/Release execution and the repository Windows, Ubuntu, and macOS CI matrix after integration. Completion Gate E2 and Batch 35 have now been consumed by the completed P7 planner.

### Completion Gate E2 — before Batch 35

* [x] Complete the shared canonical-path model:

  * [x] lexical path normalization;
  * [x] physical path resolution;
  * [x] symbolic-link and reparse-point inspection, later enriched by E3R characterization;
  * [x] missing-component policies;
  * [x] loop detection;
  * [x] relative-path calculation;
  * [x] platform root, volume, and separator semantics;
  * [x] deterministic failure without returning unresolved input as success.

Completion Gate E2 is implemented in the neutral `Icod.Path` library with a dedicated `Icod.Path.Tests` project. The model separates POSIX and Windows pathname grammar from injectable no-follow filesystem observation; resolves components in traversal order so a symbolic link is handled before a following `..`; supports strict, missing-final, and missing-suffix policies; resumes physical observation when a missing suffix returns to an existing prefix; detects repeated resolution states and configurable link-expansion limits; exposes raw final-link and reparse-point inspection; and provides component-aware relative-path and containment operations. Failures carry stable codes and never return unresolved input as a successful canonical path.

Synthetic tests exercise POSIX, drive, UNC, extended-root, volume, link, loop, missing, containment, and cancellation behavior on every runner. Conditional host tests exercise real file and directory links, dangling links, and loops where link creation is available. Full-checkout Debug, Staging, Release, and the Windows/Ubuntu/macOS CI matrix remain to be executed after integration.

This gate supplies the defining infrastructure for `readlink` and `realpath` and is a hard predecessor of Patch Phase P7. Patch Phase P2 and E2 are independent predecessors: P2 supplies the parsed filename candidates and source evidence, while E2 supplies the canonical path, root, volume, link, containment, and failure semantics used to act on them.

### Batch 35 — Symbolic-link and canonical-path resolution (2 tools)

- [x] `readlink`
- [x] `realpath`

Implement lexical versus physical resolution, missing-component policies, canonicalization modes, delimiters, quiet/verbose behavior, relative output, symlink loops, characterized reparse points, and deterministic failures. Never return the unresolved input as a false success.

Batch 35 is implemented over the neutral `Icod.Path` contract and audited against GNU Coreutils 9.11. `readlink` supports raw terminal-link inspection; `-f` all-but-final, `-e` strict-existing, and `-m` missing-suffix canonicalization; GNU option-order precedence; quiet/verbose policy; no-newline and NUL delimiters; multiple operands; continuation; cancellation; loops; and controlled reparse-point failure. Its `-f` mode ignores a trailing separator, while `-e` requires a trailing-separator operand to resolve to a directory. `realpath` supports the default and explicit `-E` missing-final policy, strict `-e`, missing-suffix `-m`, physical `-P`, logical `-L`, and no-link `-s` resolution; `--relative-to` and `--relative-base`; quiet and NUL output; multiple operands; loops; roots and volumes; and deterministic failure without echoing unresolved input. Default `realpath` ignores nonleading trailing separators, strict `-e` requires a directory there, logical mode validates the no-link spelling before processing `..`, and only the combined `-s -m` profile is guaranteed to remain purely lexical. The `Icod.Path` resolver now also exposes explicit no-link traversal and final-directory validation so both commands share the same canonicalization machinery.

Dedicated `Icod.CoreUtils.ReadLink.Tests` and `Icod.CoreUtils.RealPath.Tests` projects exercise the command profiles over deterministic injectable POSIX observations, while the expanded `Icod.Path.Tests` suite covers no-link preservation, dangling-link validation, and final-directory requirements independently. Full-checkout Debug, Staging, Release, and the Windows/Ubuntu/macOS CI matrix remain to be executed after integration.

#### Patch Wave B2 — Phase P7

Phase P7 is implemented over the Completion Gate E2 `Icod.Path` contract and the command-level validation supplied by Batch 35.

- [x] **P7:** consumes E2 for filename candidate resolution, `-d`, component-aware and platform-aware `-p`, roots and volumes, link/reparse-point observation, containment, missing-file decisions, multi-file application planning, version-control retrieval policy, and path-security tests.
- [x] Implements explicit original-file operands, quoted names, `Index:` evidence, GNU best-name and POSIX first-existing selection, `/dev/null` creation/deletion planning, and virtual state carried across multiple file patches.
- [x] Keeps matching and hunk application independent from live filesystem mutation.
- [x] Uses the neutral `Icod.Path` project rather than introducing a Patch-private canonical-path model.
- [x] Does not claim metadata, mode, backup, reject, output, prompt, or transaction conformance.

P7 produces an owned multi-file application plan over immutable virtual results. Target selection is confined to the physically canonical `-d` root: lexical traversal, cross-volume names, and link/reparse resolutions that escape the root fail deterministically. Terminal links require explicit `--follow-symlinks`; output-link mutation remains deferred. `-g`, `PATCH_GET`, and POSIX defaults are represented by an injected retrieval provider and decision policy without shell interpolation. The executable still returns controlled trouble because P8 owns artifacts, prompts, and complete user-visible statuses, while P9/P11 own committed replacement. Full-checkout Debug/Release and Windows, Ubuntu, and macOS CI validation remain pending after integration.

### Completion Gate E3 — before Batch 36

* [x] Add the authoritative shared filesystem-metadata model:

  * [x] file type and size;
  * [x] link count and link identity;
  * [x] mode, ownership, and group information;
  * [x] access, modification, inode-change, and birth timestamps;
  * [x] device, inode, and platform-equivalent identity;
  * [x] allocated-block accounting;
  * [x] filesystem information;
  * [x] timestamp mutation capabilities;
  * [x] explicit reporting of unavailable platform metadata.

Completion Gate E3 is implemented in `Icod.CoreUtils.Shared.FileSystem.Metadata`. `IFileSystemMetadataProvider` exposes injectable entry observation, containing-filesystem observation, and selective timestamp mutation. `FileSystemMetadataValue<T>` distinguishes available, unavailable, unsupported, and not-applicable values instead of using sentinels. The system provider reuses the E1 `FileSystemEntryIdentity` and `FileSystemIdentity` types and enriches them through Windows handle/security/volume APIs, Linux `statx`, macOS `stat`/`lstat`, POSIX `statvfs`, and controlled BCL fallbacks. Detailed special-file kinds, allocated-block accounting, link-object identity, post-2038 timestamp conversion, preflighted mutation capabilities, and cross-platform host tests are included.

This gate enriches and reuses the minimal entry and filesystem identities established by Completion Gate E1 rather than introducing parallel identity types. It supports `stat`, `touch`, and the file predicates subsequently required by `test`. With Completion Gate E3R complete, Patch Phase P8 now depends only on Completion Gate E4 before it can consume the shared timestamp, metadata, identity, availability, and pathname-indirection contracts for targets, backups, rejects, output files, and post-2038 patch timestamps.

### Completion Gate E3R — Windows reparse-point characterization before Batch 36

* [x] Establish one shared, injectable physical pathname-indirection contract:

  * [x] place `IPathIndirectionInspector`, `PathIndirectionInfo`, and the host implementation in the neutral `Icod.Path` project;
  * [x] preserve raw Windows reparse tags, Microsoft ownership and name-surrogate bits, physical attributes, raw and normalized targets, relative-target state, mounted-volume GUID paths, and offline/recall indicators;
  * [x] distinguish POSIX symbolic links, Windows symbolic links, directory junctions, mounted volumes, unknown name surrogates, Cloud Files placeholders, opaque reparse points, and unknown host indirection;
  * [x] decode only the documented Microsoft symbolic-link and mount-point buffers and preserve unknown provider data as opaque rather than guessing;
  * [x] distinguish a junction from a mounted volume through the Windows volume-mount API;
  * [x] avoid opening file content during classification so metadata observation does not itself hydrate remote placeholder data;
  * [x] migrate E1 traversal, E2 canonicalization, and E3 metadata to the same characterization rather than retaining parallel Boolean-only models;
  * [x] retain source-compatible Boolean overloads while adding the typed `PathDereferenceMode` vocabulary for new traversal and metadata consumers;
  * [x] make `FileSystemEntryKind.SymbolicLink` strict, represent junctions and other name surrogates separately, retain recognized non-name-surrogate reparse points' underlying file or directory kind, and quarantine only uncharacterized points;
  * [x] add platform-neutral classification tests plus conditional Windows junction integration tests for canonicalization, traversal, identity, and metadata.

Completion Gate E3R is implemented across `Icod.Path`, `Icod.CoreUtils.Shared.FileSystem.Traversal`, and `Icod.CoreUtils.Shared.FileSystem.Metadata`. On Windows, the inspector opens the physical object with no-follow semantics, obtains `FileAttributeTagInfo`, retrieves documented reparse data with `FSCTL_GET_REPARSE_POINT`, and uses `GetVolumeNameForVolumeMountPointW` to distinguish mount-manager mounted volumes from directory junctions. Cloud and known opaque non-name-surrogate points remain their underlying physical file or directory and are not reinterpreted as symbolic links. Unknown name surrogates are observable but are not followed without a decoder, while reparse points whose tag cannot be characterized are quarantined.

The historical public members named `FollowSymbolicLinks`, `MaximumSymbolicLinks`, and `IsFollowedSymbolicLink` remain for source compatibility, but their documented enabled behavior now covers all eligible pathname indirection. New shared consumers use `PathDereferenceMode.NoFollow` or `PathDereferenceMode.FollowEligiblePathIndirection`. Full-checkout Debug, Staging, Release, and Windows/Ubuntu/macOS CI validation remain to be executed after integration.

This gate is a hard predecessor of Batch 36 and informs Batches 37 through 45, Completion Gates E4 through E6, Patch P8 through P11, Tar, and every command that inspects, traverses, removes, copies, archives, mutates, or replaces a pathname object. In particular, `rm` and `rmdir` must remove a junction object without descending into its target; `cp`, `mv`, `install`, and Tar must preserve, follow, or reject each supported reparse kind explicitly; and replacement engines must revalidate the terminal physical object immediately before commit.

### Batch 36 — File metadata and timestamps (2 tools)

- [x] `stat`
- [x] `touch`

Build the authoritative metadata adapter and format-string engine. Distinguish access, modification, inode-change, and birth times where available; expose controlled platform gaps; support dereference policies, filesystems, reference files, date parsing, selective timestamps, no-create, and directories.

Batch 36 is implemented and audited against GNU Coreutils 9.11. `stat` now consumes the authoritative E3/E3R metadata provider for physical and dereferenced entry reports, hard-link counts and identities, ownership, modes, allocation, special files, access/modification/inode-change/birth timestamps, and containing-filesystem information. It supports GNU file and filesystem format directives, width/precision and numeric flags, `--format`, escape-aware `--printf`, terse output, symbolic-link and characterized reparse-point dereference policy, multiple operands, continuation after per-operand failures, and controlled diagnostics for metadata fields or cache policies the shared provider cannot yet enforce. The former creation-time substitution for inode-change time is removed.

`touch` now uses the E3 selective timestamp-mutation contract instead of `FileInfo` approximations. It supports independent access and modification updates, current and explicit GNU date expressions, reference files and reference-relative dates, POSIX `[[CC]YY]MMDDhhmm[.ss]` timestamps, no-create, directories, the compatibility `-f` option, standard-output operands where the host exposes `/dev/stdout`, and E3R no-follow mutation of symbolic-link objects when the platform advertises that capability. Dedicated `Icod.CoreUtils.Stat.Tests` and `Icod.CoreUtils.Touch.Tests` projects cover formats, filesystems, distinct change/birth fields, links, directories, creation, selective preservation, references, relative dates, no-follow behavior, diagnostics, and command endpoints. Full-checkout Debug, Staging, Release, and Windows/Ubuntu/macOS CI validation remain to be executed after integration.

### Batch 37 — Condition evaluator (1 tool)

- [x] `test`

Implement the complete GNU/POSIX operand-count grammar, file type and characteristic predicates, access checks, string and numeric comparisons, connectives, precedence, ambiguity rules, and statuses 0, 1, and 2. **Do not create a separate `[` project.**

Batch 37 is implemented against GNU Coreutils 9.11's operand-count dispatcher and expression state machine. The existing `test` executable now handles zero- through four-operand ambiguity rules, general expressions, repeated negation, parentheses, non-short-circuit `-a` and `-o` evaluation with GNU precedence, locale-aware string ordering, arbitrary-precision signed integer comparisons, GNU `-l STRING` numeric operands, and syntax status 2 while retaining `--help`, `--version`, and `[` as ordinary operands. No separate `[` project was created.

File predicates consume the E3/E3R metadata and identity contracts for regular files, directories, block and character devices, FIFOs, sockets, symbolic-link objects, size, set-user-ID, set-group-ID, sticky mode, modification-since-read, ownership, hard-link identity, and modification-time ordering. Access and terminal predicates use an injectable host boundary with effective-identity-aware Unix mode evaluation, Windows attribute and executable-extension policy, and native terminal checks. Dedicated `Icod.CoreUtils.Test.Tests` coverage exercises operand-count ambiguities, GNU `-l` shifting, connectives, non-short-circuit observation, file kinds and characteristics, links, hard-link identity, timestamps, ownership, access, terminal descriptors, diagnostics, cancellation, and host filesystem integration. Full-checkout Debug, Staging, Release, and Windows/Ubuntu/macOS CI validation remain to be executed after integration.

### Completion Gate E4 — before Batch 38

* [x] Add shared mode and basic pathname-mutation infrastructure:

  * [x] numeric mode parsing;
  * [x] symbolic mode-clause parsing;
  * [x] umask application;
  * [x] basic directory, file, link, FIFO, and device-node capability providers;
  * [x] no-follow and dereference policies built on the E3R pathname-indirection contract;
  * [x] race-aware single-path mutation;
  * [x] controlled privilege and platform diagnostics.

Completion Gate E4 is implemented in `Icod.CoreUtils.Shared.FileSystem.Modes` and `Icod.CoreUtils.Shared.FileSystem.Mutation`. The immutable mode layer parses GNU absolute numeric, operator-numeric, and symbolic expressions; applies clauses sequentially; supports class copying, conditional `X`, and special bits; filters omitted subjects through an explicit caller-supplied umask; and preserves directory set-ID bits unless a numeric expression explicitly clears them. It does not change process-global umask state.

The injectable mutation layer creates one directory or ordinary file, creates hard and symbolic links, creates real FIFOs and block or character device nodes where supported, removes one physical name or empty directory, and changes one POSIX mode. It consumes E3/E3R kinds, stable identities, reparse-point characterization, and explicit no-follow/dereference policy; uses exclusive creation primitives; revalidates identity-bearing preconditions before destructive or metadata-changing operations; verifies created hard-link identity when the provider exposes stable identities; cleans up partially created objects after controlled failure or cancellation; and returns structured unsupported, privilege, access, existence, kind, identity, cross-device, nonempty-directory, and I/O results. Special files are never emulated with ordinary files. Dedicated Shared tests cover GNU mode semantics, platform capabilities, real creation/removal/link/FIFO operations, explicit mode policy, and stale-identity rejection. Full-checkout Debug, Staging, Release, and Windows/Ubuntu/macOS CI validation remain to be executed after integration.

This gate supports `mkdir`, `rmdir`, `unlink`, `link`, `ln`, `mkfifo`, `mknod`, and the later permission commands. Patch consumes these contracts in Phases P8 through P10 for mode, creation/deletion, no-follow, symlink, and race-aware single-path policy. E4 is therefore a hard predecessor of closing P8 and P9.

#### Patch Wave C — Phase P8 and the start of Phase P9

This wave begins only after Completion Gates E3, E3R, and E4 are complete. It proceeds concurrently with Batches 38 through 43 and with the design of E6.

- [x] **P8:** implement rejects, backup policy, output-file mode, dry runs, quoting, verbosity, prompts, status aggregation, and write-failure behavior over the shared metadata, timestamp, mode, and single-path capability contracts.
- [x] Start **P9** by placing every target mutation behind an injected Patch filesystem/transaction boundary.
- [x] Use secure exclusive temporary creation and guarantee that no original is removed before a complete replacement is ready.
- [x] Add failure injection, cancellation cleanup, E3R-based terminal-object revalidation, and provisional transaction tests.
- [x] Keep command-specific reject, backup, partial-application, and prompt policy above the shared mechanism.
- [x] Treat P9's command-local replacement internals as temporary scaffolding for E6, not as a competing permanent transaction framework.

P8 depends on P7, E3, E3R, and E4. P9 may help refine the E6 contract while the E4/E5 validator batches are being implemented, but it cannot claim final transaction conformance until E6 exists.

Wave C is implemented. P8 now materializes explicit target, backup, reject, output, standard-output, and validation-only artifacts; preserves modes through E3/E4; applies requested patch-header timestamps including post-2038 values; enforces GNU 2.8 input/output `--follow-symlinks`; and handles backups, rejects, dry runs, prompts, quoting, statuses, and broken output. The initial P9 adapter stages complete sibling files through E4, preserves rollback copies, revalidates E3 identities before commit, supports injected failures and cancellation, and cleans up deterministically. It remains provisional until Wave D and Completion Gate E6 close the shared replacement contract. Full-checkout Debug/Release and three-runner CI validation remain pending after integration.

### Batch 38 — Basic directory and name removal (3 tools)

- [x] `mkdir`
- [x] `rmdir`
- [x] `unlink`

Implement modes, parents, verbose/context policy, ignore-fail behavior, parent removal, exact operand rules, and deterministic handling of files versus directories. These commands validate the new filesystem adapter without yet introducing recursive deletion.

Batch 38 is implemented over the E3/E3R metadata and E4 single-path mutation contracts. `mkdir` supports numeric and symbolic modes, process-umask behavior without persistent process-global mutation, GNU owner-write-and-search treatment for intermediate `--parents` directories, verbose creation diagnostics, and controlled security-context diagnostics. `rmdir` removes only physical empty directories, implements nonempty-only failure suppression, parent-chain processing, and verbose diagnostics, and never follows symbolic links, junctions, or other reparse points.

`unlink` enforces the exact one-operand contract and removes one physical non-directory name without traversal. Its explicit no-follow mutation precondition permits symbolic links, Windows junctions, and otherwise uncharacterized reparse points while protecting ordinary directories and mounted-volume reparse points. Dedicated `Icod.CoreUtils.MkDir.Tests`, `Icod.CoreUtils.RmDir.Tests`, and `Icod.CoreUtils.Unlink.Tests` projects cover mode and umask rules, parent creation and removal, nonempty handling, exact operands, links, and Windows junction safety. Full-checkout Debug, Staging, Release, and Windows/Ubuntu/macOS CI validation remain to be executed after integration.

### Batch 39 — Hard and symbolic links (2 tools)

- [x] `link`
- [x] `ln`

Make `link` the documented two-operand hard-link command. Build `ln` as a separate front end over shared link primitives, covering symbolic/physical/logical behavior, targets, directories, relative links, backups, force/interactive modes, and platform capability diagnostics. Do not invoke native `ln`.

Batch 39 is implemented over the E3/E3R metadata and E4 single-path mutation contracts without invoking an external `ln`. `link` now enforces the exact two-operand interface and creates one physical hard link with source-identity revalidation and controlled cross-device, access, unsupported-platform, and existing-destination diagnostics. `ln` implements GNU operand forms, hard and symbolic links, `-L`/`-P` source policy, target-directory and no-target-directory forms, relative symbolic-link text, force/interactive precedence, simple/numbered/existing backup policy, suffix and environment controls, no-dereference destination-directory behavior, same-file protection, and verbose reporting.

Dedicated `Icod.CoreUtils.Link.Tests` and `Icod.CoreUtils.Ln.Tests` projects cover hard-link identity, exact operands, existing destinations, dangling and relative symbolic links, multi-source target directories, logical and physical source behavior, replacement, backups, interactive refusal, and destination-directory symbolic links. Full-checkout Debug, Staging, Release, and Windows/Ubuntu/macOS CI validation remain to be executed after integration.

### Batch 40 — Special file creation (2 tools)

- [x] `mkfifo`
- [x] `mknod`

Add the missing GNU projects. Implement modes, FIFO creation, block/character device operands, major/minor validation, umask behavior, and controlled privilege/platform failure. Never emulate success by creating an ordinary file.

Batch 40 is implemented over the Completion Gate E4 single-path mutation contracts. `mkfifo` supports multiple FIFO operands, GNU mode expressions from the `a=rw` baseline, explicit creation-mask handling, default and explicit security-context options, continuation after per-operand failure, and controlled unsupported-platform diagnostics. `mknod` supports FIFO, block-device, and character-device forms; the `p`, `b`, `c`, and `u` type designators; hexadecimal, octal, and decimal major/minor syntax; checked unsigned range validation; mode and umask behavior; and controlled privilege, representability, and platform failures. Neither command substitutes an ordinary file for an unsupported special file. The command surface and numeric syntax are audited against GNU Coreutils 9.11.

Dedicated `Icod.CoreUtils.MkFifo.Tests` and `Icod.CoreUtils.MkNod.Tests` projects cover option and operand validation, mode and umask forwarding, special-bit rejection, type mapping, device-number bases and overflow, continuation, provider failures, and Windows no-emulation behavior. Full-checkout Debug, Staging, Release, and Windows/Ubuntu/macOS CI validation remain to be executed after integration.

### Completion Gate E5 — before Batch 41

* [x] Extend Completion Gate E1's provenance, event, identity, cycle, boundary, and structured-error contracts for recursive mutation and copying:

  * [x] mutation-safe recursive traversal without replacing the E1 read-only provider and traversal model;
  * [x] preserve-root protection;
  * [x] enforce one-filesystem boundaries through the filesystem identities and root-relative boundary policy established by E1;
  * [x] race-aware no-follow operations;
  * [x] hard-link identity tracking;
  * [x] sparse-file detection and preservation;
  * [x] metadata-preservation policy;
  * [x] destination-inside-source detection;
  * [x] partial-failure and cleanup policy;
  * [x] integration points for the later transactional replacement and backup model.

The Gate E5 implementation is supplied under `Icod.CoreUtils.Shared.FileSystem.RecursiveMutation`. `RecursiveMutationTraversalEngine` wraps the E1 event stream rather than introducing a second walker, preserves root and operand provenance, maps root-relative destinations, delegates selectors, depth and resource limits, one-filesystem, cycle, ordering, cancellation, structured-error scope, and error behavior to E1, and pairs every mutable entry with an E4 identity-bearing precondition. The gate also adds preserve-root and destination-containment preflight, repeated hard-link tracking, sparse-range copy behavior through `IFileSystemOperations`, explicit requested-versus-required E3 metadata policy, and deterministic reverse-order rollback through `RecursiveCleanupJournal`. Focused synthetic tests cover preflight, event mapping, identity reuse, sparse preservation, required-capability failure, and cleanup continuation. Full three-runner CI remains the repository merge gate.

This gate extends rather than duplicates E1. It supports recursive `chmod`, `chown`, `chgrp`, `rm`, `cp`, `mv`, `install`, `du`, and `tar` while preserving one shared traversal vocabulary for read-only and mutation-aware consumers.

Patch is not a recursive-copy consumer and does not wait for all of E5 merely to parse or match patches. E5 is nevertheless an upstream dependency of the complete E6 transaction model where E6 consumes its identity, containment, metadata-preservation, no-follow mutation, partial-failure, and cleanup contracts.

### Batch 41 — Permission modes (1 tool)

- [x] `chmod`

Batch 41 replaces the former Windows read-only approximation with an asynchronous GNU-compatible command over the E3 metadata, E4 mutation, and E5 recursive-traversal contracts. It uses `FileModeParser` and `FileModeExpression` for absolute numeric, operator-numeric, and symbolic clauses; applies the process creation mask when symbolic clauses omit `who`; supports `--reference`, recursive traversal, command-line-versus-descendant link policy, `--dereference`, `--no-dereference`, opt-in `--preserve-root`, quiet, changes-only, and verbose reporting; and carries identity-bearing preconditions into every mode mutation. Native Windows reports POSIX mode observation or mutation as unsupported and never substitutes `FileAttributes.ReadOnly`. A dedicated `ChMod.Tests` project covers octal and symbolic modes, reference modes, recursive preconditions, preserve-root, reporting, quiet failures, and Windows non-emulation. Full Windows, Ubuntu, and macOS CI remains the repository merge gate.

### Batch 42 — Ownership and group mutation (2 tools) — implemented

- [x] `chown`
- [x] `chgrp`

Implemented real Unix UID/GID mutation through E4 `chown`/`lchown` adapters with controlled unsupported diagnostics elsewhere. Shared ownership policy resolves users and groups with GNU name-first and forced `+ID` disambiguation, supports owner-only, group-only, `owner:group`, `owner:`, `:group`, no-op `:`, limited legacy `owner.group`, `--reference`, and ownership-aware `--from` filtering. Recursive operation uses E5 preserve-root preflight and postorder directory mutation; `-H`/`-L`/`-P` traversal remains independent from `--dereference`/`--no-dereference` mutation targeting; and E4 revalidates stable identity plus optional current UID/GID before mutation. Reporting honors option encounter order, changes-only, verbose success/retention/failure, and quiet diagnostics. Dedicated `ChOwn.Tests`, `ChGrp.Tests`, and shared host-mutation tests cover resolution, references, filtering, recursion, dereference policy, race preconditions, reporting, Windows non-emulation, and controlled failures. Full Windows, Ubuntu, and macOS CI remains the repository merge gate.

### Batch 43 — Recursive removal (1 tool) — implemented

- [x] `rm`

Batch 43 replaces direct recursive BCL deletion with a GNU-compatible command over E1 pathname expansion, E3 metadata, E4 single-path removal, and E5 mutation-safe traversal. It implements `-f`, `-i`, `-I`, `--interactive`, `-d`, `-r`/`-R`, `--one-file-system`, default and `all` preserve-root policy, verbose reporting, centralized `*`/`?`/`**` expansion, write-protected terminal prompts, refusal of final `.` and `..`, no-follow terminal-link handling, postorder directory removal, stable identity preconditions, and continuation after per-operand failures. Recursive parents are retained when a descendant is declined, skipped at a filesystem boundary, or cannot be removed; unavailable identity aborts the affected root rather than allowing later mutation through an unverified traversal. A trailing directory separator cannot turn a symbolic-link operand into target traversal. Dedicated `Rm.Tests` coverage exercises interaction precedence, recursive ordering and retention, glob roots, force, empty directories, preserve-root, link safety, write-protection, verbose output, and race-aware preconditions. Full Windows, Ubuntu, and macOS CI remains the repository merge gate.

#### Patch Wave D — Complete Phase P9 and close Phase P10

By this checkpoint, E2 through E4 have been exercised by their Coreutils validators, and the E5 command batches have supplied additional evidence about identity, no-follow mutation, metadata preservation, containment, and cleanup. Patch now closes its pre-E6 filesystem work.

- [x] Complete **P9** against the proposed E6 transaction interface, including target, backup, reject, output, rollback, cleanup, cancellation, and partial-failure cases.
- [x] Complete **P10** as an E2–E4 conformance closure rather than a first integration pass.
- [x] Verify GNU-compatible filename selection, containment, `-d`, `-p`, timestamps including post-2038 values, modes, metadata, input/output symlink behavior, and `--follow-symlinks`.
- [x] Add Windows, Linux, macOS, and best-effort BSD capability coverage; full runner execution remains the merge gate.
- [x] Confirm that Patch has not copied canonical-path, metadata, mode, or basic mutation machinery into command-local permanent code.
- [x] Freeze the Patch-facing E6 requirements before the E6 implementation gate closes.

Patch Wave D completes P9 and P10. Patch artifacts now carry explicit per-file recovery-unit identities; target, backup, per-target reject, and file-output artifacts recover together, while a shared explicit reject destination remains an independently reported unit. The provisional transaction boundary stages complete sibling files, flushes before commit, revalidates E3 identity, distinguishes failed-before-commit, fully rolled-back, partially committed, rollback-incomplete, and cleanup-incomplete outcomes, continues rollback and cleanup after individual failures, and retains GNU-visible multi-file partial success. `PatchE6TransactionContract` freezes secure sibling creation, per-file recovery, multi-file commit policy, containment, metadata restoration, cleanup, cancellation, atomicity reporting, durability reporting, and the complete failure-injection stage matrix without claiming E6 conformance.

P10 tests close the shared-contract boundary for lexical and physical containment, final-link and `--follow-symlinks` behavior, post-2038 timestamps, Unix modes and ownership, destination revalidation, Windows/Linux/macOS capability behavior, and a best-effort BSD capability profile. Patch continues to consume `Icod.Path`, E3 metadata, and E4 mutation providers rather than carrying permanent private replacements. Full three-runner CI remains the merge gate.

Completion of P10 is a hard predecessor of P11A.

### Completion Gate E6 — before Batch 44

* [x] Add shared transactional file-replacement infrastructure:

  * [x] secure sibling temporary files;
  * [x] atomic replacement where supported;
  * [x] backup-name generation and retention policy;
  * [x] rollback behavior after partial failure;
  * [x] pathname-containment and escape checks;
  * [x] deterministic cleanup after success, failure, and cancellation;
  * [x] explicit diagnostics where atomic replacement is unavailable;
  * [x] integration with the recursive traversal and metadata-preservation contracts established by Completion Gate E5.

Completion Gate E6 is implemented in `Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement`. Immutable artifacts carry explicit recovery-unit identities and no-follow E4 preconditions; complete sibling content, destination rollback copies, retained-backup content, and prior-backup recovery copies are securely staged and flushed before mutation. The transaction revalidates E3 identity immediately before each commit, exposes required/preferred atomicity and durability policy, implements GNU simple/numbered/existing backup names, restores requested E5 metadata, rejects containment escapes, continues reverse-order rollback and cleanup after individual failures, and reports failed-before-commit, fully rolled-back, partially committed, rollback-incomplete, cleanup-incomplete, and atomicity-unavailable outcomes. Shared tests exercise the provider seam and lifecycle failure injection; full three-runner CI remains the merge gate.

This gate is placed immediately before the consumers that require the complete replacement, backup, and rollback model. Patch Phase P11A consumes it immediately and reports contract defects while the APIs are still inexpensive to revise; `cp`, `mv`, and `install` then provide independent Coreutils/Fileutils validation in Batches 44 and 45; Patch Phase P11B closes only after that validation. During incubation, the co-resident `Icod.Patch`, `Icod.LineEditor.Ed.Shared`, `Icod.LineEditor.Sed`, and `Icod.Tar` projects may consume these contracts through project references; their genuinely cross-suite portions are candidates for `Icod.CommandFramework`.

#### Patch Phase P11A — Initial E6 transaction integration

Patch is an immediate co-validator of Completion Gate E6 rather than a consumer that waits until every Coreutils command is finished.

- [x] Replace the provisional P9 mutation internals with secure sibling temporary files, shared containment checks, backup-name and retention contracts, rollback, metadata restoration, deterministic cleanup, and explicit non-atomic capability results.
- [x] Exercise target replacement, file creation and deletion, backups, rejects, output files, partial hunk failure, multi-file partial success, symlink policy, cancellation, and every commit-stage failure.
- [x] Verify that no failure window removes the only recoverable copy of the original.
- [x] Keep Patch's GNU-visible partial-application and artifact policy above the general transaction mechanism.
- [x] Record any E6 contract defects before Batches 44 and 45 complete their independent validation.

Patch Phase P11A is implemented. `PatchE6Transaction` preserves Patch's per-file recovery units and GNU-visible independent-file continuation policy while delegating secure staging, flush, revalidation, atomic publication, rollback, metadata restoration, containment, cleanup, cancellation, and structured outcomes to `Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement`. Target, creation, deletion, retained backup, reject, alternate-output, stale-identity, same-unit rollback, independent-unit partial-success, and cancellation tests exercise the adapter. P11A exposed one E6 contract defect: Patch requires backup retention per target, including caller-selected backup pathnames, rather than transaction-wide retention. `TransactionalReplacementArtifact` therefore now supports an explicit per-artifact retention request; E6 stages and restores any previous backup before publishing the retained original. The provisional P9 transaction ceased to be selected in P11A and was removed in P11B after Batches 44 and 45 completed independent validation.

Patch Phase P11B is implemented. The unreachable `SystemPatchTransaction` and Patch-local provisional capability model are removed. `SystemPatchFileSystem` now creates only `PatchE6Transaction` and forwards the shared provider's `TransactionalReplacementCapabilities` unchanged. Wave C and Wave D failure-injection coverage continues to verify target, retained backup, reject, alternate output, metadata, link policy, rollback, cleanup, and cancellation behavior; dedicated P11B tests verify factory selection, legacy-type removal, capability forwarding, preferred-atomic non-atomic fallback reporting, final content, and E6 temporary cleanup.

P11A did not close Patch by itself. Batches 44 and 45 supplied the required independent copy, move, and installation validation before this P11B closure.

### Batch 44 — Copy and move engine (2 tools)

- [x] `cp`
- [x] `mv`

Implement source/destination classification, recursive copy, symlink and hard-link policy, metadata preservation, sparse files, reflink/copy-file-range opportunities, backup and overwrite modes, update rules, atomic replacement, cross-filesystem moves, destination-inside-source prevention, and partial-failure cleanup.

Batch 44 is implemented through `Icod.CoreUtils.Shared.FileSystem.CopyMove`. The shared engine consumes E5 recursive traversal for containment, stable entry identity, hard-link provenance, metadata planning, sparse-file policy, and filesystem-boundary control. Ordinary-file replacement and retained GNU backups are committed through E6; Linux clone and `copy_file_range` opportunities fall back to the E5 sparse copier. `mv` prefers a direct rename and performs copy-then-remove only when rename fails and `--no-copy` is not active. The command projects retain GNU-facing option precedence, prompts, diagnostics, target-directory interpretation, update rules, and partial source-by-source outcomes. Dedicated `Cp.Tests` and `Mv.Tests` projects are registered under the solution `tests` folder; full Windows, Linux, and macOS CI remains the merge gate.

### Batch 45 — Installation engine (1 tool)

- [x] `install`

Build on `mkdir`, `cp`, `chmod`, and `chown` primitives rather than invoking external utilities. Implement directory creation, modes, owners/groups, stripping policy, backups, compare mode, timestamps, SELinux-context policy, and atomic destination replacement.

Batch 45 is implemented through the E3 metadata, E4 mutation and ownership, GNU mode-expression, and E6 transactional-replacement contracts. Directory operands and `-D` leading components are created one physical component at a time without delegating to host `mkdir`, `chmod`, or `chown` utilities. File content is written to a secure sibling stage; stripping, numeric ownership, mode, timestamps, and SELinux context are completed on that private stage before durability flush and atomic publication. E6 now exposes a narrow staged-file configurator callback for command-specific pre-publication policy while retaining transaction ownership of creation, rollback, backups, commit, and cleanup.

The command supports GNU target-directory interpretation (including explicitly named directory indirection), `-T`, retained simple/numbered/existing backups, suffix selection, compare mode, source timestamp preservation, explicit strip programs without shell invocation, source-context preservation, destination-default SELinux labeling, explicit SELinux contexts for newly created objects, verbose diagnostics, help, and version output. Terminal destination symlinks and reparse objects are rejected rather than dereferenced or removed outside the E6 ordinary-file transaction contract; their targets remain unchanged. Dedicated `Install.Tests` coverage and an independent Shared E6 configurator test are registered under the solution `tests` folder. Full Windows, Linux, and macOS CI remains the merge gate.

#### Patch Phase P11B and Phase P12 — Transaction validation and final closure

After `cp`, `mv`, and `install` have independently validated E6, Patch repeats its transaction and conformance suites against the stabilized shared contract.

- [x] **P11B:** resolve contract changes exposed by Batches 44 and 45; remove all provisional Patch-local replacement code; verify target, backup, reject, output, metadata, symlink, rollback, cleanup, and non-atomic fallback consistency.
- [x] **P12:** finalize the GNU patch 2.8 option/conformance matrix, parser corpora, Linux opt-in differential tests, all-four-format Diffutils fixture interoperability, security and resource tests, signal/cancellation behavior, POSIX defaults, XML documentation, directory README files, UTF-8/LF policy, final public surface, and extraction metadata.
- [x] Classify every Shared dependency for Completion Gate G.
- [x] Document deliberate divergences, unsupported capabilities, and platform limitations.
- [x] Keep solution, repository, and package extraction deferred until Completion Gate G.
- [x] Retain Debug/Release and `windows-latest`, `ubuntu-latest`, and `macos-latest` as the repository integration gate; observe the green matrix after these files are merged into a complete checkout.

P11B and P12 are complete. The co-resident Patch implementation is closed at version 1.0, with final limitations recorded in `patch/upstream/P12-closure-audit.md`. It does not create a runtime dependency on Diffutils or LineEditor.

### In-solution LineEditor incubation sequence — Phases LE0 through LE9

This sequence does not alter command-batch numbering. The detailed architecture, repository assessment, security model, and test requirements remain in [`Icod.LineEditor-Informed-Architecture-Plan.md`](Icod.LineEditor-Informed-Architecture-Plan.md); this roadmap records the required schedule and completion dependencies.

The sequence follows the Diffutils and Patch milestones so textual ed-script and patch-format producers already exist as independent interoperability fixtures. It begins with Sed because the present Sed project is already structurally correct but internally monolithic. It does **not** create `Icod.LineEditor.Shared` as a prerequisite: most plausible cross-editor foundations already belong to the current Shared incubation project and are future `Icod.CommandFramework` candidates.

#### Phase LE0 — Correct LineEditor policy and capture the baseline

- [x] Retain `Icod.LineEditor.Sed` with lowercase assembly name `sed`, root namespace `Icod.LineEditor.Sed`, and public `Icod.LineEditor.Sed.Command`.
- [x] Rename the stale Sed test project filename to `Icod.LineEditor.Sed.Tests.csproj` and normalize the solution display name and project path while retaining the centralized `tests` solution folder.
- [x] Retain `Icod.LineEditor.Ed` and `Icod.LineEditor.Red` as the command-project identities, with public `Icod.LineEditor.Ed.Command` and `Icod.LineEditor.Red.Command` facades and lowercase assembly names `ed` and `red`; the LE0 Red facade preserves seed behavior until LE8 implements the restricted editor.
- [x] Add or verify C# 13, `net10.0`, Debug/Staging/Release, UTF-8/LF, XML documentation, output-path, solution, local-build, and CI policy for every applicable LineEditor command and test project.
- [x] Record GNU sed 4.10 and GNU ed 1.22.5 as the authoritative baselines.
- [x] Capture the full-solution and current Sed test baseline before structural refactoring in [`Icod.LineEditor-LE0-Baseline.md`](Icod.LineEditor-LE0-Baseline.md).

Phase LE0 is complete. It changes project identity and policy metadata only; command behavior remains frozen at the recorded three-runner baseline for Phase LE1.

#### Phase LE1 — Characterize and decompose `Icod.LineEditor.Sed`

- [x] Add characterization tests for option ordering, multiple script sources, diagnostics, script mode, record termination, sandboxing, in-place editing, and current command behavior before moving private types.
- [x] Keep `Icod.LineEditor.Sed.Command` as the public orchestration boundary while splitting the monolithic implementation into focused internal options, scripting, address, execution, record, regular-expression, substitution, process, and file modules.
- [x] Keep public behavior stable during decomposition; do not combine structural movement with regex, record, or replacement semantic changes.
- [x] Add substantive XML documentation for every public, protected, and internal type and member and a `README.md` in every multi-file source directory.
- [x] Add focused internal tests where useful without deleting the command-level conformance tests.

Phase LE1 is complete. The original 4,604-line implementation is now a partial `Command` facade over responsibility-focused source modules. The existing command suite remains intact, and the new characterization and module-boundary tests freeze the temporary pre-LE3 regex, pre-LE4 record, pre-LE5 script-source, and pre-LE10 replacement behavior.

#### Phase LE2 — Shared BRE/ERE foundation, scheduled earlier as Completion Gate R1

Completion Gate R1 performs this phase before Batch 26 because Grep is the first remaining BRE/ERE consumer. When the LineEditor sequence reaches this point:

- [x] verify that the Gate R1 syntax, locale, byte/text mapping, match-offset, capture, diagnostic, cancellation, and resource-limit contracts satisfy GNU Sed and GNU Ed requirements;
- [x] extend the shared contract only where the pinned LineEditor baselines expose a genuine cross-suite gap; no production extension was required by the audit;
- [x] keep Sed empty-pattern reuse, address/substitution context, match iteration, and replacement policy in `Icod.LineEditor.Sed` rather than broadening the framework contract unnecessarily.

Phase LE2 is complete. `LineEditorRegularExpressionContractTests` now exercises the Shared provider as a LineEditor consumer boundary, including BRE/ERE syntax separation, ERE composition, leftmost-longest selection, captures, locale injection, line-sensitive anchors, UTF-16 and exact source-byte coordinates, malformed-byte preservation, diagnostics, cancellation, and resource limits. The audit is recorded in `Icod.LineEditor-LE2-Regex-Contract-Audit.md`; it found no missing cross-suite production capability. Phase LE3 remains responsible for Sed-specific regex state and migration.

#### Phase LE3 — Migrate Sed to the Shared regex provider

- [x] Add a Sed-specific adapter that owns BRE/ERE selection, empty-pattern reuse, address-versus-substitution context, GNU escape preprocessing, GNU/POSIX mode, match iteration, and Sed diagnostics.
- [x] Route address and substitution compilation through the Shared provider.
- [x] Remove the private BRE/POSIX-class-to-.NET-regex translation only after equivalence and differential tests pass.
- [x] Add GNU sed differential cases for BRE, ERE, locale behavior, captures, repeated zero-length matches, and leftmost-longest results that differ from .NET's default engine.

Phase LE3 is complete. `SedRegularExpressionCompiler` now selects the Shared GNU Basic or Extended provider, retains Sed's exact compiled-expression reuse state across addresses and substitutions, owns address/substitution modifiers, GNU escape preprocessing, and GNU/POSIX policy, maps Shared diagnostics into Sed diagnostics, and implements GNU empty-match iteration above Shared leftmost-longest matching. The private `System.Text.RegularExpressions` translator and POSIX-class replacement table have been removed. `SedRegularExpressionMigrationTests` records GNU sed 4.10 differential behavior, including captures, locale classes, control and numeric escapes, strict-POSIX bracket behavior, repeated zero-length matches, and an ERE alternation whose leftmost-longest result differs from .NET's default engine. LE4 remains responsible for byte-preserving records and explicit encoding policy.

#### Phase LE4 — Correct Sed record and text semantics

- [x] Introduce a byte-preserving Sed input record that retains authoritative bytes, source identity, aggregate and per-file record numbers, separator kind, and whether the final record was terminated.
- [x] Consume the Shared segmented record foundation for LF and NUL modes; preserve CR as data unless an explicit Sed rule removes it.
- [x] Define C/POSIX byte-locale and UTF-8 decoding behavior, invalid-input policy, byte-to-text mapping, and replacement encoding.
- [x] Serialize Sed data with explicit LF or NUL separators rather than `Environment.NewLine`; reserve host-generated line endings for diagnostics and presentation only.
- [x] Add CRLF, lone-CR, invalid-UTF-8, NUL, empty-record, huge-record, multiline-pattern-space, hold-space-growth, and unterminated-final-record tests.
- [x] Document the correct memory invariant: Sed streams unrelated completed input records, while pattern and hold spaces may grow according to Sed semantics.

Phase LE4 is complete. Sed now frames input through Shared `ByteRecordReader` in LF or NUL mode, carries source and termination metadata in `SedInputRecord`, selects C/POSIX byte or UTF-8 text policy through `TextLocaleEnvironment`, preserves malformed UTF-8 through deterministic placeholders, and writes separators explicitly through `DelimitedByteRecordWriter`. The CLI uses raw console streams while the established `TextReader`/`TextWriter` facade remains available through compatibility adapters. Pattern and hold spaces retain both termination and the active LF/NUL internal separator needed by multiline, hold, print, list, and file-write commands. Real `-z` consumer evidence also extends Shared line-sensitive regex options with a caller-selected separator and explicit NUL-dot policy while preserving prior defaults. The implementation and acceptance record are documented in `Icod.LineEditor-LE4-Record-and-Text-Semantics.md`; this established the input boundary consumed by the now-complete Phase LE5.

#### Phase LE5 — Harden Sed orchestration, script sources, and capabilities

- [x] Add a `RunAsync(string[] args, CommandContext context)` core path while retaining established compatibility overloads.
- [x] Preserve command-line expression, script-file, and implicit script operands as distinct source objects with stable source names, locations, and ordering; do not combine them with `Environment.NewLine`.
- [x] Introduce injectable shell, auxiliary-file, and in-place-edit capabilities over the current Shared process, filesystem, and temporary-object mechanics.
- [x] Enforce GNU Sed sandbox restrictions both during compilation and through denied runtime capabilities.
- [x] Isolate current in-place editing behind an internal `InPlaceEditor` boundary and add failure-injection and cleanup characterization tests.
- [x] Keep the current implementation behind that boundary until Completion Gate E6 supplies the final shared transaction model.

Phase LE5 is complete. `Command.RunAsync(string[] args, CommandContext context)` is now the primary entry path and preserves LE4's byte-stream contract when binary context streams are available. `SedScriptSource` and `SedScriptDocument` retain ordered `-e`, `-f`, and implicit script identity, join sources with literal LF rather than `Environment.NewLine`, and map parser diagnostics to stable source line and column locations. Shell execution remains on Shared `ProcessRunner`; auxiliary reads/writes and in-place editing are injectable capabilities; sandbox policy is enforced both during script compilation and through denied runtime profiles. The existing replacement mechanics are isolated in `SystemInPlaceEditor`, use Shared secure temporary objects, and are covered by failure-injection and cleanup tests while the final E6 migration remains scheduled for LE10. The contract is documented in `Icod.LineEditor-LE5-Orchestration-and-Capabilities.md`; Phases LE6 and LE7 are complete and Phase LE8 is now active.

#### Phase LE6 — Create `Icod.LineEditor.Ed.Shared`

- [x] Create `Icod.LineEditor.Ed.Shared` and its dedicated test project inside the current solution.
- [x] Design and implement the complete Ed/Red mutable editor engine: scalable line storage, stable line identity where required, current and last addresses, marks, cut buffers, addresses and ranges, commands, substitutions, global commands, undo, remembered state, file operations, shell integration, diagnostics, signals, cancellation, and exit statuses.
- [x] Consume the current Shared regex, record, process, temporary, filesystem, and capability contracts rather than introducing parallel abstractions.
- [x] Define injectable Ed file and process capabilities and immutable standard and restricted security profiles.
- [x] Establish textual compatibility fixtures for ed scripts emitted by GNU Diffutils and `Icod.DiffUtils` without adding a runtime dependency on `Icod.DiffUtils.Shared`.

Phase LE6 is complete. `Icod.LineEditor.Ed.Shared` now provides bounded segmented line storage, stable line identities, Ed-specific addresses and ranges, marks, cut buffers, substitutions, global execution, reversible undo, remembered session state, injected file and process effects, immutable standard and restricted profiles, controlled diagnostics, signals, cancellation, and exit statuses. The engine consumes the current Shared GNU BRE, byte-record, process, secure-temporary, and filesystem-durability contracts. Its dedicated tests include textual GNU Diffutils-style and `Icod.DiffUtils`-style ed-script fixtures without a runtime Diffutils dependency. The design and phase boundary are recorded in `Icod.LineEditor-LE6-Ed-Shared-Engine.md`; Phase LE7 is complete and Phase LE8 is now active.

#### Phase LE7 — Rebuild `Icod.LineEditor.Ed`

- [x] Retain the public `Icod.LineEditor.Ed.Command` and lowercase assembly name `ed`.
- [x] Replace the current seed internals with the `Icod.LineEditor.Ed.Shared` engine under the standard security profile.
- [x] Implement GNU ed 1.22.5 command-line, address, buffer, command, file, process, signal, diagnostic, and exit-status conformance.
- [x] Add command-level, script, large-buffer, long-line, cancellation, broken-pipe, and interoperability tests.

Phase LE7 is complete. `Icod.LineEditor.Ed.Command` now composes the LE6 mutable engine with the standard file and process capabilities, selects Shared BRE or ERE policy, accepts GNU ed 1.22.5 invocation options and initial-address forms, preserves LF-oriented command/data semantics through byte streams, maps prompting, help, diagnostics, cancellation, modified-buffer warnings, and exit statuses at the executable boundary, and retains the lowercase `ed` assembly. The dedicated command tests cover invocation, profiles, scripts, byte counts, diagnostics, CR policy, large buffers, long lines, cancellation, broken output, text compatibility, and GNU/Icod Diffutils ed-script fixtures. The completed boundary is recorded in `Icod.LineEditor-LE7-Ed-Command.md`; Phase LE8 is now active.

#### Phase LE8 — Implement `Icod.LineEditor.Red`

- [ ] Retain the public `Icod.LineEditor.Red.Command` and lowercase assembly name `red`.
- [ ] Use the same Ed engine and make `red` and `ed --restricted` select the same immutable restricted profile.
- [ ] Deny every shell-bearing path at both parser/dispatcher and process-capability layers, including nested global-command execution and remembered shell commands.
- [ ] Route every filename-bearing operation through one platform-aware restricted file policy covering Unix paths, Windows drive-relative and rooted paths, UNC and device paths, alternate data streams, symlinks, hard links, reparse points, and validation/open races.
- [ ] Capture the restricted working-directory context once and document whether the pinned compatibility profile provides pathname restriction or stronger physical confinement.
- [ ] Add adversarial tests that verify denied operations leave buffer, address, marks, modified state, filename state, and undo state unchanged as required.

#### Phase LE9 — Perform the evidence-based LineEditor sharing audit

- [ ] Classify each candidate as cross-suite `Icod.CommandFramework` material, LineEditor-family-specific, Ed-family-specific, Sed-specific, or command-local.
- [ ] Move cross-suite regular-expression, record, diagnostic, process, temporary, filesystem, and text contracts toward the current Shared incubation project rather than wrapping them in another suite library.
- [ ] Keep the mutable editor engine and Red policy in `Icod.LineEditor.Ed.Shared`.
- [ ] Keep Sed script, address/range state, pattern and hold spaces, branching, command cycle, sandbox policy, and in-place-editing policy in `Icod.LineEditor.Sed`.
- [ ] Create `Icod.LineEditor.Shared` only if a cohesive residual library remains after those classifications and both completed engines provide real consumers.
- [ ] Record consumer evidence and dependency direction for every moved API.

Completion of Phases LE0 through LE9 leaves Sed decomposed and re-audited, Ed and Red implemented over one engine, and the family boundary justified by actual consumers. Final solution and repository extraction remains deferred until Completion Gate G.

### In-solution LineEditor transactional-replacement integration — Phase LE10

This follow-up does not alter command-batch numbering. It occurs after Completion Gate E6 and Batches 44 and 45 so the shared transaction model has first been validated by `cp`, `mv`, and `install` before the LineEditor commands replace their temporary command-local mechanisms.

- [ ] Migrate Sed in-place editing to the shared secure sibling-temporary, backup, rollback, metadata, symlink/reparse-point, atomic-replacement, and cleanup contracts.
- [ ] Migrate Ed write and replacement operations where the command semantics require transactional replacement.
- [ ] Preserve command-specific backup, append, force, write, modified-buffer, and symlink policies above the shared mechanism.
- [ ] Add atomicity, rollback, metadata, link, cancellation, failure-injection, and cleanup tests for both engines.
- [ ] Remove temporary command-local replacement implementations after all three required CI platforms pass.

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

This gate supports `env` and `nohup`, allows Coreutils `kill` to validate signal parsing, targets, and delivery in Batch 53, allows `nice` and `timeout` to validate priority, waiting, process groups, clocks, termination, and status propagation in Batch 54, and supplies the common mechanics consumed immediately by the ProcPs block and later by `Icod.Tar`, `Icod.LineEditor.Ed.Shared`, and other suites.

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
- [ ] Inventory every API in `Icod.DiffUtils.Shared`, `Icod.LineEditor.Ed.Shared`, `Icod.ProcPs.Shared`, any evidence-based `Icod.LineEditor.Shared`, and any other suite engine to detect duplication or misplaced cross-suite contracts.
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
  - [ ] `Icod.LineEditor`, containing `Icod.LineEditor.Ed.Shared`, `Icod.LineEditor.Ed`, `Icod.LineEditor.Red`, `Icod.LineEditor.Sed`, and `Icod.LineEditor.Shared` only if Phase LE9 justified it;
  - [ ] `Icod.Tar`;
  - [ ] `Icod.ProcPs`.
- [ ] Preserve relevant history, project identities, test corpora, documentation, and CI policy during each extraction.
- [ ] Convert every extracted suite to versioned `PackageReference` dependencies on `Icod.CommandFramework`.
- [ ] Retain project references within each extracted suite for its own Shared or engine projects unless a separate package boundary is independently justified.
- [ ] Treat `Icod.LineEditor.Ed.Shared` as a definite LineEditor suite engine; retain or publish a general `Icod.LineEditor.Shared` only if Phase LE9 produced a cohesive family-specific library with actual Ed and Sed consumers.
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

- Completion Gate E1 establishes read-only expansion and traversal before `Icod.Grep` and `Icod.DiffUtils.Diff` require recursive directory work. It separates operand expansion, low-level filesystem observation, and traversal orchestration; preserves root provenance; exposes entry and directory-exit phases; and gives both suites the same link, active-ancestry cycle, filesystem-boundary, pruning, structured-error, cancellation, and provider foundation without placing search- or diff-specific policy in the general Shared project.

- Batch 26 implements `Icod.Grep` directly in its final namespace while retaining the one-solution development model. Grep-specific pattern orchestration, binary policy, context grouping, and output semantics stay in the grep project; only proven cross-suite contracts are candidates for the final framework.

- `split`, `tac`, `csplit`, and `pr` remain between Grep and Diffutils because they complete the pending Coreutils/Textutils streaming and presentation work that depends on the same record, regular-expression, temporary-storage, and display-width foundations.

- Batches 30 through 34 are consecutive because the complete GNU Diffutils family should be developed as one cohesive suite. `Icod.DiffUtils.Shared` is established first; `cmp` validates byte comparison and status contracts; `diff` establishes the two-way engine and textual formats; `diff3` adds three-way comparison and merging; and `sdiff` adds side-by-side and interactive behavior.

- `Icod.DiffUtils` remains independent of `Icod.Patch` and `Icod.LineEditor.Ed` at runtime. Unified, context, normal, and ed-script text are the compatibility contracts, ensuring the implementations can interoperate with GNU and third-party tools rather than only with each other.

- Patch and the E-series gates are scheduled as one dependency-aligned workstream rather than as an isolated Patch milestone followed by an unrelated filesystem block. P0 through P4 establish syntax and immutable models; P5 and P6 proceed concurrently with E2 because they need no live path semantics; P2, P5, P6, and E2 are all required before P7; P7 together with E3, E3R, and E4 precedes P8; P9 co-develops the E6-facing mutation boundary; P10 closes E2, E3, E3R, and E4 conformance before E6; and P11 is split around the independent `cp`, `mv`, and `install` validation before P12 closes Patch.

- E5 is not treated as a direct Patch feature prerequisite because Patch does not recursively copy directory trees. Only the E5 identity, containment, metadata-preservation, no-follow mutation, failure, and cleanup contracts needed by E6 lie on Patch's transaction dependency path.

- The LineEditor sequence now follows the Patch/E-series workstream and Batches 44 and 45. This keeps the Patch suite work cohesive despite its explicit E-gate interruptions and lets LineEditor Phases LE0 through LE10 proceed as one contiguous family sequence over the already validated regex, record, filesystem, and transaction foundations. It begins by decomposing the already suite-correct `Icod.LineEditor.Sed` implementation. Structural decomposition is separated from semantic migration so current behavior can be characterized before the monolithic parser, execution, regex, record, process, and in-place-editing concerns are moved into focused internal modules.

- Completion Gate R1 advances LineEditor Phase LE2 before Batch 26 so Grep, the first remaining BRE/ERE consumer, cannot introduce a parallel engine. Sed later migrates away from .NET-regex translation and Ed, Expr, and Csplit further validate the same cross-suite regular-expression contract.

- Sed then adopts byte-preserving LF/NUL records, explicit final-record termination, exact CR and invalid-input policy, distinct script-source locations, and sandbox defense in depth. Sed-specific pattern space, hold space, ranges, branches, cycle behavior, and in-place-editing policy remain inside `Icod.LineEditor.Sed`.

- `Icod.LineEditor.Ed.Shared` is a definite suite engine because Ed and Red are the same mutable line editor under standard and restricted profiles. `Icod.LineEditor.Ed` and `Icod.LineEditor.Red` remain thin command projects with exact public `Command` classes; Red restrictions are enforced through both command parsing and denied file/process capabilities.

- A general `Icod.LineEditor.Shared` is intentionally deferred until Phase LE9. Similar command vocabulary is not sufficient evidence for sharing: Ed addresses and mutable-buffer state differ fundamentally from Sed addresses, pattern/hold spaces, and streaming cycle state. The library is created only if completed engines leave a cohesive family-specific remainder after cross-suite APIs have been classified toward `Icod.CommandFramework`.

- Completion Gates E2 through E6 remain general filesystem foundations even though Patch is an early consumer. Patch helps shape and test their cross-suite contracts but keeps filename selection, hunk application, rejects, backups, partial-application decisions, and GNU diagnostics in `Icod.Patch`. E6 is not considered stable from Patch alone: Patch Phase P11A, `cp`/`mv`, and `install` exercise different policies before P11B and P12 close Patch. LineEditor Phase LE10 follows those validations.

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
16. For a LineEditor milestone, verify the exact public command classes, preserve Sed and Ed execution-model boundaries, consume the current Shared regex/record/process/filesystem contracts rather than wrapping them, enforce Red and Sed security policies at both parse and capability layers, and keep `Icod.LineEditor.Shared` optional until the Phase LE9 consumer audit.
17. For a ProcPs milestone, verify that common processor, process, signal, priority, waiting, timing, status, and terminal mechanics are consumed from the current Shared incubation project rather than duplicated in `Icod.ProcPs.Shared`.
18. For Completion Gate G, verify every extracted repository against the published `Icod.CommandFramework` and other applicable NuGet packages before declaring the architecture stable.

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
- LineEditor milestones preserve the exact public command classes `Icod.LineEditor.Ed.Command`, `Icod.LineEditor.Red.Command`, and `Icod.LineEditor.Sed.Command`; keep Ed/Red state in `Icod.LineEditor.Ed.Shared`; keep Sed cycle state in `Icod.LineEditor.Sed`; and create `Icod.LineEditor.Shared` only after the evidence-based sharing audit;
- LineEditor tests cover GNU BRE/ERE, byte-preserving LF/NUL and final-record semantics, script-source diagnostics, Sed sandbox denial, Red shell and path denial, in-place/write atomicity, rollback, links, races, cancellation, and cleanup as applicable;
- ProcPs batches consume the shared processor and process foundation without duplicating its identities, targets, launch, wait, signal, priority, timing, status, or terminal contracts;
- Completion Gate G leaves `Icod.CommandFramework` free of suite dependencies, preserves `Icod.CoreUtils.Shared` only where Coreutils/Fileutils/Textutils-specific reuse remains, and verifies all consumers against published packages.
