# Icod.CoreUtils Audit and Refactor Roadmap

## Document status

- **Audit date:** July 27, 2026
- **Repository reviewed:** `uniblab/Icod.CoreUtils`, `main`
- **Visible main-head commit during the audit:** `0bd2731`
- **Historical batches preserved:** Batches 0 through 10 are reproduced verbatim from the prior roadmap.
- **User scope decision:** Do not create or schedule a separate `[` command project. The existing `test` project is the condition evaluator.
- **Current execution point:** Batches 0 through 9 are complete; Batch 10 is `dd`; Batch 11 begins the revised sequence.

This revision replaces the former plan from Batch 11 onward. It preserves the work history while changing the remaining order to follow actual implementation dependencies discovered in the current repository.

## Scope

The repository is a broader BSD/Linux utility collection rather than only GNU Coreutils:

- **104 command projects** currently exist, plus `Shared`.
- The solution contains **37 test/support projects** under `tests`, including `Shared.Tests` and `ProcessTestHost`.
- The repository includes seven non-Coreutils programs that remain in scope: `diff`, `ed`, `grep`, `patch`, `ps`, `sed`, and `tar`.
- Ten GNU Coreutils programs are not yet present and should be added: `hostid`, `mkfifo`, `mknod`, `mktemp`, `nohup`, `nproc`, `numfmt`, `printf`, `stty`, and `tty`.
- No separate `[` project will be added.
- On completion, the planned command-project count is **114**.

For GNU Coreutils commands, the conformance baseline is the pinned GNU Coreutils manual. For the non-Coreutils programs, use the corresponding upstream project as the primary authority:

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

- The completed batches established useful shared command-line, diagnostics, streaming, numeric, platform, and process abstractions.
- Batches 1 through 9 have command-specific tests.
- The solution builds on Windows, Linux, and macOS in CI.
- Source projects consistently reference `Shared`.
- Recent projects use asynchronous entry points and cancellation more consistently than the original implementations.

### Defects that must be corrected before broad continuation

1. **The build gate does not execute the command test suite.**  
   `build.sh`, `build.cmd`, and both CI workflows run only `Shared.Tests`, even though the solution contains many command-specific test projects. A passing build therefore does not prove that completed batches still pass.

2. **The old roadmap measurements are historical, not current.**  
   Statements such as “there is no automated test project” and “102 command projects” are now obsolete. They must be labeled as an initial baseline or removed.

3. **Several implementations silently accept unsupported behavior.**  
   Unknown or unsupported options are ignored by some commands. Unsupported platform behavior sometimes returns success. Both patterns are incompatible with a conformance-oriented port.

4. **Several commands throw unhandled `NotImplementedException`.**  
   The current `chown`, `chgrp`, `runcon`, and `chroot` implementations are examples. Unsupported operations must produce a controlled diagnostic and documented nonzero status.

5. **Some commands delegate their defining operation to an installed native utility.**  
   Examples include portions of `link`, `chcon`, `install`, `sync`, and `stdbuf`. Production implementations must not obtain apparent compatibility by invoking the same host utility. Native utilities may be used only by optional differential tests.

6. **Some implementations are not yet the command they claim to be.**
   - `diff` is not a complete difference algorithm and does not yet implement the required result-status model.
   - `patch` handles a private simplified format rather than normal, context, and unified patches.
   - `link` behaves like a partial `ln` front end rather than the simple two-operand hard-link command.
   - `stat` substitutes creation time where inode-change time is required.
   - `chmod` does not yet implement GNU/POSIX numeric and symbolic mode semantics correctly.
   - `tar` needs correct entry typing, metadata handling, and extraction-path safety.
   - `stdbuf` cannot silently run a child without applying the requested buffering mode.

7. **Several text commands use the wrong data model.**  
   Common problems include ordinal comparison instead of locale collation, UTF-16 `char` processing where bytes or locale characters are required, line-based processing for commands that must transform delimiters, and whole-input buffering where bounded memory or temporary spill files are required.

