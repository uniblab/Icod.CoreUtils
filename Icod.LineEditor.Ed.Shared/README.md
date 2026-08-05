# Icod.LineEditor.Ed.Shared

`Icod.LineEditor.Ed.Shared` is the reusable mutable editor engine shared by the future `ed` and `red` command projects.

The project owns Ed-family behavior rather than process-level command-line policy. It provides:

- segmented mutable line storage with stable line identities;
- current and last address state, marks, a cut buffer, and one-level reversible undo;
- Ed address and range parsing independent from Sed's streaming address model;
- append, insert, change, delete, print, list, number, mark, move, copy, join, yank, put, substitution, global, file, shell, undo, and quit operations;
- Shared GNU Basic Regular Expression consumption for searches and substitutions;
- injected file and process capabilities;
- immutable standard and restricted security profiles;
- controlled diagnostics, cancellation, signal, and exit-status results.

The project deliberately has no runtime reference to `Icod.DiffUtils.Shared`. Compatibility with ed scripts emitted by GNU Diffutils and `Icod.DiffUtils` is verified through textual fixtures in `tests/Ed.Shared.Tests`.

## Dependency direction

```text
Icod.CoreUtils.Shared
        ↓
Icod.LineEditor.Ed.Shared
        ↓
Icod.LineEditor.Ed       (Phase LE7)
Icod.LineEditor.Red      (Phase LE8)
```

The current `ed` and `red` executable projects are not migrated in Phase LE6. They consume this engine in LE7 and LE8 respectively.
