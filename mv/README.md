# MV(1)

## NAME

**mv** — move or rename files and directories

## SYNOPSIS

```text
mv [OPTION]... SOURCE... DEST
mv [OPTION]... SOURCE... DIRECTORY
mv [OPTION]... -t DIRECTORY SOURCE...
```

## PATHNAME GLOBBING

Source operands that contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve source-operand order and repetition; unmatched source patterns are preserved as literal operands.

The destination operand is never glob-expanded. In `-t`/`--target-directory` form, the target-directory option value remains literal while the positional source operands are eligible for expansion.

## DESCRIPTION

`Icod.CoreUtils.Mv` is a managed .NET implementation of GNU Coreutils `mv(1)`, modeled on GNU Coreutils 9.11.

The command uses the same shared copy/move engine as `cp`. It first uses native rename semantics where possible. When a move crosses filesystems, the engine can copy the hierarchy with metadata/hard-link preservation and then remove the source.

`--no-copy` disables that cross-filesystem fallback.

## OPTIONS

```text
-b, --backup[=CONTROL]
    Back up an existing destination.

-S, --suffix=SUFFIX
    Override the normal backup suffix.

-f, --force
    Replace existing destinations without prompting.

-i, --interactive
    Prompt before overwrite.

-n, --no-clobber
    Do not overwrite existing destinations.

-u, --update
    Move only when SOURCE is newer than an existing destination.

--no-copy
    Fail rather than copy-and-remove when a native rename cannot cross filesystems.

-t, --target-directory=DIR
    Move all SOURCE operands into DIR.

-T, --no-target-directory
    Treat DEST as an ordinary destination path.

-v, --verbose
    Report completed moves.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    All requested moves completed successfully.
1    One or more move operations failed.
2    Command-line usage was invalid.
130  The operation was cancelled.
```

## PLATFORM NOTES

Native rename, transactional destination replacement, recursive copy fallback, metadata preservation, and source cleanup are coordinated through the shared copy/move engine and filesystem providers.

Cross-filesystem fallback therefore preserves only metadata classes the active platform can observe and apply. `--no-copy` is useful when rename-only behavior is required.

## AUTHORS

GNU `mv` was written by Mike Parker, David MacKenzie, and Jim Meyering.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`mv(1)`, `cp(1)`, `rm(1)`
