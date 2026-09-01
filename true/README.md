# TRUE(1)

## NAME

**true** — do nothing, successfully

## SYNOPSIS

```text
true [ignored command line arguments]
true OPTION
```

## DESCRIPTION

`Icod.CoreUtils.True` is a managed .NET implementation of GNU Coreutils `true(1)`, modeled on GNU Coreutils 9.11.

The command performs no requested operation and normally returns success. Command-line arguments other than a sole `--help` or `--version` are ignored.

## OPTIONS

```text
--help
    Display command help when supplied as the sole argument.

--version
    Display version information when supplied as the sole argument.
```

## EXIT STATUS

```text
0    Normal `true` result, including successful help and version output.
1    Writing help or version information failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

The command has no filesystem, identity, terminal, or other platform dependency.

## PATHNAME GLOBBING

`true` does not perform `Icod.CommandFramework` pathname glob expansion. It has no pathname operands eligible for expansion; its purpose is solely to return a successful status. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `true` was written by Jim Meyering.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`true(1)`, `false(1)`, `test(1)`