8. **Several recursive filesystem commands lack a common traversal policy.**  
   Symlink traversal, hard-link identity, cycles, mount boundaries, sparse files, metadata preservation, and destination-inside-source detection must be centralized before `rm`, `cp`, `mv`, `du`, `ls`, and `tar` are considered conformant.

9. **Injected standard streams are not consistently respected or owned correctly.**  
   A command must use the supplied `stdin`, `stdout`, and `stderr`, and must never dispose a caller-owned standard stream.

10. **The current target framework has a near-term lifecycle deadline.**  
    Schedule migration from `net9.0` to `net10.0` LTS before Batch 12, while retaining the explicit project language version and configuration policy.

## Repository-wide rules for all remaining batches

1. Every command exposes `RunAsync(..., CancellationToken)` and retains a synchronous compatibility wrapper.
2. Naturally asynchronous I/O and child-process waits use TAP directly; do not wrap them in `Task.Run`.
3. Unknown options, missing operands, conflicting options, invalid numeric values, and unsupported modes receive deterministic diagnostics and documented nonzero statuses.
4. No production command invokes the same native utility to perform its defining operation.
5. Platform limitations use capability checks and controlled failure. They never throw an unhandled `NotImplementedException` and never silently report success.
6. Caller-owned standard streams are never disposed.
7. Byte-oriented commands operate on streams; character-oriented commands explicitly define their locale and encoding behavior.
8. Commands that can process unbounded input must stream or spill to secure temporary storage. Whole-input buffering requires an explicit, tested justification.
9. Pathname operands support centralized expansion where appropriate: `*` and `?` for segment matching and `**` for transitive recursive matching.
10. Recursive operations share one traversal engine with explicit symlink, cycle, mount-boundary, and error-continuation policies.
11. File mutation is race-aware. Temporary files are created securely and exclusively; replacement is atomic where the command promises it.
12. Security-sensitive extraction, deletion, overwrite, ownership, mode, and root/context operations receive adversarial tests.
13. Exit statuses are part of the public contract and are tested independently from displayed output.
14. Each command has class-level XML documentation containing its usage and a dedicated usage-writing function.
15. All source files use UTF-8 encoding and CRLF line endings.
16. Every `.csproj` retains C# 13 and the established Debug, Staging, and Release conditional property groups.
17. Every new project is added to the solution, to the appropriate test project, and to all build/test entry points.
18. `--help` and `--version` behavior, write failures, broken pipes, cancellation, and disposal are tested consistently.

## Engineering gates

These gates are not command batches and do not alter the historical numbering.

### Gate A — before closing Batch 11

- Change local scripts and CI to test the entire solution, not only `Shared.Tests`.
- Add a Release build to CI.
- Verify that every test project is included in the solution and actually discovered.
- Add a repository check for UTF-8/CRLF source files and forbidden generated artifacts.
- Record the exact upstream version used for every batch.
- Add a current-status section to this roadmap after every merged batch.

Recommended test command:

```text
dotnet test Icod.CoreUtils.sln -c Debug --no-build
```

### Gate B — before Batch 12

- Migrate projects and CI to `net10.0` LTS.
- Retain `<LangVersion>13.0</LangVersion>` and the existing configuration policy in every project.
- Add `Shared.FileSystem` flush and allocation capability abstractions needed by `dd`, `truncate`, and `sync`.

### Gate C — before Batch 17

Add the shared text model:

- byte, Unicode scalar, and display-column iteration;
- locale-aware collation and character classification;
- tab-stop grammar;
- field/range-list grammar;
- escape-sequence parsing;
- configurable line and NUL record readers/writers.

### Gate D — before Batch 21

Add secure temporary-workspace and external-ordering infrastructure:

- exclusive temporary creation;
- bounded-memory runs;
- stable external merge;
- configurable locale/key comparison;
- deterministic cleanup on success, failure, and cancellation.

