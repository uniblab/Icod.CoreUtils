# Icod.DiffUtils.Shared

`Icod.DiffUtils.Shared` contains behavior genuinely shared by two or more GNU
Diffutils commands (`cmp`, `diff`, `diff3`, and `sdiff`). General command-line,
filesystem, stream, text, locale, process, and platform mechanics remain in the
current `Icod.CoreUtils.Shared` incubation project.

The authoritative behavioral baseline is GNU Diffutils 3.12 (12 January 2025).
Batch 31 established comparison-input composition and the common result-status
contract. Batch 32 added the reusable two-way line model: byte-preserving UTF-8
comparison documents, incomplete-line state, line normalization policies,
Myers edit scripts, contiguous changed blocks, context-expanded hunks, and
logical side-by-side rows. Batch 33 adds GNU-compatible three-way alignment,
connected ancestor-relative regions, and overlap classification for `diff3` and
future merge-oriented Diffutils consumers.

Output syntax, directory traversal, labels, binary reporting, command-line
policy, editor invocation, and interactive behavior remain private to the
individual commands. No command project may reference another command project.
