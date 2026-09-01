# TTY(1)

## NAME

**tty** — print the terminal name connected to standard input

## SYNOPSIS

```text
tty [OPTION]...
```

## DESCRIPTION

`Icod.CoreUtils.Tty` is a managed .NET implementation of GNU Coreutils `tty(1)`, modeled on GNU Coreutils 9.11.

The command asks the shared terminal-control provider to observe standard input. If standard input is attached to a terminal and a pathname is available, that pathname is printed. A definite non-terminal result prints `not a tty` unless silent mode is selected.

## OPTIONS

```text
-s, --silent, --quiet
    Print nothing and report the terminal state through the exit status only.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

This implementation deliberately distinguishes several failure classes:

```text
0    Standard input is a terminal and its terminal pathname is available.
1    Standard input is definitively not a terminal.
2    Command-line usage was invalid.
3    Required output could not be written.
4    Terminal state or terminal pathname could not be determined, including
     cancellation or provider unavailability.
```

## PLATFORM NOTES

Terminal discovery is provider-backed through `Icod.CommandFramework.Terminal`. It does not assume a Unix `/dev/tty` namespace and can therefore represent terminal identity on Windows as well as Unix-like systems. A platform may still be unable to provide a pathname even when an interactive terminal is present.

## PATHNAME GLOBBING

`tty` does not perform `Icod.CommandFramework` pathname glob expansion. It has no pathname operands eligible for expansion; it reports the terminal attached to standard input instead. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `tty` was written by David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`tty(1)`, `stty(1)`
