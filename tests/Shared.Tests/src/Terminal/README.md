# Terminal presentation tests

These tests exercise Completion Gate F1 through injected terminal-device and environment providers. They cover attachment versus redirection, dimension precedence and fallback, `TERM`/`COLORTERM` color inference, `TERM`/`SHELL` `dircolors` inputs, GNU quoting-style parsing, shell and C escaping, control-character replacement, and a host-provider smoke check that accepts every controlled runner outcome.
