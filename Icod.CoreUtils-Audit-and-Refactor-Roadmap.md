# Icod.CoreUtils Audit and Refactor Roadmap

## Living status

| Item | Status |
|---|---|
| Completed command batches | Batches 0–13 |
| Next command batch | Batch 14 — `printf`, `anumfmt` |
| Current engineering gate | Planned Completion Gate C1 |
| Next infrastructure dependency | Completion Gate C1 — before Batch 16 |
| Current target framework | `net10.0` |
| Required CI runners | `windows-latest`, `ubuntu-latest`, `macos-latest` |
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
9. When multiple strings are sent to `WriteAsync`, `WriteLineAsync`, or related output methods, combine them with `System.String.Concat` rather than the `+` operator.
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

- [ ] `printf`
- [ ] `numfmt`

Create shared format-string, escape, numeric, grouping, padding, precision, and human-suffix components. These will later be reused by `stat`, `sort -h`, `ls -h`, `df`, and `du`.

### Batch 15 — Secure temporary objects (1 tool)

- [ ] `mktemp`

Add secure, exclusive file and directory creation, template validation, `TMPDIR` handling, suffix and directory modes, cleanup tests, and resistance to race and symlink attacks. This infrastructure is required before external sort, reverse processing, diff/patch work, and archive testing.

### Completion Gate C1 — before Batch 16

* [ ] Add the shared regular-expression foundation:

  * [ ] GNU basic regular-expression syntax and matching policy;
  * [ ] leftmost-longest matching behavior;
  * [ ] anchoring, captures, and back-references;
  * [ ] locale-aware character-class abstraction;
  * [ ] deterministic compilation and matching diagnostics;
  * [ ] explicit documentation of differences from `System.Text.RegularExpressions`;
  * [ ] injectable and testable matching providers.

This gate prevents `expr` from introducing an isolated regular-expression implementation. The same foundation will later be extended and reused by `grep`, `csplit`, and `ed`.

### Batch 16 — Expression language (1 tool)

- [ ] `expr`

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

This gate directly supports `cut` and `paste`, then remains available to `tr`, `sort`, `grep`, `split`, and related commands.

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

  * [ ] centralized pathname expansion policy for eligible operands;
  * [ ] recursive directory enumeration;
  * [ ] symlink and reparse-point traversal policy;
  * [ ] file identity sufficient for cycle detection;
  * [ ] mount-boundary policy;
  * [ ] include and exclude matching support;
  * [ ] deterministic error-continuation behavior;
  * [ ] injectable filesystem-enumeration providers.

This gate is required before recursive `grep` and will later support recursive `diff`, directory listing, filesystem accounting, and archive creation.

### Batch 26 — Regular-expression search (1 tool)

- [ ] `grep`

Implement the documented GNU grep option and pattern model, including multiple pattern sources, basic/extended/fixed/Perl-mode policy, recursive traversal, include/exclude rules, binary policy, context, filename and line metadata, counts, quiet/list modes, NUL behavior, and the required 0/1/2 status distinction. Unsupported regex dialect features must be explicit rather than ignored.

### Batch 27 — Splitting and reversing (2 tools)

- [ ] `split`
- [ ] `tac`

Repair split-output rotation and support nonseekable input, line/byte/chunk modes, suffix alphabets, filters, additional suffixes, numeric suffixes, and exact file-creation cleanup.
Implement `tac` with backward file scanning or secure temporary spooling rather than whole-input memory loading.

### Batch 28 — Pattern-directed splitting (1 tool)

- [ ] `csplit`

Reuse the regex policy established by `grep` for `csplit`, including numeric and regex addresses, offsets, repetition, suppression, prefix/suffix grammar, keep-files behavior, exact byte counts, and cleanup after failure or cancellation.

### Batch 29 — Page presentation (1 tool)

- [ ] `pr`

For `pr`, implement columns, page geometry, headers and footers, form feeds, dates, numbering, merge modes, separators, and terminal-independent output.

