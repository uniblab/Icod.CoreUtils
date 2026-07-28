# Icod.CoreUtils Audit and Refactor Roadmap

## Living status

| Item | Status |
|---|---|
| Completed command batches | Batches 0–11 |
| Current engineering gate | Planned Gate C |
| Next command batch | Batch 12 — `sync` |
| Current target framework | `net10.0` |
| Required CI runners | `windows-latest`, `ubuntu-latest`, `macos-latest` |
| Next infrastructure dependency | Completion Gate C — before Batch 16 |
| Status-maintenance rule | Update this table after every merged batch |

## Scope

The repository is a broader BSD/Linux utility collection rather than only GNU Coreutils:

- At the July 27, 2026 audit, **104 command projects** existed, plus `Shared`.
- At that audit, the solution contained **37 test/support projects** under `tests`, including `Shared.Tests` and `ProcessTestHost`.
- Seven non-Coreutils programs remain in scope: `diff`, `ed`, `grep`, `patch`, `ps`, `sed`, and `tar`.
- Ten GNU Coreutils programs are not yet present and should be added: `hostid`, `mkfifo`, `mknod`, `mktemp`, `nohup`, `nproc`, `numfmt`, `printf`, `stty`, and `tty`.
- No separate `[` project will be added.
- On completion, the planned command-project count is **114**.

For GNU Coreutils commands, the conformance baseline is the pinned GNU Coreutils manual and source. For non-Coreutils programs, use the corresponding upstream project as the primary authority:

| Program family | Primary authority |
|---|---|
| GNU Coreutils commands | GNU Coreutils manual and source |
| `sed` | GNU sed |
| `grep` | GNU grep |
| `diff` | GNU Diffutils |
| `patch` | GNU patch |
| `ed` | GNU ed |
| `tar` | GNU tar |
| `ps` | procps-ng, with an explicitly documented portability profile |

Man7 pages are useful synopses and secondary references, but they must not replace the authoritative upstream manual.

## Current repository audit

### What is working well

- The completed batches established shared command-line, diagnostics, streaming, numeric, platform, identity, process, date/time, and block-I/O abstractions.
- Batches 1 through 10 have command-specific tests.
- The complete test suite has been exercised on Windows, Ubuntu, and macOS during Batch 10 stabilization.
- Source projects consistently reference `Shared` where common behavior is appropriate.
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
   - `diff` is not a complete difference algorithm and does not yet implement the required result-status model.
   - `patch` handles a private simplified format rather than normal, context, and unified patches.
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

1. 
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
5. Commands use `CommandContext`, the Shared option/argument processor, Shared diagnostics, and injectable providers where the behavior is platform- or environment-dependent.
6. Every command exposes cancellation-aware `RunAsync(..., CancellationToken)`, retains a synchronous compatibility wrapper, and uses an asynchronous `Main` where appropriate. Naturally asynchronous I/O and child-process waits use TAP directly rather than `Task.Run`.
7. Literal newline escapes such as `\n` and `\r\n` are permitted only when they are part of the utility’s data semantics, escape grammar, or documented byte transformation. They are never used as the host platform’s generated line separator.
8. Generated line endings use `WriteLine`, `WriteLineAsync`, or `Environment.NewLine`. Line-oriented input uses `ReadLine`, `ReadLineAsync`, and `Environment.NewLine` as appropriate. Code must not hard-code `\n` or `\r\n` for host line-reading or line-writing semantics.
9. When multiple strings are sent to `WriteAsync`, `WriteLineAsync`, or related output methods, combine them with `System.String.Concat` rather than the `+` operator.  Similarly, readaing input one should use `ReadLine` or `ReadLineAsync` unless binary operation is required.
10. Each command has its own dedicated xUnit test project following the established `tests/<Tool>.Tests` pattern.
11. Each public command class has class-level XML documentation whose `<summary>` includes the command usage, plus a dedicated usage-printing or usage-writing function.
12. Every new project is added to the solution, all required configuration mappings, the appropriate solution folder, and every local and CI build/test entry point.
13. The supported CI platform targets are explicitly `windows-latest`, `ubuntu-latest`, and `macos-latest`. Platform-specific tests may be conditional, but every runner must build the full solution and execute the complete applicable test suite.

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

