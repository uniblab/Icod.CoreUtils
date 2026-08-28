# NPROC(1)

## NAME

**nproc** — print the number of available processing units

## SYNOPSIS

```text
nproc [OPTION]...
```

## DESCRIPTION

`Icod.CoreUtils.NProc` is a managed .NET implementation of GNU Coreutils `nproc(1)`, modeled on GNU Coreutils 9.11.

Without `--all`, the command prints the processing-unit count available to the current process, which can be smaller than the installed processor count. The calculation is performed by the shared processor-resource provider and `nproc` policy, including supported environment constraints.

## OPTIONS

```text
--all
    Print the number of installed processors rather than the count available
    to the current process.

--ignore=N
    If possible, subtract N processing units from the selected count.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

The result remains at least one processing unit when a usable processor count can be determined.

## EXIT STATUS

```text
0    The processor count was determined and written successfully.
1    Usage was invalid, processor information was unavailable, the operation was
     cancelled, or another provider/output failure occurred.
```

## PLATFORM NOTES

Processor discovery is provider-backed rather than tied to `/proc` or another single operating-system interface. The reported available count can reflect process affinity, runtime restrictions, container limits, or supported OpenMP-style environment policy.

## AUTHORS

GNU `nproc` was written by Giuseppe Scrivano.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`nproc(1)`, `uname(1)`
