# `stty` command implementation

Batch 51 implements reporting and mutation policy over the Completion Gate F3 terminal-control provider. `Command` owns endpoint selection and diagnostics, `SttyOptions` owns GNU option parsing, `SttyModeEditor` applies ordered command settings to immutable mode snapshots, and `SttyFormatter` renders human-readable state.

POSIX support includes the common input, output, control, and local flag families; sane, raw, cooked, newline, parity, and eight-bit aliases; named control characters; line discipline where exposed; Linux and macOS speed maps; bare numeric, `ispeed`, and `ospeed` changes; and GNU-compatible machine save/restore through `TerminalModeCodec`. The `speed` operand is reporting-only.

Windows support preserves the complete native console mode and changes only defensible console input or output bits. It supports sane/raw plus processed-input, line-input, echo, processed-output, and wrap-at-end-of-line toggles. POSIX speed, parity, line-discipline, control-character, and drain semantics fail with controlled diagnostics rather than being fabricated.
