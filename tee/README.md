# TEE(1)

## NAME

**tee** — copy standard input to standard output and files

## SYNOPSIS

```text
tee [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

`tee` is a Class C utility and performs no in-process pathname globbing. Every `FILE` operand is an output destination, so wildcard-bearing destination names remain literal when they reach the command unexpanded. This prevents internal globbing from silently turning one output destination into several overwrite or append targets. An invoking shell may still expand an unquoted pattern before `tee` starts.

## DESCRIPTION

`Icod.CoreUtils.Tee` is a managed .NET implementation of GNU Coreutils `tee(1)`, modeled on GNU Coreutils 9.11.

Input is read once as bytes and copied to standard output and every successfully opened output file. A failure on one destination is tracked without automatically preventing healthy destinations from continuing, subject to the selected output-error mode.

## OPTIONS

```text
-a, --append             append to FILEs instead of replacing them
-i, --ignore-interrupts  ignore interrupt-driven cancellation while copying
-p                       diagnose errors writing to non-pipe outputs
    --output-error[=MODE] select GNU-style write-error behavior
    --help               display command help and exit
    --version            display version information and exit
```

Supported output-error modes include `warn`, `warn-nopipe`, `exit`, and `exit-nopipe`. `-i` is recognized at the process boundary so interrupt policy is established before copy execution begins.

## EXIT STATUS

```text
0    All required outputs completed successfully.
1    Usage, input, file-open, or output processing failed.
130  The operation was cancelled when interrupts were not being ignored.
```

## PLATFORM NOTES

Command data is copied through binary streams with no decoding or newline conversion. File outputs use asynchronous host filesystem streams.

## AUTHORS

GNU `tee` was written by Mike Parker, Richard M. Stallman, and David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`tee(1)`, `cat(1)`
