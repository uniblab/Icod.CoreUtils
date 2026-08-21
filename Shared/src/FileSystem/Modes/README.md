# Coreutils file-mode policy

This directory contains the GNU/Coreutils-specific file-mode grammar and policy that remain after the neutral POSIX mode value model moved to `Icod.CommandFramework`.

## Coreutils-owned responsibilities

`Icod.CoreUtils.Shared.FileSystem.Modes` retains:

- `FileModeSubject`
- `FileModeOperator`
- `FileModeOperation`
- `FileModeClause`
- `FileModeExpression`
- `FileModeParseErrorCode`
- `FileModeParseResult`
- `FileModeParser`

`FileModeParser` accepts GNU absolute numeric modes, operator-numeric clauses, symbolic subject clauses, permission copying, conditional `X`, special bits, multiple operations, and comma-delimited changes. Parsed expressions remain immutable and can be applied repeatedly to different current modes.

Plain numeric modes preserve existing directory set-user-ID and set-group-ID bits when four or fewer digits were supplied, matching GNU's documented directory rule. Symbolic changes likewise preserve those directory bits unless an operation explicitly mentions `s`. Five or more numeric digits, an operator-numeric mode, or an explicit symbolic `s` operation can clear them. Symbolic clauses with omitted subjects are filtered through the supplied creation mask; explicitly named subjects are not.

Current-process creation-mask observation is command-neutral host mechanism and is consumed from `Icod.CommandFramework.FileSystem.Modes`.

## Framework-owned value and observation model

The neutral portable mode values and creation-mask observation contracts are owned by `Icod.CommandFramework.FileSystem.Modes`:

- `PosixFileModeBits`
- `PosixFileMode`
- `FileCreationMask`
- `IFileCreationMaskProvider`
- `SystemFileCreationMaskProvider`

Coreutils mode parsing and GNU mode policy consume those published framework types directly. This directory must not define duplicate copies of them.

## Authoritative behavior references

- [GNU Coreutils 9.11 - File permissions](https://www.gnu.org/software/coreutils/manual/html_node/File-permissions.html)
- [GNU Coreutils 9.11 - Symbolic Modes](https://www.gnu.org/software/coreutils/manual/html_node/Symbolic-Modes.html)
- [GNU Coreutils 9.11 - Numeric Modes](https://www.gnu.org/software/coreutils/manual/html_node/Numeric-Modes.html)
- [GNU Coreutils 9.11 - Operator Numeric Modes](https://www.gnu.org/software/coreutils/manual/html_node/Operator-Numeric-Modes.html)
- [GNU Coreutils 9.11 - Directories and set-ID bits](https://www.gnu.org/software/coreutils/manual/html_node/Directory-Setuid-and-Setgid.html)
