# unexpand

`unexpand` implements the GNU Coreutils 9.11 blank-to-tab interface for `net10.0` and C# 13.

## Usage

```text
unexpand [OPTION]... [FILE]...
```

| Option | Behavior |
|---|---|
| `-a`, `--all` | Convert eligible blank runs throughout each logical line. |
| `--first-only` | Convert only leading blank sequences, overriding explicit or implicit `--all`. |
| `-t LIST`, `--tabs=LIST` | Use `LIST` instead of stops every eight columns and implicitly enable all-line conversion. |
| `-LIST` | Accept the obsolete GNU tab-list syntax without implicitly enabling all-line conversion. |
| `--help` | Write usage and option documentation. |
| `--version` | Write version information. |

A single unprefixed value is a recurring interval. Multiple values are explicit stops. A final `/N` continues at global multiples of `N`; a final `+N` continues relative to the final explicit stop. Blanks after a finite explicit list is exhausted remain unchanged.

## Processing model

With no operands, or for an operand named `-`, the command reads standard input. Consecutive operands form one logical stream, so an unterminated blank run can continue into the next operand. Pending locale blanks retain their exact bytes until the processor knows whether a tab can replace them without changing the displayed column. Backspace moves one column left for subsequent calculations.

The command uses cancellation-aware asynchronous binary I/O. Untouched bytes—including byte-order marks, NULs, malformed UTF-8, and locale blanks that cannot be replaced—are preserved exactly. `LC_ALL`, `LC_CTYPE`, then `LANG` select C/POSIX byte processing or the deterministic UTF-8 profile supplied by `Icod.CoreUtils.Shared.Text`.

Implementation files are documented in `src/README.md`; dedicated tests are in `tests/Unexpand.Tests`.
