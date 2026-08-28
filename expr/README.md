# EXPR(1)

## NAME

**expr** — evaluate expressions

## SYNOPSIS

```text
expr EXPRESSION
expr OPTION
```

## DESCRIPTION

`Icod.CoreUtils.Expr` is a managed .NET implementation of GNU Coreutils `expr(1)`, modeled on GNU Coreutils 9.11.

Each expression token is a separate command-line argument. Supported operations include logical selection, numeric or lexical comparison, integer arithmetic, anchored basic-regular-expression matching, string length, substring extraction, character indexing, unary `+` quoting, and parenthesized expressions.

## EXPRESSIONS

```text
ARG1 | ARG2
ARG1 & ARG2
ARG1 < ARG2     ARG1 <= ARG2    ARG1 = ARG2
ARG1 != ARG2    ARG1 >= ARG2    ARG1 > ARG2
ARG1 + ARG2     ARG1 - ARG2
ARG1 * ARG2     ARG1 / ARG2     ARG1 % ARG2
STRING : REGEXP
match STRING REGEXP
substr STRING POS LENGTH
index STRING CHARS
length STRING
+ TOKEN
( EXPRESSION )
```

A leading `--` ends option interpretation. `--help` and `--version` are recognized when they are the sole argument.

## EXIT STATUS

```text
0    EXPRESSION is neither null nor zero.
1    EXPRESSION evaluates to null or zero.
2    EXPRESSION is syntactically or semantically invalid.
3    An internal or operational error occurred.
130  The operation was cancelled.
```

## PLATFORM NOTES

Basic regular expressions use the shared fully managed GNU-compatible engine. Lexical comparison and logical-character behavior are supplied through the expression locale provider.

## AUTHORS

GNU `expr` was written by Mike Parker, James Youngman, and Paul Eggert.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`expr(1)`, `test(1)`, `printf(1)`
