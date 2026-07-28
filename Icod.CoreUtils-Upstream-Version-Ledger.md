# Icod.CoreUtils Authoritative Upstream Version Ledger

## Purpose

This file records the exact upstream specification baseline for every completed and planned Icod.CoreUtils batch. It is the durable record required by Completion Gate A.

The version recorded here is the authority for synopsis, options, operands, environment variables, locale behavior, signals, output grammar, diagnostics, and exit statuses. Man7 pages remain useful secondary summaries, but they do not replace the pinned upstream manual and source.

## Pinning policy

1. A pin never floats automatically. A newer upstream release does not alter an existing batch baseline merely because it becomes available.
2. Completed batches use a retrospective specification pin. The pin identifies the upstream version against which the completed implementation and tests are to be audited and maintained.
3. Future batches use a planning pin. Before implementation begins, the pin must be reconfirmed. If the project deliberately adopts a newer version, this ledger must be updated before code changes begin.
4. When a batch contains commands from more than one upstream package, each package is listed explicitly.
5. Differential-test executables supplied by a CI image may have a different version. Their runtime version must be captured in the test log; they do not silently replace the authoritative pin.
6. A deliberate rebase must preserve history by recording the former pin, replacement pin, decision date, and compatibility impact.

## Pinned upstream releases

