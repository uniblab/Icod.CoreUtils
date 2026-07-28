# Shared.FileSystem

`Shared.FileSystem` is the injectable capability layer used by block-oriented and flush-oriented commands.
It distinguishes operating-system API availability from the behavior of an individual filesystem or volume.
Every operation therefore returns `PlatformOperationResult` rather than silently claiming unsupported semantics.

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
