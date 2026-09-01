# PRINTENV(1)

## NAME

**printenv** — print all or part of the environment

## SYNOPSIS

```text
printenv [OPTION]... [VARIABLE]...
```

## DESCRIPTION

`Icod.CoreUtils.PrintEnv` is a managed .NET implementation of GNU Coreutils `printenv(1)`, modeled on GNU Coreutils 9.11.

With no variable operands, the command prints the complete process environment as `NAME=VALUE` records. With one or more `VARIABLE` operands, only each variable's value is printed.

An unset requested variable produces no placeholder output and makes the final status unsuccessful.

## OPTIONS

```text
-0, --null
    End each output record with NUL instead of a newline.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    The complete environment was printed, or every requested variable existed.
1    At least one requested variable was unset or usage was invalid.
130  The operation was cancelled.
```

## PLATFORM NOTES

Values come from the current .NET process environment. Environment-variable naming, case sensitivity, and inherited content therefore follow the host operating system and process-launch environment.

## PATHNAME GLOBBING

`printenv` does not perform `Icod.CommandFramework` pathname glob expansion. Its operands are environment-variable names rather than filesystem pathnames, so `*`, `?`, and `**` are not interpreted as pathname patterns by `printenv`. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `printenv` was written by David MacKenzie and Richard Mlynarik.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`printenv(1)`, `env(1)`