| Key | Upstream authority | Pinned version | Immutable release identity | Primary specification |
|---|---|---:|---|---|
| `COREUTILS-9.11` | GNU Coreutils | 9.11 | tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6` | [GNU Coreutils 9.11 manual](https://www.gnu.org/software/coreutils/manual/coreutils.html) |
| `SED-4.10` | GNU sed | 4.10 | tag `v4.10`; commit `89b7a2224d4faa9d8baf76094b1232ad1477ef3e` | [GNU sed 4.10 release](https://lists.gnu.org/archive/html/info-gnu/2026-04/msg00009.html) |
| `NETTOOLS-2.10` | net-tools `hostname` | 2.10 | tag `v2.10` | [net-tools project and release files](https://sourceforge.net/projects/net-tools/) |
| `PROCPS-4.0.6` | procps-ng `ps` | 4.0.6 | tag `v4.0.6`; release commit `4dafddf4` | [procps-ng 4.0.6 tag](https://gitlab.com/procps-ng/procps/-/tags) |
| `GREP-3.12` | GNU grep | 3.12 | tag `v3.12`; commit `3f8c09ec197a2ced82855f9ecd2cbc83874379ab` | [GNU Grep 3.12 manual](https://www.gnu.org/software/grep/manual/grep.html) |
| `DIFFUTILS-3.12` | GNU Diffutils | 3.12 | tag `v3.12`; commit `16681a3cbcea47e82683c713b0dac7d59d85a6fa` | [GNU Diffutils 3.12 manual](https://www.gnu.org/software/diffutils/manual/diffutils.html) |
| `PATCH-2.8` | GNU patch | 2.8 | `patch-2.8.tar.xz`; SHA-256 `f87cee69eec2b4fcbf60a396b030ad6aa3415f192aa5f7ee84cad5e11f7f5ae3` | [GNU patch 2.8 release](https://lists.gnu.org/archive/html/info-gnu/2025-03/msg00014.html) |
| `ED-1.22.5` | GNU ed | 1.22.5 | `ed-1.22.5.tar.lz`; SHA-256 `56e107ddc2f29dad6690376c15bf9751509e1ee3b8241710e44edbe5c3a158cc` | [GNU ed 1.22.5 manual](https://www.gnu.org/software/ed/manual/ed_manual.html) |
| `TAR-1.35` | GNU tar | 1.35 | release `1.35`, 22 August 2023 | [GNU tar 1.35 manual](https://www.gnu.org/software/tar/manual/tar.html) |

### `hostname` profile

The `hostname` command in this repository follows the traditional Linux net-tools interface, including `--alias`, `--domain`, `--fqdn`, `--file`, `--ip-address`, `--node`, `--short`, and `--yp`/`--nis`. It is therefore pinned to net-tools rather than GNU Inetutils. The man7 `hostname(1)` page may be consulted as a secondary rendering of this interface.

## Batch-to-authority mapping

| Batch | State | Batch subject | Authoritative pin |
|---:|---|---|---|
| 0 | Completed | Foundation and repository hygiene | Repository engineering batch; no command upstream |
| 1 | Completed | Shared line/byte readers: `head`, `tail` | `COREUTILS-9.11` |
| 2 | Completed | Stream editor: `sed` | `SED-4.10` |
| 3 | Completed | Streaming byte and record I/O: `cat`, `tee`, `wc` | `COREUTILS-9.11` |
| 4 | Completed | Base encoding: `base32`, `base64`, `basenc` | `COREUTILS-9.11` |
| 5 | Completed | Checksums and digests | `COREUTILS-9.11` |
| 6 | Completed | Small deterministic commands | `COREUTILS-9.11` |
| 7 | Completed | Paths and basic host information | `COREUTILS-9.11`; `NETTOOLS-2.10` for `hostname` |
| 8 | Completed | Identity and login information | `COREUTILS-9.11` |
| 9 | Completed | Platform and process information | `COREUTILS-9.11`; `PROCPS-4.0.6` for `ps` |
| 10 | Completed | Block copy and conversion: `dd` | `COREUTILS-9.11` |
| 11 | Completed | File-size manipulation: `truncate` | `COREUTILS-9.11` |
| 12 | In progress | Filesystem flushing: `sync` | `COREUTILS-9.11` |
| 13 | Planned | Formatted and human-readable numeric output | `COREUTILS-9.11` |
| 14 | Planned | Secure temporary objects: `mktemp` | `COREUTILS-9.11` |
| 15 | Planned | Expression language: `expr` | `COREUTILS-9.11` |
| 16 | Planned | Tabs and display columns | `COREUTILS-9.11` |
| 17 | Planned | Paragraph and line-number formatting | `COREUTILS-9.11` |
| 18 | Planned | Field and record extraction | `COREUTILS-9.11` |
| 19 | Planned | External ordering and randomization | `COREUTILS-9.11` |
| 20 | Planned | Sorted-stream consumers | `COREUTILS-9.11` |
| 21 | Planned | Character transformation, graph ordering, and permuted indexing | `COREUTILS-9.11` |
| 22 | Planned | Regular-expression search: `grep` | `GREP-3.12` |
| 23 | Planned | Splitting and reversing | `COREUTILS-9.11`; regex behavior in `csplit` remains the Coreutils contract while reusing internal regex policy established in Batch 22 |
| 24 | Planned | Page presentation and binary inspection | `COREUTILS-9.11` |
| 25 | Planned | Difference engine: `diff` | `DIFFUTILS-3.12` |
| 26 | Planned | Patch application engine: `patch` | `PATCH-2.8` |
| 27 | Planned | Line editor: `ed` | `ED-1.22.5` |
| 28 | Planned | Symbolic-link and canonical-path resolution | `COREUTILS-9.11` |
| 29 | Planned | File metadata and timestamps | `COREUTILS-9.11` |
| 30 | Planned | Condition evaluator: `test` | `COREUTILS-9.11` |
| 31 | Planned | Basic directory and name removal | `COREUTILS-9.11` |
| 32 | Planned | Hard and symbolic links | `COREUTILS-9.11` |
| 33 | Planned | Special file creation | `COREUTILS-9.11` |
| 34 | Planned | Permission modes: `chmod` | `COREUTILS-9.11` |
| 35 | Planned | Ownership and group mutation | `COREUTILS-9.11` |
| 36 | Planned | Recursive removal: `rm` | `COREUTILS-9.11` |
| 37 | Planned | Copy and move engine | `COREUTILS-9.11` |
| 38 | Planned | Installation engine: `install` | `COREUTILS-9.11` |
| 39 | Planned | Color database: `dircolors` | `COREUTILS-9.11` |
| 40 | Planned | Directory listing family | `COREUTILS-9.11` |
| 41 | Planned | Filesystem usage reporting | `COREUTILS-9.11` |
| 42 | Planned | Data destruction: `shred` | `COREUTILS-9.11` |
| 43 | Planned | Archive engine: `tar` | `TAR-1.35` |
| 44 | Planned | Host and processor context | `COREUTILS-9.11` |
| 45 | Planned | Terminal identification: `tty` | `COREUTILS-9.11` |
| 46 | Planned | Terminal characteristics: `stty` | `COREUTILS-9.11` |
| 47 | Planned | Environment and hangup-independent execution | `COREUTILS-9.11` |
| 48 | Planned | Priority and time-bounded execution | `COREUTILS-9.11` |
| 49 | Planned | Signal control: `kill` | `COREUTILS-9.11` |
| 50 | Planned | Root-directory execution: `chroot` | `COREUTILS-9.11` |
| 51 | Planned | SELinux context operations | `COREUTILS-9.11` |
| 52 | Planned | Standard-stream buffering control: `stdbuf` | `COREUTILS-9.11` |


## Batch 11 implementation record

- **Batch and command:** Batch 11, `truncate`.
- **Authority reconfirmed:** 28 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manual:** [GNU Coreutils 9.11 `truncate` invocation](https://www.gnu.org/software/coreutils/manual/html_node/truncate-invocation.html).
- **Primary source:** [`src/truncate.c` at the pinned commit](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/truncate.c).
- **Secondary synopsis:** [Linux man-pages `truncate(1)`](https://man7.org/linux/man-pages/man1/truncate.1.html).
- **Differential oracle:** GNU `truncate` from the Ubuntu CI image; its runtime `truncate --version` output is to be captured whenever differential tests are run.
- **Intentional platform interpretation:** Windows has no direct `st_blksize` equivalent, so `--io-blocks` uses the target volume's allocation-unit size from `GetDiskFreeSpaceW`.
- **Best-effort platform:** FreeBSD uses the contemporary `struct stat` layout from the official [FreeBSD `sys/sys/stat.h`](https://github.com/freebsd/freebsd-src/blob/main/sys/sys/stat.h), but is not required in the current CI matrix.
- **Intentional runtime boundary:** ordinary seekable files are the conformance target; special-file and FIFO opens use portable .NET `FileStream` behavior rather than an exact cross-platform emulation of GNU's `O_NONBLOCK` open.
- **Validation completed:** the dedicated test project passed and Batch 11 was merged into `main` on 28 July 2026.


## Batch 12 implementation record

- **Batch and command:** Batch 12, `sync`.
- **Authority reconfirmed:** 28 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manual:** [GNU Coreutils 9.11 `sync` invocation](https://www.gnu.org/software/coreutils/manual/html_node/sync-invocation.html).
- **Primary source:** [`src/sync.c` at the pinned commit](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/sync.c).
- **Secondary synopsis:** [Linux man-pages `sync(1)`](https://man7.org/linux/man-pages/man1/sync.1.html).
- **Differential oracle:** GNU `sync` from the Ubuntu CI image; its runtime `sync --version` output is to be captured whenever differential tests are run.
- **Intentional platform interpretation:** Windows supplies file-specific data-and-metadata flushing through `FlushFileBuffers`, but no supported process-level equivalent of Unix `sync()` or `syncfs()`; global, filesystem-specific, and data-only requests therefore return controlled unsupported results.
- **GNU fallback retained:** when the provider lacks `syncfs` but supports global flushing, `sync -f FILE...` makes one global flush request, matching GNU Coreutils builds without `syncfs`.
- **Best-effort platform:** FreeBSD uses its documented `open`, `fcntl`, `fdatasync`, `fsync`, and `sync` interfaces, but is outside the required CI matrix.
- **Native pathname behavior:** Unix pathname operands are opened with `O_NONBLOCK`, retried write-only when needed, restored to blocking mode, flushed, and closed; Windows pathname operands use `CreateFileW` and `FlushFileBuffers`.
- **Deferred validation:** the state remains **In progress** until the dedicated test project and complete solution pass on `windows-latest`, `ubuntu-latest`, and `macos-latest`; after merge, change the Batch 12 state to **Completed**.

## Required batch-start record

Before implementation begins, the batch notes or conformance matrix must copy the following fields from this ledger:

- batch number and command list;
- authoritative package name and version;
- tag, commit, release archive, or checksum identity;
- primary manual/source location;
- date the pin was reconfirmed;
- any secondary POSIX or platform specification used;
- differential oracle executable and runtime version, when applicable;
- any intentional deviation from or deferral of the pinned specification.