### Gate E — before Batch 31

Add the shared filesystem model:

- lexical and physical path resolution;
- symlink and reparse-point inspection;
- file identity, type, mode, ownership, timestamps, links, device/inode equivalents, and allocated-block accounting;
- recursive traversal with cycle detection and mount-boundary policy;
- sparse-file and metadata-preservation helpers;
- atomic replacement and backup policy.

### Gate F — before Batch 47

Add shared system/process primitives:

- host and processor information;
- terminal discovery and terminal-mode capability abstractions;
- signal-name and signal-number parsing;
- process-group control;
- child-process stream forwarding;
- controlled Windows substitutions where semantics are genuinely equivalent.

## Batch-size policy

- A batch groups commands only when they share an implementation engine or directly validate the same new infrastructure.
- A complex parser, state machine, security boundary, or platform layer may receive a one-command batch.
- A pair or trio is preferred over a superficially thematic five- or six-command batch when the larger group would hide unrelated risk.
- Each batch must leave shared infrastructure in a reusable, documented, and tested state.
- A batch is complete only after the full solution builds and all tests pass on Windows, Linux, and macOS, subject to explicit platform capability expectations.

## Refactor order

### Historical sequence — preserved verbatim

### Batch 0 — foundation and repository hygiene

Refactor `Shared`, add automated tests and CI, centralize option/stream/process/platform helpers, and remove generated artifacts from review archives.

### Batch 1 — Stabilize shared line/byte readers (2 tools)

`head`, `tail`

### Batch 2 — Stabilize the stream editor (1 tool)

`sed`

### Batch 3 — Core streaming byte and record I/O (3 tools)

`cat`, `tee`, `wc`

### Batch 4 — Base encoding family (3 tools)

`base32`, `base64`, `basenc`

### Batch 5 — Checksum and digest family (9 tools)

`b2sum`, `cksum`, `md5sum`, `sha1sum`, `sha224sum`, `sha256sum`, `sha384sum`, `sha512sum`, `sum`

### Batch 6 — Small deterministic commands (7 tools)

`true`, `false`, `echo`, `yes`, `sleep`, `seq`, `factor`

### Batch 7 — Path and basic host-information commands (7 tools)

`basename`, `dirname`, `pathchk`, `pwd`, `printenv`, `arch`, `hostname`

### Batch 8 — Identity and login-information commands (7 tools)

`uname`, `whoami`, `logname`, `groups`, `id`, `users`, `who`

### Batch 9 — Platform and process-information commands (4 tools)

`pinky`, `ps`, `uptime`, `date`

### Batch 10 — Block-oriented copy and conversion (1 tool)

`dd`

## Revised sequence beginning with Batch 11

### Batch 11 — File-size manipulation (1 tool)

`truncate`

Implement the complete GNU size operand grammar, `--reference`, `--io-blocks`, creation policy, relative modifiers, rounding modifiers, overflow checks, sparse extension, and precise diagnostics. Reuse the safe size and file-position infrastructure introduced by `dd`.

### Batch 12 — Filesystem flushing (1 tool)

`sync`

Replace native-command delegation and no-op success with an explicit platform implementation. Support file-specific data and filesystem flushing where the platform permits it, and produce a controlled diagnostic for semantics that cannot be represented.

### Batch 13 — Binary inspection and dumping (1 tool)

`od`

Build a reusable binary-formatting engine: address radices, type strings, byte order, duplicate suppression, skip/read limits, string extraction, and nonseekable standard input. This immediately exercises the raw-byte and numeric infrastructure from Batches 10 through 12.

### Batch 14 — Formatted and human-readable numeric output (2 tools)

`printf`, `numfmt`

Create shared format-string, escape, numeric, grouping, padding, precision, and human-suffix components. These will later be reused by `stat`, `sort -h`, `ls -h`, `df`, and `du`.

