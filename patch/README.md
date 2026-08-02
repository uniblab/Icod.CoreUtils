# Icod.Patch

`Icod.Patch` is the co-resident C# implementation of GNU `patch`, targeting `net10.0` with C# 13.

The authoritative behavioral baseline is GNU patch 2.8. The pinned release metadata and verification values are recorded in [`upstream/GNU-patch-2.8.md`](upstream/GNU-patch-2.8.md), and the complete source-defined option inventory is recorded in [`upstream/GNU-patch-2.8-option-matrix.md`](upstream/GNU-patch-2.8-option-matrix.md).

## Current phase

Waves A and B1, Phases P0 through P6, are implemented:

- the project and dedicated tests are normalized in the `Icod.Patch` solution family;
- invocation uses the shared asynchronous command framework and a declarative parser containing the complete GNU 2.8 option-name inventory; options owned by later phases fail explicitly rather than being silently ignored;
- patch input may come from standard input, `-i`, or the second operand;
- the input layer preserves bytes, offsets, record terminators, incomplete final records, and bounded-resource behavior;
- the detector identifies unified, context, normal, and patch-compatible ed-script sections, including multiple sections and surrounding or interstitial non-patch text;
- complete parsers normalize all four formats into immutable byte-preserving file, range, hunk, operation, and data-line models;
- exact source records remain attached to parsed hunks for later reject serialization;
- target content is indexed in memory or in owner-private spill files while preserving LF, CRLF, CR, and incomplete final records; long records are indexed and copied without whole-record buffering;
- the pure application engine applies exact unified, context, normal, and ed operations, accumulated multi-hunk line deltas, and virtual creation or deletion without touching a live path;
- matching implements nearby offsets, configurable fuzz, canonical horizontal blank runs, reversal and already-applied detection, prerequisite policy, force/forward/batch/injected decisions, merge and diff3 conflict output, cancellation, and bounded candidate work;
- immutable result storage is independent from input storage and is cleaned deterministically on disposal;
- directive NULs, unsafe newline-bearing quoted filenames, malformed ranges and counts, inconsistent context copies, unterminated ed blocks, overflow, and configured resource limits fail deterministically.

The executable still does **not** select or modify live target files. P7 owns filename candidates, canonical paths, containment, and multi-file state; P8 and later phases own artifacts and committed replacement. A successfully parsed command therefore continues to return a controlled nonzero diagnostic at the live command boundary even though the internal virtual engine is complete.

## Historical seed format

The former private line format, in which lines beginning with `+` and `-` directly inserted or deleted target lines, has been removed. It was not GNU patch syntax and is now covered only by a characterization test proving that it is rejected as non-patch input.

## Dependency boundary

`Icod.Patch` consumes textual patch streams. It does not reference `Icod.DiffUtils.Shared`, invoke native `patch`, invoke native `ed`, create a Patch-private canonical-path framework, or commit filesystem mutations during P0–P6.
