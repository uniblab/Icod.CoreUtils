# Icod.Patch

`Icod.Patch` is the co-resident C# implementation of GNU `patch`, targeting `net10.0` with C# 13.

The authoritative behavioral baseline is GNU patch 2.8. The pinned release metadata and verification values are recorded in [`upstream/GNU-patch-2.8.md`](upstream/GNU-patch-2.8.md), and the complete source-defined option inventory is recorded in [`upstream/GNU-patch-2.8-option-matrix.md`](upstream/GNU-patch-2.8-option-matrix.md).

## Current phase

Phases P0, P1, and P2 are implemented:

- the project and dedicated tests are normalized in the `Icod.Patch` solution family;
- invocation uses the shared asynchronous command framework and a declarative parser containing the complete GNU 2.8 option-name inventory; options owned by later phases fail explicitly rather than being silently ignored;
- patch input may come from standard input, `-i`, or the second operand;
- the input layer preserves bytes, offsets, record terminators, incomplete final records, and bounded-resource behavior;
- the detector identifies unified, context, normal, and patch-compatible ed-script candidates, including multiple sections and surrounding non-patch text;
- directive NULs, unsafe newline-bearing quoted filenames, overflow, and configured resource limits fail deterministically.

This phase does **not** modify target files. Unified/context parsing begins in P3, normal/ed parsing in P4, and application begins in P5. A recognized patch therefore receives a controlled nonzero diagnostic until those phases are implemented.

## Historical seed format

The former private line format, in which lines beginning with `+` and `-` directly inserted or deleted target lines, has been removed. It was not GNU patch syntax and is now covered only by a characterization test proving that it is rejected as non-patch input.

## Dependency boundary

`Icod.Patch` consumes textual patch streams. It does not reference `Icod.DiffUtils.Shared`, invoke native `patch`, or invoke native `ed`.
