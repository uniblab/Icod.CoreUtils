# Shared.FileSystem

`Shared.FileSystem` contains injectable host-filesystem capabilities. The original operations layer supports flush, allocation, and sparse-file behavior. Completion Gate E1 adds a read-only pathname-expansion and traversal layer under [`Traversal`](Traversal/README.md), Completion Gate E3 adds authoritative metadata and timestamp mutation under [`Metadata`](Metadata/README.md), Completion Gate E4 adds race-aware single-path primitives under [`Mutation`](Mutation/README.md), Completion Gate E5 adds recursive mutation/copy policy under [`RecursiveMutation`](RecursiveMutation/README.md), Completion Gate E6 adds transactional ordinary-file replacement under [`TransactionalReplacement`](TransactionalReplacement/README.md), and Batch 42 adds shared ownership resolution and command policy under [`Ownership`](Ownership/README.md).

The operations layer distinguishes operating-system API availability from the behavior of an individual filesystem or volume. Every operation therefore returns `PlatformOperationResult` rather than silently claiming unsupported semantics. The traversal layer yields caller-independent roots, entries, event phases, identities, boundaries, cycles, and structured errors. The metadata layer enriches those same identities with typed values whose availability is always explicit.

## Operations

| Requirement | API |
|---|---|
| Data-only file flush | `FlushFileAsync(fileOrPath, FileFlushMode.DataOnly)` |
| Data-and-metadata file flush | `FlushFileAsync(fileOrPath, FileFlushMode.DataAndMetadata)` |
| One containing filesystem | `FlushFileSystemAsync(path)` |
| All mounted filesystems | `FlushAllFileSystemsAsync()` |
| Sparse extension | `ExtendSparseAsync(file, newLength)` |
| Allocated logical ranges | `GetAllocatedRangesAsync(...)` |

## Platform profile

| Capability | Windows | Linux | macOS | FreeBSD |
|---|---:|---:|---:|---:|
| Data-only file flush | Unsupported | `fdatasync` | Controlled unsupported result | `fdatasync` |
| Data-and-metadata file flush | `FlushFileBuffers` | `fsync` | `fsync` | `fsync` |
| Containing-filesystem flush | Unsupported | `syncfs` | Unsupported | Unsupported |
| Global flush | Unsupported | `sync` | `sync` | `sync` |
| Sparse extension | `FSCTL_SET_SPARSE` plus length extension | Length extension | Length extension | Length extension |
| Allocated-range query | `FSCTL_QUERY_ALLOCATED_RANGES` | `SEEK_DATA` and `SEEK_HOLE` | Controlled unsupported result | `SEEK_DATA` and `SEEK_HOLE` |

The profile reports the APIs exposed by the host. A particular filesystem, network share, permission set, or file handle may still reject an operation. Such failures are returned with a diagnostic and exception rather than converted to success.

FreeBSD support is a best-effort implementation based on its documented `fdatasync`, `fsync`, `sync`, `SEEK_DATA`, and `SEEK_HOLE` interfaces. It should remain subject to validation on an actual FreeBSD host before Gate B is declared verified there.

## Stream lifetime and cancellation

The caller owns every supplied `FileStream`. It must remain open, and callers must not concurrently dispose it or alter its native position, until the returned operation completes. The system implementation retains native handles while it performs descriptor- or handle-based work and restores the managed stream position after allocated-range queries.

The pathname file-flush overload opens its own native handle. On Unix it follows the GNU `sync` strategy: open read-only and nonblocking, retry write-only, clear nonblocking mode, perform `fdatasync` or `fsync`, and report close failures. On Windows it uses `CreateFileW` with backup semantics so directory handles can be attempted where the filesystem permits them.

Cancellation is cooperative. Tokens are observed before and between native operations. A kernel flush or a Windows filesystem-control request that is already blocking might not be interrupted immediately.

Sparse extension is not transactional. For example, Windows can successfully mark a file sparse before a later length change or flush fails. A failed result therefore does not guarantee that every earlier native side effect was rolled back.

