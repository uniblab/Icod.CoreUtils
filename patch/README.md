# Icod.Patch

`Icod.Patch` is the co-resident C# implementation of GNU `patch`, targeting `net10.0` with C# 13.

The authoritative behavioral baseline is GNU patch 2.8. Pinned release metadata and verification values are recorded in [`upstream/GNU-patch-2.8.md`](upstream/GNU-patch-2.8.md), the final option/conformance inventory is recorded in [`upstream/GNU-patch-2.8-option-matrix.md`](upstream/GNU-patch-2.8-option-matrix.md), and deliberate residual differences are recorded in [`upstream/P12-closure-audit.md`](upstream/P12-closure-audit.md).

## Current phase

Patch Waves A through D and Phases P11A–P12—Phases P0 through P12—are implemented. The co-resident Patch engineering sequence is closed; a green Debug/Release build and the Windows, Ubuntu, and macOS CI matrix remain the repository integration gate.

The command now:

- uses the shared asynchronous command framework and declarative GNU option inventory;
- preserves patch bytes, source offsets, line endings, incomplete records, and bounded spill storage;
- parses unified, context, normal, and patch-compatible ed scripts into immutable common models;
- applies exact, offset, fuzz, whitespace, reverse, prerequisite, and merge policy to immutable virtual targets;
- consumes `Icod.Path` for lexical and physical path resolution, roots, volumes, links, reparse points, missing components, and containment;
- implements explicit original-file operands, `-d`, component-aware `-p`, quoted names, `Index:` evidence, GNU/POSIX candidate ordering, `/dev/null` creation/deletion forms, multiple file patches, and per-file status aggregation;
- implements reject, backup, output, dry-run, prompt, quoting, status, mode, timestamp, and metadata policy above the shared filesystem contracts;
- places all live mutation behind injected `IPatchFileSystem` and `IPatchTransaction` boundaries;
- stages complete exclusive sibling temporary files and flushes them before any destination mutation;
- revalidates E3 identity immediately before commit and applies E4 mode, ownership, deletion, and no-follow policy;
- recovers target-related artifacts in per-file units while retaining completed earlier units for GNU-visible multi-file partial success;
- distinguishes failed-before-commit, rolled-back, partially committed, rollback-incomplete, and cleanup-incomplete transaction outcomes; and
- freezes Patch's E6-facing requirements and failure matrix while consuming the stabilized provider capability and result contracts directly;
- applies captured Icod Diffutils output in unified, context, normal, and ed forms without a production or test project dependency on Diffutils; and
- publishes a final 1.0 option surface with controlled diagnostics for source-defined capabilities that remain unavailable.

P10 intentionally enforces a containment safety boundary: every selected target, output, backup, and reject artifact must remain within the physically canonical `-d` working root. Parent traversal, cross-volume targets, and link/reparse resolutions that escape that root are rejected. Terminal links are rejected by default and followed only with `--follow-symlinks`.

Completion Gate E6 owns secure replacement, atomicity, durability, backup retention, rollback, and cleanup. Phase P11B removed the unreachable command-local P9 transaction and the provisional Patch-only capability model; production mutation now has one path through the shared E6 adapter.

## Historical seed format

The former private line format, in which lines beginning with `+` and `-` directly inserted or deleted target lines, has been removed. It was not GNU patch syntax and remains only as a characterization test proving that it is rejected as non-patch input.

## Dependency boundary

`Icod.Patch` consumes textual patch streams and the neutral `Icod.Path` contract. It does not reference `Icod.DiffUtils.Shared`, invoke native `patch`, invoke native `ed`, create Patch-private canonical-path, metadata, mode, ownership, or basic mutation frameworks, or claim final transactional replacement before Completion Gate E6.

## Final limitations

Icod.Patch 1.0 deliberately does not implement `-D`/`--ifdef`,
`--read-only=ignore|warn|fail`, the GNU `DEBUGGING`-only `-x` surface, the obsolete
three-operand `-b` compatibility form, Git binary/copy/rename payloads, hard-link-set
topology updates, or FIFO/device/socket targets. E6 does not provide persistent
crash-recovery journaling. See the closure audit for the complete platform and
behavior ledger.

## Package preparation

[`PACKAGE.md`](PACKAGE.md) records the final public facade, dependency classification,
Patch-owned behavior, and Completion Gate G extraction constraints. Physical
extraction remains deferred.

