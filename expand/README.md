# EXPAND(1)

## NAME

**expand** — convert tabs to spaces

## SYNOPSIS

```text
expand [OPTION]... [FILE]...
```

## DESCRIPTION

`expand` implements the GNU Coreutils 9.11 tab-expansion interface for `net10.0` and C# 13.

| Option | Behavior |
|---|---|
| `-i`, `--initial` | Convert only tabs in the initial blank region of each logical line. |
| `-t LIST`, `--tabs=LIST` | Use `LIST` instead of stops every eight columns. |
| `-LIST` | Accept the obsolete GNU tab-list syntax. |
| `--help` | Write usage and option documentation. |
| `--version` | Write version information. |

A single unprefixed value is a recurring interval. Multiple values are explicit stops. A final `/N` continues at global multiples of `N`; a final `+N` continues relative to the final explicit stop. Tabs beyond a finite explicit list become one space.

## PROCESSING MODEL

With no operands, or for an operand named `-`, the command reads standard input. Consecutive operands form one logical stream: when a file lacks a final newline, column and initial-region state continue into the next operand. Backspace moves one column left for later tab calculations.

The command uses cancellation-aware asynchronous binary I/O. Untouched bytes—including byte-order marks, NULs, and malformed UTF-8—are preserved exactly. `LC_ALL`, `LC_CTYPE`, then `LANG` select C/POSIX byte processing or the deterministic UTF-8 display-width profile supplied by `Icod.CoreUtils.Shared.Text`.

Implementation files are documented in `src/README.md`; dedicated tests are in `tests/Expand.Tests`.

## AUTHORS

GNU `expand` was written by David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`expand(1)`, `unexpand(1)`
