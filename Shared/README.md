# Icod.CoreUtils.Shared

`Icod.CoreUtils.Shared` is the permanent repository-local shared library for the GNU Coreutils/Fileutils/Textutils command family. Neutral command-host, stream, process, terminal, record, regular-expression, temporary-resource, pathname, and low-level filesystem mechanisms live in published foundation packages instead of being duplicated here.

## External foundation dependencies

- `Icod.CommandFramework` 2.1.0 owns neutral command infrastructure, general text/time mechanism, process and terminal mechanism, filesystem traversal and metadata, inode-pool observation, current-process creation-mask observation, and host file-clone/reflink mechanism consumed by Coreutils.
- `Icod.Path` 1.1.0 owns canonical and platform-aware pathname behavior used by the suite.
- `Icod.Terminal` 0.3.0 provides the terminal foundation consumed by shared command infrastructure.

`Icod.CoreUtils.Shared` must not reintroduce copies of framework-owned public types. A shared API that exposes a framework concept uses the permanent foundation type directly so consumers see one CLR type identity.

## Repository-local identity

`Icod.CoreUtils.Shared` is deliberately **not** an independently published NuGet package. Its project is non-packable and genuine Coreutils/Fileutils/Textutils consumers use a same-repository `ProjectReference`, for example:

```xml
<ProjectReference Include="..\Shared\Icod.CoreUtils.Shared.csproj" />
```

Do not add a `PackageReference` to `Icod.CoreUtils.Shared` and do not publish it to NuGet.org or GitHub Packages. Completion Gate G is complete: cross-repository mechanism belongs behind its canonical published foundation package rather than a co-resident sibling-suite `ProjectReference`.

## Coreutils-owned areas

The contracted library retains the following command-family behavior:

- `BinaryFormatting`: byte-oriented rendering used by binary-data utilities.
- `Checksums`: GNU checksum/digest command policy and output behavior.
- `Codecs`: Coreutils base-encoding command policy.
- `DirectoryListing`: shared `ls`, `dir`, `vdir`, and `dircolors` policy and presentation orchestration.
- `Escapes`: GNU command-specific escape grammars such as `paste`/`tr` behavior; neutral delimiter mechanism comes from `Icod.CommandFramework.Delimiters`.
- `FileSystem.CopyMove`: GNU `cp`/`mv` copy/move policy and orchestration; host clone execution is framework-owned.
- `FileSystem.Modes`: GNU symbolic/numeric mode parsing; POSIX mode values and current-process creation-mask observation are framework-owned.
- `FileSystem.Ownership`: GNU/POSIX user/group resolution and shared `chown`/`chgrp` policy.
- `FileSystem.Usage`: GNU block-size, filesystem-usage, accounting, and reporting policy; inode-pool observations come from framework metadata.
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

- root filesystem capabilities and operations, including capability-aware whole-file clone/reflink;
- read-only traversal and pathname expansion;
- authoritative metadata, timestamp mutation, and filesystem inode-pool observation;
- POSIX mode value models and current-process creation-mask observation;
- single-path mutation;
- recursive mutation/copy planning;
- transactional replacement.

GNU-visible prompting, operand interpretation, reflink selection policy, overwrite/update behavior, backup policy selection, ownership rules, and other command semantics remain in Coreutils code.

## Boundary rules

1. `Icod.CommandFramework` contains mechanism that is useful independently of Coreutils.
2. `Icod.CoreUtils.Shared` contains demonstrated Coreutils/Fileutils/Textutils reuse, not general CLI infrastructure.
3. `Icod.CoreUtils.Shared` remains in this repository and is consumed by same-repository `ProjectReference`, never by package reference.
4. Sibling-suite mechanism belongs behind published neutral package boundaries and does not establish a public CoreUtils package boundary.
5. Individual commands retain command-specific grammar and presentation when reuse is not demonstrated.
6. Public signatures use the permanent package owner of a type; duplicate lookalike value models are not permitted.
7. Injected standard streams are not owned or disposed by shared command logic.
8. Naturally asynchronous operations accept cancellation and do not use `Task.Run` as an I/O substitute.

## Compatibility

The contraction performed by Completion Gate G is an ownership and dependency-boundary change, not a command-semantics change. Existing command behavior remains the compatibility target while neutral implementation dependencies are supplied by published foundation packages and Coreutils-specific reuse remains source-built inside this repository.
