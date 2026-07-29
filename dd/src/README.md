# `dd` implementation

This directory contains the command-specific implementation of GNU-compatible `dd`. It is deliberately split into parsing, conversion, transfer, output, and reporting components so that block I/O remains testable without placing `dd`-specific policy in `Icod.CoreUtils.Shared`.

## Components

- `Command.cs` owns command-line parsing, operand validation, standard-stream adaptation, file opening, platform checks, physical flushing, diagnostics, and command exit status. `DdDirectoryInputStream` preserves the GNU-style “cannot read directory” failure path without delegating to a native utility.
- `DdOptions.cs` defines the normalized operand model: conversions, input/output flags, status policy, block sizes, paths, and count/skip/seek quantities. A quantity records whether its magnitude is expressed directly in bytes or must be scaled by the applicable block size.
- `DdNumberParser.cs` implements GNU multiplicative numeric syntax for block sizes and transfer quantities. Invalid user input is returned as a diagnostic instead of escaping as a parsing exception.
- `DdConversions.cs` contains the fixed ASCII/EBCDIC translation tables and byte-oriented ASCII case conversion routines.
- `DdConversionPipeline.cs` applies conversions in GNU order while retaining state between input blocks. It carries an unmatched `swab` byte and incomplete `block` or `unblock` records across reads, and flushes that state at end of input.
- `DdCopyEngine.cs` performs cancellation-aware asynchronous reads and writes, count/skip/seek handling, conversion, full-block accumulation, recoverable read-error policy, and signal-requested reporting. `DdOutputSink` buffers output records and implements append, sparse-hole, seek, truncation, and synchronized-write behavior.
- `DdStatistics.cs` stores thread-safe counters and serializes periodic, signal-triggered, and final GNU-style reports.

## Execution and ownership

The public entry points create or accept a `CommandContext`. Caller-provided standard streams remain caller-owned. File streams opened from `if=` or `of=` operands are command-owned and are disposed when execution completes. Naturally asynchronous I/O uses TAP directly and observes the context cancellation token; synchronous filesystem operations are kept narrow and are not wrapped in `Task.Run`.

## Portability

Managed BCL APIs are preferred. Features that the portable file APIs cannot represent are rejected with controlled diagnostics. Supported POSIX signal reporting and host-specific synchronization behavior are isolated at the command boundary rather than leaking into the conversion engine.
