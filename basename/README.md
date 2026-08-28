# BASENAME(1)

## NAME

**basename** — strip directory components and an optional suffix from pathnames

## SYNOPSIS

```text
basename NAME [SUFFIX]
basename OPTION... NAME...
```

## DESCRIPTION

`Icod.CoreUtils.BaseName` is a managed .NET implementation of GNU Coreutils `basename(1)`, modeled on GNU Coreutils 9.11.

The command removes leading directory components from each selected pathname. In the traditional two-operand form, `SUFFIX` is also removed when it is a proper trailing suffix of the resulting name.

With `--multiple` or `--suffix`, multiple pathname operands are accepted. Pathname reduction is lexical; the filesystem is not queried.

## OPTIONS

```text
-a, --multiple
    Support multiple NAME operands.

-s, --suffix=SUFFIX
    Remove a trailing SUFFIX from each resulting name.

-z, --zero
    End each output with NUL instead of a newline.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

Supplying `--suffix` also enables multiple-name mode.

## EXIT STATUS

```text
0    Every requested name was processed successfully.
1    Command-line usage was invalid or an output operation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

The pathname algorithm uses `/` as the GNU pathname separator and does not access the filesystem. It therefore gives deterministic GNU-style lexical results even when the host platform uses different native pathname conventions.

## AUTHORS

GNU `basename` was written by David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`basename(1)`, `dirname(1)`, `realpath(1)`
