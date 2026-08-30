# UNLINK(1)

## NAME

**unlink** — remove one filesystem name

## SYNOPSIS

```text
unlink FILE
unlink OPTION
```

## PATHNAME GLOBBING

`FILE` is a Class B singular pathname slot. A wildcard-bearing operand may select exactly one pathname; an unmatched pattern remains literal, while multiple matches are rejected before any removal is attempted. This preserves `unlink`'s deliberately singular operation.

## DESCRIPTION

`Icod.CoreUtils.Unlink` is a managed .NET implementation of GNU Coreutils `unlink(1)`, modeled on GNU Coreutils 9.11.

Exactly one pathname operand is required. The command observes the named entry without following path indirection, rejects an ordinary directory, then asks the shared filesystem mutation provider to remove that filesystem name.

Symbolic links, junction-like indirections, and reparse-point names are treated as names to remove rather than targets to traverse when the provider can characterize them safely.

Mounted-volume pathnames are explicitly rejected rather than being treated as ordinary unlinkable names.

## OPTIONS

```text
--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    The named filesystem entry was removed successfully.
1    Usage was invalid, the entry was unsuitable for unlink, or the filesystem
     operation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Removal is performed through the shared metadata and mutation providers with no-follow semantics. This lets the command distinguish ordinary directories, path indirections, reparse points, and volume mount points on platforms where those concepts differ.

## AUTHORS

GNU `unlink` was written by Michael Stone.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`unlink(1)`, `link(1)`, `rm(1)`
