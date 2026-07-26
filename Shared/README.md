# Icod.CoreUtils.Shared

`Icod.CoreUtils.Shared` contains reusable infrastructure for the individual core-utility executables.

## Batch 0 components

- `CommandLine`: declarative GNU/POSIX-style option parsing with short clusters, long options, required and optional values, aliases, `--`, configurable ordering, structured diagnostics, and legacy token rewrites.
- `Diagnostics`: command contexts, standard exit codes, and program-prefixed diagnostics.
- `IO`: asynchronous delimited-record readers and writers, bounded stream operations, standard-input operands, and temporary spooling.
- `Numerics`: culture-invariant integer and floating quantity parsing with exact suffix tables and overflow policies.
- `Processes`: shell-free asynchronous child-process execution with redirected stream forwarding, capture, cancellation, and process-tree termination.
- `Platform`: BCL-first capability reporting and controlled unsupported results.

## Compatibility

`SharedUtils` remains source-compatible for existing tools. Newly refactored commands should use the focused APIs instead of extending `SharedUtils`.

The intended migration is incremental:

1. Keep the command's existing synchronous `Run` entry point as a compatibility wrapper.
2. Add `RunAsync(..., CancellationToken)` and use `CommandContext` for injected and console streams.
3. Replace `SharedUtils.ParseOptions` with declarative `OptionDefinition` instances and `OptionParser`.
4. Use `DelimitedRecordReader`, `DelimitedRecordWriter`, and `StreamOperations` instead of command-specific read/copy loops.
5. Use `QuantityParser`, `ProcessRunner`, and `PlatformCapabilities` where applicable.

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