### Batch 15 — Secure temporary objects (1 tool)

`mktemp`

Add secure, exclusive file and directory creation, template validation, `TMPDIR` handling, suffix and directory modes, cleanup tests, and resistance to race and symlink attacks. This infrastructure is required before external sort, reverse processing, diff/patch work, and archive testing.

### Batch 16 — Expression language (1 tool)

`expr`

Implement a real precedence-aware expression parser with arithmetic, relations, Boolean operators, string operations, regular expressions, overflow behavior, quoting rules, and GNU exit statuses. Do not pair it with `test`; their grammars and result models are materially different.

### Batch 17 — Tabs and display columns (3 tools)

`expand`, `unexpand`, `fold`

Use the shared display-column and tab-stop model. Cover tab lists, repeated tab intervals, initial/all modes, byte versus character behavior, word-boundary folding, backspaces, carriage returns, wide characters, and invalid multibyte input.

### Batch 18 — Paragraph, line-number, and page formatting (3 tools)

`fmt`, `nl`, `pr`

Implement paragraph recognition, sentence spacing, crown/tagged modes, logical-page delimiters, numbering styles, header/footer layout, columns, page geometry, form feeds, dates, and standard-stream ownership. Share line-layout and display-width components without forcing one combined execution engine.

### Batch 19 — Field and record extraction (2 tools)

`cut`, `paste`

Implement complete byte/character/field list grammar, complement and output delimiters, NUL records, delimiter suppression, serial paste, delimiter escape cycles, multiple input streams, and correct behavior on multibyte input.

### Batch 20 — Character-set transformation (1 tool)

`tr`

Implement the full set-expression grammar: ranges, escapes, repetition, character classes, equivalence classes, complement, delete, squeeze, locale behavior, and delimiter bytes. This is sufficiently specialized to remain isolated.

### Batch 21 — External ordering engine (1 tool)

`sort`

Implement key specifications, locale collation, stable and unique modes, numeric families, month/version/human-numeric comparison, checking and merging, parallelism policy, secure temporary runs, bounded-memory external merge, zero-terminated records, and exact exit statuses.

### Batch 22 — Sorted-stream consumers (3 tools)

`comm`, `join`, `uniq`

Reuse the same collation and record model as `sort`. Preserve duplicate-key Cartesian behavior in `join`, order checking, header and output formatting, field selection, skip/check options, counting, zero records, and streaming without loading complete inputs.

### Batch 23 — Randomization and graph ordering (2 tools)

`shuf`, `tsort`

For `shuf`, use unbiased selection, secure/random-source abstractions, repeat and range modes, and bounded-memory strategies. For `tsort`, implement streaming tokenization, stable diagnostics, cycle reporting, and deterministic tests. They share no comparer engine with `sort`, which is why they follow it rather than being included in it.

### Batch 24 — Permuted index (1 tool)

`ptx`

Replace the simplified token dump with the documented input, word, ignore, reference, width, break-file, and output-format behavior. Reuse locale collation, secure spill storage, and line-layout components.

### Batch 25 — Regular-expression search (1 tool)

`grep`

Implement the documented GNU grep option and pattern model, including multiple pattern sources, basic/extended/fixed/Perl-mode policy, recursive traversal, include/exclude rules, binary policy, context, filename and line metadata, counts, quiet/list modes, NUL behavior, and the required 0/1/2 status distinction. Unsupported regex dialect features must be explicit rather than ignored.

### Batch 26 — Streaming split and reverse (2 tools)

`split`, `tac`

Repair split-output rotation and support nonseekable input, line/byte/chunk modes, suffix alphabets, filters, additional suffixes, numeric suffixes, and exact file-creation cleanup. Implement `tac` with backward file scanning or secure temporary spooling rather than whole-input memory loading.

### Batch 27 — Pattern-directed splitting (1 tool)

`csplit`