## Engineering completion gates

These gates are repository milestones rather than command batches. They do not alter the historical numbering.

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

### Completion Gate B — before Batch 12

[x] Add `Shared.FileSystem` flush and allocation capability abstractions needed by `dd`, `truncate`, and `sync`:

- [x] data-only versus data-and-metadata flush;
- [x] file-specific versus filesystem-wide flush;
- [x] sparse extension and allocated-range capability reporting;
- [x] controlled platform diagnostics where equivalent semantics are unavailable;
- [x] injectable abstractions and platform-specific integration tests.

### Completion Gate C — before Batch 16

[ ] Add the shared text model:

- [ ] byte, Unicode scalar, and display-column iteration;
- [ ] locale-aware collation and character classification;
- [ ] tab-stop grammar;
- [ ] field/range-list grammar;
- [ ] escape-sequence parsing;
- [ ] configurable line and NUL record readers/writers.

### Completion Gate D — before Batch 19

[ ] Add secure temporary-workspace and external-ordering infrastructure:

- [ ] exclusive temporary creation;
- [ ] bounded-memory runs;
- [ ] stable external merge;
- [ ] configurable locale/key comparison;
- [ ] deterministic cleanup on success, failure, and cancellation.

### Completion Gate E — before Batch 28

[ ] Add the shared filesystem model:

- [ ] lexical and physical path resolution;
- [ ] symlink and reparse-point inspection;
- [ ] file identity, type, mode, ownership, timestamps, links, device/inode equivalents, and allocated-block accounting;
- [ ] recursive traversal with cycle detection and mount-boundary policy;
- [ ] sparse-file and metadata-preservation helpers;
- [ ] atomic replacement and backup policy.

### Completion Gate F — before Batch 44

[ ] Add shared system/process primitives:

- [ ] host and processor information;
- [ ] terminal discovery and terminal-mode capability abstractions;
- [ ] signal-name and signal-number parsing;
- [ ] process-group control;
- [ ] child-process stream forwarding;
- [ ] controlled Windows substitutions where semantics are genuinely equivalent.

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

### Batch 10 — Block-oriented copy and conversion (1 tool)

- [x] `dd`

## Revised sequence beginning with Batch 11

### Batch 11 — File-size manipulation (1 tool)

- [x] `truncate`

Implement the complete GNU size operand grammar, `--reference`, `--io-blocks`, creation policy, relative modifiers, rounding modifiers, overflow checks, sparse extension, and precise diagnostics. Reuse the safe size and file-position infrastructure introduced by `dd`.

### Batch 12 — Filesystem flushing (1 tool)

- [ ] `sync`

Replace native-command delegation and no-op success with an explicit platform implementation. Support file-specific data and filesystem flushing where the platform permits it, and produce a controlled diagnostic for semantics that cannot be represented.

### Batch 13 — Formatted and human-readable numeric output (2 tools)

- [ ] `printf`
- [ ] `numfmt`

Create shared format-string, escape, numeric, grouping, padding, precision, and human-suffix components. These will later be reused by `stat`, `sort -h`, `ls -h`, `df`, and `du`.

### Batch 14 — Secure temporary objects (1 tool)

- [ ] `mktemp`

Add secure, exclusive file and directory creation, template validation, `TMPDIR` handling, suffix and directory modes, cleanup tests, and resistance to race and symlink attacks. This infrastructure is required before external sort, reverse processing, diff/patch work, and archive testing.

### Batch 15 — Expression language (1 tool)

- [ ] `expr`

Implement a real precedence-aware expression parser with arithmetic, relations, Boolean operators, string operations, regular expressions, overflow behavior, quoting rules, and GNU exit statuses. Do not pair it with `test`; their grammars and result models are materially different.

### Batch 16 — Tabs and display columns (3 tools)

- [ ] `expand`
- [ ] `unexpand`
- [ ] `fold`

Use the shared display-column and tab-stop model. Cover tab lists, repeated tab intervals, initial/all modes, byte versus character behavior, word-boundary folding, backspaces, carriage returns, wide characters, and invalid multibyte input.

