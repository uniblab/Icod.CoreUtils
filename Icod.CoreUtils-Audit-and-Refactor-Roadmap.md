# Icod.CoreUtils Initial Audit and Refactor Roadmap

## Scope

- 102 command projects plus `Shared`, all targeting `net9.0`.
- Static source review only; the audit environment does not contain the .NET SDK.
- The per-tool man7 page should be reviewed at the start of each batch.

## Initial measurements

- Tools with `RunAsync`: **3** (`head`, `tail`, and `sed`).
- Tools with asynchronous `Main`: **3**.
- Remaining synchronous tools: **99**.
- Synchronous tools containing `ReadLine` loops: **25**.
- Tools containing `ReadAll*`: **16**.
- Tools containing `ReadToEnd`: **5**.
- Tools containing blocking `Process.WaitForExit`: **10**.
- Tools containing `NotImplementedException`: **7**.
- There is no automated test project. The `/test` directory implements the `test(1)` command.
- The archive contains 103 `obj` directories and 74 `*.Backup.tmp` files; these should be removed from review packages.

## Largest implementations

| Tool | Command.cs LOC | Async | Await count |
|---|---:|:---:|---:|
| `sed` | 4115 | yes | 64 |
| `tail` | 2026 | yes | 60 |
| `head` | 1307 | yes | 38 |
| `ls` | 568 | no | 0 |
| `install` | 313 | no | 0 |
| `link` | 214 | no | 0 |
| `groups` | 189 | no | 0 |
| `ed` | 188 | no | 0 |
| `ln` | 167 | no | 0 |
| `cut` | 166 | no | 0 |
| `grep` | 160 | no | 0 |
| `comm` | 153 | no | 0 |

## Required engineering foundation

Before broad tool-by-tool work, create shared components for:

1. GNU/POSIX-compatible option parsing: short clusters, required and optional values, long options, `--name=value`, repeated options, `--`, obsolete numeric forms, and deterministic unknown-option diagnostics.
2. Async command entry points: `RunAsync(..., CancellationToken)` plus a synchronous compatibility wrapper.
3. Raw byte and delimited-record streaming, including NUL-delimited input and output.
4. Numeric operand parsing with GNU suffixes and overflow handling.
5. File headers, diagnostics, exit-code conventions, broken-pipe behavior, and cancellation.
6. Process execution with `ProcessStartInfo.ArgumentList`, redirected stream forwarding, and `WaitForExitAsync`.
7. Platform capability helpers: BCL first; Windows API only where it provides meaningful equivalent semantics; otherwise a controlled diagnostic and nonzero exit.
8. A real automated test suite with golden vectors and optional Linux parity tests against the native utility.

## Batch-size policy

- **Normal batch:** 3–6 related tools.
- **Clone-family batch:** up to 9 tools when they share one engine, as with checksum commands.
- **Complex state machine:** 1 tool per batch (`sed`, `ed`, `tar`, `grep`).
- **Closely coupled pair:** 2 tools (`head`/`tail`, `diff`/`patch`, `df`/`du`).
- A batch is complete only after build, unit tests, representative man-page option tests, large-input streaming tests, and platform-behavior tests.

## Refactor order

### Batch 0 — foundation and repository hygiene

Refactor `Shared`, add automated tests and CI, centralize option/stream/process/platform helpers, and remove generated artifacts from review archives.

### Batch 1 — Stabilize shared line/byte readers (2 tools)

`head`, `tail`

### Batch 2 — Stabilize the stream editor (1 tool)

`sed`

### Batch 3 — Core streaming byte and record I/O (3 tools)

`cat`, `tee`, `wc`

### Batch 4 — Base encoding family (3 tools)

`base32`, `base64`, `basenc`

### Batch 5 — Checksum and digest family (9 tools)

`b2sum`, `cksum`, `md5sum`, `sha1sum`, `sha224sum`, `sha256sum`, `sha384sum`, `sha512sum`, `sum`

### Batch 6 — Small deterministic commands (7 tools)

