# Icod.Patch Audit and Refactor Roadmap

## Status of this document

This document is the regenerated development roadmap for `Icod.Patch`.

It is based on the state of the `main` branch reviewed on August 3, 2026:

- [Icod.CoreUtils repository](https://github.com/uniblab/Icod.CoreUtils)
- [Main audit and refactor roadmap](https://github.com/uniblab/Icod.CoreUtils/blob/main/Icod.CoreUtils-Audit-and-Refactor-Roadmap.md)
- [Current Patch project](https://github.com/uniblab/Icod.CoreUtils/tree/main/patch)
- [Current Patch project file](https://github.com/uniblab/Icod.CoreUtils/blob/main/patch/Icod.Patch.csproj)
- [Current Patch command](https://github.com/uniblab/Icod.CoreUtils/blob/main/patch/src/Command.cs)
- [Current Shared incubation project](https://github.com/uniblab/Icod.CoreUtils/tree/main/Shared)

The authoritative upstream baseline is:

- [GNU patch 2.8 release announcement](https://lists.gnu.org/archive/html/info-gnu/2025-03/msg00014.html)
- [GNU patch 2.8 release archives](https://ftp.gnu.org/gnu/patch/)
- the GNU patch 2.8 source, tests, documentation, and installed manual pages from the pinned release.

This roadmap is subordinate to the repository-wide conventions and engineering gates in the main roadmap. Where the documents conflict, the current main roadmap governs repository policy and this document governs Patch-specific design and sequencing.

---

## Living status

| Item | Status |
|---|---|
| Suite | `Icod.Patch` |
| Co-resident executable project | `Icod.Patch` |
| Public command class | `Icod.Patch.Command` |
| Executable assembly name | `patch` |
| Current repository project | `patch/Icod.Patch.csproj` |
| Current implementation state | Waves A through D and Phase P11A implemented: normalized async front end, byte-preserving parsers, pure indexed application/matching, secure canonical multi-file planning, explicit artifact policy, E2–E4 conformance closure, and per-file transactions delegated to shared Completion Gate E6 |
| Current dedicated test project | `tests/Patch.Tests/Icod.Patch.Tests.csproj` |
| Required target framework | `net10.0` |
| Required language version | C# 13 |
| Authoritative upstream baseline | GNU patch 2.8 |
| Required CI runners | `windows-latest`, `ubuntu-latest`, `macos-latest` |
| Additional platform goal | Best-effort BSD support |
| Repository status | Co-resident in `Icod.CoreUtils.sln` until Completion Gate G |
| Scheduling model | Dependency-aligned partial order with Completion Gates E2–E6 |
| Completed scheduled waves | Wave A, P0–P4; Wave B1, P5–P6; Wave B2, P7; Wave C, P8 and initial P9; Wave D, P9–P10 closure; and Phase P11A initial E6 integration |
| Detailed ordering status | P0–P11A implemented; P11B waits for independent E6 validation by `cp`, `mv`, and `install` in Batches 44 and 45 |

---

## Wave A implementation status

Wave A, Phases P0 through P4, was implemented on August 1, 2026; full-checkout build and CI validation remain pending. The historical private `+`/`-` mutation format was removed rather than retained as a compatibility mode. The replacement front end uses the shared asynchronous `CommandContext` and `OptionParser`, accepts standard input, `-i`, and GNU-style operands, and feeds a bounded spill-backed source model. The source layer preserves byte offsets and LF, CRLF, CR, and incomplete-final-record state while identifying multiple sections and surrounding text. Complete unified, context, normal, and patch-compatible ed-script parsers now normalize file patches and hunks into immutable byte-preserving models while retaining exact source records for later reject generation.

At the end of Wave A the executable still deliberately refused to mutate live targets. Wave B1 then supplied the pure virtual application engine, Wave B2 resolved filename evidence into secure canonical multi-file plans, and Wave C added artifact creation plus provisional committed replacement.

The GNU patch 2.8 release archive metadata, official SHA-256, signing fingerprint, and `v2.8` source ref are recorded in `patch/upstream/GNU-patch-2.8.md`. Debug/Release and three-runner execution remain an integration validation item after these files are applied to a complete checkout.

## Wave B1 implementation status

Wave B1, Phases P5 and P6, is implemented as a pure application layer. It indexes existing target bytes in memory or in owner-private spill files; preserves each original record terminator; applies unified, context, normal, and ed operations; carries accumulated line deltas across multiple hunks; and returns immutable virtual-file results whose storage is independent from the input. Matching proceeds at the predicted location, then by alternating forward and backward offsets with forward tie preference, and only then by increasing fuzz. Horizontal blank runs can be canonicalized, reverse and already-applied input is detected from the first hunk and virtual existence state, and force, forward-only, batch, prerequisite, injected decision, and merge/diff3 policy are explicit engine inputs.

Wave B2 supplied filename selection, canonical paths, containment, and multi-file state over the pure engine. Wave C now consumes that plan for artifact creation and provisional committed application. Full-checkout managed build/test and all three CI runners remain pending integration validation.

## Wave B2 implementation status

Wave B2, Phase P7, is implemented as a read-only planning layer over `Icod.Path`. It decodes quoted filename evidence, extracts `Index:` candidates, honors explicit original-file operands, applies `-d` and platform-aware component stripping, ranks GNU candidates or selects the first existing POSIX candidate, and resolves every eligible target physically beneath the canonical working root. It models virtual creation and deletion, carries immutable state across multiple file patches, aggregates per-file statuses, and exposes revision-control retrieval through an injected provider and explicit disabled/ask/enabled policy.

P7 deliberately rejects lexical traversal, cross-volume targets, and links or reparse points that resolve outside the working root. Terminal links require `--follow-symlinks`. This containment policy is intentionally stricter than historical permissive absolute-path behavior and is recorded as a security divergence. P7 itself performs no target, backup, reject, output, metadata, mode, or transaction mutation; Wave C consumes its immutable plan.

Full-checkout Debug/Release and Windows, Ubuntu, and macOS CI validation remain pending after integration.

## Wave C implementation status

Wave C closes Phase P8 and starts Phase P9. The command now converts final P7 virtual state into immutable target, backup, reject, alternate-output, standard-output, and validation-only artifacts. GNU-visible policy includes simple and numbered backup naming, `PATCH_VERSION_CONTROL` and `VERSION_CONTROL` precedence, prefix and suffix interactions, automatic/context/unified rejects, `-o`, `-o -`, dry runs, quiet/verbose diagnostics, deterministic prompts, filename quoting, status aggregation, broken-output handling, normal replacement timestamps, requested local/UTC header timestamps including post-2038 values, and portable mode preservation through E3 and E4. Output, backup, and reject pathnames consume E2 final-indirection policy so GNU patch 2.8's `--follow-symlinks` rule applies to both input and output artifacts.

The initial P9 boundary is implemented through injectable `IPatchFileSystem` and `IPatchTransaction` contracts. It observes E3 identity and metadata, stages complete owner-private sibling files through E4 exclusive creation, preserves rollback copies before mutation, revalidates every destination and validation-only source immediately before commit, applies mode and timestamp metadata, and performs deterministic rollback and cleanup after injected failure or cancellation. Wave D closes P9 by making per-file recovery units, multi-file partial-success behavior, transaction outcomes, failure stages, and the Patch-facing E6 requirements explicit while retaining this adapter as provisional scaffolding.

Full-checkout Debug/Release and Windows, Ubuntu, and macOS CI validation remain pending after integration.

## Wave D implementation status

Wave D completes P9 and closes P10. Target, backup, per-target reject, and file-output artifacts now share an explicit patch-file recovery unit; shared destinations are modeled as independent units. The provisional transaction boundary distinguishes pre-commit failure, complete rollback, retained earlier-file commits, incomplete rollback, and incomplete cleanup. It flushes complete sibling temporaries before replacement, revalidates E3 observations immediately before commit, restores ownership, mode, and timestamps during recovery, continues rollback and cleanup after individual failures, and records committed and recovered units.

`PatchE6TransactionContract` freezes the requirements that Completion Gate E6 must satisfy: exclusive secure sibling creation, content completion and flush before replacement, identity revalidation, no-follow defaults, patch-file recovery scope, GNU-visible multi-file partial success, metadata restoration, containment, cancellation recovery, deterministic cleanup, and explicit atomicity and durability capabilities. P10 coverage verifies that artifact selection continues to consume `Icod.Path`, E3 metadata, and E4 mutation contracts for lexical and physical containment, terminal links, `--follow-symlinks`, post-2038 timestamps, modes, ownership, and host capability profiles. Phase P11A now adapts those Patch artifacts to shared E6; crash-recovery guarantees remain outside the current contract, and the unreachable command-local implementation is retained only until P11B removes it.

Full-checkout Debug/Release and Windows, Ubuntu, and macOS CI execution remain the integration merge gate.

---

## Executive summary

The historical Patch seed was not an incomplete GNU patch implementation in the ordinary sense. It consumed a private `+`/`-` line-command format associated with the repository's former simplified Diff output, decoded complete files as UTF-8 text, created a `.orig` backup, and rewrote the target directly.

Waves A through D and Phase P11A, P0 through P11A, have now replaced that seed with a GNU-oriented command boundary, byte-preserving source model, complete syntax parsers, a pure virtual application/matching engine, secure canonical multi-file planning, explicit artifact policy, E2–E4 conformance closure, a frozen Patch-facing E6 contract, and per-file recovery units implemented through shared Completion Gate E6. The current implementation:

- uses asynchronous orchestration and the shared `CommandContext` and `OptionParser`;
- accepts patch input from standard input, `-i`, or the GNU-style patch-file operand position;
- retains the optional original-file operand without mutating it;
- preserves patch bytes, byte offsets, line numbers, LF, CRLF, CR, and incomplete final records in a bounded spill-backed source;
- detects and completely parses unified, context, normal, and patch-compatible ed-script sections, including multiple file patches and surrounding or interstitial non-patch text;
- normalizes all four syntaxes into immutable byte-preserving file, range, hunk, operation, and data-line models while retaining exact source records for later rejects;
- rejects NUL-bearing directive lines, newline-bearing quoted filename evidence, malformed ranges, count mismatches, inconsistent context copies, unterminated ed text blocks, and resource exhaustion;
- has a dedicated Patch test project and separated fixture provenance;
- exposes pure exact/offset/fuzz/reverse/prerequisite/merge application over immutable virtual target content;
- consumes `Icod.Path` for `-d`, platform-aware `-p`, roots, volumes, links, containment, missing components, explicit operands, quoted and `Index:` names, and multi-file state;
- materializes target, backup, reject, output, standard-output, and validation-only artifacts, groups related filesystem artifacts into patch-file recovery units, and adapts those units to shared E6 transactional replacement;
- reports retained earlier-file commits separately from recovery of the currently failing file while shared E6 supplies secure sibling staging, flush, identity revalidation, atomic publication where available, rollback, metadata restoration, containment, cleanup, cancellation recovery, and structured capability diagnostics.

The former private syntax remains only as characterized historical evidence and is not exposed as a compatibility mode.

The intended co-resident architecture is:

```text
Current Shared incubation project
        ↓
Icod.Patch
        └── Icod.Patch.Command
```

Patch does not initially require `Icod.Patch.Shared`. It has one executable and one cohesive domain engine. Parser, matching, application, reject, backup, and transaction components should begin as internal modules inside `Icod.Patch`.

A separate engine or Shared assembly is created only if a concrete second consumer or package boundary later justifies it.

---

## Relationship to the main roadmap

The main roadmap begins the Patch workstream immediately after the consecutive Diffutils block so Patch has independent textual fixtures from:

```text
Icod.DiffUtils.Diff
GNU diff
other compatible diff producers
```

The production dependency boundary remains textual:

```text
Icod.DiffUtils
        │
        │ normal, context, unified, and ed patch text
        ↓
Icod.Patch
```

It must not become:

```text
Icod.Patch
        ↓
Icod.DiffUtils.Shared
```

Patch must consume patch text produced by GNU Diffutils, BSD tools, Git-compatible producers where the syntax overlaps, hand-authored patches, archived mailing-list patches, and future `Icod.DiffUtils` releases.

The E-series gates and Patch phases are scheduled as a partial order rather than two serial blocks. Parser and pure application work should not wait for filesystem gates, but Patch must consume each general filesystem contract before closing the phase that depends on it.

The dependency graph is:

```text
P0 → P1 → P2 → P3/P4 → P5 → P6
              │                   │
              │                   └──────────────┐
              │                                  │
E2 ───────────┴────────────────→ P7              │
E3 + E4 ───────────────────────→ P8              │
E4 ───────────────────────────────────────────→ P9
P7 + P8 + P9 + E2 + E3 + E4 ─→ P10
E6 + P10 ─────────────────────→ P11A
P11A + Batch 44 + Batch 45 ───→ P11B → P12
```

The hard ordering constraints are:

1. P2 and E2 must both complete before P7.
2. P7, E3, and E4 must complete before P8 is closed.
3. P6 and E4 must complete before P9's E6-facing filesystem boundary is closed.
4. P7 through P9 and E2 through E4 must complete before P10.
5. E6 and P10 must complete before P11A.
6. P11A and the independent E6 validation supplied by `cp`, `mv`, and `install` in Batches 44 and 45 must complete before P11B.
7. P11B must complete before final conformance and extraction readiness in P12.

Completion Gate E5 is not a direct Patch feature prerequisite. Patch does not need recursive-copy traversal. E5 remains upstream of E6 only to the extent that E6 consumes its identity, containment, metadata-preservation, no-follow mutation, failure, and cleanup contracts.

The resulting repository-wide waves are:

| Wave | Patch work | E-series and command validation |
|---|---|---|
| A | P0–P4: project, invocation, source model, and all patch parsers | E2/E3 design may proceed concurrently |
| B1 | P5–P6: exact application and matching over virtual target content | E2 may complete concurrently |
| B2 | P7: filenames, paths, and multi-file state | E2 and Batch 35 must complete before P7 |
| C | P8 and P9 start: artifacts, prompts, statuses, and provisional safe mutation | E3, Batch 36, E4, and Batches 38–43 proceed |
| D | P9 and P10 closure | E2–E4 are complete; E5 supplies E6 prerequisites |
| E | P11A initial transaction integration | E6 completes |
| F | P11B and P12 final closure | Batches 44 and 45 independently validate E6 |

This schedule avoids two opposite failures:

- delaying parser and matcher work until every filesystem gate is complete; and
- building permanent Patch-private path, metadata, mode, symlink, or transaction frameworks that later have to be discarded.

Patch remains co-resident until Completion Gate G and consumes general infrastructure through project references during incubation.

## Scope

`Icod.Patch` implements GNU patch 2.8 behavior in managed C# 13 for `net10.0`.

Its responsibilities include:

- command-line parsing;
- patch-source selection;
- patch format detection;
- normal patch parsing;
- context patch parsing;
- unified patch parsing;
- ed-script patch parsing where supported by GNU patch 2.8;
- leading and trailing non-patch text;
- multiple patches in one input stream;
- file-header interpretation;
- filename candidate selection;
- path-prefix stripping;
- directory changes;
- hunk application;
- exact matching;
- offset matching;
- fuzz;
- whitespace-canonical matching;
- reverse and already-applied detection;
- forced, batch, and interactive decision policy;
- prerequisite checking;
- file creation and deletion;
- multi-file application;
- output-file mode;
- dry runs;
- reject generation;
- backup generation;
- timestamp and mode handling;
- symlink policy;
- version-control retrieval policy where required by the pinned baseline;
- merge modes where required by GNU patch 2.8;
- diagnostics, verbosity, quoting, prompts, and exit statuses;
- secure and recoverable filesystem application;
- cancellation, broken-pipe, and cleanup behavior.

---

## Non-goals

The production implementation must not:

- invoke the host `patch` command;
- invoke the host `ed` command to apply ed scripts;
- depend on `Icod.DiffUtils.Shared`;
- depend on `Icod.LineEditor.Ed.Shared`;
- accept only text emitted by `Icod.DiffUtils`;
- expose the current private `+` and `-` line format as though it were GNU patch syntax;
- use whole-file UTF-16 `string` lists as the permanent input and application model;
- silently normalize line endings or encodings;
- silently weaken security or data-integrity behavior on Windows;
- fabricate Unix metadata where a platform cannot provide it;
- silently report success for unsupported operations;
- delete the original file before a recoverable replacement is ready;
- claim transactional or atomic behavior that the active platform provider cannot supply.

Native GNU patch may be used by optional differential tests, never by production code.

---

## Authoritative-source hierarchy

Use this priority order:

1. GNU patch 2.8 source and test suite;
2. GNU patch 2.8 documentation and installed manual;
3. GNU Diffutils documentation describing patch formats and patch behavior;
4. POSIX patch requirements where the selected GNU mode incorporates them;
5. historical GNU behavior required by the pinned release;
6. current upstream development documentation only as a research aid.

The current upstream `master` branch or later manual may contain behavior added after 2.8. Such behavior must not be assumed to belong to the baseline.

Every option and behavior must be classified as one of:

```text
Required by GNU patch 2.8
Required only in POSIX mode
Accepted GNU extension
Deliberately deferred
Platform-limited
Out of scope
```

The conformance ledger must identify the source file, test, manual section, or release note supporting each material behavior.

---

## GNU patch 2.8 release-specific requirements

GNU patch 2.8 was released on March 29, 2025.

Its release notes identify several requirements that deserve explicit roadmap and test coverage:

- `--follow-symlinks` applies to output files as well as input files;
- timestamps after 2038 are supported even on traditional platforms with 32-bit `time_t`;
- output filenames containing newline characters are no longer created;
- NUL bytes are rejected in diff directive lines;
- sequences of spaces and tabs around line numbers and in other POSIX-required locations are accepted;
- robustness was improved for very large sizes, stack exhaustion, I/O errors, memory exhaustion, process races, and signals arriving at difficult times.

These are not minor release-note details. They define parser, path, timestamp, resource, and failure behavior that the managed implementation must test directly.

---

## Current repository audit

## Current project identity

The current project is:

```text
patch/Icod.Patch.csproj
```

It currently sets:

```text
AssemblyName: patch
RootNamespace: Icod.Patch
TargetFramework: net10.0
```

It references the current Shared incubation project and contains the established Debug, Staging, and Release configuration groups.

P0 normalization completed:

- retained the already suite-correct `Icod.Patch.csproj` filename;
- added `<LangVersion>13.0</LangVersion>` to the first property group;
- retained `<AssemblyName>patch</AssemblyName>` and `<RootNamespace>Icod.Patch</RootNamespace>`;
- retained all established configuration groups, nullable references, implicit usings, and XML documentation;
- placed the executable in the `Icod.Patch` suite solution folder;
- added the dedicated test project beneath the top-level `tests` solution folder and all solution configurations.

Repository-wide Debug/Release and three-runner validation remains pending after integration into a complete checkout.

The project filename identifies the suite and command. The root namespace remains concise so the public facade is `Icod.Patch.Command`, without duplicating the suite name in an additional namespace segment or class name.

---

## Current entry point

P1 replaced the synchronous seed entry point with:

- asynchronous `Main`;
- a `CommandContext` core path;
- injected standard input, output, error, and byte-oriented standard input;
- cancellation propagation and repository-standard cancellation status;
- deterministic controlled operational diagnostics;
- dedicated help output and substantive XML documentation;
- a synchronous compatibility wrapper for direct callers.

A suitable orchestration path is:

```text
Program.Main
        ↓
CommandContext
        ↓
Icod.Patch.Command.RunAsync
        ↓
PatchApplication
```

The public command class should expose the repository's established synchronous compatibility wrapper and cancellation-aware asynchronous execution contract.

---

## Current command implementation

The Wave C command is a GNU-oriented front end over complete syntax parsers, a pure virtual target applicator, secure path planning, explicit artifact policy, and a provisional injected transaction boundary. It:

- accepts `[ORIGFILE [PATCHFILE]]`, `-i/--input`, standard-input patch streams, `--binary`, `--help`, and `-v/--version`;
- reserves GNU `-b` for its later `--backup` implementation rather than aliasing it to `--binary`;
- validates duplicate source selection, missing option values, extra operands, and cancellation;
- uses raw streams when available and a streaming text-reader adapter only for compatibility calls;
- indexes input into a uniquely named private temporary spool and removes that exact spool on disposal;
- recognizes and parses complete unified, context, normal, and patch-compatible ed-script file sections into immutable byte-preserving models without mutating the original-file operand;
- preserves exact hunk source records for later reject serialization and retains leading, interstitial, and trailing non-patch regions;
- maps `-f`, `-F`, `-l`, `-m`/`--merge`, `-N`, `-R`, and `-t` into the pure Wave B1 engine policy;
- implements `-d`, `-p`, `-g`, `--posix`, `POSIXLY_CORRECT`, `PATCH_GET`, and input-side `--follow-symlinks`;
- selects explicit, old, new, and `Index:` candidates through `Icod.Path`, carries state across multiple file patches, and produces virtual create/modify/delete actions;
- rejects targets that escape the canonical working root, including through links or reparse points;
- materializes target, backup, reject, output, standard-output, and validation-only artifacts with explicit metadata and expected-identity evidence;
- implements GNU backup version control, mismatch backups, reject naming and formatting, `-o`, `-r`, dry runs, quiet/verbose output, deterministic prompts, filename quoting, timestamp policy, and statuses 0, 1, and 2;
- stages every committed write behind an injected provisional transaction boundary with exclusive sibling temporaries, pre-commit revalidation, rollback copies, failure injection, cancellation cleanup, and no-follow output policy;
- keeps the P9 replacement adapter provisional until Completion Gate E6 supplies the shared final atomic replacement contract.

The implementation preserves the public namespace `Icod.Patch`, lowercase assembly name `patch`, and public `Command` facade. The private simplified format has been removed and remains covered only by a retirement characterization test.

---

## Current tests

P0 created `tests/Patch.Tests/Icod.Patch.Tests.csproj` with root namespace `Icod.Patch.Tests`, `InternalsVisibleTo` access to the internal detector, and Debug, Staging, and Release solution mappings beneath the top-level `tests` solution folder.

The suite covers direct command invocation, process-host help and diagnostics, source selection, operand conflicts, retirement of the private syntax, dry-run immutability, cancellation, status accumulation, every parsed format, separated fixture roots, source offsets and terminators, multiple sections, leading/interstitial/trailing text, headers and timestamps, ranges, hunk counts, context-copy consistency, normal data blocks, ed reverse ordering and dot protection, malformed directives and filenames, binary records, resource limits, deterministic scanner fuzz input, exact temporary-spool cleanup, indexed target round trips, spill-backed target and result lifecycles, exact and multi-hunk application, ed interpretation, creation/deletion, offsets, fuzz, whitespace matching, reversal, already-applied policy, prerequisites, merge/diff3 output, candidate limits, randomized invariants, GNU/POSIX filename ranking, `-d`, platform-aware `-p`, quoted and `Index:` names, roots, volumes, links, reparse points, containment, retrieval policy, multi-file state, path attacks, backups, rejects, output files, binary standard output, post-2038 timestamps, Unix modes, output symlinks, skipped/all-failed policies, rollback, stale-identity rejection, injected failures, cancellation cleanup, and opt-in GNU patch 2.8 differential checks.

A separate parser or engine test project is not currently justified. Full solution builds and Windows, Ubuntu, and macOS CI execution remain integration validation items.

---

## Current Shared capabilities relevant to Patch

The current Shared incubation project already contains or schedules facilities useful to Patch:

- command-line parsing;
- command contexts;
- diagnostics and quoting;
- byte and decoded text models;
- LF and NUL record framing;
- delimiter and escape parsing;
- regular-expression infrastructure;
- secure temporary workspaces;
- platform capability providers;
- filesystem metadata;
- canonical paths;
- path containment;
- process support;
- cancellation and cleanup patterns;
- future transactional replacement.

Patch should consume these facilities where their contracts fit.

Patch-specific code remains in `Icod.Patch`:

- patch syntax;
- patch documents;
- file-header interpretation;
- hunk matching;
- fuzz;
- offsets;
- reversal detection;
- reject generation;
- patch-specific backup and output policy;
- application state;
- GNU patch diagnostics and prompts.

General mechanisms should not be copied into Patch merely because their final `Icod.CommandFramework` package has not yet been extracted.

---

## Recommended architecture

## Project boundary

Initially use one executable project:

```text
Icod.Patch
```

It contains:

```text
Icod.Patch.Command
Icod.Patch.Application
internal parser modules
internal matching modules
internal application modules
internal filesystem policy modules
```

Do not create `Icod.Patch.Shared` merely for symmetry with Diffutils or ProcPs.

A Patch-specific Shared or engine project becomes justified only if:

- another executable is added to the Patch suite;
- a stable public library API is deliberately desired;
- tests cannot remain effective through internal access;
- a second real consumer needs the same Patch-domain engine;
- packaging the engine independently has a concrete use case.

Until then, another assembly would add versioning and accessibility costs without creating a meaningful dependency boundary.

---

## Public API

Keep the public surface small.

Required public type:

```text
Icod.Patch.Command
```

Potential public methods follow repository conventions:

```csharp
public static int Run(
    string[] args,
    TextReader stdin,
    TextWriter stdout,
    TextWriter stderr
);

public static Task<int> RunAsync(
    string[] args,
    TextReader stdin,
    TextWriter stdout,
    TextWriter stderr,
    CancellationToken cancellationToken
);

public static Task<int> RunAsync(
    string[] args,
    CommandContext context
);
```

Exact overloads should match current repository patterns at implementation time.

Most supporting types should remain `internal`.

---

## Proposed source layout

A possible layout is:

```text
patch/
├── README.md
├── Icod.Patch.csproj
├── Program.cs
└── src/
    ├── README.md
    ├── Command.cs
    ├── Application.cs
    │
    ├── Options/
    │   ├── README.md
    │   ├── PatchOptions.cs
    │   ├── PatchOptionDefinitions.cs
    │   └── PatchInvocation.cs
    │
    ├── Parsing/
    │   ├── README.md
    │   ├── PatchStreamParser.cs
    │   ├── PatchFormatDetector.cs
    │   ├── UnifiedPatchParser.cs
    │   ├── ContextPatchParser.cs
    │   ├── NormalPatchParser.cs
    │   ├── EdScriptPatchParser.cs
    │   ├── PatchSourceReader.cs
    │   └── PatchParseException.cs
    │
    ├── Model/
    │   ├── README.md
    │   ├── PatchDocument.cs
    │   ├── FilePatch.cs
    │   ├── PatchFormat.cs
    │   ├── PatchHeader.cs
    │   ├── FileNameCandidate.cs
    │   ├── Hunk.cs
    │   ├── HunkLine.cs
    │   ├── HunkRange.cs
    │   ├── PatchTimestamp.cs
    │   └── SourceSpan.cs
    │
    ├── Matching/
    │   ├── README.md
    │   ├── HunkMatcher.cs
    │   ├── HunkMatchRequest.cs
    │   ├── HunkMatchResult.cs
    │   ├── FuzzPolicy.cs
    │   ├── OffsetSearch.cs
    │   ├── WhitespaceMatcher.cs
    │   └── ReversePatchDetector.cs
    │
    ├── Application/
    │   ├── README.md
    │   ├── PatchApplicator.cs
    │   ├── FilePatchApplicator.cs
    │   ├── HunkApplicator.cs
    │   ├── PatchApplicationResult.cs
    │   ├── FileApplicationResult.cs
    │   ├── MergeApplicator.cs
    │   └── PrerequisiteEvaluator.cs
    │
    ├── Content/
    │   ├── README.md
    │   ├── PatchRecord.cs
    │   ├── TargetRecord.cs
    │   ├── IndexedTargetContent.cs
    │   ├── SpillBackedTargetContent.cs
    │   └── ContentWriter.cs
    │
    ├── Files/
    │   ├── README.md
    │   ├── PatchFileSelector.cs
    │   ├── FileNameSelectionPolicy.cs
    │   ├── PathStripper.cs
    │   ├── PatchPathPolicy.cs
    │   ├── PatchFileSystem.cs
    │   ├── PatchTransaction.cs
    │   ├── BackupPolicy.cs
    │   ├── RejectPolicy.cs
    │   ├── TimestampPolicy.cs
    │   └── VersionControlRetriever.cs
    │
    ├── Diagnostics/
    │   ├── README.md
    │   ├── PatchDiagnostic.cs
    │   ├── PatchDiagnosticWriter.cs
    │   ├── PatchPromptPolicy.cs
    │   └── PatchExitStatus.cs
    │
    └── Compatibility/
        ├── README.md
        ├── PosixPatchProfile.cs
        └── GnuPatchProfile.cs
```

This layout is illustrative. The final split should follow cohesive responsibilities rather than file-count targets.

Under repository policy:

- every multi-file source directory receives a substantive `README.md`;
- all public, protected, and internal types and members receive substantive XML documentation;
- command-local implementation types remain internal;
- files are UTF-8 with LF line endings.

---

## Core domain model

## Patch document

A parsed patch stream may contain:

```text
leading text
one or more file patches
inter-patch text
trailing text
```

The parser should retain source locations and enough original data to produce accurate diagnostics and rejects.

A conceptual model:

```text
PatchDocument
├── PatchPreamble
├── FilePatch[]
└── PatchTrailer
```

Each `FilePatch` may include:

```text
detected format
old and new filename candidates
index or prerequisite lines
timestamps
mode or metadata extensions
one or more hunks or ed commands
source span
format-specific original lines
```

---

## Patch formats

The initial format enum should distinguish:

```text
Unknown
Normal
Context
Unified
EdScript
```

Do not collapse normal, context, and unified patches into one parser before their lexical rules are correctly understood.

They may normalize into common application models after parsing.

### Unified patches

Support:

- old and new file headers;
- timestamps and timezone forms accepted by GNU 2.8;
- hunk ranges;
- optional hunk section text;
- context, deletion, and addition lines;
- incomplete-line markers;
- multiple file patches;
- zero-length ranges;
- file creation and deletion forms;
- leading indentation and permitted whitespace.

### Context patches

Support:

- old and new file headers;
- separator lines;
- old and new hunk ranges;
- context, added, removed, and changed lines;
- asymmetric hunk sections;
- incomplete-line markers;
- timestamps;
- file creation and deletion forms;
- reject output rules.

### Normal patches

Support:

- append, change, and delete commands;
- one-based ranges;
- old and new content blocks;
- separator lines;
- trailing-garbage validation;
- malformed command detection;
- incomplete-line behavior;
- multiple commands and multiple file patches where recognized.

### Ed scripts

GNU patch can apply ed-style difference scripts.

Production code must not:

- launch native `ed`;
- reference `Icod.LineEditor.Ed.Shared`;
- assume only scripts emitted by `Icod.DiffUtils`.

Implement the minimal internal ed-script application grammar required by GNU patch 2.8.

This parser is not a general-purpose Ed interpreter. It should support only the patch-compatible command subset established by the pinned source and tests.

---

## Source representation and byte fidelity

Patch syntax is line-oriented, but patch content is not safely modeled as arbitrary .NET text.

The implementation must preserve:

- original patch bytes where diagnostics or rejects require them;
- line separators;
- whether the final line was terminated;
- target-file bytes;
- unchanged target lines byte-for-byte;
- inserted line bytes according to patch input semantics;
- incomplete-line markers;
- CRLF and LF distinctions where the selected mode requires them;
- NUL rejection in directive lines;
- binary mode behavior.

A suitable architecture separates:

```text
patch syntax decoding
patch payload bytes
target content records
output content records
```

Do not silently decode all target files as UTF-8.

Do not normalize all output through `WriteAllLines`.

Do not assume that `Environment.NewLine` is the correct separator for patched file data.

Patch diagnostics may use host-generated line endings. Patch and target data must follow their input and command semantics.

---

## Memory and streaming strategy

Patch cannot always operate as a one-pass stream transformation.

Hunk offsets, reversal, fuzz, multiple hunks, ed scripts, and output transactions require indexed access to the target content.

The required strategy is:

```text
small target
    → in-memory indexed records

large target
    → secure spill-backed indexed records
```

Use Shared temporary-workspace infrastructure rather than unbounded memory.

The implementation should:

- stream the patch input;
- parse one file patch at a time where possible;
- avoid retaining unrelated completed files;
- index the current target file;
- spill large record content to secure temporary storage;
- preserve record terminators;
- support cancellation during reads, matching, application, writes, and cleanup;
- impose checked arithmetic and resource limits;
- handle very long lines and very large file sizes without integer overflow.

Whole-file `List<string>` buffering is not an acceptable final model.

---

## Hunk matching model

Hunk matching is the defining Patch engine.

A pure matching layer should take:

```text
target content
hunk
expected location
accumulated offset
maximum fuzz
whitespace policy
direction
```

and return:

```text
matched
not matched
matched location
offset
fuzz used
direction
context evidence
diagnostic reason
```

The matching layer must not mutate the filesystem.

---

## Exact matching

Implement exact application first.

For each hunk:

- verify the old-side content at the expected location;
- verify context and removal lines;
- preserve addition lines;
- account for prior hunk offsets;
- reject overlapping or contradictory application;
- update the logical target;
- record application results independently from displayed output.

The exact engine must be exhaustively tested before fuzz or reversal heuristics are added.

---

## Offset matching

If the expected location fails, GNU patch searches nearby locations.

The implementation must determine from the pinned source and tests:

- search ordering above and below the expected location;
- interaction with accumulated previous offsets;
- behavior at file boundaries;
- ambiguity resolution;
- reporting of successful offsets;
- performance limits for large files;
- cancellation behavior.

Do not implement a broad unbounded quadratic search without performance safeguards.

---

## Fuzz

Fuzz allows leading and trailing context to be ignored under controlled rules.

The engine must define:

- maximum fuzz;
- which context lines may be dropped;
- symmetry or asymmetry of dropped context;
- interaction with zero-context patches;
- interaction with offset search;
- reported fuzz level;
- mismatch and reject behavior.

Fuzz applies to context evidence. It must not permit removal lines to be ignored or silently modify unrelated data.

---

## Whitespace-canonical matching

Where selected by the appropriate option, whitespace matching must follow GNU patch semantics rather than a generic `Trim` or regular-expression replacement.

Define and test:

- horizontal whitespace runs;
- leading and trailing whitespace;
- empty lines;
- tabs versus spaces;
- locale effects where applicable;
- inserted output text;
- binary mode;
- CRLF handling.

Matching policy and output preservation are separate concerns.

---

## Reversal and already-applied detection

Patch may infer that a patch is reversed or has already been applied.

The implementation should:

1. attempt the requested direction;
2. use GNU-compatible evidence to consider the reverse direction;
3. apply force, forward, batch, and interactive policies;
4. distinguish reverse application from already-applied detection;
5. preserve deterministic behavior without a terminal;
6. produce the proper diagnostics and status.

Do not automatically reverse every failed patch.

The decision policy belongs above the pure matcher.

---

## Merge behavior

If GNU patch 2.8 supports the selected merge modes, implement them as explicit application profiles.

The roadmap must pin:

- available merge styles;
- conflict-marker format;
- labels;
- ancestor interpretation;
- hunk conflicts;
- output and reject interaction;
- statuses;
- binary and incomplete-line policy.

Do not reuse `Icod.DiffUtils.Shared` merge state. The public patch text and pinned Patch behavior remain authoritative.

---

## Filename selection

Patch files can provide several filename candidates.

The exact selection algorithm must be copied from the pinned GNU behavior, including relevant candidates such as:

```text
old filename
new filename
Index line
explicit original-file operand
prerequisite or revision-control context
```

Implement and test:

- nonexistent candidates;
- shortest or best candidate rules;
- timestamp and header influence;
- file creation and deletion;
- `/dev/null` or platform-equivalent patch notation where applicable;
- quoted and escaped names;
- names with spaces and tabs;
- names beginning with `-`;
- newline rejection;
- NUL rejection;
- platform path syntax.

The candidate selector should return a structured decision and evidence suitable for diagnostics.

---

## Prefix stripping and working directories

Support the GNU 2.8 behavior for:

```text
-d directory
-p number
```

Path-prefix stripping must be component-aware.

It must not be implemented by deleting a fixed number of characters.

Define cross-platform component behavior for:

- `/`;
- `\`;
- drive roots;
- UNC paths;
- device paths;
- repeated separators;
- dot segments;
- empty paths;
- names without enough components.

The main canonical-path gate eventually provides lexical and physical resolution. Patch owns the GNU `-p` policy layered over it.

---

## Security model

Patch input is untrusted.

Security work is part of the core design, not a final add-on.

## Path attacks

Test and define behavior for:

- absolute paths;
- parent traversal;
- subdirectory traversal;
- mixed separators;
- drive-relative paths;
- UNC paths;
- Windows device namespaces;
- reserved names;
- alternate data streams;
- Unicode normalization collisions;
- case-folding collisions;
- newline filenames;
- NUL directive content;
- symlink escapes;
- hard-link surprises;
- reparse points;
- directory replacement races;
- validation/open races.

Compatibility and stronger containment are not automatically identical.

The implementation must first establish GNU patch 2.8 behavior. Any deliberate stricter security behavior must be documented as a divergence and tested on all supported platforms.

---

## Symlink policy

Pin and test:

- default symlink behavior;
- `--follow-symlinks`;
- input paths;
- output paths;
- backups;
- rejects;
- file creation;
- file deletion;
- replacement races;
- Unix symlinks;
- Windows symbolic links and reparse points.

GNU patch 2.8 specifically changed `--follow-symlinks` to apply to output files as well as input files. This must have dedicated tests.

---

## Directive-line validation

GNU patch 2.8 rejects NUL bytes in diff directive lines.

The parser should distinguish:

```text
directive syntax
payload lines
non-patch leading or trailing text
```

Do not apply one blanket byte rule to all patch content without checking the pinned semantics.

Filenames containing newlines must not be used to create output files.

Diagnostics must quote hostile filenames safely.

---

## Resource attacks

Test:

- enormous line numbers;
- range overflow;
- negative or zero invalid ranges;
- enormous hunk counts;
- enormous line lengths;
- repeated failed offset searches;
- malicious fuzz cases;
- deeply repeated leading garbage;
- memory exhaustion;
- temporary-storage exhaustion;
- integer overflow;
- cancellation during every major stage.

Use checked arithmetic and explicit limits where GNU compatibility allows.

A resource-limit failure must leave original files intact.

---

## Filesystem integration and transaction model

Patch parsing, immutable models, exact application, offsets, fuzz, reversal, and most diagnostics are deliberately independent from live filesystem mutation.

The E-series contracts are adopted at the first phase that semantically needs them, not postponed to one late retrofit.

## Completion Gate E2 and Phase P7

P7 consumes E2 directly for:

- lexical path normalization;
- physical path resolution;
- link and reparse-point inspection;
- missing-component policy;
- loop detection;
- platform roots and volumes;
- containment and relative paths;
- stable structured path failures.

Patch-specific `-d`, `-p`, filename selection, candidate ranking, `/dev/null` interpretation, missing-file decisions, and version-control retrieval policy remain above these mechanisms.

P2 and E2 are independent hard predecessors of P7: P2 supplies parsed filename evidence and E2 supplies the path semantics used to act on it.

Do not create a complete provisional canonical-path subsystem inside Patch.

## Completion Gates E3 and E4 and Phase P8

P8 consumes E3 metadata and timestamp capabilities for:

- patch header timestamps;
- timestamp-setting options;
- post-2038 values;
- unavailable metadata diagnostics;
- precision differences;
- creation and deletion metadata behavior;
- output-file metadata;
- backup and reject metadata policy.

P8 consumes E4 mode and basic mutation capabilities for:

- mode parsing and representation;
- file creation and deletion;
- symlink and no-follow behavior;
- race-aware single-path operations;
- controlled privilege and platform diagnostics.

Patch owns the GNU decision policy for target, backup, reject, and output artifacts. It does not own a second metadata, mode, or low-level mutation framework.

P7, E3, and E4 must complete before P8 is closed.

## Completion Gate E5 and the E6 dependency path

Patch is not a recursive-copy command and does not directly require all of E5.

E5 matters only where E6 consumes its general:

- identity and provenance;
- containment;
- metadata-preservation;
- no-follow mutation;
- partial-failure;
- cleanup;
- destination safety

contracts.

Patch should not wait for unrelated recursive-copy behavior to parse, match, or apply hunks in a virtual model.

## Phase P9 and concurrent E6 contract development

P9 places all target mutation behind an internal injected boundary before E6 is finalized.

The provisional implementation must:

- use secure exclusive temporary creation;
- never remove the only recoverable original before a replacement is ready;
- support deterministic cleanup;
- expose cancellation and staged failure injection;
- characterize symlink and reparse-point behavior;
- keep parser and matcher contracts independent from filesystem commits;
- remain explicitly replaceable.

P9 is permitted to help shape the E6 API. It must not become a competing permanent Patch transaction framework.

## Phase P10 — E2–E4 conformance closure

P10 is no longer the first integration pass.

By P10, P7 through P9 have already consumed E2 through E4. P10 closes the conformance matrix:

- canonical path and containment behavior;
- roots, volumes, separators, and unusual names;
- timestamps, including post-2038 values;
- modes and metadata;
- race-aware single-path mutation;
- input and output symlink behavior;
- `--follow-symlinks`;
- Windows, Linux, macOS, and best-effort BSD capabilities.

P10 must complete before P11A.

## Completion Gate E6 and Phase P11A

P11A replaces the provisional P9 internals with:

- secure sibling temporary files;
- exclusive creation;
- output flushing;
- atomic replacement where supported;
- backup-name generation and retention;
- rollback;
- containment;
- metadata restoration;
- deterministic cleanup;
- explicit non-atomic capability results.

Patch's proposed file flow is:

```text
parse and validate patch
        ↓
read and index target
        ↓
compute complete proposed output
        ↓
prepare rejects and backup decisions
        ↓
write secure sibling temporary
        ↓
flush and validate
        ↓
prepare backup
        ↓
commit replacement
        ↓
restore metadata
        ↓
commit reject/output artifacts
        ↓
cleanup
```

P11A tests Patch-specific combinations including partial hunk failure, backup and reject coexistence, alternate output, multi-file partial success, identity races, and cancellation between commit stages.

## Batches 44 and 45 and Phase P11B

E6 is not considered stable solely because Patch uses it.

`cp`, `mv`, and `install` exercise different replacement, copying, cross-filesystem, ownership, mode, and installation policies. Their validation may reveal contract changes.

P11B occurs after Batches 44 and 45:

- adopt any stabilized E6 changes;
- remove all provisional Patch-local replacement code;
- rerun target, backup, reject, output, metadata, link, rollback, cleanup, cancellation, and non-atomic fallback tests;
- verify that no data-loss window remains.

Multi-file application requires an explicit transaction policy. GNU patch does not necessarily promise one all-or-nothing transaction for the entire stream. The implementation must preserve GNU-visible partial-application behavior while ensuring that each individual file transition is recoverable and accurately reported.

## Backups

Implement the complete GNU 2.8 backup policy, including relevant:

- backup enablement;
- backup-on-mismatch behavior;
- version-control selection;
- prefixes;
- basename prefixes;
- suffixes;
- simple and numbered backups;
- existing backup collisions;
- file creation and deletion;
- permissions and metadata;
- failure behavior.

Do not hard-code `.orig` as the only backup form.

Backup path construction must pass through the same security and containment analysis as target paths.

---

## Rejects

Implement:

- default reject naming;
- explicit reject output;
- discard behavior where supported;
- reject format;
- source hunk and location preservation;
- multiple rejected hunks;
- partial application;
- permissions;
- write failures;
- cleanup;
- status 1 behavior.

Rejected hunks should be emitted according to the pinned GNU format rules, not from a lossy normalized internal approximation.

The parser should preserve enough original format data to produce correct rejects.

---

## Dry run and output-file mode

A dry run must:

- parse all input;
- perform filename selection;
- read target files;
- run matching and application decisions;
- produce applicable diagnostics;
- avoid target, backup, reject, and metadata mutations;
- return the GNU-compatible status.

Output-file mode must define:

- single-file versus multi-file restrictions;
- standard output;
- binary output;
- output replacement;
- metadata;
- failure behavior;
- interaction with backups and rejects;
- broken pipes.

---

## Version-control retrieval

GNU patch contains historical support for retrieving missing files from version-control systems.

The roadmap must inventory the exact GNU 2.8 behavior, environment variables, prompts, and supported systems before implementation.

Production code must not invoke a command through unsafe shell interpolation.

A provider model should distinguish:

```text
disabled
ask
enabled
unsupported provider
retrieval failed
retrieved path
```

Version-control retrieval is not required for the first exact hunk engine, but it is required before full GNU 2.8 conformance if the pinned baseline includes it.

---

## Diagnostics and prompts

Implement the exact policy for:

- verbose, default, and silent output;
- force mode;
- batch mode;
- forward mode;
- interactive questions;
- nonterminal standard input;
- malformed patches;
- skipped patches;
- reversed patches;
- applied offsets;
- fuzz;
- rejected hunks;
- file creation and deletion;
- backups;
- version-control retrieval;
- path and security failures;
- I/O errors;
- cancellation.

Patch input and interactive answers may both involve standard input. The architecture must prevent accidental competition between them.

If patch input is read from standard input, interactive prompting may need a terminal-specific input provider according to GNU behavior. This must be pinned and tested rather than improvised.

Filenames and patch text in diagnostics must use the Shared quoting policy where compatible.

---

## Exit statuses

Treat exit statuses as a primary contract.

The expected GNU model is:

```text
0 — all patches and hunks applied successfully
1 — one or more hunks were rejected or conflicts remained
2 — serious trouble
```

The detailed mapping must cover:

- malformed input;
- missing files;
- skipped patches;
- partially applied multi-file patches;
- rejected hunks;
- dry runs;
- merge conflicts;
- backup or reject write failure;
- output write failure;
- broken pipes;
- cancellation;
- unsupported platform capabilities.

Do not derive the status only from whether an exception was thrown.

---

## Option and behavior matrix

The complete source-defined GNU patch 2.8 option inventory is pinned in [`patch/upstream/GNU-patch-2.8-option-matrix.md`](patch/upstream/GNU-patch-2.8-option-matrix.md).

The matrix records:

- every short and long spelling declared by the 2.8 source;
- aliases, argument arity, accepted value domains, and conditional entries;
- the obsolete narrow `-b SUFFIX` compatibility case;
- source-defined but normally hidden options such as `--follow-symlinks` and debug flags;
- the owning Patch phase and current implementation state;
- the upstream 2.8 test areas that supply later conformance evidence.

The inventory is complete at P0. Exact conflicts, repetition, prompts, environment-variable interactions, diagnostics, platform effects, and differential behavior are completed by each owning phase and closed in P12. Only the pinned 2.8 source and tests settle availability and spelling; later upstream documentation is not silently substituted.

---

## Detailed development phases

The phase labels do not alter the numbered CoreUtils batches.

They refine the in-solution Patch/E-series workstream in the main roadmap. The phases are listed numerically for ownership and checklist clarity, but the repository executes them according to the dependency graph above rather than treating all P phases as one isolated serial block followed by all E gates.

The scheduling checkpoints are:

```text
Wave A: P0–P4
Wave B1: P5–P6 while E2 completes
E2 + Batch 35
Wave B2: P7
E3 + Batch 36 + Batch 37
E4
Wave C: P8 complete and P9 started
Batches 38–40
E5 + Batches 41–43
Wave D: P9 and P10 close
E6
P11A
Batches 44–45
P11B and P12
```

## Phase P0 — Normalize project identity and capture the seed

- [x] Record GNU patch 2.8 in the authoritative-version ledger.
- [x] Pin the release archive and verify its SHA-256 and signing identity against the official GNU release announcement.
- [x] Record the source commit or release tag.
- [x] Generate and pin the complete GNU patch 2.8 source-defined option inventory and upstream test map.
- [x] Retain and verify the suite-correct `Icod.Patch.csproj` project identity.
- [x] Add `<LangVersion>13.0</LangVersion>`.
- [x] Preserve assembly name `patch`.
- [x] Preserve root namespace `Icod.Patch`.
- [x] Preserve public class `Icod.Patch.Command`.
- [x] Move the project into the suite-correct solution folder.
- [x] Create `Icod.Patch.Tests`.
- [x] Add it to the solution-wide build and CI entry point.
- [x] Add project and source `README.md` files.
- [x] Capture characterization coverage for the private seed syntax and its retirement.
- [x] Delete the private format; do not expose a non-GNU compatibility mode.
- [x] Establish GNU, Icod Diffutils, independent, malformed, and binary fixture directories.
- [ ] Verify Debug and Release builds and all three CI platforms after integration into a complete checkout.

## Phase P1 — Invocation, options, and command context

- [x] Replace synchronous `Main` with asynchronous orchestration.
- [x] Add a `CommandContext` core path.
- [x] Retain synchronous compatibility wrappers.
- [x] Build a declarative Shared `OptionParser` definition covering the complete GNU patch 2.8 source-defined option-name inventory.
- [x] Implement standard input and `-i` patch-source selection.
- [x] Implement original-file operand rules.
- [x] Implement `--help` and `--version`.
- [x] Implement option conflicts and missing-value diagnostics.
- [x] Define prompt-input ownership.
- [x] Define program-name and diagnostic quoting.
- [x] Define the exit-status accumulator.
- [x] Add command-line and process-host integration tests.

## Phase P2 — Patch stream, source mapping, and format detection

- [x] Build a byte-preserving patch-source reader.
- [x] Preserve source byte offsets and line locations.
- [x] Recognize permitted leading and trailing text.
- [x] Recognize multiple file patches in one stream.
- [x] Implement format detection.
- [x] Accept required spacing around directive line numbers.
- [x] Reject NUL bytes in directive lines.
- [x] Prevent output filenames containing newlines.
- [x] Handle CRLF, CR, LF, and byte-oriented input according to the P2 GNU 2.8 source contract.
- [x] Preserve incomplete records and recognize incomplete-line markers.
- [x] Distinguish malformed input from valid text containing no patch.
- [x] Add deterministic parser fuzz tests and resource limits.

## Phase P3 — Unified and context formats

- [x] Implement unified file headers.
- [x] Implement unified hunk headers and optional section text.
- [x] Implement unified hunk lines and ranges.
- [x] Implement context file headers.
- [x] Implement context old and new hunk sections.
- [x] Implement added, removed, changed, and context lines.
- [x] Normalize both formats into common immutable hunk models.
- [x] Preserve source data needed for rejects.
- [x] Implement file creation and deletion forms.
- [x] Add independent GNU, hand-written, malformed, and third-party corpora.

## Phase P4 — Normal and ed-script formats

- [x] Implement normal append, change, and delete commands.
- [x] Validate ranges and trailing garbage.
- [x] Implement normal-format data blocks.
- [x] Implement the minimal GNU patch ed-script grammar internally.
- [x] Do not invoke native `ed`.
- [x] Do not reference `Icod.LineEditor.Ed.Shared`.
- [x] Normalize operations into application models where safe.
- [x] Preserve format-specific reject behavior.
- [x] Add fixtures from GNU Diffutils and `Icod.DiffUtils`.

Wave A parser implementation is complete. The format-selection options `-u`, `-c`, `-n`, and `-e` now select the corresponding complete parser. Parser fixtures were independently smoke-checked with GNU patch 2.8 where applicable. Full repository Debug/Release builds and Windows, Ubuntu, and macOS CI execution remain pending integration validation. No P3/P4 code opens or mutates a target file.

## Phase P5 — Pure exact application engine

- [x] Implement in-memory indexed target content.
- [x] Add owner-private spill-backed target content for large files.
- [x] Preserve bytes and record terminators.
- [x] Implement exact hunk verification.
- [x] Implement additions, deletions, and replacements.
- [x] Implement accumulated hunk offsets.
- [x] Implement multi-hunk application.
- [x] Implement ed-script operation application.
- [x] Implement creation and deletion in the virtual filesystem model.
- [x] Produce immutable application results with storage ownership independent from the input.
- [x] Add property and invariant tests.
- [x] Add cancellation, spill-backed, and bounded huge-input tests.

## Phase P6 — Matching heuristics and direction policy

- [x] Implement nearby offset search with forward-before-backward tie ordering.
- [x] Implement configurable fuzz.
- [x] Implement canonicalized horizontal-blank matching.
- [x] Implement reverse-direction matching.
- [x] Implement already-applied detection, including create/delete virtual-state reversal.
- [x] Implement force, forward, and batch policies.
- [x] Implement injected interactive direction decisions without console coupling.
- [x] Implement prerequisite checks.
- [x] Implement GNU-compatible `merge` and `diff3` conflict output.
- [x] Add performance limits for adversarial matching.
- [x] Add opt-in differential tests against an installed GNU patch 2.8.

Wave B1 implementation is complete. P5/P6 remain pure and do not select canonical paths or commit filesystem changes. Full-checkout Debug/Release builds and the Windows, Ubuntu, and macOS CI matrix remain pending integration validation.

## Phase P7 — Files, paths, and multi-file application

**Hard prerequisites:** P2, P5, P6, Completion Gate E2, and the E2 validation supplied by Batch 35.

- [x] Consume the shared canonical-path provider; do not create a Patch-private replacement.
- [x] Implement filename candidates.
- [x] Implement explicit original-file operand behavior.
- [x] Implement `-d`, including relative patch-source interpretation.
- [x] Implement component-aware and platform-aware `-p`.
- [x] Implement quoted and unusual filenames.
- [x] Implement roots, volumes, alternate separators, links, reparse points, and containment through E2.
- [x] Implement multiple file patches and carry virtual state for repeated canonical targets.
- [x] Implement missing-file decisions for creation, failure, and revision-control retrieval policy.
- [x] Implement creation and deletion in the application plan without committing transactions yet.
- [x] Implement `-g`, `PATCH_GET`, POSIX defaults, and an injected version-control retrieval provider without shell interpolation.
- [x] Implement multi-file status aggregation.
- [x] Add path-security tests for traversal, cross-volume names, terminal links, reparse points, loops, and physical containment escapes.
- [x] Keep the pure matcher and hunk applicator independent from live filesystem mutation.
- [x] Do not claim metadata, mode, backup, reject, output, prompt, or transaction conformance yet.

P7 is complete. The application plan owns all virtual input and result storage and records candidate evidence, canonical selection, create/modify/delete action, retrieval provenance, per-file result, and aggregate status. Every target must remain inside the physically canonical `-d` root. This is an intentional security restriction beyond historical permissive handling of absolute or escaping patch names.

## Phase P8 — Rejects, backups, output, and user interaction

**Hard prerequisites:** P7, Completion Gates E3, E3R, and E4.

- [x] Implement reject generation.
- [x] Implement reject naming and explicit reject destinations.
- [x] Implement backup policy.
- [x] Implement backup names and version-control styles.
- [x] Consume E3 timestamp, metadata, identity, and availability contracts.
- [x] Consume E4 mode, creation/deletion, no-follow, and race-aware single-path contracts.
- [x] Implement output-file mode.
- [x] Implement dry run.
- [x] Implement verbosity and silence.
- [x] Implement deterministic prompts and noninteractive behavior.
- [x] Implement filename quoting.
- [x] Implement complete exit statuses.
- [x] Add metadata, mode, write-failure, and broken-pipe tests.
- [x] Keep GNU Patch artifact and prompt policy above the shared mechanisms.

P8 is complete. Artifact policy remains Patch-specific while canonical path, metadata, mode, timestamp, and single-path mutation behavior are consumed from E2 through E4.

## Phase P9 — E6-facing filesystem isolation and safety

**Start prerequisites:** P6 and Completion Gate E4.
**Closure prerequisites:** P8 and the agreed E6-facing contract.

This phase starts during the E4/E5 validator batches and proceeds concurrently with E6 contract design.

- [x] Put all target mutation behind `IPatchFileSystem`, `IPatchTransaction`, or equivalent injected boundaries.
- [x] Use exclusive secure temporary creation.
- [x] Ensure no original is removed before a complete replacement is ready.
- [x] Add failure injection at every stage.
- [x] Add cancellation cleanup.
- [x] Add symlink and reparse-point characterization.
- [x] Model target, backup, reject, and output artifacts explicitly.
- [x] Model GNU-visible multi-file partial success separately from per-file recoverability.
- [x] Keep parser and matcher independent from filesystem mutation.
- [x] Mark command-local replacement code provisional.
- [x] Supply Patch requirements and adversarial cases to Completion Gate E6.
- [x] Do not claim final transaction conformance.

P9 is complete. `PatchE6TransactionContract` freezes the Patch-facing recovery scope, multi-file completion policy, secure-sibling and flush requirements, containment and metadata obligations, atomicity and durability reporting, deterministic cleanup, and the full failure-injection matrix. Its provisional command-local implementation was replaced by shared E6 in P11A; removal of the now-unreachable legacy implementation remains intentionally deferred to P11B.

## Phase P10 — Close Completion Gates E2, E3, and E4 conformance

**Hard prerequisites:** P7, P8, P9, E2, E3, and E4.

This is a closure checkpoint, not the first integration pass.

- [x] Verify all path logic uses the shared canonical-path model.
- [x] Verify link and reparse-point inspection.
- [x] Verify roots, volumes, separators, and relative/contained paths.
- [x] Verify GNU-compatible containment decisions.
- [x] Verify timestamps, including post-2038 values.
- [x] Verify modes and metadata.
- [x] Verify race-aware single-path mutation.
- [x] Verify `--follow-symlinks` for input and output.
- [x] Verify target, backup, reject, and output artifact policy over the shared providers.
- [x] Add Windows, Linux, macOS, and best-effort BSD capability tests.
- [x] Remove any permanent Patch-local duplicate of E2–E4 facilities.
- [x] Freeze Patch's E6 requirements and failure matrix.

P10 is complete. Artifact destinations are rejected when lexical or physically resolved paths escape the canonical `-d` root; final links and reparse points remain no-follow by default and are followed only under explicit policy. Tests cover post-2038 timestamps, portable modes, Unix owner/group preservation, stable-identity revalidation, target/backup/reject/output recovery units, cancellation after replacement, every provisional transaction stage, and platform capability profiles. Path normalization and containment remain in `Icod.Path`; metadata and timestamps remain in E3; modes, ownership, creation, deletion, and preconditions remain in E4. No permanent Patch-local duplicate of those facilities was introduced.

P11A replaces the provisional adapter with the shared E6 implementation and validates the frozen contract rather than redesigning it inside Patch.

## Phase P11 — Integrate and validate Completion Gate E6

**Hard prerequisites for P11A:** P10 and Completion Gate E6.
**Hard prerequisites for P11B:** P11A and Batches 44 and 45.

### Phase P11A — Initial Patch integration

- [x] Migrate to secure sibling temporary files.
- [x] Migrate to shared backup naming and retention.
- [x] Use atomic replacement where supported.
- [x] Implement explicit non-atomic diagnostics where required.
- [x] Implement rollback after partial failure.
- [x] Integrate metadata preservation.
- [x] Integrate containment checks.
- [x] Integrate deterministic cleanup.
- [x] Test target, backup, reject, output, partial hunk failure, multi-file partial success, links, cancellation, and every commit-stage failure.
- [x] Verify no data-loss window.
- [x] Report contract defects before Batches 44 and 45 close.

P11A is complete. `PatchE6Transaction` translates immutable Patch artifacts into shared `TransactionalReplacementArtifact` values, preserving one recovery unit per patched file and continuing independent units to retain GNU-visible multi-file partial success. Secure sibling staging, durable file flush, E3 revalidation, atomic publication where available, rollback, containment, metadata restoration, cancellation cleanup, and structured outcomes now come from E6. Patch continues to own when backups, rejects, and alternate outputs exist and how their diagnostics and exit status are reported. P11A identified one shared-contract defect: backup retention had only a transaction-wide switch, while Patch selects backups per target and may supply an explicit prefix-derived pathname. E6 now accepts a per-artifact retained-backup request with an explicit pathname and stages any preexisting backup before commit. The former P9 implementation is unreachable and remains only for P11B removal after Batches 44 and 45 validate the shared contract independently.

### Phase P11B — Post-validator closure

After `cp`, `mv`, and `install` have independently exercised E6:

- [ ] Adopt stabilized E6 contract changes.
- [ ] Remove provisional command-local replacement code.
- [ ] Rerun transaction failure-injection tests.
- [ ] Verify target, backup, reject, and output artifact consistency.
- [ ] Verify metadata, symlink/reparse-point, rollback, cancellation, cleanup, and non-atomic fallback behavior.
- [ ] Confirm that GNU-visible partial-application behavior remains command-specific and correct.

## Phase P12 — Conformance, hardening, and extraction readiness

**Hard prerequisite:** P11B.

- [ ] Finalize the GNU 2.8 behavior/conformance matrix against every implemented option; retain the P0 source-defined inventory as the spelling and arity baseline.
- [ ] Complete parser corpora.
- [ ] Complete GNU differential tests on Linux.
- [ ] Complete independent Icod Diffutils interoperability tests.
- [ ] Complete security tests.
- [ ] Complete large-input and resource-exhaustion tests.
- [ ] Complete signal and cancellation tests.
- [ ] Complete POSIX-mode tests.
- [ ] Complete all three required CI platforms.
- [ ] Build Debug and Release.
- [ ] Audit XML documentation.
- [ ] Audit directory README files.
- [ ] Audit UTF-8/LF formatting.
- [ ] Audit final public surface.
- [ ] Classify Shared dependencies for Completion Gate G.
- [ ] Document deliberate divergences and platform limitations.
- [ ] Prepare final repository and package metadata without performing extraction early.

---

## Test architecture

## Fixture sources

Maintain distinct fixture roots:

```text
tests/Patch.Tests/fixtures/
├── gnu/
├── icod-diffutils/
├── independent/
├── malformed/
├── binary/
├── security/       # expanded in P7-P12
├── line-endings/   # expanded with parser/application cases in P3-P6
└── large/          # expanded with later parser/application stress cases
```

Do not regenerate all expected output with the implementation under test.

Store source provenance and licensing information with imported fixtures.

---

## Parser tests

Test:

- format detection;
- leading text;
- trailing text;
- multiple patches;
- unified headers;
- context headers;
- normal commands;
- ed scripts;
- timestamps;
- section headings;
- whitespace around line numbers;
- tabs;
- CRLF;
- incomplete lines;
- zero ranges;
- file creation;
- file deletion;
- quoted filenames;
- filenames with spaces;
- newline filenames;
- NUL directive lines;
- malformed ranges;
- overflow;
- truncated hunks;
- garbage within hunks;
- cancellation.

---

## Application tests

Test:

- exact additions;
- exact deletions;
- exact replacements;
- multiple hunks;
- accumulated offsets;
- empty files;
- files without final terminators;
- LF;
- CRLF;
- binary bytes;
- NUL in payload where allowed;
- very long lines;
- very large files;
- spill-backed application;
- file creation;
- file deletion;
- normal patches;
- context patches;
- unified patches;
- ed scripts;
- deterministic output.

---

## Matching tests

Test:

- expected-location match;
- forward offset;
- backward offset;
- ambiguous locations;
- maximum search;
- fuzz levels;
- insufficient context;
- whitespace canonicalization;
- reversed patch;
- already-applied patch;
- force;
- forward;
- batch;
- interactive decisions;
- overlapping hunks;
- repeated hunks;
- adversarial quadratic candidates;
- cancellation.

---

## Filename and path tests

Test both Unix-like and Windows syntax:

```text
file
directory/file
../file
/absolute/file
./file
directory\file
..\file
C:\file
C:file
\\server\share\file
\\?\device
\\.\device
file:stream
```

Also test:

- path-prefix stripping;
- insufficient components;
- repeated separators;
- dot components;
- case-folding collisions;
- Unicode normalization collisions;
- reserved names;
- newline names;
- symlinks;
- hard links;
- reparse points;
- races;
- missing components;
- output, backup, and reject containment.

---

## Transaction tests

Inject failures during:

- target open;
- patch read;
- target read;
- temporary creation;
- temporary write;
- flush;
- metadata capture;
- backup-name selection;
- backup creation;
- target replacement;
- metadata restoration;
- reject creation;
- output creation;
- cleanup;
- cancellation;
- process signal delivery.

Assert:

- original-file integrity;
- correct partial-application semantics;
- no orphan temporary files;
- correct backup retention;
- correct reject retention;
- deterministic diagnostics;
- correct exit status.

---

## CLI tests

Use the repository process test host to verify:

- executable assembly name;
- help;
- version;
- unknown options;
- missing operands;
- standard-input patches;
- patch-file options;
- redirected input and output;
- terminal and nonterminal prompts;
- dry run;
- verbosity;
- quiet mode;
- exit statuses;
- broken pipes;
- cancellation;
- current directory;
- environment variables;
- Windows and Unix quoting.

---

## Differential tests

On Linux, where GNU patch 2.8 is explicitly available to the test environment:

1. prepare identical isolated workspaces;
2. run GNU patch 2.8;
3. run Icod Patch;
4. compare:
   - resulting file bytes;
   - created and deleted files;
   - rejects;
   - backups;
   - timestamps and modes where applicable;
   - stdout;
   - stderr;
   - exit status.

Differential tests supplement, but do not replace, independent expected fixtures.

Tests must verify the exact GNU version before using it as an oracle.

---

## Cross-suite interoperability

Verify textual compatibility with `Icod.DiffUtils` without a runtime reference.

At minimum:

- normal patches emitted by `Icod.DiffUtils.Diff`;
- context patches emitted by `Icod.DiffUtils.Diff`;
- unified patches emitted by `Icod.DiffUtils.Diff`;
- ed scripts emitted by `Icod.DiffUtils.Diff`;
- labels and timestamps;
- incomplete final lines;
- created and deleted files;
- multiple hunks;
- directory comparisons where output forms are applicable.

Also verify that Icod Patch accepts equivalent fixtures from GNU Diffutils independently of Icod Diffutils.

---

## Documentation requirements

Document:

- exact usage;
- every option;
- patch formats;
- input-source precedence;
- filename selection;
- path stripping;
- fuzz;
- offsets;
- reversal;
- already-applied behavior;
- prompts;
- rejects;
- backups;
- dry run;
- output mode;
- timestamps;
- symlinks;
- POSIX mode;
- binary behavior;
- encodings and line endings;
- statuses;
- platform differences;
- security limitations;
- transaction guarantees;
- non-atomic fallbacks;
- deliberate divergences.

Every public, protected, and internal type and member receives substantive XML documentation.

Every multi-file source directory receives a substantive `README.md`.

---

## Shared-boundary classification

During implementation, classify every reusable type.

### Likely future `Icod.CommandFramework`

```text
CommandContext
option parser
diagnostics and quoting
byte-preserving records
temporary workspaces
canonical paths
metadata and modes
transactional replacement
platform capabilities
cancellation and cleanup
```

### Patch-specific

```text
PatchDocument
FilePatch
Hunk
PatchFormatDetector
UnifiedPatchParser
ContextPatchParser
NormalPatchParser
EdScriptPatchParser
HunkMatcher
FuzzPolicy
OffsetSearch
ReversePatchDetector
RejectPolicy
Patch-specific backup policy
PatchApplicationResult
```

### Command-local

```text
CLI orchestration
usage text
GNU patch prompt wording
option conflict rules
exit-status aggregation
```

Do not move Patch-specific models into the general Shared project.

---

## Completion criteria

The Patch milestone is complete only when:

- GNU patch 2.8 is pinned and recorded;
- the historical private format has been removed and is covered only by retirement characterization tests;
- the project is `Icod.Patch`;
- the public class is `Icod.Patch.Command`;
- the assembly name is `patch`;
- C# 13 and `net10.0` policies are satisfied;
- normal, context, unified, and required ed-script formats work;
- format auto-detection works;
- patch input from standard input and files works;
- multiple file patches work;
- filename selection and prefix stripping work;
- exact matching, offsets, fuzz, and reversal policy work;
- rejects and backups work;
- dry run and output-file modes work;
- creation and deletion work;
- timestamps, modes, and symlinks follow the pinned policy;
- final mutation uses the shared transactional replacement infrastructure;
- no production path invokes native `patch` or `ed`;
- no production reference to `Icod.DiffUtils.Shared` or `Icod.LineEditor.Ed.Shared` exists;
- line endings, bytes, and incomplete final records are preserved correctly;
- large inputs use bounded or spill-backed storage;
- path and resource attacks have adversarial tests;
- cancellation and broken pipes are deterministic;
- exit statuses match GNU patch;
- the dedicated test project passes;
- the entire solution passes on Windows, Ubuntu, and macOS;
- Debug and Release builds pass;
- XML documentation and directory README requirements pass;
- UTF-8/LF policy passes;
- all deliberate divergences and platform limitations are documented;
- Completion Gate G can extract the suite without stale CoreUtils identities.

---

## Immediate next actions

1. Run the integrated Patch project and focused test suite in Debug and Release on a complete checkout.
2. Run the repository Windows, Ubuntu, and macOS CI matrix and correct any integration-only failures.
3. Complete Completion Gates E3 and E4 before closing P8 metadata, mode, artifact, and single-path mutation behavior.
4. Begin P8 only over the P7 multi-file application plan; do not duplicate filename selection or canonical-path policy.
5. Keep committed filesystem mutation behind the P9/P11 transaction boundaries and do not bypass E6.
6. Keep the main roadmap, this document, the option matrix, and `CONTRIBUTING.md` synchronized at every Patch/E-series checkpoint.

---

## Recommended main-roadmap link

The main roadmap's Patch milestone should retain its concise schedule and add:

```markdown
The detailed architecture, repository assessment, security model, format
matrix, development phases, and completion criteria are maintained in
[`Icod.Patch-Development-Roadmap.md`](Icod.Patch-Development-Roadmap.md).
```

The main roadmap should not duplicate the complete Patch roadmap. It should retain:

- the GNU patch 2.8 baseline;
- textual interoperability;
- the prohibition on `Icod.DiffUtils.Shared` dependencies;
- the project identity;
- the Patch/E2–E6 partial-order graph and hard dependency edges;
- the requirement to complete this dedicated roadmap;
- final extraction at Completion Gate G.

---

## Final recommendation

Rebuild Patch as an independent textual-format consumer with a pure parser and application engine before final filesystem integration.

The preferred progression is a dependency-aligned weave:

```text
P0–P4 syntax and models
        ↓
P5–P6 pure application and matching ─── concurrent E2
        ↓                                      ↓
        └────────────── E2 + Batch 35 ─────────┘
                               ↓
P7 paths and multi-file state
        ↓
E3 + Batch 36 + E4
        ↓
P8 and P9 start while Batches 38–43 validate mutation foundations
        ↓
P9/P10 closure
        ↓
E6
        ↓
P11A initial Patch transaction integration
        ↓
Batches 44–45 independent E6 validation
        ↓
P11B and P12 final Patch closure
```

This schedule gives Patch a testable domain engine early, avoids throwaway Patch-specific path and transaction frameworks, preserves independence from Diffutils and LineEditor, and gives the E gates a demanding cross-suite consumer while their contracts are still inexpensive to revise.
