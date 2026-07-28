# Shared.FileSystem

`Shared.FileSystem` is the injectable capability layer used by block-oriented and flush-oriented commands.
It distinguishes operating-system API availability from the behavior of an individual filesystem or volume.
Every operation therefore returns `PlatformOperationResult` rather than silently claiming unsupported semantics.

## Operations

| Requirement | API |
|---|---|
| Data-only file flush | `FlushFileAsync(file, FileFlushMode.DataOnly)` |
| Data-and-metadata file flush | `FlushFileAsync(file, FileFlushMode.DataAndMetadata)` |
| One containing filesystem | `FlushFileSystemAsync(path)` |
| All mounted filesystems | `FlushAllFileSystemsAsync()` |
| Sparse extension | `ExtendSparseAsync(file, newLength)` |
| Allocated logical ranges | `GetAllocatedRangesAsync(...)` |

## Platform profile

| Capability | Windows | Linux | macOS |
|---|---:|---:|---:|
| Data-only file flush | Unsupported | `fdatasync` | Controlled unsupported result |
| Data-and-metadata file flush | `FlushFileBuffers` | `fsync` | `fsync` |
| Containing-filesystem flush | Unsupported | `syncfs` | Unsupported |
| Global flush | Unsupported | `sync` | `sync` |
| Sparse extension | `FSCTL_SET_SPARSE` plus length extension | Length extension | Length extension |
| Allocated-range query | `FSCTL_QUERY_ALLOCATED_RANGES` | `SEEK_DATA` and `SEEK_HOLE` | Controlled unsupported result |

The profile reports the APIs exposed by the host. A particular filesystem, network share, permission set, or file handle may still reject an operation. Such failures are returned with a diagnostic and exception rather than converted to success.

## Command adoption

- `dd conv=fdatasync` maps to `FileFlushMode.DataOnly`.
- `dd conv=fsync` maps to `FileFlushMode.DataAndMetadata`.
- `truncate` uses `ExtendSparseAsync` when increasing a file length and can inspect the returned allocation result.
- `sync -d FILE` uses a data-only file flush.
- `sync -f FILE` uses a containing-filesystem flush.
- `sync` without operands uses the global flush operation.

Commands should accept `IFileSystemOperations` through an overload or constructor and default to `SystemFileSystemOperations.Instance`. Tests may inject a deterministic implementation without invoking native filesystem APIs.