## Command adoption

- `dd conv=fdatasync` maps to `FileFlushMode.DataOnly`.
- `dd conv=fsync` maps to `FileFlushMode.DataAndMetadata`.
- `truncate` uses `ExtendSparseAsync` when increasing a file length and can inspect the returned allocation result.
- `sync FILE...` uses a data-and-metadata file flush for each operand.
- `sync -d FILE...` uses a data-only file flush for each operand.
- `sync -f FILE...` uses a containing-filesystem flush for each operand when available; otherwise it follows GNU Coreutils and makes one global flush request.
- `sync` without operands uses the global flush operation.

Commands should accept `IFileSystemOperations` through an overload or constructor and default to `SystemFileSystemOperations.Instance`. Tests may inject a deterministic implementation without invoking native filesystem APIs.

## Read-only traversal

Completion Gate E1 lives in `Icod.CoreUtils.Shared.FileSystem.Traversal`. It provides segment-aware `*`, `?`, bracket, and `**` matching; command-selectable pathname operand expansion; an injectable one-level provider; stable entry and filesystem identities; and iterative asynchronous traversal with separate yield/prune, link, cycle, boundary, ordering, resource, cancellation, and error-continuation policies.

See [`Traversal/README.md`](Traversal/README.md) for the API boundaries and platform profile. `IFileSystemOperations` remains focused on flush and allocation operations and is not enlarged into a traversal interface.


## Authoritative metadata and timestamps

Completion Gate E3 lives in `Icod.CoreUtils.Shared.FileSystem.Metadata`. `IFileSystemMetadataProvider` observes one entry or its containing filesystem and applies selective access, modification, and birth timestamp requests where supported. `SystemFileSystemMetadataProvider` combines the E1 identity provider with Windows handle APIs, Linux `statx`, macOS `stat`/`lstat`, POSIX `statvfs`, Windows volume APIs, and controlled BCL fallbacks.

Every optional field uses `FileSystemMetadataValue<T>` to distinguish available, unavailable, unsupported, and not-applicable states. Commands must not invent zero values for missing modes, ownership, timestamps, allocation data, or identifiers. See [`Metadata/README.md`](Metadata/README.md) for the complete contract and platform profile.

## Recursive mutation and copying

Completion Gate E5 lives in `Icod.CoreUtils.Shared.FileSystem.RecursiveMutation`. It consumes E1 events and E4 mutation preconditions instead of introducing another walker. It adds preserve-root and destination-containment preflight, root-relative destination mapping, hard-link identity tracking, sparse-range copying, requested-versus-required metadata policy, and deterministic reverse-order rollback. See [`RecursiveMutation/README.md`](RecursiveMutation/README.md).

## Transactional replacement

Completion Gate E6 lives in `Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement`. It consumes secure sibling temporary creation, E3 metadata and stable identities, E4 mutation preconditions, E5 containment and metadata-preservation plans, and the existing file/directory durability operations. The transaction stages complete destination and recovery files before mutation, revalidates every pathname immediately before commit, supports GNU simple/numbered/existing backup naming, commits explicit recovery units, and continues rollback and cleanup after individual failures.

Atomicity, durability, rollback, cleanup, and partial-commit states are returned explicitly. Commands retain policy for prompts, force/update decisions, and GNU-visible multi-file behavior. See [`TransactionalReplacement/README.md`](TransactionalReplacement/README.md).

## Ownership mutation

Batch 42 extends the E4 provider with explicit UID/GID mutation and ownership-aware `--from` preconditions, and adds `Icod.CoreUtils.Shared.FileSystem.Ownership` for GNU name/ID resolution plus the common `chown`/`chgrp` recursive policy. Recursive directory mutation is applied in postorder, and traversal-link policy remains independent of terminal dereferencing. Windows reports POSIX ownership mutation as unsupported rather than approximating it with ACLs or file attributes. See [`Ownership/README.md`](Ownership/README.md).
