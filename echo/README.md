# ECHO(1)

## NAME

**echo** — write arguments to standard output

## SYNOPSIS

```text
echo [SHORT-OPTION]... [STRING]...
echo LONG-OPTION
```

## DESCRIPTION

`Icod.CoreUtils.Echo` is a managed .NET implementation of GNU Coreutils `echo(1)`, modeled on GNU Coreutils 9.11.

The command writes its string operands separated by single spaces and normally appends a newline.

When escape interpretation is enabled, the implementation recognizes the GNU escape set for backslash, alert, backspace, escape, form feed, newline, carriage return, horizontal tab, vertical tab, `\c`, octal byte escapes, and hexadecimal byte escapes.

## OPTIONS

```text
-n
    Do not output the trailing newline.

-e
    Enable interpretation of backslash escapes.

-E
    Disable interpretation of backslash escapes. This is the normal default.

--help
    Display command help and exit when it is the sole operand and
    POSIXLY_CORRECT is not set.

--version
    Display version information and exit when it is the sole operand and
    POSIXLY_CORRECT is not set.
```

Short options may be clustered when GNU option scanning is active.

## POSIXLY_CORRECT

When `POSIXLY_CORRECT` is present, backslash escapes are enabled by default and option scanning follows the command's POSIX compatibility path. In that mode `--help` and `--version` are treated as ordinary text rather than long options.

## EXIT STATUS

```text
0    Output completed successfully.
1    A write error occurred.
130  The operation was cancelled.
```

## PLATFORM NOTES

Text is written through the supplied .NET `TextWriter`. Escape `\n` and the final line terminator use the host environment's newline sequence.

## AUTHORS

GNU `echo` was written by Brian Fox and Chet Ramey.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`echo(1)`, `printf(1)`, `yes(1)`
