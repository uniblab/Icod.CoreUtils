# Batch 72 — `Icod.Tar` archive engine

## Baseline

Batch 72 pins GNU tar 1.35 as the behavioral reference for the selected archive surface. The project remains `Icod.Tar` at `tar/Icod.Tar.csproj` and continues to depend only on the repository Shared foundation until Completion Gate G.

## Implementation

The implementation uses .NET `System.Formats.Tar` as the archive-record codec and keeps GNU command policy in `Icod.Tar`. Implemented operations are create, extract, list, append, update, delete, concatenate, and compare. GNU, ustar, and POSIX/pax output formats are selectable. Append and update preserve historical member versions by adding newer entries at the logical end; delete and concatenate use the same staged archive-rewrite path. Compressed archive mutation is rejected.

Compression supports managed gzip and argument-safe external bzip2, xz, zstd, or `--use-compress-program` filters through the shared process executor and private `TemporaryWorkspace` staging. Named compressed archives are recognized by suffix when reading; `--auto-compress` selects compression from the archive suffix when writing. Bounded read/write wrappers enforce archive and extraction resource ceilings.

Creation uses the shared `ReadOnlyPathTraversalEngine`, honors recursion/dereference policy and exclusions, and preserves link typing through `System.Formats.Tar`. Archive publication and archive rewrites use the E6 transactional replacement boundary.

GNU/PAX sparse 0.1 is implemented explicitly for `--sparse`: the engine writes and validates `GNU.sparse.size`, `GNU.sparse.numblocks`, `GNU.sparse.map`, and `GNU.sparse.name`, stores condensed data extents, reconstructs holes through a seekable staging file, and performs checked 64-bit map arithmetic. Legacy GNU sparse-header members are rejected rather than extracted incorrectly; other GNU sparse generations are not claimed by this batch.

## Extraction security boundary

Extraction rejects absolute, drive-rooted, UNC-style, NUL-containing, and escaping `..` member paths; checks every existing parent component for symlink/reparse indirection; validates symbolic and hard-link containment independently; rejects special device/FIFO creation; and detects case-folding collisions on Windows. Regular files are published using the shared transactional replacement engine, including destination observation and commit-time revalidation. Existing indirection objects are never silently replaced by archive links.

Member-count, decompressed-archive, and extracted-byte ceilings bound hostile inputs. Malformed sparse maps, arithmetic overflow, decompression failures, cancellation, and unsupported entry types produce controlled command failures.

## Tests and validation

`tests/Tar.Tests` exercises all eight operations, GNU/ustar/pax formats, gzip, selection/exclusion, sparse 0.1 round trips, links, metadata restoration, rooted/path-traversal attacks, symlink-parent and overwrite redirection, special-file refusal, Windows case collisions, malformed/overflowing sparse maps, archive/extraction budgets, decompression failure, and cancellation.

The implementation environment did not provide a .NET SDK, so the managed solution and xUnit suite could not be executed locally. Source/API, XML, solution, patch, and repository-structure checks were performed; required Windows/Ubuntu/macOS build and test validation remains the closure step.
