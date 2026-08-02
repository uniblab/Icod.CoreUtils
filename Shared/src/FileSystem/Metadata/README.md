# Shared.FileSystem.Metadata

`Icod.CoreUtils.Shared.FileSystem.Metadata` is the Completion Gate E3 authority for filesystem metadata and timestamp mutation. It enriches the stable entry and filesystem identities introduced by Completion Gate E1 rather than creating a second identity model.

## Contract

`IFileSystemMetadataProvider` exposes three injectable operations:

1. observe one pathname with an explicit terminal-link dereference policy;
2. observe the containing filesystem, volume, or mount; and
3. apply a selective timestamp-mutation request.

`SystemFileSystemMetadataProvider.Instance` is the host implementation. Commands should accept the interface through an overload or constructor and use the shared instance by default. Deterministic tests may inject a provider without touching the host filesystem.

`FileSystemMetadata` reports:

- detailed file type and logical size;
- hard-link count, immediate link target, link-object identity, and effective entry identity;
- native mode, numeric owner and group IDs, and owner/group account names or identifiers where available;
- access, modification, inode-change, and birth timestamps;
- device or volume identifier, inode or platform object number, and special-device identifier;
- allocated blocks, allocation-block size, preferred I/O block size, and derived allocated bytes;
- host file attributes; and
- timestamp-mutation capabilities.

`FileSystemInformation` reports the E1 filesystem identity, mount point or volume root, filesystem type, volume label, total/free/available capacity, block and fragment sizes, maximum component length, and read-only state.

## Explicit availability

Every field that may vary by platform or filesystem uses `FileSystemMetadataValue<T>`. Its state is one of:

- `Available` — the value was obtained;
- `Unavailable` — the value exists conceptually but could not be obtained for this observation;
- `Unsupported` — the host adapter or platform does not expose the concept; or
- `NotApplicable` — the field does not apply to this object.

Consumers must not substitute zero, an empty string, the Unix epoch, or a fabricated identity for an unavailable value. Formatting and diagnostics remain command policy.

## Link and identity semantics

The source pathname is first observed without dereferencing through `IReadOnlyFileSystemProvider`. When terminal-link following is requested, a second E1 observation supplies the effective target identity and filesystem identity.

- `EntryIdentity` always identifies the effective object represented by the metadata result.
- `LinkIdentity` identifies the source link object when the source pathname is a link.
- `WasDereferenced` records whether the result describes a followed target.
- `LinkTarget` preserves the immediate provider-reported target text when available.

This lets traversal, `stat`, `touch`, `test`, and Patch compare identities through the same E1 types.

## Platform profile

| Capability | Windows | Linux | macOS | Other/BSD fallback |
|---|---|---|---|---|
| Entry metadata | handle information and `GetFileInformationByHandleEx` | `statx` | `stat`/`lstat` | BCL with explicit gaps |
| Stable identity | E1 file ID and volume serial | E1 device/inode and mount ID | E1 device/inode | E1 provider result |
| Mode and ownership | POSIX mode unsupported; owner/group accounts through security descriptors | Native numeric IDs | Native numeric IDs | BCL mode where available; ownership explicit |
| Change time | Native handle information | Native | Native | Explicitly unavailable |
| Birth time | Creation time | Native when `statx` reports it | Native | Explicitly unsupported or unavailable |
| Allocated blocks | Native allocation size, represented in 512-byte accounting units | Native 512-byte block count | Native 512-byte block count | Explicitly unavailable |
| Filesystem information | volume APIs plus `DriveInfo` | `statvfs` plus `DriveInfo` | `statvfs` plus `DriveInfo` | `DriveInfo` where available |
| Access/modify timestamp mutation | `SetFileTime` | `utimensat` | `utimensat` | BCL fallback |
| Birth-time mutation | Supported | Unsupported | Unsupported | Platform-dependent fallback is not claimed |
| No-follow link timestamp mutation | Reparse-point handle | `AT_SYMLINK_NOFOLLOW` | `AT_SYMLINK_NOFOLLOW` | Unsupported |

Windows owner and primary-group accounts are read through the entry security descriptor. Account names are preferred; an SID string is returned when the account cannot be resolved. Unix numeric owner and group IDs remain authoritative, while display-name resolution is intentionally left to a later injectable resolver so locale and directory-service policy do not alter the metadata contract.

## Timestamp requests

`FileTimestampMutationRequest` treats access, modification, and birth times independently. Each `FileTimestampChange` is `Unchanged`, `CurrentTime`, or `Explicit`. The provider validates the complete request against `FileTimestampMutationCapabilities` before applying native changes, so an unsupported birth-time request does not partially update access or modification time first.

Unix explicit instants are converted to signed seconds plus nanoseconds and therefore do not use a 32-bit `time_t` assumption. This preserves post-2038 values on the supported 64-bit ABIs. Native precision finer than `DateTimeOffset`'s 100-nanosecond resolution is truncated only at the public managed boundary.

## Gate boundaries

E3 supplies observation and timestamp mutation, but it does not parse GNU `stat` format strings, parse user-facing date expressions, decide whether `touch` creates a missing file, define command-specific owner/group formatting, or implement mode/pathname mutation. Batch 36 owns the `stat` and `touch` command policies. Completion Gate E4 owns mode parsing and basic pathname mutation. Patch Phase P8 consumes both gates.
