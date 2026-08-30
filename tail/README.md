# TAIL(1)

## NAME

**tail** — output the last part of files and optionally follow growth

## SYNOPSIS

```text
tail [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. `*` and `?` match within one pathname component, and a component exactly equal to `**` may match zero or more complete components. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

The operand `-` is preserved and retains its standard-input meaning.

## DESCRIPTION

`Icod.CoreUtils.Tail` is a managed .NET implementation of GNU Coreutils `tail(1)`, modeled on GNU Coreutils 9.11.

By default the last ten newline-delimited records of each input are written. The command can count bytes, use NUL-delimited records, begin at an absolute record or byte position, or follow files as they grow.

## OPTIONS

```text
-c, --bytes=NUM              output last NUM bytes; leading + starts at NUM
-f, --follow[=descriptor|name] follow appended data
-F                           equivalent to --follow=name --retry
-n, --lines=NUM              output last NUM records; leading + starts at NUM
    --max-unchanged-stats=N  recheck a followed name after N unchanged observations
    --pid=PID                stop following after PID exits
-q, --quiet, --silent        never print file-name headers
    --retry                  keep trying to open an inaccessible followed file
-s, --sleep-interval=N       set polling interval in seconds
-v, --verbose                always print file-name headers
-z, --zero-terminated        use NUL rather than newline as the record delimiter
    --debug                  emit follow-mode diagnostics
    --help                   display command help and exit
    --version                display version information and exit
```

## EXIT STATUS

```text
0    The requested data was produced successfully.
1    Usage, input, follow, or output processing failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Normal output is byte preserving. Seekable files are scanned backward; forward-only sources use bounded buffering or temporary spooling. Follow mode uses cancellation-aware managed polling with descriptor and name semantics.

## AUTHORS

GNU `tail` was written by Paul Rubin, David MacKenzie, Ian Lance Taylor, and Jim Meyering.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`tail(1)`, `head(1)`, `cat(1)`