Reuse the regex policy established by `grep`. Implement numeric and regex addresses, offsets, repetition, suppression, prefix/suffix grammar, keep-files behavior, exact byte counts, and cleanup after failure or cancellation.

### Batch 28 — Difference engine (1 tool)

`diff`

Implement an actual sequence-difference algorithm, normal/context/unified/ed formats, whitespace and case policies, labels, function context, binary handling, recursive directory comparison, absent-file policy, and statuses 0 for no differences, 1 for differences, and greater than 1 for errors.

### Batch 29 — Patch application engine (1 tool)

`patch`

Parse normal, context, and unified diffs; implement file selection, strip counts, reversal detection, fuzz, offsets, backups, rejects, dry runs, timestamps, atomic replacement, and safe pathname handling. Test against files produced by the preceding `diff` batch.

### Batch 30 — Line editor (1 tool)

`ed`

Complete the address parser, command state machine, global commands, substitutions, marks, buffers, file and shell commands, modified-buffer rules, diagnostics, signals, and exit behavior. Reuse the agreed regex policy but keep the editor state machine isolated.

### Batch 31 — Symbolic-link and canonical-path resolution (2 tools)

`readlink`, `realpath`

Implement lexical versus physical resolution, missing-component policies, canonicalization modes, delimiters, quiet/verbose behavior, relative output, symlink loops, reparse points, and deterministic failures. Never return the unresolved input as a false success.

### Batch 32 — File metadata and timestamps (2 tools)

`stat`, `touch`

Build the authoritative metadata adapter and format-string engine. Distinguish access, modification, inode-change, and birth times where available; expose controlled platform gaps; support dereference policies, filesystems, reference files, date parsing, selective timestamps, no-create, and directories.

### Batch 33 — Condition evaluator (1 tool)

`test`

Implement the complete GNU/POSIX operand-count grammar, file type and characteristic predicates, access checks, string and numeric comparisons, connectives, precedence, ambiguity rules, and statuses 0, 1, and 2. **Do not create a separate `[` project.**

### Batch 34 — Basic directory and name removal (3 tools)

`mkdir`, `rmdir`, `unlink`

Implement modes, parents, verbose/context policy, ignore-fail behavior, parent removal, exact operand rules, and deterministic handling of files versus directories. These commands validate the new filesystem adapter without yet introducing recursive deletion.

### Batch 35 — Hard and symbolic links (2 tools)

`link`, `ln`

Make `link` the documented two-operand hard-link command. Build `ln` as a separate front end over shared link primitives, covering symbolic/physical/logical behavior, targets, directories, relative links, backups, force/interactive modes, and platform capability diagnostics. Do not invoke native `ln`.

### Batch 36 — Special file creation (2 tools)

`mkfifo`, `mknod`

Add the missing GNU projects. Implement modes, FIFO creation, block/character device operands, major/minor validation, umask behavior, and controlled privilege/platform failure. Never emulate success by creating an ordinary file.

### Batch 37 — Permission modes (1 tool)

`chmod`

Implement octal parsing correctly, symbolic clauses, omitted-who/umask behavior, recursive traversal, reference mode, symlink policy, preserve-root, verbose/change reporting, and Windows capability mapping without pretending that the read-only attribute is a complete Unix mode.

### Batch 38 — Ownership and group mutation (2 tools)

`chown`, `chgrp`

Replace `NotImplementedException` with real Unix ownership operations and controlled non-Unix diagnostics. Implement names and numeric IDs, reference files, dereference policies, recursive traversal, from-filtering, preserve-root, and verbose/change reporting.

### Batch 39 — Recursive removal (1 tool)

`rm`

Use the shared traversal engine. Implement interactive modes, recursive directory handling, force, one-file-system, preserve-root, empty-directory removal, symlink safety, write-protected prompts, race-aware deletion, glob expansion policy, and error continuation.

### Batch 40 — Copy and move engine (2 tools)

`cp`, `mv`

