# MKFIFO(1)

## NAME

**mkfifo** — create named pipes

## SYNOPSIS

```text
mkfifo [OPTION]... NAME...
```

## DESCRIPTION

`Icod.CoreUtils.MkFifo` is a managed .NET implementation of GNU Coreutils `mkfifo(1)`, modeled on GNU Coreutils 9.11.

Each operand is created as a FIFO through the shared filesystem mutation provider. The default requested mode is `0666` filtered by the current creation mask. An explicit chmod-style mode replaces that default masking calculation.

## OPTIONS

```text
-m, --mode=MODE
    Set permission bits to MODE.

-Z
    Request the default SELinux security context.

--context[=CTX]
    Request a specific security context.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

An explicit mode may specify permission bits only. Security-context labeling is not currently exposed by the mutation provider; an explicit `--context` value is accepted with a warning and ignored.

## EXIT STATUS

```text
0    Every requested FIFO was created successfully.
1    Usage, mode parsing, or FIFO creation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

FIFO creation is capability-dependent. Hosts or filesystems whose mutation provider cannot create POSIX named pipes return a controlled unsupported failure rather than emulating a FIFO with an unrelated IPC mechanism.

## PATHNAME GLOBBING

`mkfifo` does not perform `Icod.CommandFramework` pathname glob expansion. Its pathname operands name FIFOs to create rather than select existing filesystem entries, so wildcard characters that reach `mkfifo` remain part of the requested pathname subject to host pathname rules. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `mkfifo` was written by David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`mkfifo(1)`, `mknod(1)`
