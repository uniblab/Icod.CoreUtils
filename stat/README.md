# STAT(1)

## NAME

**stat** — display file or filesystem status

## SYNOPSIS

```text
stat [OPTION]... FILE...
```

## DESCRIPTION

`Icod.CoreUtils.Stat` is a managed .NET implementation of GNU Coreutils `stat(1)`, modeled on GNU Coreutils 9.11.

By default the command reports metadata for each named file. Filesystem mode reports information about the filesystem containing each operand instead.

Default, terse, `--format`, and `--printf` presentations share one formatting engine over the authoritative filesystem metadata provider. A file report remains available when optional containing-filesystem details cannot be obtained.

## OPTIONS

```text
-L, --dereference
    Follow pathname indirection when observing file metadata.

-f, --file-system
    Report filesystem status instead of file status.

-c, --format=FORMAT
    Use FORMAT and append a newline after each operand.

--printf=FORMAT
    Use FORMAT, interpret backslash escapes, and do not append an implicit
    newline.

-t, --terse
    Use the terse built-in format.

--cached=MODE
    Select attribute-cache behavior: always, never, or default.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

The current system metadata provider accepts `--cached=default`. Explicit `always` and `never` modes are diagnosed as unsupported because the provider does not expose cache-control semantics.

## EXIT STATUS

```text
0    Every requested operand was reported successfully.
1    Usage, formatting, metadata, or filesystem observation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

File and filesystem data come from `IFileSystemMetadataProvider`, allowing the same command and formatting engine to operate over the platform-specific metadata implementations used on Windows, Linux, and macOS. Unsupported metadata fields are represented through the provider's availability contracts rather than fabricated.

## AUTHORS

GNU `stat` was written by Michael Meskes.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`stat(1)`, `df(1)`, `du(1)`, `ls(1)`
