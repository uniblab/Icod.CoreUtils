# Icod.DiffUtils.Shared

`Icod.DiffUtils.Shared` contains behavior genuinely shared by two or more GNU Diffutils commands (`cmp`, `diff`, `diff3`, and `sdiff`). General command-line, filesystem, stream, text, locale, process, and platform mechanics are consumed from the published `Icod.CommandFramework` package.

The authoritative behavioral baseline is GNU Diffutils 3.12 (12 January 2025).

The shared library owns the reusable Diffutils comparison model: comparison inputs and result status, byte-preserving UTF-8 comparison documents, incomplete-line state, line normalization policies, Myers edit scripts, contiguous difference blocks, context-expanded hunks, GNU-compatible three-way alignment and overlap classification, and side-by-side layout/display-column mechanics.

Output syntax, directory traversal policy, labels, binary reporting, command-line policy, editor invocation, and interactive behavior remain private to the individual commands. Command projects depend on this library through repository-local `ProjectReference` entries; no Diffutils command project references another Diffutils command project.
