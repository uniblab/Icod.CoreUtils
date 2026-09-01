# UNAME(1)

## NAME

**uname** — print system information

## SYNOPSIS

```text
uname [OPTION]...
```

## DESCRIPTION

`Icod.CoreUtils.UName` is a managed .NET implementation of GNU Coreutils `uname(1)`, modeled on GNU Coreutils 9.11.

With no information-selection option, the command prints the kernel name. Selected fields are written in GNU order separated by spaces.

System information comes from the shared platform-information provider rather than from a single Unix system call.

## OPTIONS

```text
-a, --all
    Print all available information, omitting processor and hardware-platform
    fields when those fields are reported as unknown.

-s, --kernel-name
    Print the kernel name.

-n, --nodename
    Print the network node hostname.

-r, --kernel-release
    Print the kernel release.

-v, --kernel-version
    Print the kernel version.

-m, --machine
    Print the machine hardware name.

-p, --processor
    Print the processor type.

-i, --hardware-platform
    Print the hardware platform.

-o, --operating-system
    Print the operating system.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    The requested system information was written successfully.
1    Usage or platform-information retrieval failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

The platform provider normalizes native Windows, Linux, and macOS information into the fields expected by `uname`. Some non-portable fields can legitimately be `unknown`; explicit `-p` or `-i` requests print that value, while `--all` omits unknown values for those two fields.

## PATHNAME GLOBBING

`uname` does not perform `Icod.CommandFramework` pathname glob expansion. Its arguments select system-information fields rather than identify filesystem pathnames, so `*`, `?`, and `**` are not interpreted as pathname patterns by `uname`. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `uname` was written by David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`uname(1)`, `hostid(1)`, `nproc(1)`
