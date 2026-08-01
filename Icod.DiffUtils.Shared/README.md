# Icod.DiffUtils.Shared

`Icod.DiffUtils.Shared` contains behavior genuinely shared by two or more GNU
Diffutils commands (`cmp`, `diff`, `diff3`, and `sdiff`). General command-line,
filesystem, stream, text, locale, process, and platform mechanics remain in the
current `Icod.CoreUtils.Shared` incubation project.

The authoritative behavioral baseline is GNU Diffutils 3.12 (12 January 2025).
Batch 31 established comparison-input composition and the common result-status
contract. Batch 32 adds the reusable two-way line model: byte-preserving UTF-8
comparison documents, incomplete-line state, line normalization policies,
Myers edit scripts, contiguous changed blocks, context-expanded hunks, and
logical side-by-side rows. Output syntax, directory traversal, labels, binary
reporting, and command-line policy remain private to the individual command.

Later batches may extend these contracts only for demonstrated multi-command
needs. In particular, `diff3` and `sdiff` may consume the shared line and edit
models, but they must never reference the `diff` command project.
