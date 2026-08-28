# PR(1)

## NAME

**pr** — paginate or columnate files for printing

## SYNOPSIS

```text
pr [OPTION]... [FILE]...
```

## DESCRIPTION

`Icod.CoreUtils.Pr` is a managed .NET implementation of GNU Coreutils `pr(1)`, modeled on GNU Coreutils 9.11.

The command formats text into pages and columns, optionally merges files in parallel, numbers lines, expands or emits tabs, selects page ranges, and controls headers, margins, separators, and page dimensions. Defaults are 66 lines per page, 72 columns of page width, and tab stops every 8 columns.

## PRINCIPAL OPTIONS

```text
+FIRST[:LAST], --pages=FIRST[:LAST]  select pages
-COLUMN, --columns=COLUMN            print COLUMN columns down
-a, --across                         print columns across
-c, --show-control-chars             use caret and octal notation
-d, --double-space                   double-space output
-D, --date-format=FORMAT             set header date format
-e[CHAR[WIDTH]], --expand-tabs[=...] expand input tabs
-F, -f, --form-feed                  separate pages with form feeds
-h, --header=HEADER                  replace filename in the header
-i[CHAR[WIDTH]], --output-tabs[=...] replace spaces with output tabs
-J, --join-lines                     do not align or truncate columns
-l, --length=PAGE_LENGTH             set page length
-m, --merge                          print files in parallel
-n[SEP[DIGITS]], --number-lines[=...] number lines
-N, --first-line-number=NUMBER       set first printed line number
-o, --indent=MARGIN                  indent each output line
-s[CHAR], --separator[=CHAR]         set a column separator
-S[STRING], --sep-string[=STRING]    set a separator string
-t, --omit-header                    omit headers and trailers
-T, --omit-pagination                omit pagination and input form feeds
-v, --show-nonprinting               use octal notation
-w, --width=PAGE_WIDTH               set multi-column width
-W, --page-width=PAGE_WIDTH          always set and enforce page width
    --help                           display command help and exit
    --version                        display version information and exit
```

## EXIT STATUS

```text
0    All requested input was formatted successfully.
1    Usage, input, formatting, or output processing failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Pagination is managed. Header dates use the repository's shared time abstraction and generated line endings follow the repository's cross-platform text conventions.

## AUTHORS

GNU `pr` was written by Pete TerMaat and Roland Huebner.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`pr(1)`, `nl(1)`, `fmt(1)`, `fold(1)`
