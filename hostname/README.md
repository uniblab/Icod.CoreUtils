# HOSTNAME(1)

## NAME

**hostname** — print or set the active host name

## SYNOPSIS

```text
hostname [NAME]
hostname OPTION
```

## DESCRIPTION

`Icod.CoreUtils.HostName` is a managed .NET implementation of GNU Coreutils `hostname(1)`, modeled on GNU Coreutils 9.11.

With no operand, the command prints the active host name returned by the system DNS host-name API. With one `NAME` operand, it attempts to change the active host name.

More than one operand is rejected.

## OPTIONS

```text
--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    The host name was queried or changed successfully.
1    Usage was invalid, the host name could not be determined, the requested
     mutation was unsupported or denied, or another operational error occurred.
130  The operation was cancelled.
```

## PLATFORM NOTES

Host-name queries use `Dns.GetHostName()` and are available wherever that runtime API is supported.

Host-name mutation is implemented with the native `sethostname` ABI on Linux, macOS, and FreeBSD. It normally requires operating-system privileges. Mutation is deliberately reported as unsupported on other hosts, including Windows, rather than silently changing a different machine-name setting.

## PATHNAME GLOBBING

`hostname` does not perform `Icod.CommandFramework` pathname glob expansion. Host-name operands are host-name text rather than filesystem pathnames, so `*`, `?`, and `**` are not interpreted as pathname patterns by `hostname`. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `hostname` was written by Jim Meyering.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`hostname(1)`, `uname(1)`, `hostid(1)`
