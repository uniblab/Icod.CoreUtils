# `diff3` command implementation

This directory contains command-local GNU `diff3` behavior:

- validated GNU option and exactly-three-operand handling;
- materialization of mine, common-ancestor, and yours inputs, with at most one
  standard-input operand;
- historical common-file selection for normal reports and merge/edit modes;
- normal three-file reports and incomplete-line markers;
- direct merge output under `-A`, `-E`, `-X`, `-e`, `-x`, and `-3` policies;
- reverse-order `ed` scripts, marked conflicts, labels, leading-period safety,
  and System V `w`/`q` commands;
- binary-input, trailing-carriage-return, cancellation, diagnostic, and exact
  status policy.

The ancestor-relative change regions and overlap classification are supplied by
`Icod.DiffUtils.Shared.Merge`. This project does not reference the `diff`
command project and does not invoke an installed native `diff3` or `diff` to
perform its defining operation. The accepted `--diff-program` spelling receives
a controlled unsupported diagnostic so it cannot create a tool-to-tool runtime
dependency.