`true`, `false`, `echo`, `yes`, `sleep`, `seq`, `factor`

### Batch 7 — Path and basic host-information commands (7 tools)

`basename`, `dirname`, `pathchk`, `pwd`, `printenv`, `arch`, `hostname`

### Batch 8 — Identity and login-information commands (7 tools)

`uname`, `whoami`, `logname`, `groups`, `id`, `users`, `who`

### Batch 9 — Platform and process-information commands (4 tools)

`pinky`, `ps`, `uptime`, `date`

### Batch 10 — Block-oriented copy and conversion (1 tool)

`dd`

### Batch 11 — File-size manipulation (1 tool)

`truncate`

### Batch 12 — Expression evaluators (2 tools)

`expr`, `test`

### Batch 13 — Width, tab, and line-format filters (5 tools)

`expand`, `unexpand`, `fold`, `fmt`, `nl`

### Batch 14 — Field and multi-file record filters (5 tools)

`cut`, `paste`, `comm`, `join`, `uniq`

### Batch 15 — Ordering and transformation filters (5 tools)

`sort`, `shuf`, `tr`, `tsort`, `ptx`

### Batch 16 — Splitting, reversing, and presentation tools (5 tools)

`split`, `csplit`, `tac`, `pr`, `od`

### Batch 17 — Regular-expression search (1 tool)

`grep`

### Batch 18 — Difference and patch family (2 tools)

`diff`, `patch`

### Batch 19 — Line editor (1 tool)

`ed`

### Batch 20 — Archive engine (1 tool)

`tar`

### Batch 21 — Directory and link mutation (6 tools)

`mkdir`, `rmdir`, `rm`, `unlink`, `link`, `ln`

### Batch 22 — Path resolution and metadata (4 tools)

`readlink`, `realpath`, `stat`, `touch`

### Batch 23 — Permissions, ownership, contexts, and root (6 tools)

`chmod`, `chown`, `chgrp`, `chcon`, `runcon`, `chroot`

### Batch 24 — Copy, move, and install family (3 tools)

`cp`, `mv`, `install`

### Batch 25 — Directory listing family (4 tools)

`ls`, `dir`, `vdir`, `dircolors`

### Batch 26 — Filesystem usage reporting (2 tools)

`df`, `du`

### Batch 27 — Data destruction and filesystem flushing (2 tools)

`shred`, `sync`

### Batch 28 — Environment and process control (5 tools)

`env`, `nice`, `timeout`, `kill`, `stdbuf`

## Why this order

- `head` and `tail` are paired because they share count parsing, raw-byte output, record readers, headers, and buffering strategies.
- `sed` is isolated because its parser and execution engine are large enough to require a dedicated conformance campaign.
- Streaming primitives and encoding/checksum families come early because their shared components will be reused widely.
- Small deterministic tools follow, giving fast coverage and validating common CLI behavior.
- Text filters are grouped by data model: width/tab, field/record, ordering, and split/reverse.
- Platform-sensitive filesystem and identity commands come after the capability layer is established.
- `cp`, `mv`, `install`, `ls`, `tar`, `ed`, and similar commands are deliberately late because they have broad option surfaces and subtle filesystem or state-machine semantics.

## Per-batch workflow

1. Open `https://man7.org/linux/man-pages/man1/{tool}.1.html` for every tool in the batch.
2. Record synopsis, options, operands, environment variables, exit statuses, and platform-dependent behavior.
3. Compare current implementation and tests against that matrix.
4. Implement BCL behavior first, then Windows API behavior where semantically appropriate.
5. Use controlled failure—not an unhandled `NotImplementedException`—where the operation is unsupported.
6. Convert external I/O and child-process waits to TAP; avoid `Task.Run` for naturally asynchronous I/O.
7. Add large-input, cancellation, standard-stream, multiple-file, and error-path tests.
8. Build the entire solution before closing the batch.

## Immediate next action

Implement Batch 0, then audit and stabilize `head` and `tail` together as Batch 1. Their recent async work already exposes the shared abstractions that the rest of the solution needs.
