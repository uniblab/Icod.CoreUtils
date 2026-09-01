# WHO(1)

## NAME

**who** — show login-accounting information

## SYNOPSIS

```text
who [OPTION]... [FILE]
who [OPTION]... ARG1 ARG2
```

## PATHNAME GLOBBING

Only the one-operand `[FILE]` form has a Class B singular pathname slot. A wildcard-bearing accounting filename must resolve to at most one pathname; an unmatched pattern remains literal and multiple matches are rejected. The traditional two-operand `ARG1 ARG2` form, such as `who am i`, is control syntax and is never pathname-expanded.

## DESCRIPTION

`Icod.CoreUtils.Who` is a managed .NET implementation of GNU Coreutils `who(1)`, modeled on GNU Coreutils 9.11.

By default the command prints active user-process records from the system login-accounting database. A single `FILE` operand selects an alternate accounting file.

The traditional two-operand form, commonly written `who am i`, restricts output to the terminal attached to standard input; the literal values of the two operands are not otherwise interpreted.

The command can also select boot, login, init, dead-process, run-level, clock-change, and active-user records, print headings, add idle or message status, count logged-in users, and canonicalize remote host names.

## OPTIONS

```text
-a, --all
    Enable the boot, dead, login, process, runlevel, time, message-status, and
    users selections.

-b, --boot
    Print the time of the last system boot.

-d, --dead
    Print dead-process records.

-H, --heading
    Print column headings.

-l, --login
    Print system login-process records.

--lookup
    Canonicalize remote host names through DNS when possible.

-m
    Restrict output to the user and terminal associated with standard input.

-p, --process
    Print active processes spawned by init.

-q, --count
    Print login names followed by the number of logged-in users.

-r, --runlevel
    Print run-level records.

-s, --short
    Use the default short user display.

-t, --time
    Print system clock-change records.

-T, -w, --mesg, --message, --writable
    Add the terminal message status as `+`, `-`, or `?`.

-u, --users
    Print logged-in users including idle time.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    The requested accounting records were read and written successfully.
1    Usage was invalid, login accounting is unsupported, or the selected file
     or records could not be read.
130  The operation was cancelled.
```

## PLATFORM NOTES

The current system login-record provider implements the Linux `utmp` record layout and reports itself supported only on Linux. Its default database search checks `/var/run/utmp` and `/run/utmp`.

Accordingly, the production `who` command currently reports login records as unsupported on Windows and macOS rather than constructing approximations from process or environment information. Terminal message status and idle-time details use `/dev` terminal metadata when available.

## AUTHORS

GNU `who` was written by Joseph Arceneaux, David MacKenzie, and Michael Stone.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`who(1)`, `users(1)`, `pinky(1)`, `whoami(1)`
