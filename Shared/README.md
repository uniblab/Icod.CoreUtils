# Icod.CoreUtils.Shared

`Icod.CoreUtils.Shared` contains reusable infrastructure for the individual core-utility executables.

## Batch 0 components

- `CommandLine`: declarative GNU/POSIX-style option parsing with short clusters, long options, required and optional values, aliases, `--`, configurable ordering, structured diagnostics, and legacy token rewrites.
- `Diagnostics`: command contexts, standard exit codes, and program-prefixed diagnostics.
- `Formatting`: GNU-compatible escape decoding for command format strings and escaped operands.
- `IO`: asynchronous delimited-record readers and writers, bounded stream operations, standard-input operands, and temporary spooling.
- `Numerics`: culture-invariant integer and floating quantity parsing, arbitrary-precision rational arithmetic, exact suffix tables, explicit rounding, and overflow policies.
- `Processes`: shell-free asynchronous child-process execution with redirected stream forwarding, capture, cancellation, and process-tree termination.
- `RegularExpressions`: fully managed GNU basic regular-expression parsing, leftmost-longest matching, GNU/Gnulib capture-register behavior, back-references, structured diagnostics, cancellation, and injectable locale/classification providers.
- `Temporary`: cryptographically secure base-62 name generation, exclusive temporary file/directory creation, collision retries, and deterministic cleanup support.
- `Platform`: BCL-first capability reporting and controlled unsupported results.

## Text processing

The `Icod.CoreUtils.Shared.Text` namespace supplies the reusable Pre-16 Gate C2 foundation for byte-sensitive text-layout commands. `TextUnitReader` can iterate opaque bytes or decode UTF-8 scalars while retaining the exact source bytes for every unit. Invalid UTF-8 is handled explicitly by preserving each invalid byte, returning replacement scalars that still retain the replaced bytes, or throwing at a stable source-byte offset. Byte-order marks are ordinary data and are never removed.

`ITextLocaleProvider` supplies injectable blank classification and the associated byte or UTF-8 decoding profile. `TextLocaleEnvironment` resolves the active profile in `LC_ALL`, `LC_CTYPE`, then `LANG` precedence, selecting exact C/POSIX byte behavior or the deterministic UTF-8 profile. `IDisplayWidthProvider` supplies injectable scalar widths; the default managed provider is deterministic across operating systems, uses Unicode 16.0.0 East Asian Width data, treats ambiguous-width scalars as one column, and measures scalars rather than grapheme clusters. `DisplayColumnState` provides checked advancement, bounded backspace movement, carriage-return reset, and tab-stop advancement without imposing command-specific buffering policy.

`TabStopParser` accepts comma- and blank-separated values, repeated specifications, explicit stop lists, globally aligned `/N` continuation, and final-stop-relative `+N` continuation. One unprefixed value denotes a globally recurring interval. Empty specifications, redundant separators, prefix-only specifications, and zero-valued prefixed intervals reproduce GNU's default-stop behavior. Parse failures use structured error codes so command projects can produce their own GNU-compatible diagnostics.

The initial portability profile is exact for the POSIX C byte locale and supplies a deterministic UTF-8 Unicode profile whose blank classification excludes nonbreaking spaces. Locale behavior remains injectable. Other legacy or stateful encodings are not silently normalized through replacement fallback; a future provider may add them when an exact byte-preserving implementation is justified.

## Compatibility

`SharedUtils` remains source-compatible for existing tools. Newly refactored commands should use the focused APIs instead of extending `SharedUtils`.

The intended migration is incremental:

1. Keep the command's existing synchronous `Run` entry point as a compatibility wrapper.
2. Add `RunAsync(..., CancellationToken)` and use `CommandContext` for injected and console streams.
3. Replace `SharedUtils.ParseOptions` with declarative `OptionDefinition` instances and `OptionParser`.
4. Use `DelimitedRecordReader`, `DelimitedRecordWriter`, and `StreamOperations` instead of command-specific read/copy loops.
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

Shared I/O helpers never own injected standard streams. `InputSource` owns only files that it opens. Naturally asynchronous operations accept `CancellationToken` and do not use `Task.Run` as an I/O substitute.

## Regular expressions

Completion Gate C1 adds a reusable GNU BRE engine under `src/RegularExpressions`. It owns GNU/POSIX syntax, leftmost-longest selection, captures, back-references, locale-provider boundaries, cancellation, and controlled diagnostics. See [`src/RegularExpressions/README.md`](src/RegularExpressions/README.md) for the conformance profile and explicit differences from `System.Text.RegularExpressions`.