Implement source/destination classification, recursive copy, symlink and hard-link policy, metadata preservation, sparse files, reflink/copy-file-range opportunities, backup and overwrite modes, update rules, atomic replacement, cross-filesystem moves, destination-inside-source prevention, and partial-failure cleanup.

### Batch 41 — Installation engine (1 tool)

`install`

Build on `mkdir`, `cp`, `chmod`, and `chown` primitives rather than invoking external utilities. Implement directory creation, modes, owners/groups, stripping policy, backups, compare mode, timestamps, SELinux-context policy, and atomic destination replacement.

### Batch 42 — Color database (1 tool)

`dircolors`

Implement the documented database grammar, terminal selectors, file-extension rules, shell-specific output, built-in database, print-database mode, and diagnostics. Produce a reusable LS_COLORS parser for the listing family.

### Batch 43 — Directory listing family (3 tools)

`ls`, `dir`, `vdir`

Create one listing engine with three thin entry profiles. Implement locale sorting, quoting, color, columns, widths, recursion with cycle protection, symlink policy, inode/block/owner/group/mode metadata, human sizes, time styles, indicators, classification, dereference modes, and terminal-sensitive defaults. Remove independent simplified `dir` and `vdir` implementations.

### Batch 44 — Filesystem usage reporting (2 tools)

`df`, `du`

Use real allocated-block and filesystem data where available. Implement block-size environment rules, human/SI formats, inode reporting, filesystem types, exclusions, totals, apparent size, hard-link deduplication, symlink and mount policies, depth and summarize modes, NUL input, and controlled platform differences.

### Batch 45 — Data destruction (1 tool)

`shred`

Implement pass selection, random sources, exact-size handling, synchronization, removal and renaming policy, device/file distinctions, progress, and failure recovery. Document and test the limits of overwriting on SSDs, copy-on-write filesystems, snapshots, journaling, and remapped storage.

### Batch 46 — Archive engine (1 tool)

`tar`

Move `tar` after metadata, traversal, copy, ownership, modes, and secure temporary storage. Implement correct archive entry types, links, sparse files, metadata, formats, compression-process integration, selection/exclusion, incremental policy if in scope, stream operation, and extraction protections against absolute paths, `..`, symlink escapes, hard-link escapes, device creation, and overwrite races.

### Batch 47 — Host and processor context (2 tools)

`hostid`, `nproc`

Add the missing projects. Define reproducible host-ID behavior and implement available/configured processor counts, environment overrides, affinity and quota awareness, and controlled platform differences.

### Batch 48 — Terminal identification (1 tool)

`tty`

Add the missing project. Implement silent mode, terminal-name reporting, correct standard-input inspection, and statuses for terminal versus nonterminal input across supported platforms.

### Batch 49 — Terminal characteristics (1 tool)

`stty`

Add the missing project as a dedicated platform batch. Implement reading and changing terminal modes, sane/raw profiles, control characters, speed, machine-readable save/restore form, selected device handling, and a documented Windows capability boundary.

### Batch 50 — Environment and hangup-independent execution (2 tools)

`env`, `nohup`

Build the shared child-process launch environment. Implement environment clearing/removal, split-string parsing, working directory, argv0, signal policy, NUL output, command lookup, nohup redirection rules, diagnostics, and asynchronous stream forwarding.

### Batch 51 — Priority and time-bounded execution (2 tools)

`nice`, `timeout`

Implement priority adjustment without child-start races; parse full duration grammar; support signal choice, kill-after, foreground/process-group behavior, preserve-status, verbose diagnostics, and platform capability mapping.

### Batch 52 — Signal control (1 tool)

`kill`

Implement signal names and numbers, listing and translation, process and process-group targets, queued values if in scope, exact diagnostics, and Windows substitutions only where semantically defensible.

### Batch 53 — Root-directory execution (1 tool)

`chroot`

