# USERS(1)

## NAME

**users** — print the users currently logged in

## SYNOPSIS

```text
users [OPTION]... [FILE]
```

## PATHNAME GLOBBING

The optional accounting-database `FILE` is a Class B singular pathname slot. A wildcard-bearing operand must resolve to at most one pathname; an unmatched pattern remains literal and multiple matches are rejected.

## DESCRIPTION

`Icod.CoreUtils.Users` is a managed .NET implementation of GNU Coreutils `users(1)`, modeled on GNU Coreutils 9.11.

The command reads login records, selects user-process records with non-empty usernames, sorts those names ordinally, and prints them on one space-separated line.

If `FILE` is omitted, the system login database exposed by the login-record provider is used. At most one file operand is accepted.

## OPTIONS

```text
--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    Login records were read and the user list was written successfully.
1    Usage was invalid, login records are unsupported, or an operational error occurred.
130  The operation was cancelled.
```

## PLATFORM NOTES

Login-record enumeration depends on the shared login-record provider. Platforms without a supported login-record facility return a diagnostic and failure rather than synthesizing a user list from unrelated process identity information.

## AUTHORS

GNU `users` was written by Joseph Arceneaux and David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`users(1)`, `who(1)`, `whoami(1)`, `logname(1)`
