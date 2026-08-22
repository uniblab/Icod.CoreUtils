# GNU diff command implementation

This directory contains the `Icod.DiffUtils.Diff` command front end.

- `Command` parses GNU-compatible options and constructs comparison requests.
- `DiffCoordinator` handles files, standard input, recursive directories, exclusions, absent-file policy, and status aggregation.
- `DiffOutputWriter` renders normal, context, unified, ed, forward-ed, RCS, side-by-side, brief, and conditional-merge output.
- `DiffInput`, `DiffOptions`, `DiffOutputStyle`, and `DiffUsageException` hold command-local state and validation.

Reusable line documents, normalization, edit scripts, hunks, and logical side-by-side rows live in `Icod.DiffUtils.Shared`; this project does not reference another command project.
