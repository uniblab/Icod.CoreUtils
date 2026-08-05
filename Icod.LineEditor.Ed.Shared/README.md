# Icod.LineEditor.Ed.Shared

`Icod.LineEditor.Ed.Shared` is the reusable mutable editor engine shared by the `ed` and `red` command projects.

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
Icod.LineEditor.Ed
Icod.LineEditor.Red
```

The `ed` and `red` executable projects consume this engine under the standard and permanently restricted command profiles respectively.

## Phase LE9 sharing audit

The completed Ed and Sed engines were compared in Phase LE9. The audit found
no cohesive residual contract that warrants a general `Icod.LineEditor.Shared`
assembly. Neutral regular-expression, record, process, temporary, filesystem,
diagnostic, and text contracts remain in the current Shared incubation
project. Mutable buffer, address, undo, and Red security behavior remain here;
Sed program, address/range, cycle, sandbox, and in-place policy remain in
`Icod.LineEditor.Sed`.

The evidence and dependency decision are recorded in
`Icod.LineEditor-LE9-Sharing-Audit.md` and enforced by architecture-boundary
tests in the Ed.Shared and Sed test projects.
