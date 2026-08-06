# Shared directory-listing engine

This directory owns the Batch 46 implementation shared by `ls`, `dir`, `vdir`, and `dircolors`.

## Boundaries

- `DirectoryListingOptions.cs` defines the three executable profiles and their common GNU-style option vocabulary.
- `DirectoryListingEngine.cs` performs metadata-backed observation, ordering, layout, long-format rendering, classification, color application, and recursive traversal with stable-identity cycle protection.
- `LsColors.cs` parses, serializes, and applies reusable `LS_COLORS` indicator and filename-pattern rules.
- `DirColorsDatabase.cs` parses the user-facing color database, evaluates terminal selectors, exposes the built-in database, and emits Bourne- or C-shell setup commands.

The engine consumes the authoritative filesystem metadata contracts rather than duplicating platform probes. Terminal attachment, geometry, color capability, environment capture, filename quoting, and control-character handling remain owned by `Icod.CoreUtils.Shared.Terminal` from Completion Gate F1.

This namespace remains a provisional `Icod.CoreUtils.Shared` incubation boundary. Its final ownership will be reviewed at Completion Gate G after consumers establish whether any presentation primitives are genuinely cross-suite.

## Executable profiles

`ls`, `dir`, and `vdir` contain only command boundaries. `ls` selects terminal-sensitive column or single-column defaults, `dir` selects escaped column output, and `vdir` selects long output. Later command-specific compatibility work should extend the shared parser or engine rather than recreate an independent listing implementation.
