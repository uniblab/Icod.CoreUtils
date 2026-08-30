# PWD(1)

## NAME

**pwd** — print the current working directory

## SYNOPSIS

```text
pwd [OPTION]...
```

## DESCRIPTION

`Icod.CoreUtils.Pwd` is a managed .NET implementation of GNU Coreutils `pwd(1)`, modeled on GNU Coreutils 9.11.

The command prints either the logical or the physically resolved current working directory. Physical resolution walks pathname components and resolves symbolic-link targets without changing the process working directory.

Logical mode uses `PWD` only when it is rooted, contains no `.` or `..` components, and resolves to the same physical directory as the process's actual current directory.

## OPTIONS

```text
-L, --logical
    Use the validated PWD environment value even when it contains symbolic links.

-P, --physical
    Resolve symbolic links and print the physical pathname.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

If both `-L` and `-P` occur, the last one controls the selected mode. The implementation starts in logical mode when `POSIXLY_CORRECT` is set and physical mode otherwise.

## EXIT STATUS

```text
0    The current working directory was resolved and written successfully.
1    Usage or pathname resolution failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Physical resolution uses .NET filesystem APIs and recognizes the host's directory separators. On Windows this includes Windows pathname and link-resolution behavior; comparisons used to validate logical `PWD` are case-insensitive there and ordinal on other platforms.

## PATHNAME GLOBBING

`pwd` does not perform `Icod.CommandFramework` pathname glob expansion. It has no pathname operands eligible for expansion; it reports the current working directory instead. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `pwd` was written by Jim Meyering.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`pwd(1)`, `realpath(1)`, `readlink(1)`
