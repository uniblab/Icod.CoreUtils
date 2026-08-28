# CUT(1)

## NAME

**cut** — remove or select sections from each line or record

## SYNOPSIS

```text
cut OPTION... [FILE]...
```

## DESCRIPTION

Managed C# 13 implementation of GNU Coreutils 9.11 `cut` for `net10.0`.

Exactly one of `--bytes`, `--characters`, `--fields`, or `-F` is required. Lists accept `N`, `N-`, `N-M`, and `-M`, with comma or blank separators. Overlapping ranges are merged, while adjacent ranges deliberately remain distinct because `--output-delimiter` observes the beginning of each requested range.

Implemented options include `-b`, `-c`, `-f`, `-F`, `-d`, `-n`, `-O`, `-s`, `-w`, `--whitespace-delimited[=trimmed]`, `--complement`, and `-z`. The optional `trimmed` argument belongs to the long option and must be attached with `=`; the short `-w` form accepts no argument.

## TEXT AND BYTE BEHAVIOR

Byte mode streams bounded record segments. Character mode uses the shared byte-preserving C/POSIX or UTF-8 text-unit reader and retains exact source bytes, including malformed UTF-8. Under `--no-partial`, a multibyte character is emitted only when the selected byte positions form a suffix of that character, matching GNU 9.11.

Field mode streams all established fields. It retains only the first field when necessary to decide GNU's undelimited-record passthrough or suppression rule. Plain `-w` uses a run of locale blanks as the input delimiter and defaults output between selected fields to TAB. `-F` is the distinct shorthand for `-f`, `-w`, and a single-space output delimiter; an explicit `-d` disables the shorthand's implied whitespace parsing but retains its space output default, matching GNU option state. The unusual case where the field delimiter equals the record separator requires one-record lookahead and therefore materializes one field at a time. A separator at physical EOF terminates that one logical record; it does not manufacture another selectable empty field.

Terminated input records retain their LF or NUL separator. When an unterminated final record must receive a generated textual terminator, the repository convention uses `Environment.NewLine`; `-z` generates NUL.

## PROJECT STRUCTURE

- `Program.cs` is the asynchronous process entry point.
- `src/Command.cs` parses options and writes usage, help, version, and diagnostics.
- `src/CutProcessor.cs` handles byte and character ranges.
- `src/CutFieldProcessor.cs` handles explicit and whitespace-delimited fields.
- `src/CutInputStream.cs` keeps operand read failures distinct from output write failures.
- `src/CutOptions.cs` and `src/CutMode.cs` hold validated command state.
- `tests/Cut.Tests` is the dedicated xUnit project.

## AUTHORS

GNU `cut` was written by David M. Ihnat, David MacKenzie, and Jim Meyering.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`cut(1)`, `paste(1)`, `join(1)`
