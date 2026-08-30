# DIRNAME(1)

## NAME

**dirname** — strip the last component from pathnames

## SYNOPSIS

```text
dirname [OPTION] NAME...
```

## PATHNAME GLOBBING

`dirname` is a Class C utility and performs no in-process pathname globbing. Each `NAME` is pathname-shaped lexical text and need not identify an existing filesystem object, so wildcard characters remain literal when they reach the command unexpanded. An invoking shell may still expand an unquoted pattern before `dirname` starts.

## DESCRIPTION

`Icod.CoreUtils.DirName` is a managed .NET implementation of GNU Coreutils `dirname(1)`, modeled on GNU Coreutils 9.11.

For each `NAME`, the command removes trailing slashes and the final non-slash pathname component, then prints the remaining directory portion. A name without a directory component produces `.`. Root pathnames are preserved.

The operation is purely lexical and does not require the named path to exist.

## OPTIONS

```text
-z, --zero
    End each output with NUL instead of a newline.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    Every requested pathname was processed successfully.
1    Command-line usage was invalid.
130  The operation was cancelled.
```

## PLATFORM NOTES

The pathname reduction follows GNU `/` separator semantics without consulting the filesystem. This is intentionally independent of the host's native separator rules.

## AUTHORS

GNU `dirname` was written by David MacKenzie and Jim Meyering.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`dirname(1)`, `basename(1)`, `realpath(1)`
