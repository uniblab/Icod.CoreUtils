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
| `GNULIB-COREUTILS-9.11` | GNU Gnulib revision used by Coreutils 9.11 | pinned revision | commit `fb7312fa8d3df29f0ca0678f669b9a5b88a078ec` | [GNU Gnulib manual](https://www.gnu.org/software/gnulib/manual/gnulib.html) |
| `POSIX-2024` | The Open Group Base Specifications Issue 8 / IEEE Std 1003.1-2024 | Issue 8 | document number `9799919799` | [POSIX.1-2024 online specification](https://pubs.opengroup.org/onlinepubs/9799919799/) |
| `SED-4.10` | GNU sed | 4.10 | tag `v4.10`; commit `89b7a2224d4faa9d8baf76094b1232ad1477ef3e` | [GNU sed 4.10 release](https://lists.gnu.org/archive/html/info-gnu/2026-04/msg00009.html) |
| `NETTOOLS-2.10` | net-tools `hostname` | 2.10 | tag `v2.10` | [net-tools project and release files](https://sourceforge.net/projects/net-tools/) |
| `UTIL-LINUX-2.42.2` | util-linux | 2.42.2 | `util-linux-2.42.2.tar.xz`; SHA-256 `03a05d3adf9602ef128f2da05b84b3205ce60c351e5737c0370f74000679ce8a` | [util-linux 2.42.2 release files](https://www.kernel.org/pub/linux/utils/util-linux/v2.42/) |
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
| 12 | Completed | Filesystem flushing: `sync` | `COREUTILS-9.11` |
| 13 | Completed | Binary formatting: `od` | `COREUTILS-9.11` |
| 14 | Completed | Formatted and human-readable numeric output: `printf`, `numfmt` | `COREUTILS-9.11` |
| 15 | Completed | Secure temporary objects: `mktemp` | `COREUTILS-9.11` |
| 16 | Completed | Expression language: `expr` | `COREUTILS-9.11` |
| 17 | Completed | Tabs and display columns: `expand`, `unexpand`, `fold` | `COREUTILS-9.11` |
| 18 | Completed | Paragraph and line-number formatting: `fmt`, `nl` | `COREUTILS-9.11` |
| 19 | Completed | Field and record extraction: `cut`, `paste` | `COREUTILS-9.11` |
| 20 | Completed | External ordering: `sort` | `COREUTILS-9.11`; `GNULIB-COREUTILS-9.11` where Coreutils delegates common collation or temporary-file mechanics |
| 21 | Completed | External randomization: `shuf` | `COREUTILS-9.11`; `GNULIB-COREUTILS-9.11` where Coreutils delegates random-source or temporary-file mechanics |
| 22 | Completed | Sorted-stream consumers: `comm`, `join`, `uniq` | `COREUTILS-9.11` |
| 23 | Completed | Character transformation: `tr` | `COREUTILS-9.11`; `POSIX-2024` for character classes and utility semantics |
| 24 | Completed | Graph ordering: `tsort` | `COREUTILS-9.11`; `POSIX-2024` for the standardized utility contract |
| 25 | Completed | Permuted indexing: `ptx` | `COREUTILS-9.11`; `GNULIB-COREUTILS-9.11` for GNU Emacs regular-expression syntax |
| 26 | Completed | `Icod.Grep` search engine | `GREP-3.12`; `GNULIB-COREUTILS-9.11`; `POSIX-2024` |
| 27 | Completed | Splitting and reversing: `split`, `tac` | `COREUTILS-9.11` |
| 28 | Completed | Pattern-directed splitting: `csplit` | `COREUTILS-9.11`; Gate R1 authorities for regular expressions |
| 29 | Completed | Page presentation: `pr` | `COREUTILS-9.11` |
| 30 | Completed | `Icod.DiffUtils.Shared` foundation | `DIFFUTILS-3.12`; Gate E1 authorities for recursive directory comparison |
| 31 | Completed | `Icod.DiffUtils.Cmp` | `DIFFUTILS-3.12` |
| 32 | Completed | `Icod.DiffUtils.Diff` | `DIFFUTILS-3.12`; Gate E1 authorities for recursive traversal |
| 33 | Completed | `Icod.DiffUtils.Diff3` | `DIFFUTILS-3.12` |
| 34 | Completed | `Icod.DiffUtils.SDiff` | `DIFFUTILS-3.12` |
| 35 | Completed | Symbolic-link and canonical-path resolution: `readlink`, `realpath` | `COREUTILS-9.11` |
| 36 | Completed | File metadata and timestamps: `stat`, `touch` | `COREUTILS-9.11` |
| 37 | Completed | Condition evaluator: `test` | `COREUTILS-9.11`; `POSIX-2024` |
| 38 | Completed | Basic directory and name removal: `mkdir`, `rmdir`, `unlink` | `COREUTILS-9.11` |
| 39 | Completed | Hard and symbolic links: `link`, `ln` | `COREUTILS-9.11` |
| 40 | Completed | Special file creation: `mkfifo`, `mknod` | `COREUTILS-9.11`; `POSIX-2024` where standardized |
| 41 | Completed | Permission modes: `chmod` | `COREUTILS-9.11`; `POSIX-2024` |
| 42 | Completed | Ownership and group mutation: `chown`, `chgrp` | `COREUTILS-9.11`; `POSIX-2024` |
| 43 | Completed | Recursive removal: `rm` | `COREUTILS-9.11` |
| 44 | Completed | Copy and move engine: `cp`, `mv` | `COREUTILS-9.11` |
| 45 | Completed | Installation engine: `install` | `COREUTILS-9.11` |
| 46 | Completed | Color database and directory listing: `dircolors`, `ls`, `dir`, `vdir` | `COREUTILS-9.11` |
| 47 | Completed | Filesystem usage reporting: `df`, `du` | `COREUTILS-9.11` |
| 48 | Completed | Data destruction: `shred` | `COREUTILS-9.11` |
| 49 | Completed | Host and processor context: `hostid`, `nproc` | `COREUTILS-9.11`; `PROCPS-4.0.6` as a secondary consumer authority for shared processor-resource contracts |
| 50 | Completed | Terminal identification: `tty` | `COREUTILS-9.11`; `POSIX-2024` |
| 51 | Completed | Terminal characteristics: `stty` | `COREUTILS-9.11`; `POSIX-2024` |
| 52 | Completed | Environment and hangup-independent execution: `env`, `nohup` | `COREUTILS-9.11`; `POSIX-2024` |
| 53 | Completed | util-linux signal control: `kill` | `UTIL-LINUX-2.42.2` |
| 54 | Completed | Process priority control: GNU `nice`, util-linux `renice` | `COREUTILS-9.11`; `UTIL-LINUX-2.42.2`; `POSIX-2024` where standardized |
| 55 | Completed | Time-bounded execution: `timeout` | `COREUTILS-9.11` |
| 56 | Planned | `Icod.ProcPs.Shared` provider foundation | `PROCPS-4.0.6` |
| 57 | Planned | ProcPs basic system summaries: `uptime`, `free` | `PROCPS-4.0.6` |
| 58 | Planned | ProcPs sampled system statistics: `vmstat` | `PROCPS-4.0.6` |
| 59 | Planned | ProcPs process selection, signaling, and waiting: `pgrep`, `pkill`, `pidwait` | `PROCPS-4.0.6` |
| 60 | Planned | ProcPs process lookup and working directories: `pidof`, `pwdx` | `PROCPS-4.0.6` |
| 61 | Planned | ProcPs process memory maps: `pmap` | `PROCPS-4.0.6` |
| 62 | Planned | ProcPs process reporting: `ps` | `PROCPS-4.0.6` |
| 63 | Planned | ProcPs user and session reporting: `w` | `PROCPS-4.0.6` |
| 64 | Planned | ProcPs kernel parameter control: `sysctl` | `PROCPS-4.0.6` |
| 65 | Planned | ProcPs load display: `tload` | `PROCPS-4.0.6` |
| 66 | Planned | ProcPs periodic command display: `watch` | `PROCPS-4.0.6` |
| 67 | Planned | ProcPs specialized kernel-memory displays: `hugetop`, `slabtop` | `PROCPS-4.0.6` |
| 68 | Planned | ProcPs interactive process monitor: `top` | `PROCPS-4.0.6` |
| 69 | Planned | Root-directory execution: `chroot` | `COREUTILS-9.11` |
| 70 | Planned | SELinux context operations: `chcon`, `runcon` | `COREUTILS-9.11` |
| 71 | Planned | Standard-stream buffering control: `stdbuf` | `COREUTILS-9.11` |
| 72 | Planned | `Icod.Tar.Tar` archive engine | `TAR-1.35`; Gate E1 authorities for traversal |


## Engineering-gate-to-authority mapping

| Gate | State | Shared subject | Authoritative pin |
|---|---|---|---|
| A | Completed | Repository build, test, target-framework, configuration, encoding, and CI baseline | Repository engineering policy; Microsoft .NET 10 and C# 13 specifications |
| B | Completed | File flushing, sparse extension, and allocation capabilities | `COREUTILS-9.11`; supported-platform filesystem API documentation |
| C1 | Completed | GNU basic regular expressions | `COREUTILS-9.11`; `GNULIB-COREUTILS-9.11`; `POSIX-2024` |
| C2 | Completed | Byte-preserving text units, locale blanks, display columns, and tab stops | `COREUTILS-9.11`; `POSIX-2024` where locale semantics apply |
| C3 | Completed | Byte records, positional ranges, delimiters, and escape profiles | `COREUTILS-9.11`; `POSIX-2024` where standardized |
| D | Completed | Stable external ordering and secure temporary workspaces | `COREUTILS-9.11`; `GNULIB-COREUTILS-9.11` |
| E1 | Completed | Read-only pathname expansion and recursive traversal | `GREP-3.12`; `COREUTILS-9.11`; `DIFFUTILS-3.12`; `TAR-1.35`; `GNULIB-COREUTILS-9.11`; `POSIX-2024` |
| R1 | Completed | Shared GNU/POSIX BRE and ERE foundation | `GREP-3.12`; `COREUTILS-9.11`; `SED-4.10`; `ED-1.22.5`; `GNULIB-COREUTILS-9.11`; `POSIX-2024` |
| E2 | Completed | Canonical and physical pathname resolution | `COREUTILS-9.11`; `POSIX-2024`; supported-platform filesystem API documentation |
| E3 | Completed | Filesystem metadata and timestamp mutation | `COREUTILS-9.11`; `POSIX-2024`; supported-platform filesystem API documentation |
| E3R | Completed | Windows reparse-point characterization | `COREUTILS-9.11`; Windows reparse-point API documentation |
| E4 | Completed | Modes and basic pathname mutation | `COREUTILS-9.11`; `POSIX-2024` |
| E5 | Completed | Mutation-safe recursive traversal, identity, preservation, and copy policy | `COREUTILS-9.11`; `TAR-1.35`; `POSIX-2024` |
| E6 | Completed | Transactional file replacement, backup, rollback, and cleanup | `COREUTILS-9.11`; `PATCH-2.8`; `SED-4.10`; `ED-1.22.5`; `TAR-1.35` |
| F1 | Completed | Terminal-aware presentation and color policy | `COREUTILS-9.11`; `POSIX-2024` where standardized |
| F2 | Completed | Host identity and processor-resource capabilities | `COREUTILS-9.11`; `PROCPS-4.0.6`; supported-platform processor API documentation |
| F3 | Completed | Terminal identification and terminal modes | `COREUTILS-9.11`; `PROCPS-4.0.6`; `POSIX-2024` |
| F4 | Completed | Process execution, waiting, signals, priorities, clocks, and termination | `COREUTILS-9.11`; `UTIL-LINUX-2.42.2`; `PROCPS-4.0.6`; `POSIX-2024` |
| P1 | Completed | ProcPs classification and provider foundation | `PROCPS-4.0.6`; `UTIL-LINUX-2.42.2` for excluded/owned process-control profiles |
| G | Planned | Final API classification, package extraction, and repository split | All pinned suite authorities plus repository architecture policy |


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
- **Validation completed:** the dedicated test project and complete solution passed on `windows-latest`, `ubuntu-latest`, and `macos-latest`; Batch 12 was merged into `main` on 28 July 2026.


## Batch 13 implementation record

- **Batch and command:** Batch 13, `od`.
- **Authority reconfirmed:** 28 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manual:** [GNU Coreutils 9.11 `od` invocation](https://www.gnu.org/software/coreutils/manual/html_node/od-invocation.html).
- **Primary source:** [`src/od.c` at the pinned commit](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/od.c).
- **Secondary synopsis:** [Linux man-pages `od(1)`](https://man7.org/linux/man-pages/man1/od.1.html).
- **Differential oracle:** GNU `od` from the Ubuntu CI image; its runtime `od --version` output is to be captured whenever differential tests are run.
- **Reusable infrastructure:** type-string parsing, byte-order-aware primitive formatting, least-common-multiple width validation, and distributed field padding live in `Shared/src/BinaryFormatting`.
- **Intentional platform interpretation:** integral `L` follows the host C ABI assumption used by the repository: 4 bytes on Windows and pointer width on Unix-like systems. Extended native `long double` encodings that cannot be represented by .NET are rejected with a controlled diagnostic.
- **Best-effort platforms:** FreeBSD and other BSD-family systems use the Unix-like ABI assumptions above, but BSD support is **best effort** and is outside the required CI matrix.
- **Validation completed:** the dedicated test project and complete solution passed on `windows-latest`, `ubuntu-latest`, and `macos-latest`; Batch 13 was merged into `main` on 28 July 2026.


## Batch 14 implementation record

- **Batch and commands:** Batch 14, `printf` and `numfmt`.
- **Authority reconfirmed:** 28 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manual:** [GNU Coreutils 9.11 formatted output](https://www.gnu.org/software/coreutils/manual/html_node/printf-invocation.html) and [human-readable number conversion](https://www.gnu.org/software/coreutils/manual/html_node/numfmt-invocation.html).
- **Primary sources:** [`src/printf.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/printf.c) and [`src/numfmt.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/numfmt.c) at the pinned commit.
- **Differential oracles:** GNU `printf` and `numfmt` from the Ubuntu CI image; their runtime `--version` output is to be captured whenever differential tests are run.
- **Reusable infrastructure:** GNU escape decoding lives in `Shared/src/Formatting`; arbitrary-precision exact rational parsing, scaling, decimal formatting, and explicit rounding live in `Shared/src/Numerics`.
- **Intentional runtime interpretation:** numeric parsing and generated command syntax are culture-aware only where GNU delegates to the active locale; option names, scale keywords, suffix grammar, and decimal source syntax remain invariant.
- **Platform scope:** the implementation is fully managed. Windows, Linux, and macOS are the required CI platforms. FreeBSD and other BSD-family systems are **best effort** and should behave identically except for host locale data.
- **Validation completed:** both dedicated test projects, Shared tests, and the complete solution passed; Batch 14 was merged into `main` on 28 July 2026.

## Batch 15 implementation record

- **Batch and command:** Batch 15, `mktemp`.
- **Authority reconfirmed:** 28 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manual:** [GNU Coreutils 9.11 `mktemp` invocation](https://www.gnu.org/software/coreutils/manual/html_node/mktemp-invocation.html).
- **Primary source:** [`src/mktemp.c` at the pinned commit](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/mktemp.c).
- **Security primitive source:** Gnulib `lib/tempname.c` at commit `fb7312fa8d3df29f0ca0678f669b9a5b88a078ec`, the Gnulib revision recorded for the Coreutils 9.11 release: [pinned source](https://github.com/coreutils/gnulib/blob/fb7312fa8d3df29f0ca0678f669b9a5b88a078ec/lib/tempname.c). This supplies the 62-character alphabet, unbiased random selection, exclusive creation, and minimum `62^3` attempt bound.
- **Secondary synopsis:** [Linux man-pages `mktemp(1)`](https://man7.org/linux/man-pages/man1/mktemp.1.html).
- **Differential oracle:** GNU `mktemp` from the Ubuntu CI image; its runtime `mktemp --version` output is to be captured whenever differential tests are run.
- **Reusable infrastructure:** cryptographic name substitution, final-run template parsing, exclusive file/directory creation, collision-only retries, name-only availability checks, and cleanup support live in `Shared/src/Temporary`.
- **Security model:** regular files use exclusive create-new semantics; Windows directories use `CreateDirectoryW`; Linux, macOS, and best-effort FreeBSD directories use native `mkdir(..., 0700)`; Unix regular files request `0600`; existing symbolic links are collisions and are not followed or replaced.
- **Intentional warning:** `--dry-run` reproduces GNU name-only behavior but cannot reserve the returned pathname and is explicitly documented as unsafe.
- **Best-effort platform:** FreeBSD uses its documented POSIX `mkdir` and `lstat` interfaces but is outside the required CI matrix.
- **Validation status:** Batch 15 is marked completed in the authoritative roadmap. Its dedicated tests, Shared tests, and full-solution three-runner validation remain part of the permanent completion contract.

## Completion Gate C1 implementation record

- **Gate and subject:** Completion Gate C1, shared GNU basic regular-expression foundation.
- **Authority reconfirmed:** 28 July 2026.
- **First consuming command:** Batch 16, GNU Coreutils 9.11 `expr`.
- **Coreutils identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Gnulib identity:** commit `fb7312fa8d3df29f0ca0678f669b9a5b88a078ec`, the revision associated with the Coreutils 9.11 release.
- **POSIX identity:** The Open Group Base Specifications Issue 8, IEEE Std 1003.1-2024, document number `9799919799`.
- **Primary Coreutils source:** [`src/expr.c` at the pinned commit](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/expr.c), especially its `RE_SYNTAX_POSIX_BASIC & ~RE_CONTEXT_INVALID_DUP & ~RE_NO_EMPTY_RANGES` matching profile.
- **Primary Gnulib sources:** [`lib/regex.h`](https://github.com/coreutils/gnulib/blob/fb7312fa8d3df29f0ca0678f669b9a5b88a078ec/lib/regex.h), [`lib/regcomp.c`](https://github.com/coreutils/gnulib/blob/fb7312fa8d3df29f0ca0678f669b9a5b88a078ec/lib/regcomp.c), and [`lib/regexec.c`](https://github.com/coreutils/gnulib/blob/fb7312fa8d3df29f0ca0678f669b9a5b88a078ec/lib/regexec.c).
- **Primary specifications:** [Gnulib predefined syntaxes](https://www.gnu.org/software/gnulib/manual/html_node/Predefined-Syntaxes.html), [Gnulib matching policy](https://www.gnu.org/software/gnulib/manual/html_node/What-Gets-Matched_003f.html), and [POSIX.1-2024](https://pubs.opengroup.org/onlinepubs/9799919799/).
- **Matching policy:** a purpose-built managed parser and state matcher implement GNU BRE syntax, leftmost-longest whole-match selection, and deterministic GNU/Gnulib `re_match` register selection for equal endpoints and repeated subexpressions. `System.Text.RegularExpressions` is not used as the semantic engine.
- **TAP/TPL policy:** synchronous methods remain available; asynchronous methods return `ValueTask`, share the same semantics, accept `CancellationToken`, and do not use `Task.Run` to disguise CPU-bound work.
- **Locale boundary:** `IRegularExpressionCharacterClassProvider` is injectable. The supplied Unicode provider uses BCL `Rune`, Unicode categories, and `CompareInfo`; the supplied POSIX C-locale provider supplies deterministic ASCII classification and ordinal collation.
- **First-consumer compatibility:** `RegularExpressionOptions.GnuExprCompatibility` reproduces the `expr` removal of `RE_CONTEXT_INVALID_DUP` and `RE_NO_EMPTY_RANGES` without weakening strict default compilation for other consumers. `RegularExpressionMatchOptions.RequireMatchAtStart` supplies `expr`'s anchored `re_match(..., 0, ...)` behavior.
- **Controlled diagnostics:** malformed patterns, invalid back-references, invalid ranges, unsupported multi-scalar collating elements, invalid UTF-16 start indices, nesting limits, and match-state limits return stable structured diagnostics. Cancellation retains the standard `OperationCanceledException` contract.
- **Intentional BCL boundary:** .NET does not expose a culture's complete collating-element inventory. Single-scalar collating symbols and equivalence classes are supported; multi-scalar collating elements return `UnsupportedCollatingElement`. POSIX leaves multi-character bracket matching partly unspecified, and a later provider can extend this boundary without changing command code.
- **Encoding model:** matching iterates Unicode scalars, public indices and lengths are UTF-16, and malformed UTF-16 code units are matched as U+FFFD while returned source slices preserve their original code units.
- **Platform scope:** the engine is fully managed and identical on `windows-latest`, `ubuntu-latest`, and `macos-latest`. BSD-family systems are best effort under a compatible .NET runtime. TempleOS is best effort and can use the C-locale provider when globalization services are unavailable.
- **Differential oracles:** pinned GNU `expr` semantics and GNU regex/Grep test vectors; Ubuntu CI must record the runtime versions of any installed GNU executables used by optional differential tests.
- **Validation completed:** the Gate C1 corrections were accepted and merged into `main` on 29 July 2026. The complete-solution `windows-latest`, `ubuntu-latest`, and `macos-latest` contract remains the permanent regression requirement.


## Batch 16 implementation record

- **Batch and command:** Batch 16, `expr`.
- **Authority reconfirmed:** 29 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manual:** [GNU Coreutils 9.11 `expr` invocation](https://www.gnu.org/software/coreutils/manual/html_node/expr-invocation.html), including its string, numeric, relation, and example subnodes.
- **Primary source:** [`src/expr.c` at the pinned commit](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/expr.c).
- **Differential oracle:** GNU `expr` 9.7 available in the implementation environment, run with `LC_ALL=C.UTF-8`; CI must capture the runtime `expr --version` whenever optional differential tests are run.
- **Parser and evaluation model:** the command uses an immediate, precedence-aware evaluator matching GNU's `eval` through `eval7` structure. Boolean branches still parse skipped expressions; arithmetic, comparisons, and regular-expression matching honor GNU's `evaluate` flag, while `length`, `index`, and `substr` retain GNU's normal prefix-operation behavior. Prefix string operators recurse at their own precedence level, and binary operators associate from the left.
- **Numeric model:** all arithmetic and integer comparisons use BCL `BigInteger`, reproducing GNU multiple-precision behavior and truncating division and remainder toward zero without native GMP or P/Invoke.
- **Regular-expression model:** `:` and `match` consume Completion Gate C1 through `RegularExpressionOptions.GnuExprCompatibility` and `RegularExpressionMatchOptions.RequireMatchAtStart`. The first capture determines a string result; otherwise the result is the matched logical-character count.
- **Locale and text model:** `IExpressionLocaleProvider` is injectable. The default uses BCL `CompareInfo` for current-culture collation and Unicode scalar values for `length`, `index`, `substr`, and regex match length. .NET argument strings cannot preserve arbitrary invalid byte sequences or emulate every host C library's single-byte locale, so those cases remain an explicit managed-runtime boundary rather than invoking native locale APIs.
- **TAP/TPL policy:** synchronous compatibility remains available; command execution and Gate C1 calls are cancellation-aware; naturally asynchronous output is awaited; CPU work is not wrapped in `Task.Run`.
- **Controlled statuses:** 0 denotes a non-null/nonzero result, 1 a null or zero result, 2 an invalid expression, 3 an internal/provider/output failure, and the repository cancellation status is returned for requested cancellation. Excessive expression nesting is diagnosed with status 2 rather than risking an uncatchable stack overflow.
- **Platform scope:** the implementation is fully managed and intended to behave identically on `windows-latest`, `ubuntu-latest`, and `macos-latest`. BSD-family systems are best effort under a compatible .NET runtime. TempleOS is best effort and would require a compatible managed runtime and an appropriate injected locale provider.
- **Validation completed:** Batch 16 was accepted and merged into `main`. Its dedicated tests and the complete-solution `windows-latest`, `ubuntu-latest`, and `macos-latest` contract remain permanent regression requirements.


## Completion Gate C2 implementation record

- **Gate and subject:** Completion Gate C2, shared byte-preserving text units, locale-aware blanks, display columns, checked column movement, and GNU tab-stop grammar.
- **Authority reconfirmed:** 29 July 2026.
- **First consuming commands:** Batch 17, GNU Coreutils 9.11 `expand`, `unexpand`, and `fold`.
- **Coreutils identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary sources:** [`src/expand-common.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/expand-common.c), [`src/expand.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/expand.c), [`src/unexpand.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/unexpand.c), and [`src/fold.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/fold.c).
- **Reusable infrastructure:** `Shared/src/Text` supplies incremental byte and UTF-8 scalar iteration, exact source-byte retention, explicit malformed-input policy, injectable locale and width providers, deterministic Unicode display widths, checked display-column operations, and reusable explicit and recurring tab-stop models.
- **Encoding boundary:** the managed implementation provides exact C/POSIX byte behavior and deterministic UTF-8 decoding. Arbitrary stateful legacy multibyte encodings are not silently approximated through replacement decoding.
- **Width boundary:** production display widths use a checked-in deterministic Unicode table rather than host `wcwidth`; providers remain injectable for alternate locale or terminal profiles.
- **Provisional classification:** the text-unit, locale, width, and tab-stop APIs remain cross-suite `Icod.CommandFramework` candidates incubated in `Icod.CoreUtils.Shared` until later consumers establish the permanent package boundary.
- **Validation completed:** the Gate C2 implementation and its dedicated Shared tests were accepted and merged into `main` on 29 July 2026. The complete-solution three-runner contract remains the permanent regression requirement.

## Completion Gate C3 implementation record

- **Gate and subject:** Completion Gate C3, shared byte-record framing, positional range lists, byte delimiters and separator cycles, and command-profile escape parsing.
- **Authority reconfirmed:** 29 July 2026.
- **First consuming commands:** Batch 19, GNU Coreutils 9.11 `cut` and `paste`; the low-level escaped-byte profile is retained for later `tr`.
- **Coreutils identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary sources:** [`src/set-fields.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/set-fields.c), [`src/set-fields.h`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/set-fields.h), [`src/cut.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/cut.c), [`src/paste.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/paste.c), and the low-level escape handling in [`src/tr.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/tr.c).
- **Record model:** `Shared/src/Records` supplies line-feed and NUL separators, bounded independently owned record segments, explicit terminated-versus-unterminated state, and writer operations that leave separator synthesis to the consuming command. `ByteRecordReader` supplies the preferred materialized content-plus-termination model. The existing whole-record `Shared.IO.DelimitedByteRecordReader` remains source-compatible and delegates through that model to the segmented core.
- **Range model:** `Shared/src/Ranges` parses ASCII-decimal `N`, `N-`, `N-M`, and `-M` forms separated by commas, ASCII spaces, or horizontal tabs, supports configurable domains, complement, and open-ended ranges, and returns stable source-positioned diagnostics. Overlapping ranges merge; adjacent ranges intentionally remain separate because GNU consumers can observe requested range starts.
- **Delimiter model:** `Shared/src/Delimiters` distinguishes required nonempty match delimiters from possibly empty output separators, supplies deterministic repeating separator cycles, and incrementally matches multibyte delimiters across input-buffer boundaries.
- **Escape model:** `Shared/src/Escapes` extracts neutral backslash scanning and structured diagnostics while retaining separate GNU profiles. `paste` maps `\0` to an empty separator, drops the backslash on unknown escapes, and rejects a trailing backslash. The future `tr` profile retains escaped-state metadata, one-to-three-digit octal parsing, and GNU warnings for a trailing backslash or an overflowing three-digit octal form. `GnuEscapeDecoder` retains its established formatting grammar.
- **Managed argument boundary:** .NET supplies command operands as decoded UTF-16 strings rather than original `argv` bytes. The escape profiles therefore default to deterministic UTF-8 and permit an injected stateless encoding; they do not claim exact stateful legacy command-line encoding behavior.
- **Intentional boundary:** C3 does not implement `cut`, `paste`, field splitting, the complete `tr` set-expression grammar, sorting, Grep, or Sed policy. It shares byte framing and syntax mechanics while leaving command semantics and final diagnostic wording to consumers.
- **TAP/TPL and ownership:** naturally asynchronous stream reads, writes, and flushes accept cancellation tokens and use awaited BCL operations. Shared record helpers never dispose caller-owned streams and do not wrap CPU parsing in `Task.Run`.
- **Documentation and tests:** every public, protected, or internal declaration has XML documentation; each multi-file source and test directory has a README; Shared tests characterize the old materializing record API and cover bounded segmentation, NUL records, final unterminated records, range normalization and errors, complements, delimiter cycles, multibyte matching, all escape profiles, malformed input, cancellation, and stream ownership.
- **Validation status:** source structure, XML documentation presence, repository-relative placement, UTF-8/LF policy, project wildcard inclusion, and conformance-oriented cases were checked. A .NET SDK was unavailable in the implementation container, so `Shared.Tests` and the complete solution still require build/test validation on `windows-latest`, `ubuntu-latest`, and `macos-latest` before this record is changed to fully validated.

## Batch 17 implementation record

- **Batch and commands:** Batch 17, `expand`, `unexpand`, and `fold`.
- **Authority reconfirmed:** 29 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manuals:** [GNU Coreutils 9.11 `expand`](https://www.gnu.org/software/coreutils/manual/html_node/expand-invocation.html), [`unexpand`](https://www.gnu.org/software/coreutils/manual/html_node/unexpand-invocation.html), and [`fold`](https://www.gnu.org/software/coreutils/manual/html_node/fold-invocation.html) invocation documentation.
- **Primary sources:** [`src/expand.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/expand.c), [`src/unexpand.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/unexpand.c), [`src/fold.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/fold.c), and their shared [`src/expand-common.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/expand-common.c) parser at the pinned commit.
- **Differential oracle:** GNU `expand`, `unexpand`, and `fold` from the Ubuntu CI image; their runtime `--version` output is to be captured whenever optional differential tests are run.
- **Text and byte model:** all three commands process `TextUnit` values from Gate C2, preserve untouched source bytes including BOMs and malformed sequences, and use explicit C/POSIX or deterministic UTF-8 locale profiles.
- **Tab model:** `expand` and `unexpand` share the Gate C2 parser for explicit stops, repeated `-t` values, periodic single stops, `/N` globally aligned continuation, `+N` continuation relative to the final explicit stop, and finite-list exhaustion. The obsolete command-specific numeric forms remain command-local preprocessing.
- **Command distinctions:** `expand` and `unexpand` preserve logical-line state across unterminated operand boundaries; `fold` resets its current column for each operand while retaining GNU's preceding-character-width state used by backspace. `unexpand --tabs` implies all-line conversion, obsolete `-LIST` does not, and `--first-only` overrides either all-line request.
- **Folding model:** `fold` implements byte, decoded-character, and display-column counting; locale-blank word boundaries; tab, carriage-return, and backspace movement; multibyte-scalar integrity; and bounded buffering for arbitrarily long zero-column input.
- **TAP/TPL policy:** asynchronous stream opening, reading, output, help, version, usage, and diagnostics are awaited and cancellation-aware. Synchronous compatibility wrappers block only at the public boundary. Operand processing remains ordered and is not wrapped in `Task.Run` or parallelized.
- **Intentional line-ending interpretation:** untouched input line endings are reproduced exactly. New fold boundaries use `Environment.NewLine`, following the repository's generated-output convention rather than forcing GNU's LF byte on Windows.
- **Documentation and tests:** each command has command-level and source-directory documentation, class-level XML usage text, dedicated usage/help/version writers, XML documentation on every public, protected, or internal declaration, and a dedicated xUnit test project covering options, binary fidelity, Unicode widths, invalid input, operand boundaries, cancellation, ownership, and read/write failures.
- **Validation status:** source structure, project and solution wiring, XML documentation presence, UTF-8/LF policy, and conformance-oriented state-machine cases were checked. A .NET SDK was unavailable in the implementation container, so the three dedicated test projects and complete solution still require build/test validation on `windows-latest`, `ubuntu-latest`, and `macos-latest` before the implementation record can be changed to fully validated.

## Batch 18 implementation record

- **Batch and commands:** Batch 18, `fmt` and `nl`.
- **Authority reconfirmed:** 29 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manuals:** [GNU Coreutils 9.11 `fmt`](https://www.gnu.org/software/coreutils/manual/html_node/fmt-invocation.html) and [`nl`](https://www.gnu.org/software/coreutils/manual/html_node/nl-invocation.html) invocation documentation.
- **Primary sources:** [`src/fmt.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/fmt.c) and [`src/nl.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/nl.c) at the pinned commit.
- **Differential oracle:** GNU `fmt` and `nl` 9.7 available in the implementation environment, run with controlled `LC_ALL` values. The pinned 9.11 source remains authoritative where behavior changed; CI must capture each runtime command's `--version` whenever optional differential tests run.
- **Shared logical-line model:** `Shared/src/Text/TextLine` and `TextLineReader` retain exact source bytes, distinguish a terminating line-feed byte without normalizing it, and expose a managed matching surface for command-local decisions. The abstraction is shared without combining the two execution engines.
- **`fmt` model:** paragraph and prefix recognition are byte-oriented, matching GNU's `strlen` and input-column behavior rather than terminal display width. The command implements default, split-only, crown-margin, and tagged-paragraph recognition; GNU sentence and punctuation metadata; the pinned dynamic-programming costs; `--prefix`, `--uniform-spacing`, `--width`, `--goal`, and obsolete first-argument `-WIDTH`; tab expansion while reading and equivalent tab reintroduction in generated indentation. Retained words preserve their exact input bytes, including BOM and malformed-byte values.
- **`nl` model:** all operands form one document whose line number, logical-page section, blank-group state, and deferred overflow state persist across files and repeated standard-input operands. Header, body, and footer styles support all, nonempty, none, and shared GNU basic regular expressions. Section delimiters support disabled, one-character completed, ordinary two-character, and GNU extended multi-character forms; one-character counting follows the active C/POSIX byte or deterministic UTF-8 scalar profile.
- **Numeric and output model:** `nl` accepts signed starting values and increments, positive blank-group and field-width values, culture-invariant left/right/right-zero formatting, arbitrary separator strings, and GNU's leading-white-space/leading-plus numeric grammar while rejecting trailing white space. Untouched source line feeds remain exact; an unterminated final `nl` input line receives a generated `Environment.NewLine`, and generated paragraph and logical-page lines use `Environment.NewLine` under the repository convention.
- **TAP/TPL policy:** file opening, byte reads, writes, help, version, usage, regular-expression compilation and matching, and diagnostics are cancellation-aware and awaited. Synchronous wrappers block only at the public boundary. Ordered operand state prevents parallel execution, so CPU work is not wrapped in `Task.Run`.
- **Documentation and tests:** both commands have command-level and source-directory READMEs, class-level XML usage documentation, dedicated usage/help/version writers, XML documentation for every public, protected, or internal declaration, and dedicated xUnit projects covering documented options, modes, exact bytes, prefix and delimiter edge cases, numeric grammar, regular expressions, operand continuity, cancellation, ownership, overflows, and controlled read/write failures. Shared logical-line tests cover byte, UTF-8, malformed-input, carriage-return, empty-line, synchronous, asynchronous, and cancellation behavior.
- **Managed-runtime boundaries:** the implementation deliberately does not invoke host C locale or regex libraries. `fmt` preserves GNU's byte-width rules; `nl` uses the existing fully managed Gate C1 GNU BRE engine and Gate C2 C/POSIX or deterministic UTF-8 profile. GNU's arbitrary `MAXWORDS` and `MAXCHARS` emergency paragraph split limits are not reproduced as fixed unsafe buffers; managed collections retain the same ordinary paragraph optimizer without those implementation-size constants.
- **Validation completed:** Batch 18 was accepted and merged into `main`. Its dedicated tests, Shared tests, and the complete-solution three-runner contract remain permanent regression requirements.


## Batch 19 implementation record

- **Batch and commands:** Batch 19, `cut` and `paste`.
- **Authority reconfirmed:** 30 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manuals:** [GNU Coreutils 9.11 `cut`](https://www.gnu.org/software/coreutils/manual/html_node/cut-invocation.html) and [`paste`](https://www.gnu.org/software/coreutils/manual/html_node/paste-invocation.html) invocation documentation.
- **Primary sources:** [`src/cut.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/cut.c), [`src/paste.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/paste.c), and the shared [`src/set-fields.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/set-fields.c) positional-range implementation.
- **Differential oracle:** GNU `cut` and `paste` 9.7 available in the implementation environment for unchanged behaviors. The pinned 9.11 source remains authoritative for newer `cut` options such as `-F`, `-w`, and character-aware `-n`; CI must record runtime `--version` output whenever optional differential tests run.
- **Shared foundations:** both commands consume Completion Gate C3. `cut` uses normalized one-based ranges whose adjacent boundaries remain observable, and `paste` uses bounded segmented records plus the shared GNU delimiter-cycle parser. Character and whitespace decisions reuse Completion Gate C2's deterministic C/POSIX and UTF-8 profiles.
- **`cut` model:** byte and character modes stream bounded input and preserve exact selected bytes, including malformed UTF-8. `--no-partial` follows GNU's selected-suffix rule for multibyte characters. Field mode supports explicit one-character delimiters, empty/NUL delimiters, locale-blank runs, `trimmed` whitespace, `-F`, undelimited-record passthrough or suppression, complements, output delimiters, and the special field-delimiter-equals-record-separator case.
- **`paste` model:** parallel mode opens all ordinary operands before producing output, shares one buffered reader for repeated standard-input operands, preserves leading and interior exhausted columns, and suppresses unused trailing delimiters. Serial mode resets the delimiter cycle per operand, continues after an operand-open failure, and emits one output terminator for an empty operand.
- **Memory and streaming policy:** byte, character, and ordinary paste records are processed in bounded segments. GNU field passthrough can require deferring an arbitrarily long first field until a delimiter is known; the implementation documents and limits materialization to that semantic ambiguity. The record-separator-as-field-delimiter edge case uses one-record lookahead.
- **TAP/TPL policy:** file opening, reads, writes, usage, help, version, and diagnostics are cancellation-aware and awaited. Ordered record and operand semantics prohibit parallel output processing; CPU work is not hidden in `Task.Run`.
- **Generated record terminators:** existing LF or NUL record separators are preserved by `cut`. Unterminated textual `cut` records and all newly generated default `paste` rows use `Environment.NewLine` under repository policy; `-z` uses NUL.
- **Documentation and tests:** both commands include command and source READMEs, dedicated usage/help/version writers, XML documentation for every public, protected, and internal declaration, and dedicated xUnit projects covering ranges, fields, Unicode and malformed bytes, NUL records, delimiter cycles, uneven and repeated inputs, large records, cancellation, ownership, and controlled read/write failures.
- **Validation completed:** Batch 19 was accepted and merged into `main`. Its dedicated tests, Shared tests, and the complete-solution three-runner contract remain permanent regression requirements.

## Batch 20 implementation record

- **Batch and command:** Batch 20, `sort`.
- **Authority reconfirmed:** 30 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manual:** [GNU Coreutils 9.11 `sort` invocation](https://www.gnu.org/software/coreutils/manual/html_node/sort-invocation.html).
- **Primary source:** [`src/sort.c` at the pinned commit](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/sort.c), together with the Gnulib revision pinned for Coreutils 9.11 where common locale, comparison, temporary-file, or merge mechanics are delegated.
- **Differential oracle:** the installed GNU `sort` on a CI or development host may be used only after recording its runtime `sort --version`; the 9.11 manual and source remain authoritative.
- **Reusable infrastructure:** Completion Gate D established locale-aware collation, reusable key parsing and comparison, stable original-order tracking, bounded sorted runs, external merge, and secure temporary-workspace cleanup in Shared.
- **Validation completed:** Batch 20 was accepted and merged into `main`. Dedicated command and Shared tests plus the complete-solution three-runner contract remain permanent regression requirements.

## Batch 21 implementation record

- **Batch and command:** Batch 21, `shuf`.
- **Authority reconfirmed:** 30 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manual:** [GNU Coreutils 9.11 `shuf` invocation](https://www.gnu.org/software/coreutils/manual/html_node/shuf-invocation.html).
- **Primary source:** [`src/shuf.c` at the pinned commit](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/shuf.c), together with the pinned Gnulib revision where Coreutils delegates random-source or temporary-file mechanics.
- **Differential oracle:** the installed GNU `shuf` may be used only with its runtime version captured; the 9.11 source remains authoritative.
- **Reusable infrastructure:** the command consumes Shared byte-record, segmented-record, output, diagnostic, and secure temporary-spool contracts. Random-source interpretation, unbiased bounded selection, partial Fisher-Yates permutation, range sampling, repeat policy, and external occurrence indexing remain command-local.
- **Validation completed:** Batch 21 was accepted and merged into `main`. Dedicated command and Shared tests plus the complete-solution three-runner contract remain permanent regression requirements.

## Batch 22 implementation record

- **Batch and commands:** Batch 22, `comm`, `join`, and `uniq`.
- **Authority reconfirmed:** 30 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manuals:** [GNU Coreutils 9.11 `comm`](https://www.gnu.org/software/coreutils/manual/html_node/comm-invocation.html), [`join`](https://www.gnu.org/software/coreutils/manual/html_node/join-invocation.html), and [`uniq`](https://www.gnu.org/software/coreutils/manual/html_node/uniq-invocation.html) invocation documentation.
- **Primary sources:** [`src/comm.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/comm.c), [`src/join.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/join.c), and [`src/uniq.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/uniq.c) at the pinned commit.
- **Differential oracle:** installed GNU executables may be used only after recording each runtime version.
- **Reusable infrastructure:** Shared supplies byte-record collation. `comm` remains a constant-memory two-way merge, `join` buffers one equal-key group from each input to preserve duplicate-key Cartesian products, and `uniq` remains an adjacent-record streaming state machine.
- **Validation completed:** Batch 22 was accepted and merged into `main`. All three dedicated test projects, Shared tests, and the complete-solution three-runner contract remain permanent regression requirements.

## Batch 23 implementation record

- **Batch and command:** Batch 23, `tr`.
- **Authority reconfirmed:** 30 July 2026.
- **Authoritative package:** GNU Coreutils 9.11, with POSIX.1-2024 as a secondary standard for character classes and utility semantics.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`; POSIX document `9799919799`.
- **Primary manual:** [GNU Coreutils 9.11 `tr` invocation](https://www.gnu.org/software/coreutils/manual/html_node/tr-invocation.html).
- **Primary source:** [`src/tr.c` at the pinned commit](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/tr.c).
- **Differential oracle:** installed GNU `tr` may be used only after recording its runtime version.
- **Reusable infrastructure:** the command consumes Shared byte-stream, locale, character-class, delimiter, and escape foundations. Set-expression parsing, repetition, equivalence, complement, deletion, translation, and squeezing remain command-local.
- **Validation completed:** Batch 23 was accepted and merged into `main`. Dedicated command and Shared tests plus the complete-solution three-runner contract remain permanent regression requirements.

## Batch 24 implementation record

- **Batch and command:** Batch 24, `tsort`.
- **Authority reconfirmed:** 30 July 2026.
- **Authoritative package:** GNU Coreutils 9.11, with POSIX.1-2024 as the secondary standardized utility contract.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`; POSIX document `9799919799`.
- **Primary manual:** [GNU Coreutils 9.11 `tsort` invocation](https://www.gnu.org/software/coreutils/manual/html_node/tsort-invocation.html).
- **Primary source and fixtures:** [`src/tsort.c`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/tsort.c) and [`tests/misc/tsort.pl`](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/tests/misc/tsort.pl) at the pinned commit.
- **Differential oracle:** installed GNU `tsort` may be used only after recording its runtime version and controlling the locale.
- **Reusable infrastructure:** Shared supplies the cancellation-aware asynchronous byte-token reader. The deterministic graph, FIFO release ordering, relation ordering, loop reporting, one-edge loop breaking, and continued output remain command-local.
- **Validation completed:** Batch 24 was accepted and merged into `main` after the cancellation assertion was corrected to accept the valid `TaskCanceledException` subtype of `OperationCanceledException`. Dedicated command and Shared tests plus the complete-solution three-runner contract remain permanent regression requirements.

## Batch 25 implementation record

- **Batch and command:** Batch 25, `ptx`.
- **Authority reconfirmed:** 30 July 2026.
- **Authoritative package:** GNU Coreutils 9.11.
- **Immutable identity:** tag `v9.11`; commit `c01fd163a47468a8296fb369f5233853bb551bb6`.
- **Primary manual:** [GNU Coreutils 9.11 `ptx` invocation](https://www.gnu.org/software/coreutils/manual/html_node/ptx-invocation.html).
- **Primary source:** [`src/ptx.c` at the pinned commit](https://github.com/coreutils/coreutils/blob/c01fd163a47468a8296fb369f5233853bb551bb6/src/ptx.c).
- **Regular-expression authority:** Gnulib revision `fb7312fa8d3df29f0ca0678f669b9a5b88a078ec`, especially `RE_SYNTAX_EMACS`, exposed through the Shared GNU Emacs profile.
- **Differential oracle:** GNU `ptx` 9.7 was available in the implementation environment and used under `LC_ALL=C`; future differential runs must record the runtime version.
- **Reusable infrastructure:** parameter and source I/O use Shared byte streams and record readers; keyword occurrences use the Shared external-ordering engine and secure temporary workspaces; regular-expression options use the Shared managed GNU engine. No command project references another command project.
- **Memory model:** source contexts are written once to a temporary context spool and occurrences carry lightweight offsets; ordering spills through Shared when the memory budget is exceeded. Default sentence and traditional line contexts stream incrementally. An explicitly supplied sentence regular expression may require a complete source matching surface, while occurrence storage and ordering remain externally bounded.
- **TAP/TPL policy:** command I/O, parameter-file reads, source processing, spooling, merge ordering, output, cancellation, and cleanup are asynchronous. CPU-bound matching, field planning, and comparison remain synchronous and cancellation-aware rather than being wrapped in `Task.Run`.
- **Validation completed:** Batch 25 was accepted and merged into `main` after correcting the ambiguous span overload and preserving an input reference across custom-regexp contexts that begin mid-line. The dedicated `Ptx.Tests`, Shared tests, and complete-solution three-runner contract remain permanent regression requirements.

## Completion Gate E1 planning record

- **Gate and subject:** Completion Gate E1, shared read-only pathname expansion and recursive traversal.
- **Authority reconfirmed:** 30 July 2026.
- **First consuming command:** Batch 26, GNU Grep 3.12 `grep`.
- **Primary command authority:** `GREP-3.12`, especially recursive traversal, dereference modes, device and directory policy, include/exclude selection, filename presentation, diagnostics, and statuses.
- **Cross-suite consumer authorities:** `COREUTILS-9.11` for later listing and filesystem-accounting commands; `DIFFUTILS-3.12` for recursive directory comparison; `TAR-1.35` for archive traversal and exclusions.
- **Shared implementation references:** `GNULIB-COREUTILS-9.11` for the pinned GNU traversal, cycle-checking, filename-matching, and filesystem helper behavior; `POSIX-2024` for standardized pathname, globbing, directory, symbolic-link, and filesystem semantics.
- **Platform authorities:** official Windows, Linux, and macOS filesystem and reparse/symbolic-link API documentation determine ABI and capability behavior. They do not override the pinned command semantics.
- **Required boundary:** the gate produces caller-independent result, identity, policy, error, and injectable-provider contracts. Grep-specific pattern selection, binary policy, context grouping, and output formatting remain in `Icod.Grep.Grep`.
- **Implementation state:** prepared on the `Gate_e1` branch in `Icod.CoreUtils.Shared.FileSystem.Traversal`; full repository and required three-runner validation remain pending before the gate can be marked complete.
- **Implemented contract:** segment-aware `*`, `?`, bracket, and complete-segment `**` matching; command-selectable operand expansion; root provenance; injectable one-level observation; Windows file/volume identities; Linux `statx` identities; macOS `stat`/`lstat` identities; explicit unavailable capabilities; iterative preorder/postorder traversal; root-only and all-link policies; active-ancestry cycle detection; filesystem boundaries; independent yield/prune selection; structured errors; limits; cancellation; and synthetic plus conditional host integration tests.
- **Gate boundary retained:** canonicalization and complete link-chain resolution remain E2; authoritative metadata remains E3; mutation and copy safety remain E5.

## Completion Gate R1 planning record

- **Gate and subject:** Completion Gate R1, shared GNU/POSIX BRE and ERE foundation.
- **Authority reconfirmed:** 30 July 2026.
- **First full search consumer:** Batch 26, GNU Grep 3.12 `grep`.
- **Primary language authorities:** `POSIX-2024` for BRE, ERE, leftmost-longest matching, captures, bracket expressions, intervals, and diagnostics; `GNULIB-COREUTILS-9.11` for GNU syntax profiles and managed compatibility targets.
- **Consumer authorities:** `GREP-3.12` for Basic, Extended, fixed-string orchestration, matching and offset expectations, and diagnostics; `COREUTILS-9.11` for existing `expr`, `nl`, `ptx`, and future `csplit` consumers; `SED-4.10` and `ED-1.22.5` for later LineEditor validation.
- **Required compatibility:** preserve the current Basic provider as the source-compatible default and retain the GNU `expr` and GNU Emacs profiles already consumed by completed batches.
- **Required boundary:** define original bytes, decoded scalars, UTF-16 positions, match and capture offsets, invalid-input behavior, locale profiles, cancellation, and resource limits without introducing a dependency on `System.Text.RegularExpressions` for BRE or ERE semantics.
- **Perl mode:** GNU Grep `-P` is a separate PCRE capability decision and is not part of the BRE/ERE language provider.
- **State:** not started; must be completed and validated before Batch 26.

## Completion Gate P1 implementation record

- **Gate and subject:** Completion Gate P1, ProcPs classification and provider foundation.
- **Authority reconfirmed:** 7 August 2026.
- **Authoritative package:** procps-ng 4.0.6, tag `v4.0.6`, release commit `4dafddf4`.
- **Selected executable inventory:** `uptime`, `free`, `vmstat`, `pgrep`, `pkill`, `pidwait`, `pidof`, `pwdx`, `pmap`, `ps`, `w`, `sysctl`, `tload`, `watch`, `hugetop`, `slabtop`, and `top`.
- **Deliberate exclusions:** procps-ng `kill`, `skill`, and `snice`; repository `kill` and `renice` remain pinned to `UTIL-LINUX-2.42.2`.
- **`pidwait` decision:** procps-ng renamed `pwait` to `pidwait` in 4.0.0; 4.0.6 installs `pidwait` and no `pwait` compatibility launcher, so Batch 59 has no `Icod.ProcPs.PWait` project.
- **Cross-suite boundary:** F2-F4 host-resource, process identity/target, launch, wait, signal, priority, monotonic-time, status, and terminal contracts remain in the current Shared incubation layer and are classified as future `Icod.CommandFramework` candidates.
- **Shared gap closed:** Linux individual-process queued signal values are implemented through `IProcessSignalProvider`/`sigqueue(3)` and advertised as `QueuedSignalDelivery`; positive-PID util-linux `kill --queue` consumes the Shared path, and ProcPs must do the same rather than adding another queued-signal abstraction.
- **Observation policy:** Linux `/proc` is authoritative; Windows, macOS, and BSD providers are capability-driven. `ObservationFidelity` distinguishes exact, equivalent, approximated, synthesized, and unavailable semantics independently from source provenance.
- **ProcPs-owned layer:** process enumeration and detailed snapshots, selection grammar, procfs/sysfs/cgroup parsers, process fields and personalities, metrics, sampling/delta interpretation, screen state, configuration, sorting, filtering, and interaction remain in `Icod.ProcPs.Shared`.
- **Architecture record:** `Icod.CoreUtils-Completion-Gate-P1-ProcPs-Foundation.md`.
- **Next step:** Batch 56 creates `Icod.ProcPs.Shared` and its tests over these frozen common contracts.

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
