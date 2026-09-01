# DATE(1)

## NAME

**date** — print, parse, format, or set date and time values

## SYNOPSIS

```text
date [OPTION]... [+FORMAT]
date [-u|--utc|--universal] [MMDDhhmm[[CC]YY][.ss]]
```

## PATHNAME GLOBBING

`date` is a Class C utility and performs no in-process pathname globbing. Positional operands are date/time or formatting syntax rather than pathname collections. The path-valued `--file=DATEFILE` and `--reference=FILE` option values also remain literal; `--file=-` retains its standard-input meaning.

## DESCRIPTION

`Icod.CoreUtils.Date` is a managed .NET implementation of GNU Coreutils `date(1)`, modeled on GNU Coreutils 9.11.

With no date source, the command formats the current date and time. A date may instead come from `--date`, a date file, a reference file's modification time, or the traditional numeric operand. GNU-style relative and absolute date text is parsed by the shared time subsystem.

A `+FORMAT` operand selects output formatting. Common GNU directives implemented by the formatter include `%Y`, `%m`, `%d`, `%H`, `%M`, `%S`, `%N`, `%z`, `%Z`, `%s`, `%F`, `%T`, `%R`, `%a`, `%A`, `%b`, `%B`, `%c`, `%x`, and `%X`.

## OPTIONS

```text
-d, --date=STRING
    Display the time described by STRING instead of the current time.

--debug
    Write date-parsing details to standard error.

-f, --file=DATEFILE
    Parse and print one date for each line of DATEFILE. A file name of `-`
    reads from standard input.

-I[FMT], --iso-8601[=FMT]
    Produce ISO 8601 output. FMT may be date, hours, minutes, seconds, or ns.

--resolution
    Print the timestamp resolution exposed by this implementation.

-R, --rfc-email, --rfc-822
    Produce RFC 5322-style date and time output.

--rfc-3339=FMT
    Produce RFC 3339 output. FMT may be date, seconds, or ns.

-r, --reference=FILE
    Use FILE's last modification time as the date source.

-s, --set=STRING
    Set the system clock to the time described by STRING.

-u, --utc, --universal
    Use Coordinated Universal Time for printing or setting.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

Date-source options are mutually exclusive. A traditional non-format operand is also treated as a date source and requests a system-clock change.

## EXIT STATUS

```text
0    The requested operation completed successfully.
1    Usage, parsing, reference-file access, formatting, or clock setting failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Reading and formatting time is portable across supported .NET hosts. Setting the system clock is delegated to the active date/time provider and therefore depends on host support and process privilege. Unsupported or denied clock changes are diagnosed rather than silently ignored.

The `--resolution` result reflects the resolution exposed by this implementation, not necessarily the physical resolution of every underlying hardware clock.

## AUTHORS

GNU `date` was written by David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`date(1)`, `touch(1)`, `stat(1)`
