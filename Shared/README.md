# Icod.CoreUtils.Shared

`Icod.CoreUtils.Shared` contains reusable infrastructure for the individual core-utility executables.

## Batch 0 components

- `CommandLine`: declarative GNU/POSIX-style option parsing with short clusters, long options, required and optional values, aliases, `--`, configurable ordering, structured diagnostics, and legacy token rewrites.
- `Diagnostics`: command contexts, standard exit codes, and program-prefixed diagnostics.
- `Formatting`: GNU-compatible formatting escape decoding for command format strings and escaped operands; neutral scanning is shared with the explicit C3 escape profiles.
- `IO`: compatibility delimited-record readers and writers, bounded stream operations, standard-input operands, and temporary spooling.
- `Numerics`: culture-invariant integer and floating quantity parsing, arbitrary-precision rational arithmetic, exact suffix tables, explicit rounding, and overflow policies.
- `Processes`: shell-free asynchronous child-process execution with redirected stream forwarding, capture, cancellation, and process-tree termination.
- `RegularExpressions`: fully managed GNU basic regular-expression parsing, leftmost-longest matching, GNU/Gnulib capture-register behavior, back-references, structured diagnostics, cancellation, and injectable locale/classification providers.
- `Temporary`: cryptographically secure base-62 name generation, exclusive temporary file/directory creation, collision retries, and deterministic cleanup support.
- `Icod.CommandFramework.FileSystem.Traversal` (package dependency): segment-aware pathname expansion, injectable one-level filesystem observation, stable entry/filesystem identities, and iterative event-based read-only traversal.
- `Icod.CommandFramework.FileSystem.Metadata` (package dependency): authoritative entry and filesystem metadata, explicit availability, E1 identity reuse, allocated-block accounting, and selective timestamp mutation.
- `FileSystem.Ownership`: GNU/POSIX user and group resolution plus shared `chown`/`chgrp` recursive command policy.
- `FileSystem.Mutation`: race-aware single-path creation, linking, removal, mode mutation, and UID/GID mutation with explicit capability and identity preconditions.
- `FileSystem.RecursiveMutation`: E1-based recursive mutation/copy planning, preserve-root and containment preflight, hard-link and sparse-file preservation, metadata policy, and rollback cleanup.
- `FileSystem.TransactionalReplacement`: secure sibling staging, E3/E4 revalidation, atomic publication, GNU backup naming, per-file recovery units, metadata restoration, rollback, durability reporting, and deterministic cleanup.
- `Platform`: BCL-first capability reporting and controlled unsupported results.

## Text processing

The `Icod.CoreUtils.Shared.Text` namespace supplies the reusable Completion Gate C2 foundation for byte-sensitive text-layout commands. `TextUnitReader` can iterate opaque bytes or decode UTF-8 scalars while retaining the exact source bytes for every unit. Invalid UTF-8 is handled explicitly by preserving each invalid byte, returning replacement scalars that still retain the replaced bytes, or throwing at a stable source-byte offset. Byte-order marks are ordinary data and are never removed.

`TextLineReader` and `TextLine` add byte-preserving logical-line iteration for later formatting commands. A line feed is retained as explicit line metadata, while carriage returns and every other byte remain ordinary units. Consumers can reproduce the original bytes exactly or create a managed decision string for regular-expression and layout work without turning that string into the authoritative serialization.

`ITextLocaleProvider` supplies injectable blank classification and the associated byte or UTF-8 decoding profile. `TextLocaleEnvironment` resolves the active profile in `LC_ALL`, `LC_CTYPE`, then `LANG` precedence, selecting exact C/POSIX byte behavior or the deterministic UTF-8 profile. `IDisplayWidthProvider` supplies injectable scalar widths; the default managed provider is deterministic across operating systems, uses Unicode 16.0.0 East Asian Width data, treats ambiguous-width scalars as one column, and measures scalars rather than grapheme clusters. `DisplayColumnState` provides checked advancement, bounded backspace movement, carriage-return reset, and tab-stop advancement without imposing command-specific buffering policy.

