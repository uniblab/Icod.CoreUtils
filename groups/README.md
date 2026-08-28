# GROUPS(1)

## NAME

**groups** — print group memberships

## SYNOPSIS

```text
groups [OPTION]... [USERNAME]...
```

## DESCRIPTION

`Icod.CoreUtils.Groups` is a managed .NET implementation of GNU Coreutils `groups(1)`, modeled on GNU Coreutils 9.11.

With no username operands, the command prints the current process user's real primary group followed by the supplementary groups reported by the identity provider. With usernames, one line is produced for each resolvable user.

Duplicate group identities are removed before output. A nonexistent requested user is diagnosed and makes the final status unsuccessful without preventing later usernames from being examined.

## OPTIONS

```text
--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    All requested identities were reported successfully.
1    Usage was invalid, an identity lookup failed, or an operational error occurred.
130  The operation was cancelled.
```

## PLATFORM NOTES

Identity and group discovery is supplied by `Icod.CommandFramework`'s system identity provider rather than by direct parsing of Unix account files. Results therefore follow the capabilities and identity model exposed for the current platform.
## AUTHORS

GNU `groups` was written by David MacKenzie and James Youngman.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`groups(1)`, `id(1)`, `whoami(1)`, `logname(1)`
