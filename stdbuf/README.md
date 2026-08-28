# STDBUF(1)

## NAME

**stdbuf** — run a command with modified standard-stream buffering

## SYNOPSIS

```text
stdbuf OPTION... COMMAND [ARG]...
```

## DESCRIPTION

`Icod.CoreUtils.StdBuf` is a managed .NET implementation of GNU Coreutils `stdbuf(1)`, modeled on GNU Coreutils 9.11.

The command selects buffering modes for a child's standard input, output, and/or error streams, sets the `_STDBUF_I`, `_STDBUF_O`, and `_STDBUF_E` environment variables, and arranges for the platform's buffering-control helper to be preloaded into the child.

At least one buffering mode must be supplied.

## OPTIONS

```text
-i, --input=MODE
    Adjust standard-input buffering.

-o, --output=MODE
    Adjust standard-output buffering.

-e, --error=MODE
    Adjust standard-error buffering.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

`MODE` may be:

```text
L       line buffered; invalid for standard input
0       unbuffered
SIZE    a byte count
```

Size suffixes include binary powers such as `K`, `M`, and `G`; decimal powers such as `KB`, `MB`, and `GB`; and IEC spellings such as `KiB`, `MiB`, and `GiB`, continuing through the larger supported prefixes.

## EXIT STATUS

```text
125    stdbuf itself failed or buffering control is unsupported.
126    COMMAND was found but could not be invoked.
127    COMMAND could not be found.
other  The exit status translated from COMMAND.
```

## PLATFORM NOTES

Active buffering control is available only on supported Linux ELF targets in the current implementation. Other platforms report that standard-stream buffering control is unsupported rather than pretending to apply the requested modes.

The child retains native standard-handle inheritance; `stdbuf` does not interpose managed pipes that would defeat the buffering behavior it is trying to control.

## AUTHORS

GNU `stdbuf` was written by Pádraig Brady.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`stdbuf(1)`, `env(1)`
