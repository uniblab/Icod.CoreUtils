# Icod.CoreUtils Audit and Refactor Roadmap

## Living status

| Item | Status |
|---|---|
| Completed command batches | For completion status, see list of batches below |
| Current engineering gate | For completion status, see list of batches below |
| Next infrastructure dependency | For completion status, see list of batches below |
| Current target framework | `net10.0` |
| Required CI runners | `windows-latest`, `ubuntu-latest`, `macos-latest` |
| Status-maintenance rule | Update this table after every merged batch |

## Scope

`Icod.CoreUtils` is a cross-platform .NET implementation of GNU Coreutils. Its scope expressly includes the file-manipulation and text-processing command families historically distributed as **GNU Fileutils** and **GNU Textutils**. These are natural Coreutils inclusions rather than unrelated extensions: GNU combined `fileutils`, `sh-utils`, and `textutils` into the unified `coreutils` package in 2003. The current GNU Coreutils project continues to describe itself as the basic file, shell, and text manipulation utilities of the GNU operating system.

Historical references:

- [GNU Coreutils FAQ — Fileutils, shellutils and textutils](https://www.gnu.org/software/coreutils/faq/coreutils-faq.html#Fileutils-shellutils-and-textutils)
- [GNU Coreutils 5.0 release announcement](https://lists.gnu.org/archive/html/coreutils-announce/2003-04/msg00000.html)

The primary supported CI targets are `windows-latest`, `ubuntu-latest`, and `macos-latest`. BSD support remains a best-effort target. The implementation is therefore not a Unix-only port: platform-independent behavior is preferred, native behavior is implemented per supported ABI where required, and unsupported platform capabilities receive controlled diagnostics.

The repository temporarily contains commands owned by other upstream GNU or Linux utility families. They are useful here while common infrastructure is being discovered and stabilized, but they are not intended to remain permanent members of `Icod.CoreUtils`:

- `diff` moves to `Icod.DiffUtils` together with `cmp`, `diff3`, and `sdiff`;
- `grep` moves to `Icod.Grep`;
- `patch` moves to `Icod.Patch`;
- `ed` moves to `Icod.Ed` together with `red`;
- `ps` moves to `Icod.ProcPs`, whose eventual scope is the complete procps-ng command family;
- `sed` moves to `Icod.Sed`;
- `tar` moves to `Icod.Tar`.

These commands are being vacated because each belongs to a distinct upstream project, release cycle, conformance manual, implementation engine, and security or portability boundary. Dedicated repositories allow each suite to evolve and version independently while still consuming the common cross-platform command infrastructure developed here.

No separate `[` project will be added. The existing `test` project remains the condition evaluator.

## Eventual architecture

The current `Shared` project is deliberately serving as an **incubation area** while the remaining Coreutils, Fileutils, and Textutils work reveals which APIs are truly cross-suite and which are specific to Coreutils. It would be premature to split that code now: later text, filesystem, terminal, process, and platform batches will continue to change the abstractions and will provide the evidence needed to choose stable public package boundaries.

The ultimate architecture has three layers:

1. **`Icod.CommandFramework`** — functionality genuinely common across independent command suites;
2. **suite-specific shared libraries** — functionality shared only within one upstream family, such as `Icod.CoreUtils.Shared`, `Icod.DiffUtils.Shared`, or `Icod.ProcPs.Shared`;
3. **individual command projects** — thin command front ends over the applicable shared engine.

During the current roadmap, cross-suite and Coreutils-specific facilities may coexist in `Icod.CoreUtils.Shared`. Every significant new Shared API should nevertheless be treated as provisionally belonging to one of these future layers. After the command roadmap is substantially complete, a dedicated architectural audit will classify the actual consumers and perform the extraction with evidence rather than prediction.

The eventual dependency direction is:

```text
Icod.CommandFramework
        ↓
Suite-specific Shared project, when required
        ↓
Command projects
```

Representative examples are:

```text
Icod.CommandFramework
├── Icod.CoreUtils.Shared
│   └── Icod.CoreUtils command projects
├── Icod.DiffUtils.Shared
│   └── cmp / diff / diff3 / sdiff
├── Icod.ProcPs.Shared
│   └── procps-ng command projects
├── Icod.Ed.Shared
│   └── ed / red
├── Icod.Grep
├── Icod.Patch
├── Icod.Sed
└── Icod.Tar
```

Across repositories, dependencies use versioned NuGet `PackageReference` entries. Within one suite repository, commands normally use `ProjectReference` to that suite's Shared or engine project so a single pull request can evolve the engine and its commands together.

`Icod.CoreUtils` must not acquire production dependencies on sibling command-suite repositories. Interoperability between sibling repositories should normally occur through documented command-line behavior and textual formats rather than runtime references. Examples include unified diffs flowing from `Icod.DiffUtils` to `Icod.Patch`, and ed scripts flowing from `Icod.DiffUtils` to `Icod.Ed`.

### Icod.CommandFramework

`Icod.CommandFramework` will eventually become its own solution, repository, and NuGet package. It will contain only APIs demonstrated to be useful across two or more independent command suites, such as:

- command contexts and injected standard streams;
- common argument-processing foundations;
- diagnostics, quoting, and exit-status support;
- cancellation, broken-pipe, and disposal behavior;
- high-performance cross-platform file I/O;
- byte, text, record, delimiter, locale, and display-width abstractions;
- secure temporary-object and workspace infrastructure;
- general filesystem capability, traversal, and metadata abstractions;
- terminal, child-process, signal, and platform-capability abstractions.

The final extraction must be based on a consumer and API audit. An API moves to `Icod.CommandFramework` because multiple suites need the same contract, not merely because it currently resides in `Shared`.

### Icod.CoreUtils.Shared

`Icod.CoreUtils.Shared` may remain after `Icod.CommandFramework` is extracted. Its purpose is narrower: it contains behavior shared among Coreutils, Fileutils, and Textutils commands that is not a suitable cross-suite framework contract. If retained, it will also become its own solution, repository, and versioned NuGet package. Individual `Icod.CoreUtils` command projects will consume the published binary through `PackageReference`, while `Icod.CoreUtils.Shared` itself will reference `Icod.CommandFramework`.

Likely examples include Coreutils-specific option combinations, backup and overwrite policies, block-size conventions, ownership and mode presentation, listing models, copy/move/install policies, and other engines shared by multiple Coreutils commands but not by Diffutils, Grep, Patch, Sed, Tar, ProcPs, or other suites.

The final dependency is therefore:

```text
Icod.CoreUtils command
        ↓
Icod.CoreUtils.Shared
        ↓
Icod.CommandFramework
```

### Icod.DiffUtils

`Icod.DiffUtils` contains `cmp`, `diff`, `diff3`, and `sdiff`.
`Icod.DiffUtils.Shared` owns suite-specific comparison, differencing, hunk construction, merge, and difference-output infrastructure. It ultimately references `Icod.CommandFramework`, not `Icod.CoreUtils` or `Icod.CoreUtils.Shared` as a permanent architectural dependency.

### Icod.Grep

`Icod.Grep` contains `grep`. A separate Shared or engine project is added only where it improves reuse or testability.
Grep-specific pattern-source handling, matcher orchestration, recursive selection, binary-input policy, context grouping, and output formatting remain in the grep repository. General regular-expression, record, traversal, and command-runtime contracts ultimately come from `Icod.CommandFramework`.

Obsolete `egrep` and `fgrep` compatibility launchers are not added implicitly. They require a separate, explicit scope decision.

### Icod.Patch

`Icod.Patch` contains `patch`. Its repository owns patch-format parsing, hunk application, offset and fuzz matching, reversal detection, rejection reporting, backup policy, and transactional patch-application behavior.

`Icod.Patch` consumes ordinary textual patch formats. It must not require a production reference to `Icod.DiffUtils.Shared`; compatibility is verified through GNU and Icod-generated fixtures at the public file-format boundary. General command, filesystem, temporary-file, and transactional-replacement contracts ultimately come from `Icod.CommandFramework`.

### Icod.Ed

`Icod.Ed` contains `ed` and its restricted companion `red`.
`Icod.Ed.Shared` owns editor-specific address parsing, command parsing, mutable line buffers, marks, substitutions, global commands, undo state, file operations, shell-command integration, and restricted-mode enforcement.

Compatibility with ed scripts produced by GNU Diffutils and `Icod.DiffUtils` is tested through textual scripts rather than a runtime project dependency. General regular-expression, text, process, and filesystem contracts ultimately come from `Icod.CommandFramework`.

### Icod.ProcPs

`Icod.ProcPs` is the dedicated home for the **complete procps-ng tool family**, not only `ps`. The existing `ps` implementation seeds the repository; the destination roadmap will inventory the commands installed by the pinned procps-ng baseline and schedule each one explicitly.

`Icod.ProcPs.Shared` owns process enumeration, `/proc` and platform-provider abstractions, selection, snapshots, field definitions, sorting, personality profiles, terminal association, CPU and memory calculations, and other behavior shared within procps-ng. Cross-suite command, terminal, process-launch, signal, and platform contracts ultimately come from `Icod.CommandFramework`.

### Icod.Sed

`Icod.Sed` contains `sed` and owns the stream-editor program parser, address model, pattern and hold spaces, substitution engine, branching, command cycle, in-place editing behavior, and GNU sed compatibility policy.

General command, regular-expression, text-record, temporary-file, and filesystem contracts ultimately come from `Icod.CommandFramework`. Sed-specific state and execution semantics do not move into the framework.

### Icod.Tar

`Icod.Tar` contains `tar` and owns archive-format handling, entry models, sparse-file archive behavior, compression integration, selection and exclusion rules, incremental behavior where in scope, and extraction security policy.

General command, traversal, metadata, temporary-workspace, process, and transactional-replacement contracts ultimately come from `Icod.CommandFramework`. Archive-specific formats and state do not move into `Icod.CoreUtils.Shared` or the framework.

### Repository extraction policy

Each extraction milestone must:

- preserve relevant source and test history where practical;
- keep the lowercase executable assembly name unchanged;
- move projects and namespaces to the new repository family;
- reproduce the established `net10.0`, C# 13, Debug/Staging/Release, UTF-8/LF, XML documentation, and three-runner CI policies;
- remove the command from the CoreUtils solution, packaging, inventories, and release workflows;
- add a migration note identifying the new repository and package;
- establish cross-repository compatibility fixtures where a textual contract crosses repository boundaries;
- create a suite-specific Shared or engine project only where the destination suite has a real reuse or testability need;
- document any transitional dependency on `Icod.CoreUtils.Shared` while `Icod.CommandFramework` is still incubating;
- migrate cross-suite dependencies to the published `Icod.CommandFramework` package when the final framework extraction is completed;
- distinguish clean transfer of ownership from completion of the new repository's conformance roadmap.

## Authoritative Source

For current GNU Coreutils commands—including the natural historical GNU Fileutils and GNU Textutils command families—the conformance baseline is the pinned GNU Coreutils manual and source. The separate Fileutils and Textutils packages are historical provenance, not competing modern specifications.

For commands owned by sibling repositories, use the corresponding upstream project as the primary authority.

| Program family or command | Eventual owner | Primary authority |
|---|---|---|
| GNU Coreutils, including historical GNU Fileutils and GNU Textutils families | `Icod.CoreUtils` | GNU Coreutils manual and source |
| `sed` | `Icod.Sed` | GNU sed |
| `grep` | `Icod.Grep` | GNU grep 3.12 |
| `cmp`, `diff`, `diff3`, `sdiff` | `Icod.DiffUtils` | GNU Diffutils 3.12 |
| `patch` | `Icod.Patch` | GNU patch 2.8 |
| `ed`, `red` | `Icod.Ed` | GNU ed 1.22.5 |
| procps-ng command family, seeded by `ps` | `Icod.ProcPs` | procps-ng 4.0.6, with an explicitly documented portability profile |
| `tar` | `Icod.Tar` | GNU tar |

Man7 pages are useful synopses and secondary references, but they must not replace the authoritative upstream manual.

## Current repository audit

### What is working well

- The completed batches established shared command-line, diagnostics, streaming, numeric, platform, identity, process, date/time, and block-I/O abstractions.
- Batches 1 through 15 have command-specific tests.
- The complete test suite has been exercised on Windows, Ubuntu, and macOS during Batch 10 stabilization.
- Source projects consistently reference `Shared` where common behavior is appropriate; that project currently incubates both future `Icod.CommandFramework` APIs and Coreutils-specific `Icod.CoreUtils.Shared` APIs.
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
   - The existing `diff` implementation is not a complete difference algorithm and does not yet implement the required result-status model; this defect transfers to the initial `Icod.DiffUtils` audit.
   - The existing `patch` implementation handles a private simplified format rather than normal, context, and unified patches; this defect transfers to the initial `Icod.Patch` audit.
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

1. Project filenames and namespaces use conventional PascalCase where practical, such as `Icod.CoreUtils.BaseName.csproj` and `Icod.CoreUtils.BaseName`.
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
11. Each public command class has class-level XML documentation whose `<summary>` includes the command usage, plus a dedicated usage-printing or usage-writing function.
12. Every new project is added to the solution, all required configuration mappings, the appropriate solution folder, and every local and CI build/test entry point.
13. The supported CI platform targets are explicitly `windows-latest`, `ubuntu-latest`, and `macos-latest`. Platform-specific tests may be conditional, but every runner must build the full solution and execute the complete applicable test suite.
14. Do not use `Assert.True` to check for substrings. Use `Assert.StartsWith`, `Assert.EndsWith` instead. (https://xunit.net/xunit.analyzers/rules/xUnit2009).
15. Extracted repositories retain these conventions unless their own roadmap records a deliberate exception.
16. Cross-repository compatibility is tested at the public command-line or textual-format boundary unless a dependency has been deliberately classified as cross-suite infrastructure. During the current roadmap such APIs may be incubated in `Icod.CoreUtils.Shared`; their permanent public home is `Icod.CommandFramework` after the final extraction audit.

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
16. Extraction milestones remove stale solution, packaging, namespace, documentation, and CI references from this repository and establish equivalent checks in the destination repository.
17. Until the final framework audit, every substantial Shared API records a provisional classification: cross-suite `Icod.CommandFramework` candidate, Coreutils-only `Icod.CoreUtils.Shared` candidate, or command-local implementation. The classification may change when real consumers provide better evidence.

## Engineering completion gates

These gates are repository milestones rather than command batches. They do not alter the historical numbering.

## Batch-size policy

- A batch groups commands only when they share an implementation engine or directly validate the same new infrastructure.
- A complex parser, state machine, security boundary, or platform layer may receive a one-command batch.
- A pair or trio is preferred over a superficially thematic five- or six-command batch when the larger group would hide unrelated risk.
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

`ps` was implemented and stabilized as part of this historical batch. Its completed history remains recorded here, while ownership will later transfer to `Icod.ProcPs` under the repository extraction milestone after Batch 32.

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

* [ ] Add the shared text-unit and display-column model:

  * [ ] byte iteration;
  * [ ] decoded Unicode-scalar iteration;
  * [ ] explicit invalid-encoding policy;
  * [ ] display-column width calculation;
  * [ ] tab-stop grammar and repeated tab intervals;
  * [ ] backspace and carriage-return column behavior;
  * [ ] injectable width and locale providers.

This gate provides only the facilities needed by `expand`, `unexpand`, `fold`, and the later page-layout commands. It does not prematurely introduce sorting or external-storage behavior.

### Batch 17 — Tabs and display columns (3 tools)

- [ ] `expand`
- [ ] `unexpand`
- [ ] `fold`

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

This gate is completed before the `Icod.Grep` extraction milestone so the new repository can consume a stable read-only traversal contract for recursive search. It also supports later Coreutils directory listing and filesystem accounting, and may be consumed by `Icod.DiffUtils` for recursive directory comparison and by `Icod.Tar` for archive traversal. The cross-suite portions are candidates for `Icod.CommandFramework`.

### Batch 26 — GNU grep extraction milestone

- [ ] Remove `grep` from the Icod.CoreUtils solution and repository.
- [ ] Transfer the existing `grep` implementation, tests, and relevant history to `Icod.Grep`.
- [ ] Establish the `Icod.Grep` solution, three-runner CI, configuration, documentation, and packaging conventions.
- [ ] Establish `Icod.Grep.Shared` for suite-specific pattern-source handling, matcher orchestration, recursive selection, binary-input policy, context grouping, and output formatting where those components are genuinely shared.
- [ ] Consume the transitional regular-expression, text-record, and read-only traversal contracts currently housed in `Icod.CoreUtils.Shared`, without moving grep-specific behavior into that library; migrate the cross-suite dependency to `Icod.CommandFramework` after the final framework extraction.
- [ ] Record GNU grep 3.12 as the initial authoritative baseline.
- [ ] Preserve the required grep status model: 0 for selected lines, 1 for no selected lines, and 2 for errors.
- [ ] Remove `grep` from CoreUtils packaging, command inventories, and release workflows.
- [ ] Add a migration note identifying `Icod.Grep` as the command's new home.

Implementation and conformance work for `grep` continues under the `Icod.Grep` roadmap. Completion of this milestone means that ownership has been transferred cleanly; it does not mean that GNU grep compatibility is complete.

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

### Batch 30 — Diffutils extraction milestone

- [ ] Remove `diff` from the Icod.CoreUtils solution and repository.
- [ ] Transfer the existing `diff` implementation and history to Icod.DiffUtils.
- [ ] Establish the Icod.DiffUtils solution and CI conventions.
- [ ] Add projects for `cmp`, `diff`, `diff3`, and `sdiff`.
- [ ] Establish Icod.DiffUtils.Shared for suite-specific comparison,
      differencing, merging, and output-format infrastructure.
- [ ] Record GNU Diffutils 3.12 as the initial authoritative baseline.
- [ ] Establish textual compatibility fixtures for `Icod.Patch` and `Icod.Ed`, including normal, context, unified, and ed-script output where applicable.
- [ ] Keep `Icod.DiffUtils` independent of `Icod.Patch` and `Icod.Ed` at runtime.

Implementation and conformance work for `cmp`, `diff`, `diff3`, and `sdiff`
continues under the Icod.DiffUtils roadmap. Completion of this milestone means
that ownership has been transferred cleanly; it does not mean that the four
Diffutils commands are complete.

### Batch 31 — GNU patch extraction milestone

- [ ] Remove `patch` from the Icod.CoreUtils solution and repository.
- [ ] Transfer the existing `patch` implementation, tests, and relevant history to `Icod.Patch`.
- [ ] Establish the `Icod.Patch` solution, three-runner CI, configuration, documentation, and packaging conventions.
- [ ] Establish `Icod.Patch.Shared` for patch-format parsing, hunk application, fuzz and offset matching, reversal detection, reject generation, backups, and transactional application behavior where those components are genuinely shared.
- [ ] Record GNU patch 2.8 as the initial authoritative baseline.
- [ ] Establish checked-in normal, context, and unified patch corpora produced by GNU Diffutils 3.12.
- [ ] Establish cross-repository compatibility tests using `Icod.DiffUtils` output.
- [ ] Keep the production dependency boundary textual: `Icod.Patch` must not require `Icod.DiffUtils.Shared` merely to consume patch files.
- [ ] Plan to consume the transactional replacement contracts published by Completion Gate E6 when the full patch engine is implemented.
- [ ] Remove `patch` from CoreUtils packaging, command inventories, and release workflows.
- [ ] Add a migration note identifying `Icod.Patch` as the command's new home.

Implementation and conformance work for `patch` continues under the `Icod.Patch` roadmap. Completion of this milestone means that ownership has been transferred cleanly; it does not mean that GNU patch compatibility is complete.

### Batch 32 — GNU ed extraction milestone

- [ ] Remove `ed` from the Icod.CoreUtils solution and repository.
- [ ] Transfer the existing `ed` implementation, tests, and relevant history to `Icod.Ed`.
- [ ] Establish the `Icod.Ed` solution, three-runner CI, configuration, documentation, and packaging conventions.
- [ ] Add command projects for `ed` and `red`.
- [ ] Establish `Icod.Ed.Shared` for editor-specific address parsing, command parsing, mutable line buffers, marks, substitutions, global commands, undo state, file operations, shell-command integration, and restricted-mode enforcement.
- [ ] Record GNU ed 1.22.5 as the initial authoritative baseline.
- [ ] Consume the transitional regular-expression and text contracts currently housed in `Icod.CoreUtils.Shared` without moving editor state into that library; migrate the cross-suite dependency to `Icod.CommandFramework` after the final framework extraction.
- [ ] Establish textual compatibility tests for ed scripts emitted by GNU Diffutils and `Icod.DiffUtils`.
- [ ] Plan to consume published process and transactional replacement contracts when the full editor engine is implemented.
- [ ] Remove `ed` from CoreUtils packaging, command inventories, and release workflows.
- [ ] Add a migration note identifying `Icod.Ed` as the command's new home.

Implementation and conformance work for `ed` and `red` continues under the `Icod.Ed` roadmap. Completion of this milestone means that ownership has been transferred cleanly; it does not mean that GNU ed compatibility is complete.

### Repository extraction milestone — Icod.Sed

This milestone does not alter command-batch numbering. It preserves completed historical Batch 2 while moving the command to the repository that matches its upstream ownership and state-machine architecture.

- [ ] Remove `sed` from the Icod.CoreUtils solution and repository while preserving Batch 2 as the historical implementation record.
- [ ] Transfer the existing `sed` implementation, tests, and relevant history to `Icod.Sed`.
- [ ] Establish the `Icod.Sed` solution, three-runner CI, configuration, documentation, and packaging conventions.
- [ ] Establish an internal engine or Shared project for the sed program parser, address model, pattern and hold spaces, substitution engine, branching, command cycle, and in-place editing where that separation improves reuse or testability.
- [ ] Record the pinned GNU sed release as the initial authoritative baseline.
- [ ] Consume transitional regular-expression, text-record, temporary-file, and filesystem contracts currently housed in `Icod.CoreUtils.Shared`; migrate genuinely cross-suite dependencies to `Icod.CommandFramework` after the final framework extraction.
- [ ] Keep sed-specific execution state out of `Icod.CoreUtils.Shared` and `Icod.CommandFramework`.
- [ ] Remove `sed` from CoreUtils packaging, command inventories, and release workflows.
- [ ] Add a migration note identifying `Icod.Sed` as the command's new home.

Completion of this milestone means ownership of the already implemented `sed` command has transferred cleanly. Further GNU sed conformance continues under the `Icod.Sed` roadmap.

### Repository extraction milestone — Icod.ProcPs

This milestone does not alter command-batch numbering. It is placed with the other repository-boundary work so that completed historical Batch 9 does not need to be reopened or renumbered.

- [ ] Remove `ps` from the Icod.CoreUtils solution and repository while preserving Batch 9 as the historical implementation record.
- [ ] Transfer the existing `ps` implementation, tests, and relevant history to `Icod.ProcPs`.
- [ ] Establish the `Icod.ProcPs` solution, three-runner CI, configuration, documentation, and packaging conventions.
- [ ] Establish `Icod.ProcPs.Shared` for process enumeration, `/proc` and platform-provider abstractions, selection, snapshots, field definitions, sorting, personality profiles, terminal association, CPU and memory calculations, and platform-specific process metadata shared by procps-ng tools.
- [ ] Record procps-ng 4.0.6 as the initial authoritative baseline.
- [ ] Inventory every command installed by the pinned procps-ng baseline and create a dedicated `Icod.ProcPs` roadmap covering the complete suite rather than only `ps`.
- [ ] Define and document the Linux procps-ng compatibility profile and controlled Windows, macOS, and BSD capability boundaries.
- [ ] Consume transitional command, terminal, signal, and platform contracts currently housed in `Icod.CoreUtils.Shared`; migrate genuinely cross-suite dependencies to `Icod.CommandFramework` after the final framework extraction.
- [ ] Remove `ps` from CoreUtils packaging, command inventories, and release workflows.
- [ ] Add a migration note identifying `Icod.ProcPs` as the command's new home.

Completion of this milestone means ownership of the already implemented `ps` command has transferred cleanly and the destination repository has adopted the full procps-ng family as its planned scope. Implementation and conformance of the remaining procps-ng commands continue under the `Icod.ProcPs` roadmap.

### Completion Gate E2 — before Batch 33

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

### Batch 33 — Symbolic-link and canonical-path resolution (2 tools)

- [ ] `readlink`
- [ ] `realpath`

Implement lexical versus physical resolution, missing-component policies, canonicalization modes, delimiters, quiet/verbose behavior, relative output, symlink loops, reparse points, and deterministic failures. Never return the unresolved input as a false success.

### Completion Gate E3 — before Batch 34

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

### Batch 34 — File metadata and timestamps (2 tools)

- [ ] `stat`
- [ ] `touch`

Build the authoritative metadata adapter and format-string engine. Distinguish access, modification, inode-change, and birth times where available; expose controlled platform gaps; support dereference policies, filesystems, reference files, date parsing, selective timestamps, no-create, and directories.

### Batch 35 — Condition evaluator (1 tool)

- [ ] `test`

Implement the complete GNU/POSIX operand-count grammar, file type and characteristic predicates, access checks, string and numeric comparisons, connectives, precedence, ambiguity rules, and statuses 0, 1, and 2. **Do not create a separate `[` project.**

### Completion Gate E4 — before Batch 36

* [ ] Add shared mode and basic pathname-mutation infrastructure:

  * [ ] numeric mode parsing;
  * [ ] symbolic mode-clause parsing;
  * [ ] umask application;
  * [ ] basic directory, file, link, FIFO, and device-node capability providers;
  * [ ] no-follow and dereference policies;
  * [ ] race-aware single-path mutation;
  * [ ] controlled privilege and platform diagnostics.

This gate supports `mkdir`, `rmdir`, `unlink`, `link`, `ln`, `mkfifo`, `mknod`, and the later permission commands.

### Batch 36 — Basic directory and name removal (3 tools)

- [ ] `mkdir`
- [ ] `rmdir`
- [ ] `unlink`

Implement modes, parents, verbose/context policy, ignore-fail behavior, parent removal, exact operand rules, and deterministic handling of files versus directories. These commands validate the new filesystem adapter without yet introducing recursive deletion.

### Batch 37 — Hard and symbolic links (2 tools)

- [ ] `link`
- [ ] `ln`

Make `link` the documented two-operand hard-link command. Build `ln` as a separate front end over shared link primitives, covering symbolic/physical/logical behavior, targets, directories, relative links, backups, force/interactive modes, and platform capability diagnostics. Do not invoke native `ln`.

### Batch 38 — Special file creation (2 tools)

- [ ] `mkfifo`
- [ ] `mknod`

Add the missing GNU projects. Implement modes, FIFO creation, block/character device operands, major/minor validation, umask behavior, and controlled privilege/platform failure. Never emulate success by creating an ordinary file.

### Completion Gate E5 — before Batch 39

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

### Batch 39 — Permission modes (1 tool)

- [ ] `chmod`

Implement octal parsing correctly, symbolic clauses, omitted-who/umask behavior, recursive traversal, reference mode, symlink policy, preserve-root, verbose/change reporting, and Windows capability mapping without pretending that the read-only attribute is a complete Unix mode.

### Batch 40 — Ownership and group mutation (2 tools)

- [ ] `chown`
- [ ] `chgrp`

Replace `NotImplementedException` with real Unix ownership operations and controlled non-Unix diagnostics. Implement names and numeric IDs, reference files, dereference policies, recursive traversal, from-filtering, preserve-root, and verbose/change reporting.

### Batch 41 — Recursive removal (1 tool)

- [ ] `rm`

Use the shared traversal engine. Implement interactive modes, recursive directory handling, force, one-file-system, preserve-root, empty-directory removal, symlink safety, write-protected prompts, race-aware deletion, glob expansion policy, and error continuation.

### Completion Gate E6 — before Batch 42

* [ ] Add shared transactional file-replacement infrastructure:

  * [ ] secure sibling temporary files;
  * [ ] atomic replacement where supported;
  * [ ] backup-name generation and retention policy;
  * [ ] rollback behavior after partial failure;
  * [ ] pathname-containment and escape checks;
  * [ ] deterministic cleanup after success, failure, and cancellation;
  * [ ] explicit diagnostics where atomic replacement is unavailable;
  * [ ] integration with the recursive traversal and metadata-preservation contracts established by Completion Gate E5.

This gate is placed immediately before `cp` and `mv`, the first remaining Coreutils/Fileutils commands that require the complete replacement, backup, and rollback model. The cross-suite contracts are candidates for `Icod.CommandFramework` and may then be consumed by `Icod.Patch`, `Icod.Ed`, `Icod.Sed`, and `Icod.Tar` without reverse dependencies.

### Batch 42 — Copy and move engine (2 tools)

- [ ] `cp`
- [ ] `mv`

Implement source/destination classification, recursive copy, symlink and hard-link policy, metadata preservation, sparse files, reflink/copy-file-range opportunities, backup and overwrite modes, update rules, atomic replacement, cross-filesystem moves, destination-inside-source prevention, and partial-failure cleanup.

### Batch 43 — Installation engine (1 tool)

- [ ] `install`

Build on `mkdir`, `cp`, `chmod`, and `chown` primitives rather than invoking external utilities. Implement directory creation, modes, owners/groups, stripping policy, backups, compare mode, timestamps, SELinux-context policy, and atomic destination replacement.

### Completion Gate F1 — before Batch 44

* [ ] Add shared terminal-aware presentation capabilities:

  * [ ] terminal-versus-redirected stream detection;
  * [ ] terminal width and height discovery;
  * [ ] color-capability policy;
  * [ ] quoting and control-character presentation policy;
  * [ ] environment and terminal-name inputs used by `dircolors`;
  * [ ] injectable providers for deterministic tests;
  * [ ] controlled fallback when terminal information is unavailable.

This gate provides only the presentation capabilities needed by `dircolors`, `ls`, `dir`, and `vdir`.

### Batch 44 — Color database and directory listing family (4 tools)

- [ ] `dircolors`
- [ ] `ls`
- [ ] `dir`
- [ ] `vdir`

Implement the documented `dircolors` database grammar, terminal selectors, file-extension rules, shell-specific output, built-in database, print-database mode, and diagnostics. Produce a reusable `LS_COLORS` parser for the listing engine.

Create one listing engine with three thin entry profiles. Implement locale sorting, quoting, color, columns, widths, recursion with cycle protection, symlink policy, inode/block/owner/group/mode metadata, human sizes, time styles, indicators, classification, dereference modes, and terminal-sensitive defaults. Remove independent simplified `dir` and `vdir` implementations.

### Batch 45 — Filesystem usage reporting (2 tools)

- [ ] `df`
- [ ] `du`

Use real allocated-block and filesystem data where available. Implement block-size environment rules, human/SI formats, inode reporting, filesystem types, exclusions, totals, apparent size, hard-link deduplication, symlink and mount policies, depth and summarize modes, NUL input, and controlled platform differences.

### Batch 46 — Data destruction (1 tool)

- [ ] `shred`

Implement pass selection, random sources, exact-size handling, synchronization, removal and renaming policy, device/file distinctions, progress, and failure recovery. Document and test the limits of overwriting on SSDs, copy-on-write filesystems, snapshots, journaling, and remapped storage.

### Batch 47 — GNU tar extraction milestone

- [ ] Remove `tar` from the Icod.CoreUtils solution and repository.
- [ ] Transfer the existing `tar` implementation, tests, and relevant history to `Icod.Tar`.
- [ ] Establish the `Icod.Tar` solution, three-runner CI, configuration, documentation, and packaging conventions.
- [ ] Establish an internal archive engine or `Icod.Tar.Shared` where useful for archive formats, entry models, sparse-file behavior, compression integration, selection and exclusion rules, incremental behavior, and extraction policy.
- [ ] Record the pinned GNU tar release as the initial authoritative baseline.
- [ ] Consume the mature traversal, metadata, temporary-workspace, process, and transactional-replacement contracts developed by the preceding Coreutils/Fileutils batches; migrate genuinely cross-suite contracts to `Icod.CommandFramework` after the final framework extraction.
- [ ] Establish adversarial extraction tests covering absolute paths, `..`, symlink escapes, hard-link escapes, device creation, metadata restoration, and overwrite races.
- [ ] Keep archive-format and archive-state behavior in `Icod.Tar`, not in `Icod.CoreUtils.Shared` or `Icod.CommandFramework`.
- [ ] Remove `tar` from CoreUtils packaging, command inventories, and release workflows.
- [ ] Add a migration note identifying `Icod.Tar` as the command's new home.

`tar` is deliberately extracted only after canonical path resolution, metadata, timestamps, directory and link mutation, permissions, ownership, recursive traversal, copy/move, installation, listing, filesystem accounting, secure temporary storage, process integration, and transactional replacement have matured. Full GNU tar implementation and conformance continue under the `Icod.Tar` roadmap.

### Completion Gate F2 — before Batch 48

* [ ] Add shared host and processor-information capabilities:

  * [ ] host-identifier retrieval and normalization;
  * [ ] configured, online, and currently available processor counts;
  * [ ] processor-affinity awareness;
  * [ ] container and quota awareness where available;
  * [ ] command-specific environment overrides;
  * [ ] controlled and documented platform differences.

This gate directly supports `hostid` and `nproc`.

### Batch 48 — Host and processor context (2 tools)

- [ ] `hostid`
- [ ] `nproc`

Add the missing projects. Define reproducible host-ID behavior and implement available/configured processor counts, environment overrides, affinity and quota awareness, and controlled platform differences.

### Completion Gate F3 — before Batch 49

* [ ] Add shared terminal-identification and terminal-control capabilities:

  * [ ] terminal pathname discovery;
  * [ ] terminal attachment inspection for selected file descriptors;
  * [ ] terminal-mode retrieval and mutation;
  * [ ] input and output speed reporting;
  * [ ] control-character representation;
  * [ ] machine-readable mode serialization and restoration;
  * [ ] explicit Unix and Windows capability boundaries.

This gate supports `tty` and `stty` without requiring child-process or signal infrastructure prematurely.

### Batch 49 — Terminal identification (1 tool)

- [ ] `tty`

Add the missing project. Implement silent mode, terminal-name reporting, correct standard-input inspection, and statuses for terminal versus nonterminal input across supported platforms.

### Batch 50 — Terminal characteristics (1 tool)

- [ ] `stty`

Add the missing project as a dedicated platform batch. Implement reading and changing terminal modes, sane/raw profiles, control characters, speed, machine-readable save/restore form, selected device handling, and a documented Windows capability boundary.

### Completion Gate F4 — before Batch 51

* [ ] Add shared child-process and signal primitives:

  * [ ] executable lookup;
  * [ ] argument-safe process launching without shell interpolation;
  * [ ] working-directory and environment construction;
  * [ ] asynchronous standard-stream forwarding;
  * [ ] cancellation and child-process cleanup;
  * [ ] signal-name and signal-number parsing;
  * [ ] signal listing and translation;
  * [ ] signal-disposition control required by `nohup`;
  * [ ] process and process-group targeting;
  * [ ] child termination and exit-status translation;
  * [ ] controlled Windows substitutions where semantics are defensible.

This gate supports `env` and `nohup`, allows `kill` to validate the signal layer in Batch 52, and allows `nice` and `timeout` to reuse it in Batch 53.

### Batch 51 — Environment and hangup-independent execution (2 tools)

- [ ] `env`
- [ ] `nohup`

Build the shared child-process launch environment. Implement environment clearing/removal, split-string parsing, working directory, `argv0`, signal policy, NUL output, command lookup, `nohup` redirection rules, diagnostics, and asynchronous stream forwarding.

### Batch 52 — Signal control (1 tool)

* [ ] `kill`

Implement signal-name and signal-number parsing, signal listing and translation, process and process-group targets, queued values where supported and in scope, exact diagnostics and exit statuses, and Windows substitutions only where they are semantically defensible.

### Batch 53 — Priority and time-bounded execution (2 tools)

* [ ] `nice`
* [ ] `timeout`

Implement priority adjustment without child-start races. Parse the complete duration grammar and support signal selection, kill-after behavior, foreground and process-group handling, status preservation, verbose diagnostics, exact exit-status propagation, and explicit platform-capability handling.

### Batch 54 — Root-directory execution (1 tool)

- [ ] `chroot`

Replace `NotImplementedException` with a real Unix implementation and controlled diagnostics elsewhere. Implement users/groups, group initialization, skip-chdir policy, command lookup after root change, privileges, and process execution without shell interpolation.

### Batch 55 — SELinux context operations (2 tools)

- [ ] `chcon`
- [ ] `runcon`

Treat these as Linux/SELinux capability commands. Use native APIs or stable libraries rather than invoking external commands. Implement reference and component contexts, dereference and recursion policy, preserve-root, compute/process context behavior, and explicit diagnostics when SELinux is unavailable.

### Batch 56 — Standard-stream buffering control (1 tool)

- [ ] `stdbuf`

Begin with a documented feasibility decision. The current silent fallback is unacceptable. Implement supported preload/native-shim semantics where reliable; otherwise report controlled unsupported behavior for affected commands and platforms. Test child startup, environment injection, buffering modes, and exit-status propagation.

### Completion Gate G — final shared-library classification and Icod.CommandFramework extraction

This gate is deliberately last. The purpose is to extract the framework after the command suites have supplied enough real consumers to reveal stable boundaries.

- [ ] Inventory every public, protected, and internal API currently in `Icod.CoreUtils.Shared` and record its actual consumers.
- [ ] Classify each API as:
  - [ ] cross-suite and suitable for `Icod.CommandFramework`;
  - [ ] shared only by Coreutils/Fileutils/Textutils and suitable for `Icod.CoreUtils.Shared`;
  - [ ] shared only within another suite and suitable for that suite's Shared library;
  - [ ] command-local and unsuitable for a public shared package.
- [ ] Review namespace design, accessibility, XML documentation, binary compatibility, trimming/AOT behavior, native ABI boundaries, and package dependency direction before freezing public contracts.
- [ ] Create the `Icod.CommandFramework` solution and repository with independent Windows, Ubuntu, and macOS CI.
- [ ] Publish `Icod.CommandFramework` as a versioned NuGet package with symbols, SourceLink, deterministic builds, package documentation, and a Semantic Versioning policy.
- [ ] Move only demonstrated cross-suite functionality into `Icod.CommandFramework`.
- [ ] Retain or create `Icod.CoreUtils.Shared` for Coreutils/Fileutils/Textutils-only reuse, make it depend on `Icod.CommandFramework` rather than duplicating framework behavior, and publish it as its own versioned NuGet package.
- [ ] Convert individual `Icod.CoreUtils` command projects to `PackageReference` dependencies on the published `Icod.CoreUtils.Shared` binary rather than a source-tree project reference.
- [ ] Convert cross-repository consumers—including `Icod.DiffUtils`, `Icod.Grep`, `Icod.Patch`, `Icod.Ed`, `Icod.Sed`, `Icod.Tar`, and `Icod.ProcPs`—to versioned `PackageReference` dependencies on `Icod.CommandFramework`.
- [ ] Retain `ProjectReference` within each suite repository for its own Shared or engine projects unless a separate package boundary is independently justified.
- [ ] Eliminate circular dependencies and ensure `Icod.CommandFramework` has no production dependency on any command suite.
- [ ] Build and test every consuming repository against the published NuGet binaries rather than unpublished source-tree references.
- [ ] Publish an architecture and migration document explaining the final package boundaries and replacement of transitional `Icod.CoreUtils.Shared` dependencies.

Completion of this gate establishes `Icod.CommandFramework` as the neutral cross-platform command foundation. It does not require `Icod.CoreUtils.Shared` to disappear; that package remains appropriate for behavior genuinely specific to the Coreutils, Fileutils, and Textutils family.

## Why the tools are scheduled this way

* `Icod.CoreUtils` retains GNU Coreutils together with the historical GNU Fileutils and GNU Textutils command families because those packages were merged into GNU Coreutils and now form one natural upstream suite. Repository extraction is therefore based on genuine upstream and architectural boundaries, not on an artificial separation of file or text commands from Coreutils.
* Batches 0 through 10 are preserved as the historical foundation of the project. They establish shared command-line, diagnostics, streaming, numeric, platform, identity, process, date/time, and block-I/O abstractions used by the remaining Coreutils/Fileutils/Textutils commands and by sibling repositories created through later extraction milestones.
* `sed` and `ps` remain recorded in completed Batches 2 and 9 because those are the batches in which they were implemented and stabilized. Their later transfers to `Icod.Sed` and `Icod.ProcPs` change repository ownership without rewriting project history or reopening completed command batches.
* `truncate`, `sync`, and `od` follow `dd` because they exercise closely related raw-file capabilities. Together, these batches establish file sizing, sparse extension, allocation reporting, data and metadata flushing, byte offsets, bounded reads, binary interpretation, and reusable binary formatting before the roadmap moves into higher-level text processing.
* `printf` and `numfmt` are scheduled early because formatted numeric output, escape processing, padding, precision, grouping, and human-readable quantities recur throughout later commands such as `sort`, `stat`, `ls`, `df`, and `du`.
* `mktemp` precedes every remaining command or sibling suite that may require secure temporary storage. It establishes exclusive temporary-file and temporary-directory creation before external sorting, reverse processing, patch and editor work, transactional replacement, and archive testing depend upon temporary workspaces.
* Completion Gate C1 precedes `expr` so the project establishes one documented regular-expression foundation rather than allowing `expr` and `csplit` to develop incompatible matching behavior. Its cross-suite portions are candidates for eventual extraction into `Icod.CommandFramework` for `Icod.Grep`, `Icod.Sed`, and `Icod.Ed`, while suite-specific state remains outside the framework.
* Completion Gate C2 introduces byte, Unicode-scalar, display-column, and tab-stop behavior immediately before `expand`, `unexpand`, and `fold`, the first remaining commands that require those distinctions. `fmt`, `nl`, and the later `pr` batch then reuse the same display-width and line-layout model.
* Completion Gate C3 introduces record delimiters, range grammars, field selection, separators, and escape processing immediately before `cut` and `paste`. Those primitives are then reused by `tr`, `sort`, `split`, and related Coreutils/Textutils commands. Genuinely cross-suite record contracts are candidates for `Icod.CommandFramework` and can then be consumed by `Icod.Grep` and `Icod.Sed`.
* Completion Gate D remains a single gate because locale collation, sort-key comparison, bounded-memory run generation, stable external merging, temporary-workspace management, and cancellation cleanup form one cohesive external-ordering engine. `sort` is the first command to validate that engine.
* `shuf` follows `sort` because it can reuse the established record and temporary-storage infrastructure, while remaining a separate batch because unbiased random selection and permutation are not sorting operations.
* `comm`, `join`, and `uniq` follow `sort` so they consume the same collation, ordering, record, and comparison rules. This avoids subtle disagreements about whether inputs are ordered or whether adjacent records and join keys are equal.
* `tr`, `tsort`, and `ptx` are kept in separate batches because character-set transformation, graph ordering, and permuted indexing are distinct execution engines. They are nevertheless scheduled after the shared text, locale, tokenization, ordering, and spill-storage primitives they can reuse.
* Completion Gate E1 establishes safe read-only traversal before the `Icod.Grep` extraction milestone. This gives the new repository a stable recursive enumeration, symlink, cycle, include/exclude, mount-boundary, and error-continuation contract. The same work supports Coreutils directory listing and filesystem accounting, recursive directory comparison in `Icod.DiffUtils`, and archive traversal in `Icod.Tar`; its cross-suite portions are later extracted into `Icod.CommandFramework`.
* Batch 26 transfers `grep` to `Icod.Grep` after the regular-expression, record, and read-only traversal foundations are available. Grep belongs to its own upstream package and has a dedicated search engine, option model, binary-input policy, recursive-selection model, context grouping, and exit-status contract. Those search-specific concerns remain in `Icod.Grep`; only demonstrated cross-suite contracts later move to `Icod.CommandFramework`.
* `split` and `tac` follow secure temporary storage and the shared record model because both require bounded-memory handling of potentially unbounded input. `csplit` follows separately because its pattern-address grammar and transactional output-file behavior are materially more complex. It consumes Completion Gate C1 directly and does not depend on `Icod.Grep`.
* `pr` follows the earlier display-column and formatting batches so page geometry, columns, headers, footers, separators, and numbering can reuse established width and layout behavior without being coupled to the unrelated binary-formatting engine used by `od`.
* Batch 30 transfers `cmp`, `diff`, `diff3`, and `sdiff` to `Icod.DiffUtils`, matching the GNU Diffutils package boundary. Comparison, hunk construction, two-way and three-way merging, and difference-output behavior share `Icod.DiffUtils.Shared`; compatibility with patch consumers and ed scripts is tested through textual formats rather than runtime dependencies.
* Batch 31 transfers `patch` to `Icod.Patch`. Although `patch` consumes formats emitted by `diff`, it is a transactional file-mutation engine with its own upstream package, parser, fuzz and offset rules, reject handling, backup policy, and security boundary. Its production contract with `Icod.DiffUtils` remains textual.
* Batch 32 transfers `ed` to `Icod.Ed` and adds `red` as the restricted companion profile over the same editor engine. Address parsing, mutable buffers, editor state, substitutions, global commands, undo, file semantics, shell integration, and restricted-mode enforcement belong in the editor repository rather than CoreUtils.
* The `Icod.Sed` extraction milestone transfers the completed stream editor to the repository matching GNU sed's independent upstream package and substantial parser/state-machine architecture. Sed-specific address, pattern-space, hold-space, branching, and command-cycle behavior does not belong in Coreutils Shared or the neutral framework.
* The `Icod.ProcPs` extraction milestone transfers the already completed `ps` implementation without changing Batch 9 history and establishes the destination repository for the complete procps-ng family. Process enumeration, `/proc` parsing, selection, personalities, and process-specific presentation belong in `Icod.ProcPs.Shared`; only cross-suite process and platform contracts later move to `Icod.CommandFramework`.
* Completion Gate E2 introduces canonical-path and symbolic-link resolution immediately before `readlink` and `realpath`, allowing those commands to validate lexical normalization, physical resolution, missing-component policy, loop detection, and platform root semantics.
* Completion Gate E3 follows canonical-path resolution and precedes `stat`, `touch`, and `test`. This establishes one authoritative model for file types, sizes, ownership, modes, timestamps, links, allocated blocks, filesystem information, and unavailable platform metadata.
* `test` follows `stat` and `touch` because its file predicates depend upon the metadata model those commands first exercise. It remains separate from `expr` because operand-count parsing, predicate evaluation, ambiguity rules, and exit statuses constitute a different language. No separate `[` project is required.
* Completion Gate E4 introduces mode parsing, umask behavior, link creation, directory creation, FIFO and device-node capabilities, dereference policy, and race-aware single-path mutation before the basic filesystem-mutation batches.
* `mkdir`, `rmdir`, and `unlink` validate basic pathname mutation before link creation and special-file creation add more platform-specific behavior. `link` and `ln` then share link primitives while retaining their different command-line contracts. `mkfifo` and `mknod` follow once mode, privilege, and platform-capability handling are established.
* Completion Gate E5 extends the read-only traversal model into a mutation-safe recursive engine before recursive permissions, ownership changes, deletion, copying, and moving begin. It adds preserve-root protection, mount boundaries, no-follow operations, hard-link identity, sparse-file handling, metadata preservation, destination-inside-source detection, and partial-failure cleanup.
* `chmod` precedes `chown` and `chgrp` so numeric and symbolic mode handling is completed before the roadmap proceeds to the more platform- and privilege-dependent ownership operations.
* `rm` follows the recursive mutation gate but precedes copying so deletion safety, preserve-root behavior, prompting, symlink handling, and error continuation can be validated independently from destination creation and metadata preservation.
* Completion Gate E6 appears immediately before `cp` and `mv`, the first remaining Coreutils/Fileutils commands that require the complete secure-sibling-temporary-file, backup, rollback, pathname-containment, and atomic-replacement model. Its cross-suite contracts are candidates for `Icod.CommandFramework`, allowing `Icod.Patch`, `Icod.Ed`, `Icod.Sed`, and `Icod.Tar` to reuse them without reverse dependencies.
* `cp` and `mv` share source/destination classification, overwrite and backup policy, recursive traversal, metadata preservation, sparse-file handling, hard-link tracking, atomic replacement, and cross-filesystem behavior. They precede `install`, which deliberately builds on the completed directory, copy, mode, ownership, timestamp, backup, and replacement primitives.
* Completion Gate F1 introduces only the terminal-aware presentation capabilities needed by `dircolors`, `ls`, `dir`, and `vdir`. This avoids implementing unrelated host, terminal-control, child-process, and signal facilities prematurely.
* `dircolors` is grouped with the directory-listing family because it produces the `LS_COLORS` model consumed by the shared listing engine. `ls`, `dir`, and `vdir` are treated as thin command profiles over one implementation so their sorting, quoting, metadata, recursion, color, width, and terminal-sensitive behavior cannot drift apart.
* `df` and `du` follow the filesystem metadata and traversal work because they depend upon real filesystem statistics, allocated-block accounting, mount policies, hard-link identity, block-size rules, and human-readable numeric formatting.
* `shred` is isolated because destructive overwrite semantics, storage-device limitations, synchronization, renaming, and removal policy require a focused safety and capability review. Its position before `tar` is organizational rather than a dependency: the archive engine does not depend upon data-destruction behavior.
* Batch 47 transfers `tar` to `Icod.Tar` only after nearly the entire filesystem foundation has matured: canonical paths, safe traversal, file types, links, sparse files, modes, ownership, timestamps, copying, temporary storage, compression-process integration, and transactional replacement. GNU tar is an independent upstream package and a large archive/security engine, so archive formats, entry state, and extraction policy belong in `Icod.Tar`, while reusable cross-suite infrastructure later moves to `Icod.CommandFramework`.
* Completion Gate F2 introduces host and processor information immediately before `hostid` and `nproc`, avoiding premature coupling to terminal or child-process behavior. Cross-suite host and processor capability contracts are candidates for `Icod.CommandFramework` and may later be consumed by `Icod.ProcPs`.
* Completion Gate F3 introduces terminal identification and terminal-mode control immediately before `tty` and `stty`. These commands remain separate because identifying whether a stream is attached to a terminal is substantially simpler than reading, serializing, and mutating terminal characteristics.
* Completion Gate F4 establishes command lookup, argument-safe process launch, environment construction, asynchronous stream forwarding, process cleanup, signal translation, process groups, and exit-status handling before the remaining process-control commands. Cross-suite process primitives are candidates for `Icod.CommandFramework` and may support `Icod.Ed`, `Icod.Sed`, `Icod.Tar`, and `Icod.ProcPs` without transferring suite-specific behavior into the framework.
* `env` and `nohup` are the first CoreUtils consumers of the shared child-process layer because they establish environment construction, command lookup, redirection, signal disposition, and stream forwarding.
* `kill` follows as the dedicated validator of signal names, signal numbers, listing, translation, process targets, process groups, and platform substitutions. It intentionally precedes `timeout` so timeout handling can reuse an already tested signal-control layer.
* `nice` and `timeout` are grouped because both alter the conditions under which a child process executes. They share race-free child startup, process-group handling, status propagation, and platform-capability reporting, while adding priority adjustment and time-bounded termination respectively.
* `chroot`, `chcon`, and `runcon` are scheduled near the end because they require mature child-process, identity, privilege, filesystem, and platform-capability abstractions and have substantial Unix- or Linux-specific security implications.
* `stdbuf` is last because its defining behavior may require a native preload library or platform-specific shim that cannot be implemented portably through ordinary managed process APIs. By this point, child startup, environment injection, stream forwarding, diagnostics, and exit-status propagation will already be established, allowing the remaining feasibility decision to focus narrowly on buffering control.
* Completion Gate G is deliberately deferred until the end. The remaining command and extraction work supplies real consumer evidence, allowing `Icod.CommandFramework` to be carved out on stable architectural lines while `Icod.CoreUtils.Shared` retains only Coreutils/Fileutils/Textutils-specific reuse.
* Complex parsers, state machines, security boundaries, platform-specialized commands, and repository extractions are intentionally isolated. Commands are grouped only where they share a real execution engine or directly validate the same new infrastructure, rather than merely because their traditional descriptions appear related.

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
15. For an extraction milestone, verify history transfer, namespace and project renaming, destination CI, package ownership, transitional Shared dependencies, migration documentation, and public-format compatibility before removing the source project.
16. For Completion Gate G, verify every consuming repository against the published `Icod.CommandFramework` NuGet package before declaring the architecture stable.

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
- extraction milestones leave no stale source, solution, packaging, CI, or inventory references in CoreUtils and establish green destination-repository CI;
- Completion Gate G leaves `Icod.CommandFramework` free of suite dependencies, preserves `Icod.CoreUtils.Shared` only where Coreutils/Fileutils/Textutils-specific reuse remains, and verifies all consumers against published packages.
