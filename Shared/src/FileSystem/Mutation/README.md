# Shared single-path mutation

This directory implements Completion Gate E4's injectable boundary for basic, non-recursive pathname mutation.

`IFileSystemMutationProvider` supplies ordinary file and directory creation, hard and symbolic links, Windows directory junctions, FIFOs, block and character device nodes, physical name removal, empty-directory removal, and mode changes. Every operation returns `FileSystemMutationResult`, which separates unsupported host behavior from supported failures and classifies privilege, access, existence, kind, identity, indirection, cross-device, and nonempty-directory diagnostics.

`FileSystemMutationPrecondition` carries the E3/E3R effective kind, stable identity, and explicit `PathDereferenceMode` that a caller observed while deciding what to do. `SystemFileSystemMutationProvider` revalidates that observation immediately before destructive or metadata-changing work. Creation uses exclusive host primitives and restrictive initial modes before applying the requested final mode; removal always addresses the physical terminal pathname object; dereferencing is accepted only through `FollowEligiblePathIndirection`. Unknown Windows reparse points are rejected rather than guessed.

The provider intentionally performs only one-path operations. Identity revalidation makes these operations race-aware, but it is not an atomic pathname transaction; transactional replacement and rollback remain Completion Gate E6 responsibilities. Recursive traversal, preserve-root, containment, one-filesystem boundaries, hard-link maps, sparse files, and partial-tree cleanup remain Completion Gate E5 responsibilities.

Platform policy:

- Windows uses exclusive `CreateDirectoryW`, native hard-link creation, and BCL symbolic links. Directory junctions are created as `IO_REPARSE_TAG_MOUNT_POINT` objects through `CreateFileW` and `FSCTL_SET_REPARSE_POINT`; the same implementation supports NTFS and ReFS, while unsupported destination filesystems return a controlled capability failure. Junction targets must be existing local directories; UNC targets and exact volume-GUID mount targets are rejected. Junction removal addresses the reparse-point directory itself and never traverses its target. POSIX modes, FIFOs, and device nodes are reported as unsupported rather than emulated.
- Linux, macOS, and best-effort FreeBSD support use the BCL where it preserves semantics and narrow `libc` calls for `mkdir`, `linkat`, `mkfifo`, `mknod`, `unlink`, and `rmdir`.
- Device-node creation reports controlled privilege failures. It never substitutes an ordinary file.
- Requested creation modes are filtered by an explicit `FileCreationMask`; no process-global umask is changed.

Authoritative behavior references:

- [GNU Coreutils 9.11 manual](https://www.gnu.org/software/coreutils/manual/html_node/index.html)
- [POSIX `mkdir`](https://pubs.opengroup.org/onlinepubs/9799919799/functions/mkdir.html)
- [POSIX `linkat`](https://pubs.opengroup.org/onlinepubs/9799919799/functions/linkat.html)
- [POSIX `mkfifo`](https://pubs.opengroup.org/onlinepubs/9799919799/functions/mkfifo.html)
- [POSIX `mknod`](https://pubs.opengroup.org/onlinepubs/9799919799/functions/mknod.html)

Windows junction references:

- [Microsoft file-system reparse points](https://learn.microsoft.com/windows-hardware/drivers/ifs/reparse-points)
- [Microsoft ReFS feature comparison](https://learn.microsoft.com/windows-server/storage/refs/refs-overview)
- [`FSCTL_SET_REPARSE_POINT`](https://learn.microsoft.com/windows/win32/api/winioctl/ni-winioctl-fsctl_set_reparse_point)
