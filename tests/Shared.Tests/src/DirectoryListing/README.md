# Directory-listing tests

This directory verifies the Batch 46 shared engine used by `ls`, `dir`, `vdir`, and `dircolors`.

- `DirectoryListingOptionParserTests.cs` covers executable profiles, clustered and long options, block-size suffixes, and controlled usage failures.
- `DirectoryListingCommandTests.cs` exercises deterministic listing layouts and metadata-backed long output through injected terminal and environment providers.
- `LsColorsTests.cs` verifies reusable `LS_COLORS` parsing, escaping, serialization, file-type precedence, and glob rules.
- `DirColorsDatabaseTests.cs` verifies the documented database grammar, selector state machine, built-in database, diagnostics, shell inference, and inspection output.
