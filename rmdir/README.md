# RMDIR(1)

## NAME

**rmdir** — remove empty directories

## SYNOPSIS

```text
rmdir [OPTION]... DIRECTORY...
```

## DESCRIPTION

`Icod.CoreUtils.RmDir` is a managed .NET implementation of GNU Coreutils `rmdir(1)`, modeled on GNU Coreutils 9.11.

Each operand must identify an actual directory rather than a symbolic link or other pathname indirection. Removal is performed with a metadata-derived mutation precondition so the entry observed is the entry removed.

With `--parents`, successful removal continues upward through ancestor directories until no further removable parent remains.

## OPTIONS

```text
--ignore-fail-on-non-empty
    Ignore a failure caused only by a directory not being empty.

-p, --parents
    Remove DIRECTORY and then its ancestors.

-v, --verbose
    Report each directory as it is processed.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    Every requested directory was removed or an eligible non-empty failure was
     ignored.
1    Usage, metadata observation, or directory removal failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Directory observation and removal use no-follow semantics through the shared metadata/mutation providers. `--parents` stops before attempting to remove the host filesystem root.

## AUTHORS

GNU `rmdir` was written by David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`rmdir(1)`, `mkdir(1)`, `rm(1)`
