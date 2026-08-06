# Terminal presentation

`Icod.CoreUtils.Shared.Terminal` is the Completion Gate F1 presentation layer for `dircolors`, `ls`, `dir`, and `vdir`. It is a provisional cross-suite `Icod.CommandFramework` candidate because later ProcPs and Tar consumers may need the same observation contracts.

## Responsibilities

- distinguish attached and redirected standard streams through an injectable provider;
- discover terminal dimensions and apply `COLUMNS`, `LINES`, then deterministic fallback precedence;
- capture the `TERM` terminal name plus `COLORTERM`, `SHELL`, and `QUOTING_STYLE` without reading process-global state inside command policy;
- infer ANSI 16-color, 256-color, and true-color capability and resolve never, auto, and always color modes;
- resolve GNU directory-listing quoting defaults, retain invalid environment values for command diagnostics, and present control characters deterministically;
- convert unsupported console APIs and failed geometry queries into controlled observations.

## Boundary

This namespace does not implement cursor movement, raw mode, echo control, signals, process groups, pseudo-terminals, or full-screen interfaces. Those mechanics belong to later terminal-control gates. It also does not parse the `dircolors` database or `LS_COLORS`; Batch 46 owns those Coreutils-specific grammars above these neutral observations.
