# DD(1)

## NAME

**dd** — copy and convert data in blocks

## SYNOPSIS

```text
dd [OPERAND]...
dd OPTION
```

## DESCRIPTION

`Icod.CoreUtils.DD` is a managed .NET implementation of GNU Coreutils `dd(1)`, modeled on GNU Coreutils 9.11.

The command copies binary data from an input stream or file to an output stream or file while applying GNU-style block sizing, offsets, conversions, open flags, sparse handling, synchronization, and status reporting.

When standard input or standard output is used, production execution uses binary streams so arbitrary bytes are not passed through text encoding or newline conversion.

## OPERANDS

```text
bs=BYTES
    Read and write up to BYTES at a time. This overrides ibs and obs.

cbs=BYTES
    Set the conversion-record size.

conv=CONVS
    Apply a comma-separated list of conversions.

count=N
    Copy only N input blocks.

ibs=BYTES
    Set the input block size. The default is 512.

if=FILE
    Read from FILE instead of standard input.

iflag=FLAGS
    Apply comma-separated input flags.

obs=BYTES
    Set the output block size. The default is 512.

of=FILE
    Write to FILE instead of standard output.

oflag=FLAGS
    Apply comma-separated output flags.

seek=N, oseek=N
    Skip N output blocks before writing.

skip=N, iseek=N
    Skip N input blocks before copying.

status=LEVEL
    Select none, noxfer, or progress reporting.
```

`N` and `BYTES` accept GNU-style multiplicative suffixes, including decimal forms such as `kB`/`MB`, binary forms such as `K`/`M`, and IEC forms such as `KiB`/`MiB`. Larger suffixes through `Q` are supported. Multiplication with `x` is also accepted.

A trailing `B` on `count`, `skip`, or `seek` makes the value a byte count instead of a block count.

## CONVERSIONS

```text
ascii     convert EBCDIC to ASCII
ebcdic    convert ASCII to EBCDIC
ibm       convert ASCII to alternate EBCDIC
block     pad newline-terminated records to cbs with spaces
unblock   replace trailing spaces in cbs-sized records with newline
lcase     convert uppercase to lowercase
ucase     convert lowercase to uppercase
sparse    seek rather than physically write all-NUL output blocks
swab      swap each pair of input bytes
sync      pad short input blocks
excl      fail if the output already exists
nocreat   do not create the output
notrunc   do not truncate the output
noerror   continue after recoverable read errors
fdatasync flush output data before finishing
fsync     flush output data and metadata before finishing
```

## FLAGS

```text
append    append output
direct    request direct I/O
directory require a directory
dsync     request synchronized data I/O
sync      request synchronized data and metadata I/O
fullblock accumulate complete input blocks
nonblock  request non-blocking I/O
noatime   avoid updating access time
nocache   request cache discard behavior
noctty    do not acquire a controlling terminal
nofollow  do not follow symbolic links
```

Some flags require native host support. Unsupported flag/path combinations are diagnosed rather than approximated with unrelated behavior.

On supporting POSIX hosts, sending `USR1` to a running `dd` requests an I/O statistics report without terminating the copy.

## OPTIONS

```text
--help
    Display the complete usage reference and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    The copy completed successfully.
1    Usage, conversion, I/O, synchronization, or other operational processing failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

The core copy/conversion engine is managed, while file-opening flags, physical flush operations, sparse behavior, and signal reporting are host-capability dependent.

Binary standard-stream boundaries are preserved on Windows, Linux, and macOS. POSIX signal behavior is available only where the runtime and host expose the corresponding signal facility.

## AUTHORS

GNU `dd` was written by Paul Rubin, David MacKenzie, and Stuart Kemp.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`dd(1)`, `cp(1)`, `sync(1)`
