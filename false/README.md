# FALSE(1)

## NAME

**false** — do nothing, unsuccessfully

## SYNOPSIS

```text
false [ignored command line arguments]
false OPTION
```

## DESCRIPTION

`Icod.CoreUtils.False` is a managed .NET implementation of GNU Coreutils `false(1)`, modeled on GNU Coreutils 9.11.

The command performs no requested operation and returns an unsuccessful status. Command-line arguments are otherwise ignored.

When `--help` or `--version` is the sole argument, the corresponding text is written, but the command still retains the unsuccessful status associated with `false`.

## OPTIONS

```text
--help
    Display command help when supplied as the sole argument.

--version
    Display version information when supplied as the sole argument.
```

## EXIT STATUS

```text
1    Normal `false` result, including help and version requests.
130  The operation was cancelled before completion.
```

## PLATFORM NOTES

The command has no filesystem, identity, terminal, or other platform dependency.

## AUTHORS

GNU `false` was written by Jim Meyering.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`false(1)`, `true(1)`, `test(1)`
