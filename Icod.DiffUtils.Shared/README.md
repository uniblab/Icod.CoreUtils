# Icod.DiffUtils.Shared

`Icod.DiffUtils.Shared` contains behavior genuinely shared by two or more GNU
Diffutils commands (`cmp`, `diff`, `diff3`, and `sdiff`). General command-line,
filesystem, stream, text, locale, process, and platform mechanics remain in the
current `Icod.CoreUtils.Shared` incubation project.

The authoritative behavioral baseline is GNU Diffutils 3.12 (12 January 2025).
Batch 31 establishes comparison-input composition and the common result-status
contract. Later batches may add line normalization, edit scripts, ranges,
hunks, output models, directory coordination, three-way merge models, and
side-by-side primitives only after at least two Diffutils consumers require
those abstractions.
