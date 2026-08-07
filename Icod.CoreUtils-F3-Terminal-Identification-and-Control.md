# Completion Gate F3 — Terminal identification and control

## Purpose

Completion Gate F3 establishes a policy-neutral terminal-control foundation before `tty` and `stty`. The gate answers factual host questions—whether an endpoint is a terminal, what stable name can be reported, what complete native mode is active, and whether that mode can be changed—without embedding either command's option grammar, diagnostics, or exit-status policy.

## Public contracts

`ITerminalControlProvider` accepts a `TerminalEndpoint` identifying either a borrowed process file descriptor or a named device opened and owned by the provider for the duration of one operation. It exposes three operations:

1. `Observe` reports attachment, pathname or console alias, native platform, and capability flags.
2. `GetMode` returns a complete immutable `TerminalModeSnapshot`.
3. `SetMode` applies a complete snapshot using immediate, drain, or drain-and-discard timing where the native platform supports it.

Every operation reports `Available`, `Unavailable`, `Unsupported`, or `Failed`. A redirected regular file is an available nonterminal observation. A valid operation that the selected endpoint cannot expose is unavailable. A platform capability that does not exist is unsupported. Native API failures are failed results with an optional native error code.

## POSIX implementation

Linux and macOS use `isatty`, `ttyname_r`, `tcgetattr`, `tcsetattr`, `cfgetispeed`, and `cfgetospeed`. Existing descriptors are never closed. Named devices are opened with `O_NOCTTY` and nonblocking access and are closed after the operation.

The snapshot retains:

- all four native terminal flag words;
- the complete native control-character array;
- the host disabled-character value;
- native input and output speed codes plus recognized baud rates;
- the Linux line-discipline byte where represented by the ABI;
- the native flag width needed to reject restoration onto an incompatible ABI.

The supported CI ABIs are represented explicitly: Linux uses 32-bit `tcflag_t`, 32 control characters, and separate 32-bit speed fields; macOS uses 64-bit flags, 20 control characters, and 64-bit speed fields. Other Unix-like hosts receive a controlled unsupported result until their ABI is added and tested.

## Windows implementation

Windows resolves standard descriptors through `GetStdHandle`, other CRT descriptors through `_get_osfhandle`, and named console devices through `CreateFileW`. `GetConsoleMode` determines attachment and retrieves the complete mode; `SetConsoleMode` applies it.

An attached handle reports the stable alias `CONIN$` or `CONOUT$` and identifies whether its mode is input or output. Windows capabilities include attachment, pathname, mode retrieval, mode mutation, and serialization. POSIX speeds, line discipline, control characters, and drain or input-flush timing are explicitly unsupported rather than guessed.

## Serialization and restoration

POSIX modes use GNU `stty -g`'s machine form: lowercase hexadecimal input, output, control, and local flags followed by every native control-character byte, all separated by colons. Restoration requires the exact control-array length and rejects fields that exceed the destination flag or byte width. Speeds and line discipline are intentionally preserved from the live baseline because GNU's portable save form does not include them.

Windows modes use `win32-v1-input:` or `win32-v1-output:` followed by an eight-digit hexadecimal console mode. The direction prefix prevents an input mode from being restored to an output handle or vice versa.

`TerminalControlCharacterFormatter` renders the disabled value as `<undef>`, control bytes as caret notation, delete as `^?`, and high-bit bytes with the `M-` prefix used by GNU diagnostics.

## Ownership boundary for Batches 50 and 51

Batch 50 will implement `tty` as a thin consumer of `Observe(StandardInput)`, adding silent mode, GNU output wording, and command exit status.

Batch 51 will implement `stty` above `GetMode`, `SetMode`, and `TerminalModeCodec`. That project owns option parsing, control-character names and native indices, `sane` and `raw` profiles, human-readable display, device selection, GNU diagnostics, and the documented reduced Windows vocabulary.

F3 deliberately does not introduce child processes, signal handling, process groups, pseudo-terminal creation, or full-screen terminal mechanics. Those facilities are not prerequisites for `tty` or the first `stty` implementation.