`TabStopParser` accepts comma- and blank-separated values, repeated specifications, explicit stop lists, globally aligned `/N` continuation, and final-stop-relative `+N` continuation. One unprefixed value denotes a globally recurring interval. Empty specifications, redundant separators, prefix-only specifications, and zero-valued prefixed intervals reproduce GNU's default-stop behavior. Parse failures use structured error codes so command projects can produce their own GNU-compatible diagnostics.

The initial portability profile is exact for the POSIX C byte locale and supplies a deterministic UTF-8 Unicode profile whose blank classification excludes nonbreaking spaces. Locale behavior remains injectable. Other legacy or stateful encodings are not silently normalized through replacement fallback; a future provider may add them when an exact byte-preserving implementation is justified.

## Record, range, delimiter, and escape processing

Completion Gate C3 adds four composable namespaces without placing command policy in Shared.

- `Icod.CoreUtils.Shared.Records` frames line-feed or NUL byte records. `DelimitedByteRecordSegmentReader` bounds input retention even for enormous records and reports record termination explicitly; `ByteRecordReader` provides the corresponding content-plus-termination materializing model; `DelimitedByteRecordWriter` writes content and separators separately.
- `Icod.CoreUtils.Shared.Ranges` parses and normalizes GNU positional range lists, including leading-open, trailing-open, complement, and configurable general domains. Overlaps merge, but adjacent ranges deliberately remain separate so consumers can observe requested range starts.
- `Icod.CoreUtils.Shared.Delimiters` distinguishes nonempty match delimiters from possibly empty output separators, supplies repeating separator cycles, and incrementally matches multibyte delimiters across arbitrary input buffers.
- `Icod.CoreUtils.Shared.Escapes` supplies neutral backslash scanning, structured diagnostics, GNU `paste` delimiter parsing, and the low-level escaped-byte stream required by later GNU `tr` parsing. Formatting, `paste`, and `tr` remain separate grammar profiles because identical source escapes intentionally have different meanings.

The existing `Icod.CoreUtils.Shared.IO.DelimitedByteRecordReader` remains source-compatible and delegates to the C3 `ByteRecordReader`, which in turn uses the segmented framing engine. Decoded `TextReader`/`TextWriter` record APIs remain available when exact source bytes are not part of a command's contract.

## Compatibility

`SharedUtils` remains source-compatible for existing tools. Newly refactored commands should use the focused APIs instead of extending `SharedUtils`.

The intended migration is incremental:

1. Keep the command's existing synchronous `Run` entry point as a compatibility wrapper.
2. Add `RunAsync(..., CancellationToken)` and use `CommandContext` for injected and console streams.
3. Replace `SharedUtils.ParseOptions` with declarative `OptionDefinition` instances and `OptionParser`.
4. Use `DelimitedRecordReader` and `DelimitedRecordWriter` for intentionally decoded text; use `Icod.CoreUtils.Shared.Records` when record bytes, NUL termination, or unterminated-final-record state must remain exact. Use `StreamOperations` instead of command-specific copy loops.
5. Use `QuantityParser`, `ProcessRunner`, and `PlatformCapabilities` where applicable.
6. Use `IRegularExpressionProvider` instead of translating GNU BRE patterns into `System.Text.RegularExpressions`.

## Option parser example

```csharp
var parser = new OptionParser(
	new OptionDefinition[] {
		new(
			"lines",
			'n',
			new[] { "lines" },
			OptionValueArity.Required
		),
		new(
			"quiet",
			'q',
			new[] { "quiet", "silent" }
		)
	},
	new OptionParserSettings {
		Ordering = OptionOrdering.Permute,
		AllowLongOptionAbbreviations = true
	}
);

var result = parser.Parse( args );
if ( !result.IsSuccess ) {
	foreach ( var error in result.Errors ) {
		await context.StandardError.WriteLineAsync(
			OptionDiagnosticFormatter.Format(
				context.ProgramName,
				error
			)
		).ConfigureAwait( false );
	}
}
```

Options are preserved in encounter order. A command can therefore implement “last option wins” by reading the final occurrence, while still retaining every source spelling and argument index for diagnostics.

## Legacy token forms

Tool-specific obsolete syntax belongs in a token rewrite rule rather than in the core parser. For example, a `head` migration can rewrite `-25` into `-n` and `25` before normal parsing. Rewritten tokens retain the original source token and argument index.

