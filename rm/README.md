# RM(1)

## NAME

**rm** — remove files and directories

## SYNOPSIS

```text
rm [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process using the canonical traversal expander. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

A `**` pattern selects explicit removal roots only. It does not authorize recursive directory removal; directory recursion still requires `-r`/`-R`/`--recursive`. Wildcard-discovered symbolic-link directories are not traversed by globbing, and the command's existing `.`/`..` and filesystem-root protections remain in force after expansion.

## DESCRIPTION

`Icod.CoreUtils.Rm` is a managed .NET implementation of GNU Coreutils `rm(1)`, modeled on GNU Coreutils 9.11.

The command expands the repository's supported pathname patterns, observes entries without following the final pathname indirection, and performs race-aware removal through the shared traversal, metadata, identity, and mutation providers.

Recursive removal protects filesystem roots by default, tracks filesystem boundaries when requested, and retains observation identities as mutation preconditions. Literal `.` and `..` operands are always refused.

## OPTIONS

```text
-f, --force
    Ignore nonexistent files and arguments and never prompt.

-i
    Prompt before every removal.

-I
    Prompt once before removing more than three arguments or before recursive
    removal.

--interactive[=WHEN]
    Select prompting: never, once, or always.

-d, --dir
    Remove empty directories without recursive mode.

-r, -R, --recursive
    Remove directories and their contents recursively.

--one-file-system
    Stay on the starting filesystem of each recursive operand.

--preserve-root[=all]
    Protect `/`; with `all`, also refuse recursive removal at separately mounted
    filesystem roots.

--no-preserve-root
    Disable filesystem-root protection. This option may not be abbreviated.

-v, --verbose
    Report each successful removal.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

When standard input is a terminal and interaction has not been disabled, the implementation can also prompt before removing write-protected entries. Write protection is determined from provider metadata and the effective process identity when available.

## EXIT STATUS

```text
0    Every requested removal completed, was skipped by user choice, or was
     ignored under --force.
1    Usage, expansion, traversal, metadata, or mutation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Recursive deletion is implemented through stable-identity-aware traversal and no-follow mutation preconditions, including Windows reparse-point/path-indirection handling.

Permission and write-protection checks are platform/provider aware: Windows read-only attributes can trigger protection, while POSIX-style modes and identities are used when those fields are available.

## AUTHORS

GNU `rm` was written by Paul Rubin, David MacKenzie, Richard M. Stallman, and Jim Meyering.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`rm(1)`, `rmdir(1)`, `unlink(1)`, `shred(1)`
