# CHOWN(1)

## NAME

**chown** — change file owner and group

## SYNOPSIS

```text
chown [OPTION]... [OWNER][:[GROUP]] FILE...
chown [OPTION]... --reference=RFILE FILE...
```

## PATHNAME GLOBBING

Target `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

The OWNER/GROUP specification, `--reference=RFILE`, and `--from=CURRENT` remain literal control values. A `**` pattern selects initial targets only; recursive ownership changes remain controlled by `-R` and the command's link-traversal options.

## DESCRIPTION

`Icod.CoreUtils.ChOwn` is a managed .NET implementation of GNU Coreutils `chown(1)`, modeled on GNU Coreutils 9.11.

The command changes owner, group, or both for each selected filesystem entry. User/group names and numeric identities are resolved through the shared identity provider. A missing owner or group component retains the corresponding current value according to GNU-style owner specification rules.

With `--reference`, owner and group are taken from `RFILE`. `--from=CURRENT` conditions changes on the currently observed owner/group.

## OPTIONS

```text
-c, --changes
    Report only files whose ownership actually changes.

-f, --silent, --quiet
    Suppress most error diagnostics.

-v, --verbose
    Report every file processed.

--dereference
    Affect the referent of each symbolic link.

-h, --no-dereference
    Affect a symbolic link itself rather than its referent when supported.

--from=CURRENT
    Apply a change only when the current owner/group matches CURRENT.

--no-preserve-root
    Do not treat `/` specially during recursive processing.

--preserve-root
    Refuse to operate recursively on `/`.

--reference=RFILE
    Copy the relevant ownership values from RFILE.

-R, --recursive
    Operate recursively.

-H
    With -R, traverse command-line directory symbolic links.

-L
    With -R, traverse every directory symbolic link encountered.

-P
    With -R, do not traverse directory symbolic links. This is the default.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    Every requested ownership operation completed or was intentionally skipped.
1    Usage, identity resolution, traversal, metadata, or mutation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

POSIX ownership mutation is capability-dependent. Unsupported hosts are diagnosed explicitly rather than receiving a best-effort approximation.

Recursive processing uses the shared race-aware traversal engine and mutation preconditions. No-follow ownership changes on pathname indirections depend on the capabilities of the active filesystem provider.

## AUTHORS

GNU `chown` was written by David MacKenzie and Jim Meyering.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`chown(1)`, `chgrp(1)`, `chmod(1)`, `id(1)`
