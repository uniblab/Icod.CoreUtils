# MKNOD(1)

## NAME

**mknod** — create block, character, or FIFO special files

## SYNOPSIS

```text
mknod [OPTION]... NAME TYPE [MAJOR MINOR]
```

## DESCRIPTION

`Icod.CoreUtils.MkNod` is a managed .NET implementation of GNU Coreutils `mknod(1)`, modeled on GNU Coreutils 9.11.

`TYPE` is `b` for a block device, `c` or `u` for a character device, or `p` for a FIFO. Block and character devices require `MAJOR` and `MINOR`; FIFO creation omits them.

Device numbers accept decimal, a leading `0` for octal, or `0x` for hexadecimal. Creation is delegated to the shared filesystem mutation provider.

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

An explicit mode may specify permission bits only. Explicit security-context labeling is currently unavailable and an explicit context request is accepted with a warning and ignored.

## EXIT STATUS

```text
0    The requested special file was created successfully.
1    Usage, mode/device-number parsing, or special-file creation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Special-file creation is highly platform and privilege dependent. The command reports provider failures for unsupported device nodes, invalid device numbers, insufficient privilege, and unavailable FIFO support rather than substituting ordinary files.

## PATHNAME GLOBBING

`mknod` does not perform `Icod.CommandFramework` pathname glob expansion. Its pathname operand names a filesystem object to create rather than select an existing entry, so wildcard characters that reach `mknod` remain part of the requested pathname subject to host pathname rules. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `mknod` was written by David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`mknod(1)`, `mkfifo(1)`
