# Terminal presentation and control

`Icod.CoreUtils.Shared.Terminal` contains the neutral terminal foundations established by Completion Gates F1 and F3. The namespace remains a provisional cross-suite `Icod.CommandFramework` candidate because directory listing, future ProcPs and Tar consumers, and terminal commands need the same observations without depending on one another.

## Presentation responsibilities from Gate F1

- distinguish attached and redirected standard streams through an injectable provider;
- discover terminal dimensions and apply `COLUMNS`, `LINES`, then deterministic fallback precedence;
- capture the `TERM` terminal name plus `COLORTERM`, `SHELL`, and `QUOTING_STYLE` without reading process-global state inside command policy;
- infer ANSI 16-color, 256-color, and true-color capability and resolve never, auto, and always color modes;
- resolve GNU directory-listing quoting defaults, retain invalid environment values for command diagnostics, and present filename control characters deterministically;
- convert unsupported console APIs and failed geometry queries into controlled observations.

## Identification and control responsibilities from Gate F3

- identify borrowed file descriptors and owned named terminal or console devices;
- inspect terminal attachment and discover a POSIX terminal pathname or stable Windows console alias;
- retrieve and apply complete Linux/macOS `termios` or Windows console-mode snapshots;
- preserve native POSIX input and output speed codes while reporting recognized baud rates;
- preserve the complete native POSIX control-character array and render bytes using GNU-compatible visible notation;
- serialize POSIX modes using GNU's colon-separated hexadecimal flag and control-character form;
- serialize Windows input and output console modes using separate version-stable prefixes;
- distinguish available, unavailable, unsupported, and failed operations rather than fabricating cross-platform data;
- permit command tests to inject a provider without opening process-global handles.

## Platform boundary

Linux and macOS expose terminal pathnames, complete `termios` flags, native speeds, control characters, immediate application, drain-before-application, and drain-plus-input-discard application. Windows exposes console attachment, `CONIN$` or `CONOUT$`, and complete `GetConsoleMode`/`SetConsoleMode` values. Windows does not synthesize POSIX baud rates, control characters, line discipline, or drain semantics.

The shared layer does not assign GNU option names, control-character names, `sane` or `raw` profiles, or command exit statuses. Batch 50 owns `tty` policy and Batch 51 owns `stty` parsing, profiles, display, diagnostics, and exit behavior. Cursor movement, signals, process groups, pseudo-terminals, and full-screen interfaces remain outside this gate. The namespace also does not parse the `dircolors` database or `LS_COLORS`; Batch 46 owns those Coreutils-specific grammars above the neutral presentation observations.

## Completion Gate P1 ProcPs boundary

ProcPs `tload`, `watch`, `hugetop`, `slabtop`, and `top` consume these neutral attachment, geometry, environment, presentation-capability, and terminal-mode observations. `Icod.ProcPs.Shared` owns full-screen buffers, interaction state, field layout, sorting/filtering, configuration, refresh decisions, and command-specific restoration policy. It must not introduce duplicate terminal handles, geometry probes, or console/termios snapshots merely to keep ProcPs code under a suite namespace.
