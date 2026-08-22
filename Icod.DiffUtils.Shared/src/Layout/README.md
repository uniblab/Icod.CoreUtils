# Side-by-side layout

This directory contains the GNU Diffutils-specific logical-row and display-column
primitives shared by `diff --side-by-side` and `sdiff`.

`SideBySideLayout` aligns edit operations, calculates GNU field and gutter
geometry, emits tab-stop-aware padding, formats complete rows, truncates without
splitting Unicode scalar values, expands or preserves input tabs, and accounts
for combining and wide characters. `SideBySideGeometry` records the reusable
field width, right-column offset, and separator column.

Interactive prompting, merge decisions, editor invocation, and output-file
transactions remain command-local to `Icod.DiffUtils.SDiff`.
