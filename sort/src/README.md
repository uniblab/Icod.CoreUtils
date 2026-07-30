# sort implementation

This directory contains the command-local GNU `sort` policy layered over the shared Completion Gate D ordering engine.

## Responsibilities

- Parse GNU field-key syntax and ordering options; field character offsets are interpreted as byte offsets so records remain byte-preserving.
- Extract default and explicit keys from newline- or NUL-delimited records, including multibyte UTF-8 field separators.
- Implement lexical, exact numeric, general numeric, human numeric, month, random, and GNU file-version comparison families.
- Resolve `LC_COLLATE`, `LC_CTYPE`, `LC_NUMERIC`, and `LC_TIME` independently with POSIX precedence.
- Implement stable, unique, check, bounded-fan-in merge, output-file, file-list, and NUL-record behavior with exact status translation.
- Feed ordinary sorting to `Icod.CoreUtils.Shared.Ordering.ExternalOrderingEngine<T>` so memory limits, secure spill runs, stable multi-pass merge, and deterministic cleanup remain shared infrastructure.

## Compatibility boundaries

The implementation rejects unsupported or contradictory choices rather than accepting misleading no-ops. In particular, incompatible ordering families and character filters receive controlled diagnostics; only one temporary-directory operand is currently accepted; and GNU performance/debug extensions such as `--parallel`, `--compress-program`, and `--debug` are not advertised.

## Dependency rule

The command references only `Icod.CoreUtils.Shared`. It does not reference another individual tool. Comparison rules unique to GNU `sort` stay command-local; reusable collation, key syntax, run codecs, external merge, and workspace ownership remain in Shared.