### Batch 30 — Difference engine (1 tool)

- [ ] `diff`

Implement an actual sequence-difference algorithm, normal/context/unified/ed formats, whitespace and case policies, labels, function context, binary handling, recursive directory comparison, absent-file policy, and statuses 0 for no differences, 1 for differences, and greater than 1 for errors.

### Completion Gate E2 — before Batch 31

* [ ] Add shared transactional file-replacement infrastructure:

  * [ ] secure sibling temporary files;
  * [ ] atomic replacement where supported;
  * [ ] backup-name generation and retention policy;
  * [ ] rollback behavior after partial failure;
  * [ ] pathname-containment and escape checks;
  * [ ] deterministic cleanup after success, failure, and cancellation;
  * [ ] explicit diagnostics where atomic replacement is unavailable.

This gate is placed immediately before `patch`, the first remaining command that requires safe transactional modification of existing files.

### Batch 31 — Patch application engine (1 tool)

- [ ] `patch`

Parse normal, context, and unified diffs; implement file selection, strip counts, reversal detection, fuzz, offsets, backups, rejects, dry runs, timestamps, atomic replacement, and safe pathname handling. Test against files produced by the preceding `diff` batch.

### Batch 32 — Line editor (1 tool)

- [ ] `ed`

Complete the address parser, command state machine, global commands, substitutions, marks, buffers, file and shell commands, modified-buffer rules, diagnostics, signals, and exit behavior. Reuse the agreed regex policy but keep the editor state machine isolated.

### Completion Gate E3 — before Batch 33

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

### Completion Gate E4 — before Batch 34

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

### Completion Gate E5 — before Batch 36

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

### Completion Gate E6 — before Batch 39

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
  * [ ] backup and overwrite coordination with Completion Gate E2.

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

### Batch 47 — Archive engine (1 tool)

- [ ] `tar`

`tar` is deliberately scheduled after canonical path resolution, metadata, timestamps, directory and link mutation, permissions, ownership, recursive traversal, copy/move, installation, listing, filesystem accounting, and secure temporary storage.

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

## Why the tools are scheduled this way

