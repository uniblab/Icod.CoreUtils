# SLEEP(1)

## NAME

**sleep** — delay for a specified amount of time

## SYNOPSIS

```text
sleep NUMBER[SUFFIX]...
sleep OPTION
```

## DESCRIPTION

`Icod.CoreUtils.Sleep` is a managed .NET implementation of GNU Coreutils `sleep(1)`, modeled on GNU Coreutils 9.11.

Each operand specifies a non-negative duration. Multiple durations are added together and the command waits for the total.

`NUMBER` may be fractional or use exponent notation. The implementation also accepts positive `inf` or `infinity` for an indefinite wait that ends only through cancellation.

## SUFFIXES

```text
s    seconds; this is the default
m    minutes
h    hours
d    days
```

## OPTIONS

```text
--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    The requested interval elapsed.
1    Usage, duration parsing, overflow, or an output operation failed.
130  The wait was cancelled.
```

## PLATFORM NOTES

Waiting is implemented asynchronously with .NET timers and cancellation support. Very long finite waits are divided into bounded delay chunks rather than relying on a single platform timer interval.

## PATHNAME GLOBBING

`sleep` does not perform `Icod.CommandFramework` pathname glob expansion. Its operands are durations rather than filesystem pathnames, so `*`, `?`, and `**` are not interpreted as pathname patterns by `sleep`. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `sleep` was written by Jim Meyering and Paul Eggert.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`sleep(1)`, `timeout(1)`
