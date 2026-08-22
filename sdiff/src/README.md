# Icod.DiffUtils.SDiff command implementation

The command consumes byte-preserving comparison documents, line policies, Myers
edit scripts, and side-by-side display primitives from `Icod.DiffUtils.Shared`.
It does not reference or invoke `Icod.DiffUtils.Diff` for its defining operation.

Command-local responsibilities include GNU `sdiff` option parsing, file/directory
operand resolution, binary policy, ignored-difference grouping, interactive merge
commands, editor selection, and transactional output replacement. Display uses
GNU field and gutter geometry, configurable tab stops, Unicode display-column
measurement, common-line suppression, left-column mode, and incomplete-line
slash or backslash markers without querying terminal capabilities.

Interactive editing launches `EDITOR`, or `ed` when it is unset, through
`ProcessStartInfo.ArgumentList` with `UseShellExecute = false`; no command text or
temporary path is interpolated through a shell. Editor command parsing preserves
quoted arguments and Windows path backslashes.

Merge output is written to a unique temporary file in the destination directory
and replaces the requested path only after every difference is resolved. Quit,
EOF, cancellation, editor failure, and filesystem failure remove the temporary
file and leave an existing destination unchanged. Binary merge mode follows GNU
behavior by transactionally producing an empty output file without prompting.
