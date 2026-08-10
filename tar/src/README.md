# Icod.Tar archive engine

This directory contains the command-local archive engine for `Icod.Tar`. `System.Formats.Tar` is used as the tar record codec, while GNU-compatible command policy remains here.

- `Command.cs` — public command entry point and GNU tar 1.35 help/version surface.
- `TarCommandLine.cs` — operation and option parsing, including old-style option words.
- `TarArchiveEngine.cs` — create/list/extract/append/update/delete/concatenate/compare orchestration and Shared filesystem integration.
- `TarCompression.cs` — gzip and external filter plumbing with bounded streams and private temporary workspace staging.
- `TarExtractionPolicy.cs` — extraction-root containment, member selection/exclusion, parent indirection checks, and Windows case-fold collision protection.
- `TarSparse.cs` — GNU/PAX sparse 0.1 map validation, writing, reconstruction, and comparison.

Extraction is intentionally a trust boundary. Archive member paths, links, sparse metadata, special file types, and overwrite destinations are validated before publication; regular-file writes use the Shared transactional-replacement layer so destination identity is revalidated at commit.