### Batch 17 — Paragraph and line-number formatting (2 tools)

- [ ] `fmt`
- [ ] `nl`

Implement paragraph recognition, sentence spacing, crown/tagged modes, logical-page delimiters, numbering styles, header/body separation, and standard-stream ownership. Share line-layout and display-width components without forcing one combined execution engine.

### Batch 18 — Field and record extraction (2 tools)

- [ ] `cut`
- [ ] `paste`

Implement complete byte/character/field list grammar, complement and output delimiters, NUL records, delimiter suppression, serial paste, delimiter escape cycles, multiple input streams, and correct behavior on multibyte input.

### Batch 19 — External ordering and randomization (2 tools)

- [ ] `sort`
- [ ] `shuf`

For `sort`, implement key specifications, locale collation, stable and unique modes, numeric families, month/version/human-numeric comparison, checking and merging, secure temporary runs, bounded-memory external merge, zero-terminated records, and exact exit statuses. For `shuf`, use unbiased selection, secure/random-source abstractions, repeat and range modes, and bounded-memory strategies. The commands share temporary-storage and record infrastructure but retain separate execution engines.

### Batch 20 — Sorted-stream consumers (3 tools)

- [ ] `comm`
- [ ] `join`
- [ ] `uniq`

Reuse the collation and record model established by `sort`. Preserve duplicate-key Cartesian behavior in `join`, order checking, header and output formatting, field selection, skip/check options, counting, zero records, and streaming without loading complete inputs.

### Batch 21 — Character transformation, graph ordering, and permuted indexing (3 tools)

- [ ] `tr`
- [ ] `tsort`
- [ ] `ptx`

Implement the full `tr` set-expression grammar, including ranges, escapes, repetition, character classes, equivalence classes, complement, delete, squeeze, locale behavior, and delimiter bytes. Implement `tsort` tokenization, deterministic ordering, stable diagnostics, and cycle reporting. Replace the simplified `ptx` token dump with documented input, word, ignore, reference, width, break-file, collation, spill-storage, and output-format behavior. These commands share text, locale, tokenization, and ordering primitives but not one monolithic execution engine.

### Batch 22 — Regular-expression search (1 tool)

- [ ] `grep`

Implement the documented GNU grep option and pattern model, including multiple pattern sources, basic/extended/fixed/Perl-mode policy, recursive traversal, include/exclude rules, binary policy, context, filename and line metadata, counts, quiet/list modes, NUL behavior, and the required 0/1/2 status distinction. Unsupported regex dialect features must be explicit rather than ignored.

### Batch 23 — Splitting and reversing (3 tools)

- [ ] `split`
- [ ] `csplit`
- [ ] `tac`

Repair split-output rotation and support nonseekable input, line/byte/chunk modes, suffix alphabets, filters, additional suffixes, numeric suffixes, and exact file-creation cleanup. Reuse the regex policy established by `grep` for `csplit`, including numeric and regex addresses, offsets, repetition, suppression, prefix/suffix grammar, keep-files behavior, exact byte counts, and cleanup after failure or cancellation. Implement `tac` with backward file scanning or secure temporary spooling rather than whole-input memory loading.

### Batch 24 — Page presentation and binary inspection (2 tools)

- [ ] `pr`
- [ ] `od`

For `pr`, implement columns, page geometry, headers and footers, form feeds, dates, numbering, merge modes, separators, and terminal-independent output. For `od`, build a reusable binary-formatting engine covering address radices, type strings, byte order, duplicate suppression, skip/read limits, string extraction, and nonseekable standard input. The commands share formatting and width infrastructure while retaining separate data engines.

### Batch 25 — Difference engine (1 tool)

- [ ] `diff`

Implement an actual sequence-difference algorithm, normal/context/unified/ed formats, whitespace and case policies, labels, function context, binary handling, recursive directory comparison, absent-file policy, and statuses 0 for no differences, 1 for differences, and greater than 1 for errors.

### Batch 26 — Patch application engine (1 tool)

- [ ] `patch`

Parse normal, context, and unified diffs; implement file selection, strip counts, reversal detection, fuzz, offsets, backups, rejects, dry runs, timestamps, atomic replacement, and safe pathname handling. Test against files produced by the preceding `diff` batch.

