# CHCON(1)

## NAME

**chcon** — change SELinux security context of files

## SYNOPSIS

```text
chcon [OPTION]... CONTEXT FILE...
chcon [OPTION]... [-u USER] [-r ROLE] [-l RANGE] [-t TYPE] FILE...
chcon [OPTION]... --reference=RFILE FILE...
```

## PATHNAME GLOBBING

Target `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

The complete CONTEXT operand, component values supplied by `-u`, `-r`, `-t`, or `-l`, and `--reference=RFILE` remain literal. A `**` pattern selects initial targets only; recursive context changes remain controlled by `-R` and the command's link-traversal options.

## DESCRIPTION

`Icod.CoreUtils.ChCon` implements the GNU Coreutils 9.11 `chcon` command for changing SELinux file security contexts. A complete context may be supplied directly, selected from a reference file, or assembled by changing individual user, role, type, or range components.

The command uses an injectable SELinux platform provider, validates complete or reconstructed contexts before applying them, and reports unsupported or disabled SELinux facilities as controlled failures rather than silently succeeding.

## OPTIONS

```text
      --dereference
    Affect the referent of each symbolic link.

-h, --no-dereference
    Affect symbolic links instead of referenced files.

      --reference=RFILE
    Use RFILE's security context.

-u, --user=USER
    Set USER in the target security context.

-r, --role=ROLE
    Set ROLE in the target security context.

-t, --type=TYPE
    Set TYPE in the target security context.

-l, --range=RANGE
    Set RANGE in the target security context.

-R, --recursive
    Operate on files and directories recursively.

-H
    Follow command-line symbolic links to directories during recursion.

-L
    Follow every symbolic link to a directory during recursion.

-P
    Do not traverse symbolic links during recursion; this is the default.

      --preserve-root
    Refuse a recursive operation on '/'.

      --no-preserve-root
    Do not treat '/' specially; this is the default.

-v, --verbose
    Report each file whose context is changed.

-f
    Accepted as a GNU compatibility no-op.

      --help
    Display command help and exit.

      --version
    Display version information and exit.
```

Recursive traversal preserves the GNU distinction among `-H`, `-L`, and `-P`. Context changes may either dereference or operate on symbolic links according to the selected dereference policy. `--reference` cannot be combined with component options such as `--user` or `--type`.

## EXIT STATUS

```text
0  All requested context changes succeeded.
1  Usage, SELinux availability, traversal, validation, context access, or context update failed.
```

Cancellation is reported as failure because the current command contract uses status `1` for an interrupted operation.

## PLATFORM NOTES

`chcon` requires an SELinux-capable host and appropriate permissions to read, validate, and change security contexts. Hosts without an available SELinux provider receive an explicit diagnostic.

## AUTHORS

GNU `chcon` was written by Russell Coker and Jim Meyering.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`chcon(1)`, `runcon(1)`
