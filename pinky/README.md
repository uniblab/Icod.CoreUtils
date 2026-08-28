# PINKY(1)

## NAME

**pinky** — print a lightweight report about users and login sessions

## SYNOPSIS

```text
pinky [OPTION]... [USER]...
```

## DESCRIPTION

`Icod.CoreUtils.Pinky` is a managed .NET implementation of GNU Coreutils `pinky(1)`, modeled on GNU Coreutils 9.11.

Short format is the default. It reports matching login sessions with fields for login name, full name, terminal, idle time, login time, and remote host according to the selected suppression options.

Long format reports account information for explicitly named users and can include the user's home directory, shell, `.project`, and `.plan` files. At least one username is required in long format.

If both long- and short-format selectors are present, the last one on the command line determines the active format.

## OPTIONS

```text
-l, --long-format
    Produce long-format account output.

-b
    Omit home directory and shell in long format.

-h
    Omit the `.project` file in long format.

-p
    Omit the `.plan` file in long format.

-s, --short-format
    Produce short-format session output.

-f
    Omit column headings in short format.

-w
    Omit the user's full name in short format.

-i
    Omit the user's full name and remote host in short format.

-q
    Omit the user's full name, remote host, and idle time in short format.

--lookup
    Attempt to canonicalize displayed remote host names through DNS.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    The requested report was produced successfully.
1    Usage was invalid or one or more explicitly requested long-format users
     could not be resolved.
130  The operation was cancelled.
```

Unreadable `.project` or `.plan` files produce warnings but do not by themselves make the command fail.

## PLATFORM NOTES

The system user-information provider reads `/etc/passwd` on Linux and macOS when available, falling back to the current runtime user when a system account database cannot be read.

Login-session enumeration currently reads Linux `utmp` records. On interactive Windows hosts, the provider supplies a synthetic console session for the current user. Other hosts without a supported session source can therefore produce an empty short report even though long-format account lookup remains available.

Idle time is derived from terminal access time when a `/dev` terminal is available.

## AUTHORS

GNU `pinky` was written by Joseph Arceneaux, David MacKenzie, and Kaveh Ghazi.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`pinky(1)`, `who(1)`, `users(1)`, `whoami(1)`