## Streaming and ownership

Shared I/O and record helpers never own injected standard streams. `InputSource` owns only files that it opens. Naturally asynchronous operations accept `CancellationToken` and do not use `Task.Run` as an I/O substitute.

## Regular expressions

Completion Gate C1 adds a reusable GNU BRE engine under `src/RegularExpressions`. It owns GNU/POSIX syntax, leftmost-longest selection, captures, back-references, locale-provider boundaries, cancellation, and controlled diagnostics. See [`src/RegularExpressions/README.md`](src/RegularExpressions/README.md) for the conformance profile and explicit differences from `System.Text.RegularExpressions`.

## External ordering

The `Icod.CoreUtils.Shared.Ordering` namespace supplies the Completion Gate D execution model without creating dependencies between individual tools. Locale selection follows `LC_ALL`, `LC_COLLATE`, and `LANG` precedence; C/POSIX profiles compare bytes, while named supported locales use injectable managed collation. Reusable collation keys, GNU sort-key syntax parsing, composite key rules, and original-input ordinals separate comparison policy from command front ends.

`ExternalRunBuilder<T>` creates stable sorted runs under a caller-provided memory estimate. `StableExternalMerger<T>` validates and merges run streams, and `ExternalOrderingEngine<T>` performs bounded-fan-in intermediate passes when necessary. `IExternalRunCodec<T>` keeps temporary serialization independent of record type; `ByteRecordRunCodec` supplies the byte-preserving Coreutils format.

`TemporaryWorkspace` owns the secure directory and run files. Cleanup ignores the operation cancellation token and is attempted after success, failure, and cancellation. Combined operation and cleanup failures preserve both exceptions. This ordering and workspace layer is shared incubation infrastructure and a provisional `Icod.CommandFramework` candidate; no command project is referenced by it.

## Framework-owned read-only pathname traversal

Completion Gate E1 traversal now lives in `Icod.CommandFramework.FileSystem.Traversal`. Coreutils consumes `PathnamePattern`, `PathnameExpander`, `IReadOnlyFileSystemProvider`, `ReadOnlyPathTraversalEngine`, stable entry/filesystem identities, link policy, cycle detection, and traversal events from the published framework package. G3F1 removes the duplicate CoreUtils implementation and its duplicate Shared tests.
## Framework-owned filesystem metadata and timestamps

Completion Gate E3 metadata now lives in `Icod.CommandFramework.FileSystem.Metadata`. Coreutils consumes the framework metadata model, explicit availability values, filesystem information, timestamp-mutation contracts, and system provider directly from the published package. G3E2 removed the duplicate CoreUtils implementation and its duplicate Shared tests.
## Recursive filesystem mutation and copying

Completion Gate E5 adds `Icod.CoreUtils.Shared.FileSystem.RecursiveMutation`. The mutation-aware traversal engine wraps the E1 event stream, preserves its root provenance and traversal vocabulary, and attaches E4 identity-bearing preconditions to physical entries. Additional contracts cover preserve-root, one-filesystem delegation, destination containment, repeated hard links, sparse allocation, E3 metadata preservation, partial failure, and the rollback seam consumed by Completion Gate E6. See [`src/FileSystem/RecursiveMutation/README.md`](src/FileSystem/RecursiveMutation/README.md).

Completion Gate E6 adds `Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement`. It stages complete secure sibling files and rollback copies before mutation, revalidates stable E3 identity immediately before commit, publishes through explicit atomicity and durability contracts, supports GNU backup naming, groups artifacts by recovery unit, restores E5 metadata, and continues reverse-order rollback and cleanup after failures. See [`src/FileSystem/TransactionalReplacement/README.md`](src/FileSystem/TransactionalReplacement/README.md).

## Ownership mutation

Batch 42 adds `Icod.CoreUtils.Shared.FileSystem.Ownership`. It resolves user and group names through `IIdentityProvider`, honors GNU name-first and forced `+ID` syntax, supports reference and `--from` ownership, separates recursive traversal from terminal dereferencing, and combines E3 observations with ownership-aware E4 preconditions and E5 postorder recursion. The system mutation provider uses `chown` and `lchown` on supported Unix hosts and returns a controlled unsupported result on Windows.
