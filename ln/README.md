# LN(1)

## NAME

**ln** — create hard or symbolic links

## SYNOPSIS

```text
ln [OPTION]... [-T] TARGET LINK_NAME
ln [OPTION]... TARGET
ln [OPTION]... TARGET... DIRECTORY
ln [OPTION]... -t DIRECTORY TARGET...
```

## PATHNAME GLOBBING

Globbing is mode- and grammar-aware. Symbolic-link `TARGET` operands are link payload text and are never pathname-expanded, so nonexistent or wildcard-bearing symbolic targets remain exactly as supplied. In hard-link mode, source `TARGET` operands use collection expansion only when the already-selected invocation targets a directory (including `-t DIRECTORY`); otherwise the single source slot must resolve to at most one pathname. Destination names and target-directory operands always remain literal.

## DESCRIPTION

`Icod.CoreUtils.Ln` is a managed .NET implementation of GNU Coreutils `ln(1)`, modeled on GNU Coreutils 9.11.

Hard links are created by default. `--symbolic` creates symbolic links instead. Multiple targets may be linked into a directory, or `--target-directory` may specify that directory explicitly.

Existing destinations are left alone unless force, interactive replacement, or backup policy permits replacement.

## OPTIONS

```text
-b, --backup[=CONTROL]
    Back up each existing destination before replacement.

-f, --force
    Remove existing destination files.

-i, --interactive
    Prompt before replacing a destination.

-L, --logical
    Dereference TARGET symbolic links when making hard links.

-n, --no-dereference
    Treat a destination symbolic link to a directory as a file.

-P, --physical
    Make hard links directly to symbolic links when supported.

-r, --relative
    With --symbolic, write a target relative to the link's location.

-s, --symbolic
    Make symbolic links instead of hard links.

-S, --suffix=SUFFIX
    Override the normal backup suffix.

-t, --target-directory=DIR
    Create links in DIR.

-T, --no-target-directory
    Treat LINK_NAME as an ordinary destination path.

-v, --verbose
    Print each successfully created link.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

`--relative` requires `--symbolic`. `--target-directory` and `--no-target-directory` are mutually exclusive.

## EXIT STATUS

```text
0    Every requested link was created or intentionally left unchanged.
1    Usage, destination replacement, or link creation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Hard-link and symbolic-link creation are routed through the shared filesystem mutation provider. Filesystem type, privilege policy, volume boundaries, and operating-system link capabilities determine whether a requested link can be created.

Relative symbolic targets are computed lexically from the link location and then passed to the provider unchanged.

## AUTHORS

GNU `ln` was written by Mike Parker and David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`ln(1)`, `link(1)`, `cp(1)`, `readlink(1)`
