# Icod.Patch

`Icod.Patch` is the co-resident C# implementation of GNU `patch`, targeting `net10.0` with C# 13.

The authoritative behavioral baseline is GNU patch 2.8. Pinned release metadata and verification values are recorded in [`upstream/GNU-patch-2.8.md`](upstream/GNU-patch-2.8.md), and the source-defined option inventory is recorded in [`upstream/GNU-patch-2.8-option-matrix.md`](upstream/GNU-patch-2.8-option-matrix.md).

## Current phase

Patch Waves A, B1, and B2—Phases P0 through P7—are implemented.

The command now:

- uses the shared asynchronous command framework and declarative GNU option inventory;
- reads patch input from standard input, `-i`, or the second operand;
- preserves patch bytes, offsets, line endings, incomplete records, and bounded spill storage;
- parses unified, context, normal, and patch-compatible ed scripts into immutable common models;
- applies exact, offset, fuzz, whitespace, reverse, prerequisite, and merge policy to immutable virtual targets;
- consumes `Icod.Path` for lexical and physical path resolution, roots, volumes, links, reparse points, missing components, and containment;
- implements explicit original-file operands, `-d`, component-aware `-p`, quoted names, `Index:` evidence, GNU/POSIX candidate ordering, `/dev/null` creation/deletion forms, multiple file patches, and per-file status aggregation;
- models positive, disabled, and decision-gated `-g`/`PATCH_GET` revision-control retrieval through an injected provider boundary;
- retains virtual state across multiple patches that select the same canonical target;
- produces a multi-file application plan without creating, replacing, deleting, backing up, or rejecting live filesystem artifacts.

P7 deliberately uses a containment safety boundary: every selected target must remain within the physically canonical `-d` working root. Parent traversal, cross-volume targets, and link/reparse resolutions that escape that root are rejected even where historical `patch` implementations might accept an absolute or escaping name. Terminal links are rejected by default and followed only with `--follow-symlinks`; output-link behavior remains part of the later mutation phases.

The executable still returns controlled trouble after a plan is built because P8 owns rejects, backups, output destinations, prompts, metadata/mode integration, and user-visible statuses, while P9 and P11 own safe committed replacement.

## Historical seed format

The former private line format, in which lines beginning with `+` and `-` directly inserted or deleted target lines, has been removed. It was not GNU patch syntax and remains only as a characterization test proving that it is rejected as non-patch input.

## Dependency boundary

`Icod.Patch` consumes textual patch streams and the neutral `Icod.Path` contract. It does not reference `Icod.DiffUtils.Shared`, invoke native `patch`, invoke native `ed`, create a Patch-private canonical-path framework, or commit filesystem mutations during P0–P7.
