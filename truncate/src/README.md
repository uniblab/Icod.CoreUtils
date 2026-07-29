# `truncate` implementation

This directory contains the GNU-compatible `truncate` command and the small platform boundary required for file allocation and length behavior.

## Components

- `Command.cs` parses options and operands, resolves `--reference`, applies creation policy, expands pathnames, computes each requested target length, invokes the platform abstraction, and converts unsupported or failed operations into deterministic diagnostics and exit statuses.
- `TruncateSizeSpecification.cs` defines the normalized size modes and parses GNU size expressions. It handles absolute and relative modifiers, at-most/at-least behavior, rounding modes, decimal and binary suffixes, inherited command-line mode, overflow, and malformed operands without throwing for ordinary user errors.
- `ITruncatePlatform.cs` is the injectable boundary for preferred I/O block size and logical-length changes. Results distinguish success, unsupported capability, and controlled failure.
- `SystemTruncatePlatform.cs` implements that boundary. BCL `FileStream` operations perform ordinary length changes, the Shared filesystem capability layer is preferred for sparse extension, and narrowly scoped native metadata calls obtain host I/O block sizes where the BCL exposes no equivalent.

## Size application

The parser produces a mode and non-negative magnitude. Command orchestration combines that specification with the current or reference length, performs checked arithmetic, and applies `--io-blocks` scaling only after the relevant filesystem block size is known. This separation keeps syntax independent from file access and makes edge cases independently testable.

## Portability and failure policy

Windows, Linux, macOS, and FreeBSD metadata layouts are isolated in `SystemTruncatePlatform`. Unknown or unsupported platforms return capability results rather than throwing `NotImplementedException`. TempleOS and other future ports can supply another `ITruncatePlatform` implementation without changing command grammar or size arithmetic.
