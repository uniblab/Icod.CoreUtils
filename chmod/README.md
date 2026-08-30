# CHMOD(1)

## NAME

**chmod** — change file mode bits

## SYNOPSIS

```text
chmod [OPTION]... MODE[,MODE]... FILE...
chmod [OPTION]... OCTAL-MODE FILE...
chmod [OPTION]... --reference=RFILE FILE...
```

## PATHNAME GLOBBING

Target `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

MODE/OCTAL-MODE operands and `--reference=RFILE` remain literal control values. A `**` pattern selects initial targets only; recursive mode changes remain controlled by `-R` and the command's link-traversal options.

## DESCRIPTION

`Icod.CoreUtils.ChMod` is a managed .NET implementation of GNU Coreutils `chmod(1)`, modeled on GNU Coreutils 9.11.

The command applies symbolic or octal mode expressions through the shared mode parser and filesystem mutation provider. Symbolic expressions honor the current creation mask where GNU semantics require it; `--reference` copies the permission mode from another file.

Recursive mode uses the shared race-aware traversal engine, with explicit policies for symbolic-link traversal, dereferencing, and root preservation.

## OPTIONS

```text
-c, --changes
    Report only entries whose mode changes.

-f, --silent, --quiet
    Suppress most error diagnostics.

-v, --verbose
    Report every entry processed.

--dereference
    Affect the referent of each symbolic link.

-h, --no-dereference
    Affect symbolic links themselves rather than their referents when supported.

--no-preserve-root
    Do not treat `/` specially during recursive processing. This is the default.

--preserve-root
    Refuse to operate recursively on `/`.

--reference=RFILE
    Use RFILE's mode instead of a MODE operand.

-R, --recursive
    Change files and directories recursively.

-H
    With -R, traverse command-line directory symbolic links.

-L
    With -R, traverse every directory symbolic link encountered.

-P
    With -R, do not traverse directory symbolic links.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

With `-R`, `--dereference` requires `-H` or `-L`. When recursive traversal is `-P`, the command uses no-follow mutation semantics.

## EXIT STATUS

```text
0    Every requested mode operation completed successfully.
1    Usage, mode parsing, traversal, metadata, or mutation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Mode observation and mutation are supplied by the shared filesystem metadata/mutation providers. Exact POSIX permission capabilities therefore depend on the current platform and filesystem.

Recursive operations retain observed entry identities as mutation preconditions where possible, reducing the opportunity for pathname replacement races between traversal and mutation.

## AUTHORS

GNU `chmod` was written by David MacKenzie and Jim Meyering.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`chmod(1)`, `chown(1)`, `chgrp(1)`, `mkdir(1)`