* Batches 0 through 10 are preserved as the historical foundation of the project. They establish the shared command-line, diagnostics, streaming, numeric, platform, identity, process, date/time, and block-I/O abstractions used by the remaining commands.
* `truncate`, `sync`, and `od` follow `dd` because they exercise closely related raw-file capabilities. Together, these batches establish file sizing, sparse extension, allocation reporting, data and metadata flushing, byte offsets, bounded reads, binary interpretation, and reusable binary formatting before the roadmap moves into higher-level text processing.
* `printf` and `numfmt` are scheduled early because formatted numeric output, escape processing, padding, precision, grouping, and human-readable quantities recur throughout later commands such as `sort`, `stat`, `ls`, `df`, and `du`.
* `mktemp` precedes every remaining command that may require secure temporary storage. It establishes exclusive temporary-file and temporary-directory creation before external sorting, reverse processing, patch application, transactional replacement, and archive testing depend upon temporary workspaces.
* Completion Gate C1 precedes `expr` so the project establishes one documented regular-expression foundation rather than allowing `expr`, `grep`, `csplit`, and `ed` to develop incompatible matching behavior independently.
* Completion Gate C2 introduces byte, Unicode-scalar, display-column, and tab-stop behavior immediately before `expand`, `unexpand`, and `fold`, the first remaining commands that require those distinctions. `fmt`, `nl`, and the later `pr` batch then reuse the same display-width and line-layout model.
* Completion Gate C3 introduces record delimiters, range grammars, field selection, separators, and escape processing immediately before `cut` and `paste`. Those primitives are then reused by `tr`, `sort`, `grep`, `split`, and other commands without duplicating parsing behavior.
* Completion Gate D remains a single gate because locale collation, sort-key comparison, bounded-memory run generation, stable external merging, temporary-workspace management, and cancellation cleanup form one cohesive external-ordering engine. `sort` is the first command to validate that engine.
* `shuf` follows `sort` because it can reuse the established record and temporary-storage infrastructure, while remaining a separate batch because unbiased random selection and permutation are not sorting operations.
* `comm`, `join`, and `uniq` follow `sort` so they consume the same collation, ordering, record, and comparison rules. This avoids subtle disagreements about whether inputs are ordered or whether adjacent records and join keys are equal.
* `tr`, `tsort`, and `ptx` are kept in separate batches because character-set transformation, graph ordering, and permuted indexing are distinct execution engines. They are nevertheless scheduled after the shared text, locale, tokenization, ordering, and spill-storage primitives they can reuse.
* Completion Gate E1 introduces safe read-only traversal before recursive `grep`, the first remaining command that needs directory enumeration, symlink policy, cycle detection, include and exclude matching, mount-boundary handling, and controlled continuation after filesystem errors.
* `grep` precedes `csplit` so the project establishes its regular-expression dialect and matching policy in the dedicated search engine before pattern-directed file splitting depends upon it.
* `split` and `tac` are scheduled after secure temporary storage and the shared record model because both require bounded-memory handling of potentially unbounded input. `csplit` follows separately because its pattern-address grammar and transactional output-file behavior are materially more complex.
* `pr` follows the earlier display-column and formatting batches so page geometry, columns, headers, footers, separators, and numbering can reuse established width and layout behavior without being coupled to the unrelated binary-formatting engine used by `od`.
* `diff` precedes `patch` so the project first defines and tests the normal, context, unified, and ed-style difference formats that `patch` must subsequently consume.
* Completion Gate E2 is placed between `diff` and `patch` because `patch` is the first remaining command that requires secure sibling temporary files, backups, rollback, pathname-containment checks, and atomic replacement of existing files.
* `ed` follows the regular-expression, temporary-storage, difference, and replacement work because its command language combines pattern matching, mutable text buffers, file replacement, subprocess execution, and a substantial state machine. It remains isolated so editor-specific state does not distort the shared text abstractions.
* Completion Gate E3 introduces canonical-path and symbolic-link resolution immediately before `readlink` and `realpath`, allowing those commands to validate lexical normalization, physical resolution, missing-component policy, loop detection, and platform root semantics.
* Completion Gate E4 follows canonical-path resolution and precedes `stat`, `touch`, and `test`. This establishes one authoritative model for file types, sizes, ownership, modes, timestamps, links, allocated blocks, filesystem information, and unavailable platform metadata.
* `test` follows `stat` and `touch` because its file predicates depend upon the metadata model those commands first exercise. It remains separate from `expr` because operand-count parsing, predicate evaluation, ambiguity rules, and exit statuses constitute a different language. No separate `[` project is required.
* Completion Gate E5 introduces mode parsing, umask behavior, link creation, directory creation, FIFO and device-node capabilities, dereference policy, and race-aware single-path mutation before the basic filesystem-mutation batches.
* `mkdir`, `rmdir`, and `unlink` validate basic pathname mutation before link creation and special-file creation add more platform-specific behavior. `link` and `ln` then share link primitives while retaining their different command-line contracts. `mkfifo` and `mknod` follow once mode, privilege, and platform-capability handling are established.
* Completion Gate E6 extends the read-only traversal model into a mutation-safe recursive engine before recursive permissions, ownership changes, deletion, copying, and moving begin. It adds preserve-root protection, mount boundaries, no-follow operations, hard-link identity, sparse-file handling, metadata preservation, destination-inside-source detection, and partial-failure cleanup.
* `chmod` precedes `chown` and `chgrp` so numeric and symbolic mode handling is completed before the roadmap proceeds to the more platform- and privilege-dependent ownership operations.
* `rm` follows the recursive mutation gate but precedes copying so deletion safety, preserve-root behavior, prompting, symlink handling, and error continuation can be validated independently from destination creation and metadata preservation.
* `cp` and `mv` share source/destination classification, overwrite and backup policy, recursive traversal, metadata preservation, sparse-file handling, hard-link tracking, atomic replacement, and cross-filesystem behavior. They precede `install`, which deliberately builds on the completed directory, copy, mode, ownership, timestamp, backup, and replacement primitives.
* Completion Gate F1 introduces only the terminal-aware presentation capabilities needed by `dircolors`, `ls`, `dir`, and `vdir`. This avoids implementing unrelated host, terminal-control, child-process, and signal facilities prematurely.
* `dircolors` is grouped with the directory-listing family because it produces the `LS_COLORS` model consumed by the shared listing engine. `ls`, `dir`, and `vdir` are treated as thin command profiles over one implementation so their sorting, quoting, metadata, recursion, color, width, and terminal-sensitive behavior cannot drift apart.
* `df` and `du` follow the filesystem metadata and traversal work because they depend upon real filesystem statistics, allocated-block accounting, mount policies, hard-link identity, block-size rules, and human-readable numeric formatting.
* `shred` is isolated because destructive overwrite semantics, storage-device limitations, synchronization, renaming, and removal policy require a focused safety and capability review. Its position before `tar` is organizational rather than a dependency: the archive engine does not depend upon data-destruction behavior.
* `tar` is scheduled late because archive correctness depends upon nearly the entire filesystem foundation: canonical paths, safe traversal, file types, links, sparse files, modes, ownership, timestamps, copying, temporary storage, compression-process integration, and transactional extraction. Deferring it also allows extraction protections against absolute paths, `..`, symlink escapes, hard-link escapes, device creation, and overwrite races to build upon established security primitives.
* Completion Gate F2 introduces host and processor information immediately before `hostid` and `nproc`, avoiding premature coupling to terminal or child-process behavior.
* Completion Gate F3 introduces terminal identification and terminal-mode control immediately before `tty` and `stty`. These commands remain separate because identifying whether a stream is attached to a terminal is substantially simpler than reading, serializing, and mutating terminal characteristics.
* Completion Gate F4 establishes command lookup, argument-safe process launch, environment construction, asynchronous stream forwarding, process cleanup, signal translation, process groups, and exit-status handling before the remaining process-control commands.
* `env` and `nohup` are the first consumers of the shared child-process layer because they establish environment construction, command lookup, redirection, signal disposition, and stream forwarding.
* `kill` follows as the dedicated validator of signal names, signal numbers, listing, translation, process targets, process groups, and platform substitutions. It intentionally precedes `timeout` so timeout handling can reuse an already tested signal-control layer.
* `nice` and `timeout` are grouped because both alter the conditions under which a child process executes. They share race-free child startup, process-group handling, status propagation, and platform-capability reporting, while adding priority adjustment and time-bounded termination respectively.
* `chroot`, `chcon`, and `runcon` are scheduled near the end because they require mature child-process, identity, privilege, filesystem, and platform-capability abstractions and have substantial Unix- or Linux-specific security implications.
* `stdbuf` is last because its defining behavior may require a native preload library or platform-specific shim that cannot be implemented portably through ordinary managed process APIs. By this point, child startup, environment injection, stream forwarding, diagnostics, and exit-status propagation will already be established, allowing the remaining feasibility decision to focus narrowly on buffering control.
* Complex parsers, state machines, security boundaries, and platform-specialized commands are intentionally isolated in single-command batches. Commands are grouped only where they share a real execution engine or directly validate the same new infrastructure, rather than merely because their traditional descriptions appear related.

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
13. Verify UTF-8 encoding and LF line endings, lowercase assembly names, required project configuration, and absence of generated artifacts.
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
1. Implement Batch 13 (`od`) to complete the raw-file and binary-formatting sequence established by `dd`, `truncate`, and `sync`.
2. Continue with Batch 14 (`printf`, `numfmt`).
3. Rename `mktmp` to `mktemp` and implement Batch 15.
4. Implement Batch 16 (`expr`).
5. Complete Completion Gate C before beginning Batch 17.
