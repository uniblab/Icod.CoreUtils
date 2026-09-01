# PASTE(1)

## NAME

**paste** — merge corresponding or successive records of files

## SYNOPSIS

```text
paste [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. `*` and `?` match within one pathname component, and a component exactly equal to `**` may match zero or more complete components. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

The operand `-` is preserved and retains its standard-input meaning.

## DESCRIPTION

Managed C# 13 implementation of GNU Coreutils 9.11 `paste` for `net10.0`.

Parallel mode combines corresponding records from all operands. Serial mode joins every record of one operand before moving to the next. Repeated `-` operands share one standard-input reader and therefore consume successive records rather than rewinding or losing prefetched bytes.

Implemented options are `-d/--delimiters`, `-s/--serial`, and `-z/--zero-terminated`. Delimiter lists cycle by Unicode scalar and recognize GNU's `\b`, `\f`, `\n`, `\r`, `\t`, `\v`, `\\`, and `\0`; `\0` is an empty delimiter slot, not a NUL byte.

## STREAMING MODEL

Each operand uses the Completion Gate C3 segmented byte-record reader. Records of arbitrary size are copied in bounded segments. Parallel mode defers separators for empty or exhausted columns until a later column produces a record, preserving leading and interior empty columns without emitting trailing unused delimiters. A read failure marks the command unsuccessful but does not prevent remaining columns or later serial operands from being processed.

Output rows are newly generated records. Following repository policy, default output uses `Environment.NewLine`; `-z` uses NUL. Serial mode emits one terminator even for an empty operand, matching GNU behavior.

## PROJECT STRUCTURE

- `Program.cs` is the asynchronous process entry point.
- `src/Command.cs` parses options and writes usage, help, version, and diagnostics.
- `src/PasteProcessor.cs` implements parallel and serial execution.
- `src/PasteInput.cs` owns an opened source and bounded segmented reader.
- `src/PasteInputException.cs` keeps operand read failures distinct from output write failures.
- `src/PasteOptions.cs` stores validated command settings.
- `tests/Paste.Tests` is the dedicated xUnit project.

## AUTHORS

GNU `paste` was written by David M. Ihnat and David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`paste(1)`, `cut(1)`, `join(1)`
