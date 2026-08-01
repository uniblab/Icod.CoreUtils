# Edit models and line differencing

This directory contains the Diffutils-specific two-way edit model shared by `diff`, `diff3`, and `sdiff`.

- `LineDiffEngine` computes a shortest edit script and groups edits into changed blocks and context hunks.
- `EditScript`, `EditOperation`, and `EditOperationKind` describe the ordered comparison result.
- `DifferenceBlock` and `DifferenceHunk` provide command-neutral ranges used by textual and merge-oriented front ends.

Output syntax and command-line policy do not belong here; they remain in the individual command projects.