### Batch 27 — Line editor (1 tool)

- [ ] `ed`

Complete the address parser, command state machine, global commands, substitutions, marks, buffers, file and shell commands, modified-buffer rules, diagnostics, signals, and exit behavior. Reuse the agreed regex policy but keep the editor state machine isolated.

### Batch 28 — Symbolic-link and canonical-path resolution (2 tools)

- [ ] `readlink`
- [ ] `realpath`

Implement lexical versus physical resolution, missing-component policies, canonicalization modes, delimiters, quiet/verbose behavior, relative output, symlink loops, reparse points, and deterministic failures. Never return the unresolved input as a false success.

### Batch 29 — File metadata and timestamps (2 tools)

- [ ] `stat`
- [ ] `touch`

Build the authoritative metadata adapter and format-string engine. Distinguish access, modification, inode-change, and birth times where available; expose controlled platform gaps; support dereference policies, filesystems, reference files, date parsing, selective timestamps, no-create, and directories.

### Batch 30 — Condition evaluator (1 tool)

- [ ] `test`

Implement the complete GNU/POSIX operand-count grammar, file type and characteristic predicates, access checks, string and numeric comparisons, connectives, precedence, ambiguity rules, and statuses 0, 1, and 2. **Do not create a separate `[` project.**

### Batch 31 — Basic directory and name removal (3 tools)

- [ ] `mkdir`
- [ ] `rmdir`
- [ ] `unlink`

Implement modes, parents, verbose/context policy, ignore-fail behavior, parent removal, exact operand rules, and deterministic handling of files versus directories. These commands validate the new filesystem adapter without yet introducing recursive deletion.

### Batch 32 — Hard and symbolic links (2 tools)

- [ ] `link`
- [ ] `ln`

Make `link` the documented two-operand hard-link command. Build `ln` as a separate front end over shared link primitives, covering symbolic/physical/logical behavior, targets, directories, relative links, backups, force/interactive modes, and platform capability diagnostics. Do not invoke native `ln`.

### Batch 33 — Special file creation (2 tools)

- [ ] `mkfifo`
- [ ] `mknod`

Add the missing GNU projects. Implement modes, FIFO creation, block/character device operands, major/minor validation, umask behavior, and controlled privilege/platform failure. Never emulate success by creating an ordinary file.

### Batch 34 — Permission modes (1 tool)

- [ ] `chmod`

Implement octal parsing correctly, symbolic clauses, omitted-who/umask behavior, recursive traversal, reference mode, symlink policy, preserve-root, verbose/change reporting, and Windows capability mapping without pretending that the read-only attribute is a complete Unix mode.

### Batch 35 — Ownership and group mutation (2 tools)

- [ ] `chown`
- [ ] `chgrp`

Replace `NotImplementedException` with real Unix ownership operations and controlled non-Unix diagnostics. Implement names and numeric IDs, reference files, dereference policies, recursive traversal, from-filtering, preserve-root, and verbose/change reporting.

### Batch 36 — Recursive removal (1 tool)

- [ ] `rm`

Use the shared traversal engine. Implement interactive modes, recursive directory handling, force, one-file-system, preserve-root, empty-directory removal, symlink safety, write-protected prompts, race-aware deletion, glob expansion policy, and error continuation.

### Batch 37 — Copy and move engine (2 tools)

- [ ] `cp`
- [ ] `mv`

Implement source/destination classification, recursive copy, symlink and hard-link policy, metadata preservation, sparse files, reflink/copy-file-range opportunities, backup and overwrite modes, update rules, atomic replacement, cross-filesystem moves, destination-inside-source prevention, and partial-failure cleanup.

### Batch 38 — Installation engine (1 tool)

- [ ] `install`

Build on `mkdir`, `cp`, `chmod`, and `chown` primitives rather than invoking external utilities. Implement directory creation, modes, owners/groups, stripping policy, backups, compare mode, timestamps, SELinux-context policy, and atomic destination replacement.

### Batch 39 — Color database (1 tool)

- [ ] `dircolors`

