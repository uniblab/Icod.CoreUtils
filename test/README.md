# TEST(1)

## NAME

**test** — evaluate a conditional expression

## SYNOPSIS

```text
test EXPRESSION
test
```

## DESCRIPTION

`Icod.CoreUtils.Test` is a managed .NET implementation of GNU Coreutils `test(1)`, modeled on GNU Coreutils 9.11.

The command evaluates POSIX/GNU-style string, integer, file, terminal, and compound expressions. It is the `test` executable only; this repository does not create a separate `[` executable from this command implementation.

With no expression, the result is false. A single nonempty string is true.

## EXPRESSIONS

### File predicates

```text
-b FILE   block special file
-c FILE   character special file
-d FILE   directory
-e FILE   exists
-f FILE   regular file
-g FILE   set-group-ID bit set
-G FILE   owned by the effective group
-h FILE   symbolic link
-k FILE   sticky bit set
-L FILE   symbolic link
-N FILE   modified since it was last read
-O FILE   owned by the effective user
-p FILE   FIFO
-r FILE   readable
-s FILE   size greater than zero
-S FILE   socket
-t FD     file descriptor FD is a terminal
-u FILE   set-user-ID bit set
-w FILE   writable
-x FILE   executable/searchable
```

### String expressions

```text
-n STRING       STRING has nonzero length
-z STRING       STRING has zero length
S1 = S2         strings are equal
S1 == S2        strings are equal
S1 != S2        strings are not equal
S1 < S2         S1 sorts before S2 under the active collation rules
S1 > S2         S1 sorts after S2 under the active collation rules
```

### Integer expressions

```text
N1 -eq N2   equal
N1 -ne N2   not equal
N1 -lt N2   less than
N1 -le N2   less than or equal
N1 -gt N2   greater than
N1 -ge N2   greater than or equal
```

`-l STRING` supplies the string length where an integer operand is accepted.

### File comparisons

```text
FILE1 -ef FILE2   refer to the same filesystem object
FILE1 -nt FILE2   FILE1 is newer than FILE2
FILE1 -ot FILE2   FILE1 is older than FILE2
```

### Compound expressions

```text
! EXPR
    Logical negation.

EXPR1 -a EXPR2
    Logical AND.

EXPR1 -o EXPR2
    Logical OR.

( EXPR )
    Group an expression when the parentheses are passed as literal operands.
```

The operands `--help` and `--version` are ordinary strings for `test`; they are not informational options.

## EXIT STATUS

```text
0    EXPRESSION evaluated true.
1    EXPRESSION evaluated false.
2    The expression had invalid syntax.
130  Evaluation was cancelled.
```

## PLATFORM NOTES

Filesystem predicates are evaluated through injected metadata, identity, access, and terminal observations. The implementation uses platform metadata when available and returns false for predicates that cannot be established from the host observation rather than inventing Unix metadata.

String ordering uses the active locale collation rules supplied by the evaluation host.

## PATHNAME GLOBBING

`test` does not perform `Icod.CommandFramework` pathname glob expansion. Its arguments form an expression whose meaning depends on argument count and position; expanding pathname patterns internally would change that expression grammar. An invoking shell or other caller may still expand arguments before `test` receives them.

## AUTHORS

GNU `test` was written by Kevin Braunsdorf and Matthew Bradburn.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`test(1)`, `stat(1)`, `tty(1)`
