# PATHCHK(1)

## NAME

**pathchk** — check whether pathnames are valid or portable

## SYNOPSIS

```text
pathchk [OPTION]... NAME...
```

## PATHNAME GLOBBING

`pathchk` is a Class C utility and performs no in-process pathname globbing. Each `NAME` is validated exactly as supplied to the command; expanding wildcard-bearing text first could change the pathname spelling that `pathchk` is intended to examine. An invoking shell may still expand an unquoted pattern before `pathchk` starts.

## DESCRIPTION

`Icod.CoreUtils.PathChk` is a managed .NET implementation of GNU Coreutils `pathchk(1)`, modeled on GNU Coreutils 9.11.

The command validates pathname spelling and length without creating or modifying filesystem objects. It can check either host-oriented limits or the stricter portable filename rules selected by `-p` and `--portability`.

## OPTIONS

```text
-p, --posix
    Check against the portable limits used by this implementation for broadly
    POSIX-compatible pathnames.

-P, --leading-hyphen
    Diagnose empty names and pathname components beginning with `-`.

--portability
    Apply both the -p and -P checks.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

Portable checking permits letters, digits, `.`, `_`, `-`, and `/`; checks the pathname's UTF-8 byte length against 255 bytes; and checks each component against 14 bytes.

Without portable mode, host-oriented checks use the .NET pathname parser, reject embedded NUL, limit components to 255 UTF-8 bytes, and use a pathname limit of 32767 bytes on Windows or 4095 bytes on other supported hosts.

## EXIT STATUS

```text
0    Every pathname satisfied the requested checks.
1    At least one pathname failed validation or usage was invalid.
130  The operation was cancelled.
```

## NOTES

The validation is lexical and limit-oriented. Successful `pathchk` output does not imply that the pathname currently exists, is writable, or can necessarily be created under every filesystem mounted on the host.

## AUTHORS

GNU `pathchk` was written by Paul Eggert, David MacKenzie, and Jim Meyering.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`pathchk(1)`, `realpath(1)`, `readlink(1)`
