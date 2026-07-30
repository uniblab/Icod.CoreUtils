# Shared external-ordering infrastructure

`Icod.CoreUtils.Shared.Ordering` is the Completion Gate D foundation for `sort` and later sorted-stream consumers. It belongs in the shared project: no command project references another command project, and no type in this directory depends on a command implementation.

## Locale and keys

- `CollationEnvironment` resolves `LC_ALL`, `LC_COLLATE`, and `LANG` in POSIX precedence. `C`, `POSIX`, and their encoded variants select exact bytewise ordering; named locales are normalized for the managed culture provider and unsupported names return a controlled result.
- `ICollationProvider` makes collation injectable. `SystemCollationProvider` supports direct comparison and immutable reusable `CollationKey` values.
- `SortKeyParser` parses the shared `F[.C][OPTS][,F[.C][OPTS]]` grammar into structured endpoints and deterministic errors. It does not decide how fields are extracted from a command's record model.
- `SortKeyRule<T>` and `CompositeSortKeyComparer<T>` compose extracted keys without coupling the shared layer to `sort` options or diagnostics.
- `StableItem<T>` records the original input ordinal, and `StableComparer<T>` uses that ordinal only after primary comparison equality.

## External execution

- `ExternalRunBuilder<T>` consumes `IAsyncEnumerable<T>`, estimates retained bytes, and spills independently sorted stable runs. One opaque item may exceed the configured estimate; it is emitted as a one-item run rather than subdivided.
- `StableExternalMerger<T>` performs a checked k-way stable merge and detects truncated or overlong run files.
- `ExternalOrderingEngine<T>` limits merge fan-in and creates intermediate runs when required, so both memory use and simultaneously open run streams remain bounded.
- `IExternalRunCodec<T>` keeps serialization injectable. `ByteRecordRunCodec` supplies the deterministic length-prefixed format needed by byte-preserving Coreutils record commands.

## Ownership and cleanup

The engine owns every stream it opens but never owns the input sequence's underlying resources or a destination resource hidden behind the output callback. Temporary files are owned by `TemporaryWorkspace`. Cleanup is attempted without the operation cancellation token on success, failure, and cancellation. When an operation and cleanup both fail, the engine reports an `AggregateException` containing both failures rather than silently discarding either one.

These APIs are provisional shared infrastructure. Locale, record, workspace, and general external-ordering contracts remain candidates for later extraction into `Icod.CommandFramework` after independent suite consumers demonstrate the final boundary.
