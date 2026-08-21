# Icod.CoreUtils.Shared

`Icod.CoreUtils.Shared` contains reusable behavior that is specific to the GNU Coreutils/Fileutils/Textutils command family. Neutral command-host, stream, process, terminal, record, regular-expression, temporary-resource, and low-level filesystem mechanisms live in published foundation packages instead of being duplicated here.

## Package dependencies

- `Icod.CommandFramework` 1.0.0 owns the neutral `CommandLine`, `Delimiters`, `Diagnostics`, `Host`, `IO`, `Processes`, `Records`, `RegularExpressions`, `Temporary`, `Terminal`, general text/time mechanism, and neutral filesystem contracts/providers consumed by Coreutils.
- `Icod.Path` 1.0.0 owns canonical and platform-aware path behavior used by the suite.

`Icod.CoreUtils.Shared` must not reintroduce copies of framework-owned public types. A shared API that exposes a framework concept uses the `Icod.CommandFramework` type directly so consumers see one CLR type identity.

## Coreutils-owned areas

The contracted library retains the following command-family behavior:

- `BinaryFormatting`: byte-oriented rendering used by binary-data utilities.
- `Checksums`: GNU checksum/digest command policy and output behavior.
- `Codecs`: Coreutils base-encoding command policy.
- `DirectoryListing`: shared `ls`, `dir`, `vdir`, and `dircolors` policy and presentation orchestration.
- `Escapes`: GNU command-specific escape grammars such as `paste`/`tr` behavior; neutral delimiter mechanism comes from `Icod.CommandFramework.Delimiters`.
- `FileSystem.CopyMove`: GNU `cp`/`mv` copy/move policy and orchestration.
- `FileSystem.Modes`: GNU symbolic/numeric mode parsing plus the Coreutils creation-mask provider; the POSIX mode value model comes from `Icod.CommandFramework.FileSystem.Modes`.
- `FileSystem.Ownership`: GNU/POSIX user/group resolution and shared `chown`/`chgrp` policy.
- `FileSystem.Usage`: GNU block-size, filesystem-usage, and reporting policy.
- `Formatting`: GNU-compatible format-string and escaped-operand behavior.
- `Numerics`: GNU/Coreutils numeric operand, suffix, rounding, and quantity grammar.
- `Ordering`: Coreutils external-ordering policy and codecs, using framework record and temporary-resource contracts.
- `Platform`: Coreutils-specific login, process-information, system-information, system-metrics, and user-information providers retained after the platform split.
- `Ranges`: GNU positional range-list parsing and normalization.
- `Text`: GNU tab-stop parsing policy. General byte/UTF-8 text units, display-width, and tab-stop value models are framework-owned.
- `Time`: GNU date parsing/formatting and wall-clock mutation policy. Monotonic scheduling is framework-owned.
- `SharedUtils`: compatibility surface for existing commands while focused APIs continue to replace legacy helpers.

## Framework-owned filesystem mechanism

Coreutils consumes the following directly from `Icod.CommandFramework.FileSystem` and its subnamespaces:

- root filesystem capabilities and operations;
- read-only traversal and pathname expansion;
- authoritative metadata and timestamp mutation;
- POSIX mode value models;
- single-path mutation;
- recursive mutation/copy planning;
- transactional replacement.

GNU-visible prompting, operand interpretation, overwrite/update behavior, backup policy selection, ownership rules, and other command semantics remain in Coreutils code.

## Boundary rules

1. `Icod.CommandFramework` contains mechanism that is useful independently of Coreutils.
2. `Icod.CoreUtils.Shared` contains demonstrated Coreutils/Fileutils/Textutils reuse, not general CLI infrastructure.
3. Individual commands retain command-specific grammar and presentation when reuse is not demonstrated.
4. Public signatures use the permanent package owner of a type; duplicate lookalike value models are not permitted.
5. Injected standard streams are not owned or disposed by shared command logic.
6. Naturally asynchronous operations accept cancellation and do not use `Task.Run` as an I/O substitute.

## Compatibility

The contraction performed by Completion Gate G is a repository/package ownership change, not a command-semantics change. Existing command behavior remains the compatibility target while implementation dependencies move to the published foundation packages.
