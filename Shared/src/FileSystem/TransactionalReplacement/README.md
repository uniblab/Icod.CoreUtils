# Transactional replacement

Completion Gate E6 provides a command-neutral transaction layer for ordinary-file replacement, creation, and deletion. It is intended for `cp`, `mv`, `install`, Patch, and later LineEditor consumers. Command-specific overwrite prompts, update rules, partial-success policy, and diagnostics remain above this layer.

## Contract

`TransactionalFileReplacementTransaction` accepts immutable `TransactionalReplacementArtifact` values. Every artifact carries:

- an explicit recovery-unit ID;
- a no-follow E4 mutation precondition based on an authoritative E3 observation;
- a replacement, deletion, or validation-only action;
- an optional complete-file writer;
- an optional staged-file configurator for command-specific work after the complete content write and before durability flushing or publication;
- optional E5 recursive-entry provenance and requested-versus-required metadata policy;
- an optional explicit backup pathname.

All complete destination files, rollback copies, retained-backup copies, and prior-backup recovery copies are staged and data-and-metadata flushed before the first destination mutation. Each temporary file is created exclusively with cryptographic randomness in the directory of the pathname it may replace, preserving same-filesystem atomic-publication eligibility.

A replacement artifact may configure its private stage after the content writer closes and before E6 flushes or publishes it. This narrow hook supports consumers such as `install`, which must strip and apply ownership, mode, timestamps, and security labels before a complete destination becomes visible. E6 retains ownership of stage creation, rollback, backups, commit, and cleanup; non-replacement artifacts cannot supply the hook.

Immediately before each namespace mutation, the transaction re-observes the destination and compares existence, kind, and stable E3 identity. Existing mutable objects must be ordinary files with stable identity. Callers that want followed-link behavior must resolve the target before building the E6 artifact; E6 itself always operates no-follow.

## Recovery units and outcomes

Artifacts sharing a recovery-unit ID commit or roll back together. Independent units may either stop after the first failed unit or continue according to `TransactionalReplacementCommitPolicy`. Results distinguish:

- success;
- failure before any unit remains committed;
- a changed unit that was fully rolled back;
- partial commit across independent units;
- incomplete rollback;
- incomplete terminal cleanup;
- mandatory atomicity unavailable before commit.

Rollback and cleanup run in reverse order and continue after individual failures. Failed cleanup paths remain registered and are retried before `CommitAsync` returns and again by `DisposeAsync`.

## Backups

`TransactionalBackupNameGenerator` implements GNU-style simple, numbered, and existing backup selection. Existing mode performs one bounded sibling-directory scan to determine whether numbered backups already exist. Numbered candidate selection also reserves every transaction destination and previously selected backup, preventing intra-transaction collisions.

A retained backup is published from the pre-commit rollback copy. If a simple or explicit backup already exists, its complete contents are staged separately before any commit so rollback can restore both the destination and the earlier backup. Numbered backups use no-replace publication.

## Atomicity and durability

`ITransactionalReplacementFileSystem` exposes explicit capability and result contracts. The system provider uses:

| Operation | Windows | Linux | macOS | FreeBSD |
|---|---|---|---|---|
| Replace existing sibling | `ReplaceFileW` | `rename` | `rename` | `rename` |
| Publish absent sibling | `MoveFileExW` without replace | same-filesystem `File.Move`/`rename` semantics | same-filesystem `File.Move`/`rename` semantics | same-filesystem `File.Move`/`rename` semantics |
| Remove observed file | E4 mutation provider | E4 mutation provider | E4 mutation provider | E4 mutation provider |
| File and directory durability | shared `IFileSystemOperations` | shared `IFileSystemOperations` | shared `IFileSystemOperations` | shared `IFileSystemOperations` |

Unknown hosts may use the documented portable move fallback only when the selected atomicity policy permits it. Every non-atomic or unknown result is surfaced as a controlled diagnostic. Mandatory staged-file or directory durability fails through a structured result rather than silently claiming durability.

## Containment and E5 integration

`TransactionalReplacementPathSafety` delegates containment to E5 `RecursivePathSafety`, which in turn consumes the E2 canonical-path boundary for physical checks. Destination and backup paths must remain at or below the configured containment root.

`TransactionalReplacementArtifact.FromRecursiveEntry` consumes an E5 `RecursiveMutationEntry`, including its mapped destination and E4 precondition. `RecursiveMetadataPreservationPlan.Required` preserves the distinction between requested and mandatory metadata so the E6 provider can distinguish best-effort unsupported metadata from required preservation.

## Testing

The Shared test suite uses an injectable in-memory provider and lifecycle failure injectors. Coverage includes backup naming, containment escape rejection, complete rollback after a later artifact failure, independent-unit partial commit, mandatory atomicity rejection, E3 identity revalidation, cancellation cleanup, cleanup retry, retained backups, terminal cleanup failure, successful staged-file configuration, and configurator-failure cleanup before publication.