Implement the documented database grammar, terminal selectors, file-extension rules, shell-specific output, built-in database, print-database mode, and diagnostics. Produce a reusable `LS_COLORS` parser for the listing family.

### Batch 40 — Directory listing family (3 tools)

- [ ] `ls`
- [ ] `dir`
- [ ] `vdir`

Create one listing engine with three thin entry profiles. Implement locale sorting, quoting, color, columns, widths, recursion with cycle protection, symlink policy, inode/block/owner/group/mode metadata, human sizes, time styles, indicators, classification, dereference modes, and terminal-sensitive defaults. Remove independent simplified `dir` and `vdir` implementations.

### Batch 41 — Filesystem usage reporting (2 tools)

- [ ] `df`
- [ ] `du`

Use real allocated-block and filesystem data where available. Implement block-size environment rules, human/SI formats, inode reporting, filesystem types, exclusions, totals, apparent size, hard-link deduplication, symlink and mount policies, depth and summarize modes, NUL input, and controlled platform differences.

### Batch 42 — Data destruction (1 tool)

- [ ] `shred`

Implement pass selection, random sources, exact-size handling, synchronization, removal and renaming policy, device/file distinctions, progress, and failure recovery. Document and test the limits of overwriting on SSDs, copy-on-write filesystems, snapshots, journaling, and remapped storage.

### Batch 43 — Archive engine (1 tool)

- [ ] `tar`

`tar` is deliberately scheduled after canonical path resolution, metadata, timestamps, directory and link mutation, permissions, ownership, recursive traversal, copy/move, installation, listing, filesystem accounting, secure temporary storage, and data-destruction semantics. Implement correct archive entry types, links, sparse files, metadata, formats, compression-process integration, selection/exclusion, incremental policy if in scope, stream operation, and extraction protections against absolute paths, `..`, symlink escapes, hard-link escapes, device creation, and overwrite races.

### Batch 44 — Host and processor context (2 tools)

- [ ] `hostid`
- [ ] `nproc`

Add the missing projects. Define reproducible host-ID behavior and implement available/configured processor counts, environment overrides, affinity and quota awareness, and controlled platform differences.

### Batch 45 — Terminal identification (1 tool)

- [ ] `tty`

Add the missing project. Implement silent mode, terminal-name reporting, correct standard-input inspection, and statuses for terminal versus nonterminal input across supported platforms.

### Batch 46 — Terminal characteristics (1 tool)

- [ ] `stty`

Add the missing project as a dedicated platform batch. Implement reading and changing terminal modes, sane/raw profiles, control characters, speed, machine-readable save/restore form, selected device handling, and a documented Windows capability boundary.

### Batch 47 — Environment and hangup-independent execution (2 tools)

- [ ] `env`
- [ ] `nohup`

Build the shared child-process launch environment. Implement environment clearing/removal, split-string parsing, working directory, `argv0`, signal policy, NUL output, command lookup, `nohup` redirection rules, diagnostics, and asynchronous stream forwarding.

### Batch 48 — Priority and time-bounded execution (2 tools)

- [ ] `nice`
- [ ] `timeout`

Implement priority adjustment without child-start races; parse full duration grammar; support signal choice, kill-after, foreground/process-group behavior, preserve-status, verbose diagnostics, and platform capability mapping.

### Batch 49 — Signal control (1 tool)

- [ ] `kill`

Implement signal names and numbers, listing and translation, process and process-group targets, queued values if in scope, exact diagnostics, and Windows substitutions only where semantically defensible.

### Batch 50 — Root-directory execution (1 tool)

- [ ] `chroot`

Replace `NotImplementedException` with a real Unix implementation and controlled diagnostics elsewhere. Implement users/groups, group initialization, skip-chdir policy, command lookup after root change, privileges, and process execution without shell interpolation.

### Batch 51 — SELinux context operations (2 tools)

- [ ] `chcon`
- [ ] `runcon`

Treat these as Linux/SELinux capability commands. Use native APIs or stable libraries rather than invoking external commands. Implement reference and component contexts, dereference and recursion policy, preserve-root, compute/process context behavior, and explicit diagnostics when SELinux is unavailable.

### Batch 52 — Standard-stream buffering control (1 tool)

