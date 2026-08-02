# Icod.Patch

`Icod.Patch` is the co-resident C# implementation of GNU `patch`, targeting `net10.0` with C# 13.

The authoritative behavioral baseline is GNU patch 2.8. The pinned release metadata and verification values are recorded in [`upstream/GNU-patch-2.8.md`](upstream/GNU-patch-2.8.md), and the complete source-defined option inventory is recorded in [`upstream/GNU-patch-2.8-option-matrix.md`](upstream/GNU-patch-2.8-option-matrix.md).

## Current phase

Wave A, Phases P0 through P4, is implemented:

- the project and dedicated tests are normalized in the `Icod.Patch` solution family;
- invocation uses the shared asynchronous command framework and a declarative parser containing the complete GNU 2.8 option-name inventory; options owned by later phases fail explicitly rather than being silently ignored;
- patch input may come from standard input, `-i`, or the second operand;
- the input layer preserves bytes, offsets, record terminators, incomplete final records, and bounded-resource behavior;
- the detector identifies unified, context, normal, and patch-compatible ed-script sections, including multiple sections and surrounding or interstitial non-patch text;
- complete parsers normalize all four formats into immutable byte-preserving file, range, hunk, operation, and data-line models;
- exact source records remain attached to parsed hunks for later reject serialization;
- unified/context creation and deletion forms, normal append/change/delete commands, and GNU Diffutils ed single-dot protection are represented without invoking native tools;
- directive NULs, unsafe newline-bearing quoted filenames, malformed ranges and counts, inconsistent context copies, unterminated ed blocks, overflow, and configured resource limits fail deterministically.

Wave A does **not** modify target files. Pure exact application begins in P5, so a successfully parsed patch receives a controlled nonzero diagnostic until that phase is implemented.

## Historical seed format

The former private line format, in which lines beginning with `+` and `-` directly inserted or deleted target lines, has been removed. It was not GNU patch syntax and is now covered only by a characterization test proving that it is rejected as non-patch input.

## Dependency boundary

`Icod.Patch` consumes textual patch streams. It does not reference `Icod.DiffUtils.Shared`, invoke native `patch`, or invoke native `ed`.
