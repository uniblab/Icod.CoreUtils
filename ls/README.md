# LS(1)

## NAME

**ls** — list directory contents and file information

## SYNOPSIS

```text
ls [OPTION]... [FILE]...
```

## DESCRIPTION

`Icod.CoreUtils.Ls` is a managed .NET implementation of GNU Coreutils `ls(1)`, modeled on GNU Coreutils 9.11.

With no operands, `ls` lists the current directory. Its default layout is terminal-sensitive: attached terminal output uses width-aware vertical columns, while redirected output defaults to one entry per line.
The executable is a thin profile over the shared managed directory-listing engine. That engine owns filesystem metadata observation, ordering, filename quoting, `LS_COLORS` application, terminal-width-aware layout, long-format rendering, classification indicators, and recursive traversal with stable-identity cycle protection.

## PRINCIPAL OPTIONS

```text
-a, --all
    Include entries beginning with `.`.

-A, --almost-all
    Include hidden entries but omit implied `.` and `..`.

-B, --ignore-backups
    Omit names ending in `~`.

-d, --directory
    List directory operands themselves rather than their contents.

-l
    Use long listing format.

-h, --human-readable
    Use human-readable powers of 1024 for sizes.

--si
    Use powers of 1000 for human-readable sizes.

-i, --inode
    Show inode or provider identity numbers when available.

-s, --size
    Show allocated-size block counts.

-R, --recursive
    Recurse into subdirectories.

-r, --reverse
    Reverse the selected sort order.

-S
    Sort by logical size.

-t
    Sort by the selected timestamp.

-X
    Sort by extension.

-v
    Use natural version ordering.

-1, -C, -x, -m
    Select single-column, vertical-column, horizontal-column, or comma layout.

-F, --classify
    Append file classification indicators.

--color[=WHEN]
    Colorize names; WHEN is always, auto, or never.

--quoting-style=WORD
    Select GNU-style filename quoting.

-H, -L, -P
    Select command-line, all, or no pathname dereference.

--dereference-command-line-symlink-to-dir
    Follow command-line links that resolve to directories.

--time=WORD
    Select modification, access, change, or birth time.

--time-style=STYLE
    Select locale, iso, long-iso, full-iso, or +FORMAT presentation.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

Additional shared options cover ignore/hide patterns, explicit output width and tab size, block-size policy, numeric IDs, long-format owner/group fields, directory-first grouping, indicator styles, quoting/control-character policies, and the shared format/sort vocabulary.

## EXIT STATUS

```text
0    All requested entries were listed successfully.
1    One or more filesystem observations or recursive listings failed.
2    Command-line usage was invalid.
```

## PLATFORM NOTES

Terminal attachment, terminal width, color capability, environment capture, filename quoting, and control-character presentation are supplied by shared abstractions. `TIME_STYLE`, `QUOTING_STYLE`, and `LS_COLORS` are honored by the shared engine where applicable.

Recursive traversal uses stable filesystem identities to diagnose directory loops instead of depending only on textual path comparison.

## AUTHORS

GNU `ls` was written by Richard M. Stallman and David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`ls(1)`, `dir(1)`, `vdir(1)`, `dircolors(1)`, `stat(1)`
