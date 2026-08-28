# YES(1)

## NAME

**yes** — repeatedly output a line until stopped

## SYNOPSIS

```text
yes [STRING]...
yes OPTION
```

## DESCRIPTION

`Icod.CoreUtils.Yes` is a managed .NET implementation of GNU Coreutils `yes(1)`, modeled on GNU Coreutils 9.11.

The command repeatedly writes all `STRING` operands joined by single spaces. With no operands, it repeatedly writes `y`.

For throughput, the implementation expands the requested line into a reusable output block before entering its write loop.

## OPTIONS

```text
--help
    Display command help and exit when supplied as the sole argument.

--version
    Display version information and exit when supplied as the sole argument.
```

Other arguments, including strings beginning with `-`, are output as data.

## EXIT STATUS

```text
0    Help or version output completed successfully.
1    Standard output failed or was disposed.
130  Repeated output was cancelled.
```

In normal data-producing operation the command does not terminate successfully on its own.

## PLATFORM NOTES

The repeated line uses the host environment's newline sequence and is written through the supplied .NET text stream.

## AUTHORS

GNU `yes` was written by David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`yes(1)`, `echo(1)`, `printf(1)`
