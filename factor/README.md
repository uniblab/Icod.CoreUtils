# FACTOR(1)

## NAME

**factor** — print prime factors of integers

## SYNOPSIS

```text
factor [OPTION] [NUMBER]...
```

## DESCRIPTION

`Icod.CoreUtils.Factor` is a managed .NET implementation of GNU Coreutils `factor(1)`, modeled on GNU Coreutils 9.11.

Each nonnegative integer is printed followed by its prime factors in ascending order. If no operands are supplied, whitespace-separated integers are read from standard input.

The implementation uses arbitrary-precision `BigInteger` arithmetic, trial division by small primes, Miller-Rabin-style primality testing, and Pollard-rho factorization.

## OPTIONS

```text
-h, --exponents
    Group repeated prime factors using exponent notation.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

For example, exponent mode may print repeated factors as `2^3` rather than `2 2 2`.

## EXIT STATUS

```text
0    Every supplied or read integer was factored successfully.
1    An operand was invalid or an I/O operation failed.
130  The operation was cancelled.
```

## NOTES

Inputs are parsed as decimal integers. Negative values and non-numeric tokens are rejected. Values `0` and `1` are printed with no prime factors after the colon.

Because arbitrary-precision values are accepted, the time required to factor very large composite integers can grow substantially.

## AUTHORS

GNU `factor` was written by Paul Rubin, Torbjörn Granlund, and Niels Möller.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`factor(1)`, `seq(1)`