- [ ] `stdbuf`

Begin with a documented feasibility decision. The current silent fallback is unacceptable. Implement supported preload/native-shim semantics where reliable; otherwise report controlled unsupported behavior for affected commands and platforms. Test child startup, environment injection, buffering modes, and exit-status propagation.

## Why this revised order is stronger

- `truncate` and `sync` immediately consolidate the raw-file infrastructure created for `dd`.
- `printf` and `numfmt` precede later commands that need rich numeric or format-string output.
- `mktemp` precedes external sorting, reverse processing, diff/patch, and archives.
- Display-width, locale, record, set, and range grammars are established before the text commands that depend on them.
- The former large ordering/transformation batch is split into `sort`/`shuf` and `tr`/`tsort`/`ptx`, so external ordering does not hide unrelated text and graph engines.
- The former split/presentation batch is split into `split`/`csplit`/`tac` and `pr`/`od`, keeping file rotation and reversal separate from page and binary presentation.
- `sort` precedes `comm`, `join`, and `uniq`, ensuring one collation and spill model.
- `grep` establishes the regex policy before `csplit`; `diff` precedes `patch`.
- Filesystem metadata and canonical-path behavior precede conditions, mutation, recursive traversal, copy, listing, usage accounting, and archives.
- `tar` is moved after the filesystem metadata, permission, ownership, traversal, and copy engines because archive correctness depends on all of them.
- Process launch precedes priority, timeout, signals, root changes, SELinux execution, and buffering control.
- Platform-specialized commands are isolated so Windows substitutions and Unix-only behavior are explicit rather than hidden inside broad batches.

## Per-batch workflow

1. Pin the authoritative upstream package and version.
2. Record synopsis, options, operands, environment variables, locale effects, signals, output grammar, exit statuses, and platform-dependent behavior.
3. Produce a conformance matrix marking each item as required, intentionally deferred, platform-limited, or not applicable.
4. Compare the current implementation and tests against that matrix.
5. Design or extend shared infrastructure before adding command-local duplicates.
6. Implement BCL behavior first, then focused native interop where required for semantics.
7. Add synchronous and asynchronous unit tests using injected streams.
8. Add CLI integration tests through `ProcessTestHost`.
9. Add differential tests against the pinned upstream utility where licensing and runner availability permit.
10. Add large-input, bounded-memory, cancellation, broken-pipe, standard-stream, multiple-file, invalid-input, and cleanup tests.
11. Add platform capability and native-ABI tests for `windows-latest`, `ubuntu-latest`, and `macos-latest`.
12. Run Debug and Release builds, then the entire applicable solution test suite on all three required runners.
13. Verify UTF-8/CRLF formatting, lowercase assembly names, required project configuration, and absence of generated artifacts.
14. Update this roadmap’s living status and record any deliberately deferred behavior.

## Batch completion checklist

A batch is complete only when:

- every scheduled command has a complete option/operand matrix;
- all required behavior is implemented or explicitly documented as platform-limited;
- no unknown option is silently ignored;
- no unsupported operation throws `NotImplementedException`;
- no production path delegates to the same native utility;
- all command and Shared tests pass;
- large inputs satisfy the stated memory strategy;
- cancellation and broken-pipe behavior are deterministic;
- exit statuses match the upstream contract;
- `windows-latest`, `ubuntu-latest`, and `macos-latest` CI expectations are green;
- the full solution builds in Debug and Release;
- source encoding and line-ending checks pass;
- lowercase assembly names and PascalCase project/namespace conventions are preserved;
- the target framework and project configuration satisfy the current completion gate;
- roadmap status and documentation are updated.

## Immediate next actions

1. Complete Completion Gate A, including migration to `net10.0` LTS and full Debug/Release validation on `windows-latest`, `ubuntu-latest`, and `macos-latest`.
2. Implement Batch 11 (`truncate`) using the safe size and file-position infrastructure established for `dd`.
3. Close Batch 11 only after the complete solution passes on all three required runners.
4. Complete Completion Gate B by adding the Shared flush and allocation capability layer.
5. Continue with Batch 12 (`sync`) and Batch 13 (`printf`, `numfmt`).
