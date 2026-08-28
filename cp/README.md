# CP(1)

## NAME

**cp** — copy files and directories

## SYNOPSIS

```text
cp [OPTION]... SOURCE... DEST
cp [OPTION]... SOURCE... DIRECTORY
cp [OPTION]... -t DIRECTORY SOURCE...
```

## DESCRIPTION

`Icod.CoreUtils.Cp` is a managed .NET implementation of GNU Coreutils `cp(1)`, modeled on GNU Coreutils 9.11.

The command uses the shared copy/move engine for regular files, directories, links, metadata preservation, sparse-file handling, backups, overwrite policy, hard-link preservation, and clone/reflink requests.

Directory copying requires recursive mode. Destination interpretation follows the ordinary single-destination, target-directory, and no-target-directory forms.

## OPTIONS

```text
-a, --archive
    Equivalent to recursive no-dereference copying with all supported metadata
    and hard-link relationships preserved.

-R, -r, --recursive
    Copy directories recursively.

-H
    Follow symbolic links named on the command line.

-L, --dereference
    Follow all symbolic links.

-P, --no-dereference
    Never follow symbolic links.

-d
    Preserve hard links and do not dereference symbolic links.

-p
    Preserve mode, ownership, and timestamps.

--preserve[=ATTR_LIST]
    Preserve selected metadata classes. Implemented classes include mode,
    ownership, timestamps, links, and all.

--no-preserve=ATTR_LIST
    Disable selected metadata preservation.

--sparse=WHEN
    Control sparse-file creation: never, auto, or always.

--reflink[=WHEN]
    Control clone/reflink copying: never, auto, or always.

-b, --backup[=CONTROL]
    Back up an existing destination.

-S, --suffix=SUFFIX
    Override the normal backup suffix.

-f, --force
    Replace existing destinations.

-i, --interactive
    Prompt before overwrite.

-n, --no-clobber
    Do not overwrite existing destinations.

-u, --update
    Copy only when SOURCE is newer than an existing destination.

--remove-destination
    Remove an existing destination before copying.

-l, --link
    Create hard links instead of copying file data.

-s, --symbolic-link
    Create symbolic links instead of copying file data.

-t, --target-directory=DIR
    Copy all SOURCE operands into DIR.

-T, --no-target-directory
    Treat DEST as an ordinary destination path.

-v, --verbose
    Report completed copies.

-x, --one-file-system
    Stay on each recursive source's starting filesystem.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

Metadata classes `context` and `xattr` are not currently supported by the shared copy contract and are rejected when explicitly requested.

## EXIT STATUS

```text
0    All requested copies completed successfully.
1    One or more copy operations failed.
2    Command-line usage was invalid.
130  The operation was cancelled.
```

## PLATFORM NOTES

The shared engine provides one copy policy across Windows, Linux, and macOS while delegating filesystem-specific capabilities such as metadata, sparse files, hard links, symbolic links, and reflinks to the host providers.

`--reflink=always`, required metadata, and required sparse behavior fail when the underlying platform cannot satisfy the requested capability; automatic modes may fall back to ordinary copying.

## AUTHORS

GNU `cp` was written by Torbjörn Granlund, David MacKenzie, and Jim Meyering.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`cp(1)`, `mv(1)`, `ln(1)`, `rm(1)`
