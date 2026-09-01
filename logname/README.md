# LOGNAME(1)

## NAME

**logname** — print the user's login name

## SYNOPSIS

```text
logname [OPTION]...
```

## DESCRIPTION

`Icod.CoreUtils.LogName` is a managed .NET implementation of GNU Coreutils `logname(1)`, modeled on GNU Coreutils 9.11.

The command asks the shared identity provider for the login name associated with the current login session and prints that name.

Unlike `whoami`, `logname` describes the login identity rather than the effective user identity. No operands are accepted.

## OPTIONS

```text
--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    A login name was obtained and written successfully.
1    Usage was invalid, no login name was available, or an operational error occurred.
130  The operation was cancelled.
```

## PLATFORM NOTES

Login-name discovery is supplied by the cross-platform identity provider. Some execution environments, service accounts, containers, or detached sessions may not have a meaningful login name even though an effective process identity exists.

## PATHNAME GLOBBING

`logname` does not perform `Icod.CommandFramework` pathname glob expansion. It has no pathname operands eligible for command-owned expansion, so `*`, `?`, and `**` are not interpreted as pathname patterns by `logname`. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `logname` was written by David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`logname(1)`, `whoami(1)`, `id(1)`, `groups(1)`
