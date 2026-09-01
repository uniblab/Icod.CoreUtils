# LINK(1)

## NAME

**link** — create a hard link to a file

## SYNOPSIS

```text
link FILE1 FILE2
link OPTION
```

## PATHNAME GLOBBING

`FILE1` is the existing-source Class B singular pathname slot. A wildcard-bearing source must resolve to at most one pathname; an unmatched pattern remains literal and multiple matches are rejected. `FILE2` names the new hard link and is always treated literally.

## DESCRIPTION

`Icod.CoreUtils.Link` is a managed .NET implementation of GNU Coreutils `link(1)`, modeled on GNU Coreutils 9.11.

The command creates a hard link named `FILE2` to the existing `FILE1`. Exactly two pathname operands are required.

The destination must not already exist. The shared filesystem mutation provider is asked to follow eligible source path indirection while creating the hard-link name.

## OPTIONS

```text
--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    The hard link was created successfully.
1    Usage was invalid or the filesystem operation failed.
130  The operation was cancelled.
```

Typical failures include an existing destination, a missing source or parent, a cross-device link, insufficient permission, or lack of hard-link support.

## PLATFORM NOTES

Hard-link creation is routed through the shared filesystem mutation abstraction so Windows, Linux, and macOS can use their native facilities. Filesystem type, privilege policy, and volume boundaries can still limit whether a requested hard link is possible.

## AUTHORS

GNU `link` was written by Michael Stone.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`link(1)`, `ln(1)`, `unlink(1)`
