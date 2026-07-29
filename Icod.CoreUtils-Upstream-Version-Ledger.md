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
| `GNULIB-COREUTILS-9.11` | GNU Gnulib regular-expression implementation used by Coreutils 9.11 | pinned revision | commit `fb7312fa8d3df29f0ca0678f669b9a5b88a078ec` | [GNU Gnulib regular expressions](https://www.gnu.org/software/gnulib/manual/html_node/Regular-expressions.html) |
| `POSIX-2024` | The Open Group Base Specifications Issue 8 / IEEE Std 1003.1-2024 | Issue 8 | document number `9799919799` | [POSIX.1-2024 regular-expression definitions and rationale](https://pubs.opengroup.org/onlinepubs/9799919799/) |
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
| 12 | Completed | Filesystem flushing: `sync` | `COREUTILS-9.11` |
| 13 | Completed | Binary formatting: `od` | `COREUTILS-9.11` |
| 14 | Completed | Formatted and human-readable numeric output: `printf`, `numfmt` | `COREUTILS-9.11` |
| 15 | Completed | Secure temporary objects: `mktemp` | `COREUTILS-9.11` |
| 16 | Planned | Expression language: `expr` | `COREUTILS-9.11` |
| 17 | Planned | Tabs and display columns | `COREUTILS-9.11` |
| 18 | Planned | Paragraph and line-number formatting | `COREUTILS-9.11` |
| 19 | Planned | Field and record extraction | `COREUTILS-9.11` |
| 20 | Planned | External ordering and randomization | `COREUTILS-9.11` |
| 21 | Planned | Sorted-stream consumers | `COREUTILS-9.11` |
| 22 | Planned | Character transformation, graph ordering, and permuted indexing | `COREUTILS-9.11` |
| 23 | Planned | Regular-expression search: `grep` | `GREP-3.12` |
| 24 | Planned | Splitting and reversing | `COREUTILS-9.11`; regex behavior in `csplit` remains the Coreutils contract while reusing internal regex policy established in Batch 22 |
| 25 | Planned | Page presentation and binary inspection | `COREUTILS-9.11` |
| 26 | Planned | Difference engine: `diff` | `DIFFUTILS-3.12` |
| 27 | Planned | Patch application engine: `patch` | `PATCH-2.8` |
| 28 | Planned | Line editor: `ed` | `ED-1.22.5` |
| 29 | Planned | Symbolic-link and canonical-path resolution | `COREUTILS-9.11` |
| 30 | Planned | File metadata and timestamps | `COREUTILS-9.11` |
| 31 | Planned | Condition evaluator: `test` | `COREUTILS-9.11` |
| 32 | Planned | Basic directory and name removal | `COREUTILS-9.11` |
| 33 | Planned | Hard and symbolic links | `COREUTILS-9.11` |
| 34 | Planned | Special file creation | `COREUTILS-9.11` |
| 35 | Planned | Permission modes: `chmod` | `COREUTILS-9.11` |
| 36 | Planned | Ownership and group mutation | `COREUTILS-9.11` |
| 37 | Planned | Recursive removal: `rm` | `COREUTILS-9.11` |
| 38 | Planned | Copy and move engine | `COREUTILS-9.11` |
| 39 | Planned | Installation engine: `install` | `COREUTILS-9.11` |
| 40 | Planned | Color database: `dircolors` | `COREUTILS-9.11` |
| 41 | Planned | Directory listing family | `COREUTILS-9.11` |
| 42 | Planned | Filesystem usage reporting | `COREUTILS-9.11` |
| 43 | Planned | Data destruction: `shred` | `COREUTILS-9.11` |
| 44 | Planned | Archive engine: `tar` | `TAR-1.35` |
| 45 | Planned | Host and processor context | `COREUTILS-9.11` |
| 46 | Planned | Terminal identification: `tty` | `COREUTILS-9.11` |
| 47 | Planned | Terminal characteristics: `stty` | `COREUTILS-9.11` |
| 48 | Planned | Environment and hangup-independent execution | `COREUTILS-9.11` |
| 49 | Planned | Priority and time-bounded execution | `COREUTILS-9.11` |
| 50 | Planned | Signal control: `kill` | `COREUTILS-9.11` |
| 51 | Planned | Root-directory execution: `chroot` | `COREUTILS-9.11` |
| 52 | Planned | SELinux context operations | `COREUTILS-9.11` |
| 53 | Planned | Standard-stream buffering control: `stdbuf` | `COREUTILS-9.11` |


## Engineering-gate-to-authority mapping

| Gate | State | Shared subject | Authoritative pin |
|---|---|---|---|
| C1 | Implementation prepared; CI validation pending | GNU basic regular expressions | `COREUTILS-9.11`; `GNULIB-COREUTILS-9.11`; `POSIX-2024` |

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
- **Validation status:** implementation and Shared unit tests are prepared. Completion Gate C1 remains unchecked until the complete solution builds and all applicable tests pass on `windows-latest`, `ubuntu-latest`, and `macos-latest`.

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
