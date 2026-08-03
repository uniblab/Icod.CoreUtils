# Shared file modes

This directory implements Completion Gate E4's reusable GNU/POSIX mode vocabulary.
`PosixFileMode` retains the twelve portable permission and special bits independently of the host platform. `FileCreationMask` applies a caller-supplied umask without changing process-global state. `FileModeParser` accepts absolute numeric modes, GNU operator-numeric clauses, symbolic subject clauses, permission copying, conditional `X`, special bits, multiple operations, and comma-delimited changes. Parsed expressions remain immutable and can be applied repeatedly to different current modes.
Plain numeric modes preserve existing directory set-user-ID and set-group-ID bits when four or fewer digits were supplied, matching GNU's documented directory rule. Symbolic changes likewise preserve those directory bits unless an operation explicitly mentions `s`. Five or more numeric digits, an operator-numeric mode, or an explicit symbolic `s` operation can clear them. Symbolic clauses with omitted subjects are filtered through the supplied umask; explicitly named subjects are not.
`IFileCreationMaskProvider` makes the active creation mask injectable. The system provider reads Linux `/proc/self/status` without changing process state, uses the native query-and-restore idiom under a lock on macOS and best-effort FreeBSD, and reports an empty mask on Windows where POSIX creation masks do not apply.
Authoritative behavior references:
- [GNU Coreutils 9.11 — File permissions](https://www.gnu.org/software/coreutils/manual/html_node/File-permissions.html)
- [GNU Coreutils 9.11 — Symbolic Modes](https://www.gnu.org/software/coreutils/manual/html_node/Symbolic-Modes.html)
- [GNU Coreutils 9.11 — Numeric Modes](https://www.gnu.org/software/coreutils/manual/html_node/Numeric-Modes.html)
- [GNU Coreutils 9.11 — Operator Numeric Modes](https://www.gnu.org/software/coreutils/manual/html_node/Operator-Numeric-Modes.html)
- [GNU Coreutils 9.11 — Directories and set-ID bits](https://www.gnu.org/software/coreutils/manual/html_node/Directory-Setuid-and-Setgid.html)
