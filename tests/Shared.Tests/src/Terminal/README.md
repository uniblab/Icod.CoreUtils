# Terminal presentation and control tests

These tests exercise Completion Gates F1 and F3 through injected providers and deterministic value objects.

Gate F1 coverage includes attachment versus redirection, dimension precedence and fallback, `TERM`/`COLORTERM` color inference, `TERM`/`SHELL` `dircolors` inputs, GNU quoting-style parsing, shell and C escaping, filename control-character replacement, and a presentation-provider smoke check.

Gate F3 coverage includes endpoint validation, controlled availability states, injectable terminal-control providers, POSIX and Windows mode serialization and restoration, destination ABI validation, console direction safety, GNU-compatible control-character notation, regular-file nonterminal detection, and read-only live-runner smoke checks. Tests never mutate the active runner terminal; command-specific mutation behavior belongs to the later `stty` suite, where providers can be injected or a dedicated pseudo-terminal fixture can be used.
