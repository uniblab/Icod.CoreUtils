# FOLD(1)

## NAME

**fold** — wrap input lines to a specified width

## SYNOPSIS

```text
fold [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. `*` and `?` match within one pathname component, and a component exactly equal to `**` may match zero or more complete components. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

The operand `-` is preserved and retains its standard-input meaning.

## DESCRIPTION

`fold` implements the GNU Coreutils 9.11 line-folding interface for `net10.0` and C# 13.

| Option | Behavior |
|---|---|
| `-b`, `--bytes` | Count exact source bytes. |
| `-c`, `--characters` | Count decoded scalar or preserved invalid-byte units. |
| `-s`, `--spaces` | Break after the final eligible blank, or inside a word longer than the width. |
| `-w WIDTH`, `--width=WIDTH` | Use a positive width instead of 80. |
| `-WIDTH` | Accept the obsolete GNU width syntax. |
| `--help` | Write usage and option documentation. |
| `--version` | Write version information. |

When several byte/character counting options occur, the last one wins. Display-column mode is the default. In non-byte modes, tab advances to the next multiple of eight, carriage return resets the current column, and backspace reverses the remembered width of the last ordinary character.

## PROCESSING MODEL

With no operands, or for an operand named `-`, the command reads standard input. Each operand starts with column zero, while GNU's remembered last-character width remains available to backspace calculations across lines and operands. A valid multibyte scalar is never split. Input without a final newline remains unterminated.

The command uses cancellation-aware asynchronous binary I/O and bounded movable buffering, including long zero-column sequences. Untouched bytes—including byte-order marks, NULs, and malformed UTF-8—are preserved exactly. Generated fold separators follow the repository convention and use `Environment.NewLine`; existing input newlines are reproduced unchanged.

Implementation files are documented in `src/README.md`; dedicated tests are in `tests/Fold.Tests`.

## AUTHORS

GNU `fold` was written by David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`fold(1)`, `fmt(1)`