Replace `NotImplementedException` with a real Unix implementation and controlled diagnostics elsewhere. Implement users/groups, group initialization, skip-chdir policy, command lookup after root change, privileges, and process execution without shell interpolation.

### Batch 54 — SELinux context operations (2 tools)

`chcon`, `runcon`

Treat these as Linux/SELinux capability commands. Use native APIs or stable libraries rather than invoking external commands. Implement reference and component contexts, dereference and recursion policy, preserve-root, compute/process context behavior, and explicit diagnostics when SELinux is unavailable.

### Batch 55 — Standard-stream buffering control (1 tool)

`stdbuf`

Begin with a documented feasibility decision. The current silent fallback is unacceptable. Implement supported preload/native-shim semantics where reliable; otherwise report controlled unsupported behavior for affected commands and platforms. Test child startup, environment injection, buffering modes, and exit-status propagation.

## Why this revised order is stronger

- `truncate`, `sync`, and `od` immediately consolidate the raw-file infrastructure created for `dd`.
- `printf` and `numfmt` precede every later command that needs rich numeric or format-string output.
- `mktemp` precedes external sorting, reverse processing, diff/patch, and archives.
- Display-width, locale, record, set, and range grammars are established before the text commands that depend on them.
- `sort` precedes `comm`, `join`, `uniq`, and `ptx`, ensuring one collation and spill model.
- `grep` establishes the regex policy before `csplit`; `diff` precedes `patch`.
- Filesystem metadata and canonical-path behavior precede conditions, mutation, recursive traversal, copy, listing, usage accounting, and archives.
- `tar` is moved late because archive correctness depends on almost every filesystem abstraction.
- Process launch precedes priority, timeout, signals, root changes, SELinux execution, and buffering control.
- Platform-specialized commands are isolated so Windows substitutions and Unix-only behavior are explicit rather than hidden inside broad batches.

## Per-batch workflow

1. Pin the authoritative upstream package and version.
2. Record synopsis, options, operands, environment variables, locale effects, signals, output grammar, exit statuses, and platform-dependent behavior.
3. Produce a conformance matrix marking each item as required, intentionally deferred, platform-limited, or not applicable.
4. Compare current implementation and tests against that matrix.
5. Design or extend shared infrastructure before adding command-local duplicates.
6. Implement BCL behavior first, then focused native interop where required for semantics.
7. Add synchronous and asynchronous unit tests using injected streams.
8. Add CLI integration tests through `ProcessTestHost`.
9. Add Linux differential tests against the pinned upstream utility where licensing and environment permit.
10. Add large-input, bounded-memory, cancellation, broken-pipe, standard-stream, multiple-file, invalid-input, and cleanup tests.
11. Add platform capability tests on Windows, Linux, and macOS.
12. Run Debug and Release builds, then the entire solution test suite.
13. Verify UTF-8/CRLF formatting and absence of generated artifacts.
14. Update this roadmap’s current status and record any deliberately deferred behavior.

## Batch completion checklist

A batch is complete only when:

- every scheduled command has a complete option/operand matrix;
- all required behavior is implemented or explicitly documented as platform-limited;
- no unknown option is silently ignored;
- no unsupported operation throws `NotImplementedException`;
- no production path delegates to the same native utility;
- all command and shared tests pass;
- large inputs satisfy the stated memory strategy;
- cancellation and broken-pipe behavior are deterministic;
- exit statuses match the upstream contract;
- Windows, Linux, and macOS CI expectations are green;
- the full solution builds in Debug and Release;
- source encoding and line-ending checks pass;
- roadmap status and documentation are updated.

## Immediate next actions

1. Finish Batch 10 (`dd`) under the preserved roadmap.
2. Correct the build scripts and CI so they execute every test project.
3. Implement Batch 11 (`truncate`).
4. Migrate to `net10.0` LTS before beginning Batch 12.
5. Continue with Batch 12 (`sync`) and Batch 13 (`od`) to consolidate the raw-file foundation.
