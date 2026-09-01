# MKDIR(1)

## NAME

**mkdir** — create directories

## SYNOPSIS

```text
mkdir [OPTION]... DIRECTORY...
```

## DESCRIPTION

`Icod.CoreUtils.MkDir` is a managed .NET implementation of GNU Coreutils `mkdir(1)`, modeled on GNU Coreutils 9.11.

The command creates each requested directory through the shared filesystem mutation provider. `--parents` creates missing ancestor directories and treats already existing directory components as successful.

Without an explicit mode, the requested directory mode is `0777` filtered by the current creation mask. An explicit symbolic or octal mode is evaluated by the shared file-mode engine.

## OPTIONS

```text
-m, --mode=MODE
    Set the final directory mode using chmod-style syntax.

-p, --parents
    Create missing parent directories and do not fail for existing directory
    components.

-v, --verbose
    Report every directory actually created.

-Z
    Request the default SELinux security context.

--context[=CTX]
    Request a specific security context.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

Security-context labeling is not currently exposed by the mutation contract. `--context=CTX` is accepted with a warning that the request is ignored; `-Z` is retained for compatibility.

## EXIT STATUS

```text
0    All requested directories were created or already satisfied --parents.
1    Usage, mode parsing, path observation, or directory creation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Directory creation and mode application use the shared mutation and creation-mask providers. Parent creation follows host pathname semantics through `System.IO.Path`.

POSIX mode fidelity depends on platform/provider capability. Security-context labeling is not currently implemented.

## PATHNAME GLOBBING

`mkdir` does not perform `Icod.CommandFramework` pathname glob expansion. Its `DIRECTORY` operands name directories to create rather than select existing filesystem entries, so wildcard characters that reach `mkdir` remain part of the requested pathname subject to host pathname rules. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `mkdir` was written by David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`mkdir(1)`, `rmdir(1)`, `chmod(1)`
