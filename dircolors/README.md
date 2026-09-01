# DIRCOLORS(1)

## NAME

**dircolors** — generate shell commands for the `LS_COLORS` environment variable

## SYNOPSIS

```text
dircolors [OPTION]... [FILE]
```

## PATHNAME GLOBBING

The optional database `FILE` is a Class B singular pathname slot. A wildcard-bearing operand must resolve to at most one pathname; an unmatched pattern remains literal and multiple matches are rejected. `-` remains the standard-input database sentinel. Option values are not pathname-expanded.

## DESCRIPTION

`Icod.CoreUtils.DirColors` is a managed .NET implementation of GNU Coreutils `dircolors(1)`, modeled on GNU Coreutils 9.11.

The command parses a GNU-style color database, evaluates its `TERM` and `COLORTERM` selectors, compiles file-type and filename-pattern rules into `LS_COLORS`, and emits shell commands that install the resulting value.

Without `FILE`, the built-in database is used. A `FILE` operand of `-` reads the database from standard input.

## OPTIONS

```text
-b, --sh, --bourne-shell
    Emit Bourne-compatible shell commands.

-c, --csh, --c-shell
    Emit C-shell-compatible commands.

-p, --print-database
    Print the built-in color database.

--print-ls-colors
    Display the compiled color rules in an escaped, visually inspectable form.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

When neither `-b` nor `-c` is supplied, shell syntax is inferred from `SHELL`: `csh` and `tcsh` select C-shell syntax and other recognized shell paths select Bourne syntax. If no shell can be inferred, the command reports an error rather than guessing.

## EXIT STATUS

```text
0    The requested database or shell commands were produced successfully.
1    Usage, database parsing, terminal selection, or shell inference failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

The database parser, selector matching, `LS_COLORS` serializer, and shell quoting are fully managed. The built-in database includes common terminal selectors and file-type/extension rules, so normal use does not depend on a host-installed GNU `dircolors` database.

## AUTHORS

GNU `dircolors` was written by H. Peter Anvin.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`dircolors(1)`, `ls(1)`, `dir(1)`, `vdir(1)`
