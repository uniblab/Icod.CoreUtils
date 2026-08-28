# HOSTID(1)

## NAME

**hostid** — print the numeric identifier for the current host

## SYNOPSIS

```text
hostid [OPTION]
```

## DESCRIPTION

`Icod.CoreUtils.HostId` is a managed .NET implementation of GNU Coreutils `hostid(1)`, modeled on GNU Coreutils 9.11.

The command obtains the current host identifier from the shared host-resource provider and prints its hexadecimal representation.

No non-option operands are accepted.

## OPTIONS

```text
--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    A host identifier was obtained and written successfully.
1    Usage was invalid, the identifier was unavailable, the operation was
     cancelled, or another provider/output failure occurred.
```

## PLATFORM NOTES

The implementation deliberately uses the cross-platform host identifier provider rather than assuming a Unix `/etc/hostid` storage mechanism. The meaning and availability of the returned identifier therefore follow the provider for the current operating system.
## AUTHORS

GNU `hostid` was written by Jim Meyering.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`hostid(1)`, `uname(1)`, `hostname(1)`
