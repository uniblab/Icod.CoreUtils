# WHOAMI(1)

## NAME

**whoami** — print the effective user name

## SYNOPSIS

```text
whoami [OPTION]...
```

## DESCRIPTION

`Icod.CoreUtils.WhoAmI` is a managed .NET implementation of GNU Coreutils `whoami(1)`, modeled on GNU Coreutils 9.11.

The command queries the shared identity provider and prints the name associated with the current effective user identity.

No operands are accepted.

## OPTIONS

```text
--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    The effective user identity was obtained and written successfully.
1    Usage or identity lookup failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Effective-user lookup is provided by the cross-platform identity abstraction. On Unix-like systems this corresponds to the effective user identity; on Windows it follows the effective identity model exposed by the provider.

## PATHNAME GLOBBING

`whoami` does not perform `Icod.CommandFramework` pathname glob expansion. It has no pathname operands eligible for expansion; it reports the effective user identity instead. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `whoami` was written by Richard Mlynarik.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`whoami(1)`, `id(1)`, `logname(1)`, `groups(1)`
