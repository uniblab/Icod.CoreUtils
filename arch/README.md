# ARCH(1)

## NAME

**arch** — print the machine architecture

## SYNOPSIS

```text
arch [OPTION]...
```

## DESCRIPTION

`Icod.CoreUtils.Arch` is a managed .NET implementation of GNU Coreutils `arch(1)`, modeled on GNU Coreutils 9.11.

The command prints the architecture reported for the current operating-system environment using GNU-style spellings where a normalization is required.

Known mappings include `x86_64`, `i686`, `aarch64`, `armv7l`, `armv6l`, `ppc64le`, `s390x`, `loongarch64`, `riscv64`, and `wasm`. Other runtime architecture names are written in lowercase.

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
0    The architecture name was written successfully.
1    Command-line usage or output processing failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Architecture discovery uses `.NET` `RuntimeInformation.OSArchitecture`; it does not invoke an external `uname` or inspect `/proc`. The normalization layer gives the common architectures GNU-compatible spellings across supported hosts.

## PATHNAME GLOBBING

`arch` does not perform `Icod.CommandFramework` pathname glob expansion. It has no pathname operands eligible for command-owned expansion, so `*`, `?`, and `**` are not interpreted as pathname patterns by `arch`. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `arch` was written by David MacKenzie and Karel Zak.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`arch(1)`, `uname(1)`
