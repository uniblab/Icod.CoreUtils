# Three-way comparison model

This directory contains the GNU Diffutils-specific three-way comparison model
used by `diff3` and available to later suite consumers that need
ancestor-relative change classification.

`ThreeWayMergeEngine` aligns each non-common input against the selected common
input through the Batch 32 line engine. It applies GNU-style change-boundary
shifting before combining pairwise blocks into connected regions, which keeps
repeated lines, same-point insertions, boundary insertions, adjacent changes,
and incomplete final lines aligned consistently. Each region retains the exact
source lines and zero-based positions for all three external inputs and is
classified as mine-only, yours-only, an already-applied ancestor conflict, or a
true overlap. The caller may select the second or third external input as the
pairwise common file to reproduce GNU `diff3`'s historical normal-report and
merge-mode mappings.

Command-line parsing, GNU normal-report syntax, merge policy, conflict-marker
presentation, labels, and `ed` script serialization remain in
`Icod.DiffUtils.Diff3`.
