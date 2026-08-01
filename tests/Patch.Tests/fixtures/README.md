# Patch fixture provenance

The fixture tree deliberately separates producers and failure classes.

- `gnu/` contains original minimal textual forms transcribed from the documented GNU diff/patch grammars. They are not copies of complete upstream test files; broader differential verification belongs to later parser and conformance phases.
- `icod-diffutils/` contains output shaped as produced by the co-resident Icod Diffutils commands. Production code has no project reference to Diffutils.
- `independent/` contains hand-authored interoperability examples, including surrounding mail text and multiple file sections.
- `malformed/` contains intentionally invalid directives and unsafe filenames.
- `binary/` contains byte-oriented and mixed-line-ending inputs generated specifically for this test project.

The authoritative upstream archive and checksum are recorded in `patch/upstream/GNU-patch-2.8.md`.
